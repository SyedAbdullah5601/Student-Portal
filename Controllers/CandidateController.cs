using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Models;
using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using System.Linq;

namespace StudentPortal.Controllers;

public class CandidateController : Controller
{
    private readonly StudentLoginContext _context;
    private readonly IConfiguration _config;

    public CandidateController(StudentLoginContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    #region Helpers
    private async Task LogActivity(string action, string status, string details, int? userId = null, string? role = null)
    {
        try
        {
            var log = new SystemLog
            {
                Action = action,
                Status = status,
                Details = details,
                UserId = userId,
                UserRole = role,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                ControllerName = "Candidate",
                ActionName = ControllerContext.ActionDescriptor.ActionName,
                Timestamp = DateTime.Now
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Logging failed: {ex.Message}"); }
    }

    private async Task<string> GetUsernameWithPrefix(string username, int userTypeId)
    {
        if (string.IsNullOrEmpty(username)) return "";
        var role = await _context.UserTypes.FindAsync(userTypeId);
        string prefix = role?.Prefix ?? "";
        if (!string.IsNullOrEmpty(prefix) && !username.StartsWith(prefix))
            return prefix + username;
        return username;
    }
    #endregion

    #region Views
    [HttpGet] public IActionResult Attendance() => View();
    [HttpGet] public IActionResult Register() => View();
    [HttpGet] public IActionResult Login() => View();
    [HttpGet] public IActionResult Logs() => View();
    [HttpGet] public IActionResult AdminManagement() => View();
    [HttpGet] public IActionResult AssignCourses() => View();
    [HttpGet] public IActionResult Enroll() => View();
    [HttpGet] public IActionResult Grades() => View();
    [HttpGet] public IActionResult MyCourses() => View();
    [HttpGet] public IActionResult MyClasses() => View();
    [HttpGet]
    public IActionResult StudentManagement(int assignmentId)
    {
        ViewBag.AssignmentId = assignmentId;
        return View();
    }
    [HttpGet]
    public IActionResult Notifications()
    {
        var roleId = HttpContext.Session.GetInt32("UserTypeId");
        if (roleId != 3) { return RedirectToAction("Dashboard"); }

        return View();
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        var roleId = HttpContext.Session.GetInt32("UserTypeId");
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null || roleId == null || roleId == 0)
            return RedirectToAction("Login");
        ViewBag.UserRole = roleId;
        ViewBag.UserId = userId;
        return View();
    }
    [HttpGet]
    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");
        return View();
    }
    #endregion

    [HttpPost]
    public async Task<IActionResult> HandleAction([FromBody] CandidateViewModel vm)
    {
        try
        {
            switch (vm.Operaid)
            {
                case CrudAction.Select:
                    var student = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == vm.CandidateId);
                    if (student == null) return NotFound();
                    return Json(new { success = true, data = new { student.FirstName, student.LastName, student.Email, student.Username, student.PhoneNo, student.UserTypeId, DateOfBirth = student.DateOfBirth.ToString("yyyy-MM-dd") } });

                case CrudAction.Verify:
                    // FIX: Added await to prevent Task-to-String conversion bug
                    string vUsername = await GetUsernameWithPrefix(vm.Username, vm.UserTypeId);
                    return Json(new { isAvailable = !await _context.Candidates.AnyAsync(x => x.Username == vUsername) });

                case CrudAction.Insert:
                    if (HttpContext.Session.GetInt32("UserId") == null)
                    {
                        if (!string.IsNullOrEmpty(vm.FirstName))
                        {
                            return await RegisterCandidate(vm);
                        }
                        return Json(new { success = false, message = "Session expired. Please log in." });
                    }
                    int currentId = HttpContext.Session.GetInt32("UserId") ?? 0;
                    if (await _context.Enrollments.AnyAsync(e => e.AssignmentId == vm.AssignmentId && e.StudentId == currentId))
                        return Json(new { success = false, message = "Already enrolled." });

                    _context.Enrollments.Add(new Enrollment { AssignmentId = vm.AssignmentId, StudentId = currentId, CreatedAt = DateTime.Now });
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Successfully enrolled!" });

                case CrudAction.AdminCreateUser:
                    return await RegisterCandidate(vm);

                case CrudAction.Login:
                    return await LoginCandidate(vm);

                case CrudAction.VerifyOtp:
                    return await VerifyOtp(vm);

                case CrudAction.ResendOtp:
                    var resendUser = await _context.Candidates.FindAsync(vm.CandidateId);
                    if (resendUser != null) await SendOtpEmail(resendUser.Email, resendUser.OtpCode, resendUser.CandidateId);
                    return Json(new { success = true });

                case CrudAction.FetchLogs:
                    var logs = await _context.SystemLogs.OrderByDescending(l => l.Timestamp).Take(100).ToListAsync();
                    return Json(new { success = true, data = logs });

                case CrudAction.FetchAllMenus:
                    return Json(new
                    {
                        success = true,
                        menus = await _context.Menus.ToListAsync(),
                        roles = await _context.UserTypes.Select(r => new { r.UserTypeId, r.TypeName }).ToListAsync(), // Cleaned up
                        mappings = await _context.RoleMenuMappings.Select(m => new { m.UserTypeId, m.MenuId }).ToListAsync()
                    });

                case CrudAction.SaveMenuMapping:
                    var toRemove = _context.RoleMenuMappings.Where(m => m.UserTypeId == vm.UserTypeId);
                    _context.RoleMenuMappings.RemoveRange(toRemove);
                    if (vm.SelectedMenuIds != null)
                    {
                        foreach (var mId in vm.SelectedMenuIds)
                        {
                            _context.RoleMenuMappings.Add(new RoleMenuMapping
                            {
                                UserTypeId = vm.UserTypeId,
                                MenuId = mId
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Permissions updated!" });

                case CrudAction.FetchAttendanceSheet: // OperaID 16
                    var managementData = await _context.Enrollments
                        .Where(e => e.AssignmentId == vm.AssignmentId)
                        .Include(e => e.Student)
                        .Include(e => e.Attendances)
                        .Select(e => new
                        {
                            enrollmentId = e.EnrollmentId,
                            firstName = e.Student != null ? e.Student.FirstName : "N/A",
                            lastName = e.Student != null ? e.Student.LastName : "N/A",
                            username = e.Student != null ? e.Student.Username : "N/A",
                            marks = e.Mark ?? 0,
                            attendanceRecords = e.Attendances
                                .Select(a => new { a.WeekNumber, a.Status })
                                .ToList()
                        }).ToListAsync();
                    return Json(new { success = true, data = managementData });

                case CrudAction.SaveAttendance:
                    var att = await _context.Attendances.FirstOrDefaultAsync(a => a.EnrollmentId == vm.EnrollmentId && a.WeekNumber == vm.WeekNumber);
                    if (att != null) att.Status = vm.Status;
                    else _context.Attendances.Add(new Attendance { EnrollmentId = vm.EnrollmentId ?? 0, WeekNumber = vm.WeekNumber ?? 1, Status = vm.Status });
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                case CrudAction.SaveGrades: // OperaID 19
                    if (string.IsNullOrEmpty(vm.BulkData)) return BadRequest();
                    // Use dynamic or a specific DTO that includes status
                    var updates = JsonConvert.DeserializeObject<List<dynamic>>(vm.BulkData);

                    foreach (var update in updates)
                    {
                        int enId = (int)update.enrollmentId;
                        var enrollments = await _context.Enrollments.FindAsync(enId);
                        if (enrollments != null)
                            enrollments.Mark = (int)update.marks;

                        var existingAttendance = _context.Attendances.Where(a => a.EnrollmentId == enId);
                        _context.Attendances.RemoveRange(existingAttendance);

                        if (update.attendanceDetails != null)
                        {
                            foreach (var detail in update.attendanceDetails)
                            {
                                _context.Attendances.Add(new Attendance
                                {
                                    EnrollmentId = enId,
                                    WeekNumber = (int)detail.weekNumber,
                                    Status = (string)detail.status
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    await LogActivity("Bulk Update", "Success", $"Teacher updated marks/attendance for Assignment {vm.AssignmentId}");
                    return Json(new { success = true });

                case CrudAction.FetchCourseDetails:
                    var sId = HttpContext.Session.GetInt32("UserId");
                    var av = await _context.CourseAssignments.Select(a => new
                    {
                        a.AssignmentId,
                        a.Course.CourseCode,
                        a.Course.CourseName,
                        FacultyName = a.Faculty.FirstName,
                        a.SectionName,
                        IsEnrolled = _context.Enrollments.Any(e => e.AssignmentId == a.AssignmentId && e.StudentId == sId)
                    }).ToListAsync();
                    return Json(new { success = true, data = av });

                case CrudAction.FetchAllUsers:
                    var users = await _context.Candidates
                        .Include(u => u.UserType)
                        .Select(u => new {
                            u.CandidateId,
                            u.FirstName,
                            u.LastName,
                            u.Username,
                            u.Email,
                            u.PhoneNo,
                            u.UserTypeId,
                            RoleName = u.UserType.TypeName,
                            JoinedDate = u.CreatedAt.HasValue ? u.CreatedAt.Value.ToString("MMM dd, yyyy") : "N/A"
                        }).ToListAsync();
                    return Json(new { success = true, data = users });

                case CrudAction.DeleteUser:
                    var userToDelete = await _context.Candidates
                        .Include(u => u.Enrollments!)
                            .ThenInclude(e => e.Attendances!)
                        .Include(u => u.CourseAssignments!)
                            .ThenInclude(a => a.Enrollments!)
                                .ThenInclude(ae => ae.Attendances!)
                        .Include(u => u.SystemLogs!)
                        .FirstOrDefaultAsync(u => u.CandidateId == vm.CandidateId);
                    if (userToDelete == null) return NotFound();
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var studentAttendances = userToDelete.Enrollments
                                .SelectMany(e => e.Attendances ?? new List<Attendance>())
                                .ToList();
                            _context.Attendances.RemoveRange(studentAttendances);
                            _context.Enrollments.RemoveRange(userToDelete.Enrollments);
                            foreach (var assignment in userToDelete.CourseAssignments)
                            {
                                var studentInClassAttendances = assignment.Enrollments
                                    .SelectMany(e => e.Attendances ?? new List<Attendance>())
                                    .ToList();
                                _context.Attendances.RemoveRange(studentInClassAttendances);
                                _context.Enrollments.RemoveRange(assignment.Enrollments);
                            }
                            _context.CourseAssignments.RemoveRange(userToDelete.CourseAssignments);
                            if (userToDelete.SystemLogs.Any()) _context.SystemLogs.RemoveRange(userToDelete.SystemLogs);
                            _context.Candidates.Remove(userToDelete);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return Json(new { success = true });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            return Json(new { success = false, message = "Deep Delete Failed: " + (ex.InnerException?.Message ?? ex.Message) });
                        }
                    }

                case CrudAction.UpdateUser:
                    var userToUpdate = await _context.Candidates
                        .Include(u => u.Enrollments)
                        .Include(u => u.CourseAssignments)
                        .FirstOrDefaultAsync(u => u.CandidateId == vm.CandidateId);
                    if (userToUpdate == null) return NotFound();
                    if (userToUpdate.UserTypeId != vm.UserTypeId)
                    {
                        if (userToUpdate.Enrollments.Any() || userToUpdate.CourseAssignments.Any())
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Cannot change Role: This user has active enrollments or course assignments linked to their current role."
                            });
                        }
                        userToUpdate.UserTypeId = vm.UserTypeId;
                    }
                    userToUpdate.FirstName = vm.FirstName;
                    userToUpdate.LastName = vm.LastName;
                    userToUpdate.Email = vm.Email;
                    userToUpdate.PhoneNo = vm.PhoneNo ?? "";
                    try
                    {
                        await _context.SaveChangesAsync();
                        await LogActivity("Admin Update", "Success", $"Updated user: {userToUpdate.Username}");
                        return Json(new { success = true });
                    }
                    catch (DbUpdateException ex)
                    {
                        return Json(new { success = false, message = "Database constraint error: " + ex.InnerException?.Message });
                    }

                case CrudAction.FetchCourses:
                    var allCourses = await _context.Courses.ToListAsync();
                    return Json(new { success = true, data = allCourses });

                case CrudAction.FetchFacultyCourses:
                    int? currentTeacherId = HttpContext.Session.GetInt32("UserId");
                    var assignments = await _context.CourseAssignments
                        .Where(a => a.FacultyId == currentTeacherId) // Filter by logged-in teacher
                        .Select(a => new {
                            a.AssignmentId,
                            courseCode = a.Course.CourseCode,
                            courseName = a.Course.CourseName,
                            sectionName = a.SectionName,
                            studentCount = a.Enrollments.Count() // Extra info for the teacher
                        }).ToListAsync();
                    return Json(new { success = true, data = assignments });

                case CrudAction.FetchAssignments:
                    var assignedList = await _context.CourseAssignments
                        .Include(a => a.Course)
                        .Include(a => a.Faculty)
                        .Select(a => new {
                            a.AssignmentId,
                            CourseCode = a.Course.CourseCode,
                            CourseName = a.Course.CourseName,
                            FacultyName = a.Faculty.FirstName + " " + a.Faculty.LastName,
                            a.SectionName
                        }).ToListAsync();
                    return Json(new { success = true, data = assignedList });

                case CrudAction.SaveAssignment:
                    bool exists = await _context.CourseAssignments.AnyAsync(a =>
                        a.CourseId == vm.CourseId &&
                        a.FacultyId == vm.CandidateId &&
                        a.SectionName == vm.SectionName);

                    if (exists) return Json(new { success = false, message = "This faculty is already assigned to this course section." });

                    var newAssign = new CourseAssignment
                    {
                        CourseId = vm.CourseId,
                        FacultyId = vm.CandidateId,
                        SectionName = vm.SectionName ?? "A"
                    };
                    _context.CourseAssignments.Add(newAssign);
                    await _context.SaveChangesAsync();
                    await LogActivity("Course Assignment", "Success", $"Assigned Faculty {vm.CandidateId} to Course {vm.CourseId}");
                    return Json(new { success = true });

                case CrudAction.DeleteAssignment:
                    var toDelete = await _context.CourseAssignments
                        .Include(a => a.Enrollments)
                        .FirstOrDefaultAsync(a => a.AssignmentId == vm.AssignmentId);

                    if (toDelete == null) return NotFound();

                    if (toDelete.Enrollments.Any())
                        return Json(new { success = false, message = "Cannot remove assignment. Students are already enrolled in this section." });

                    _context.CourseAssignments.Remove(toDelete);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                case CrudAction.CreateUserType:
                    if (string.IsNullOrEmpty(vm.NewUserTypeName))
                        return Json(new { success = false, message = "Role name is required." });
                    bool roleExists = await _context.UserTypes.AnyAsync(ut => ut.TypeName == vm.NewUserTypeName);
                    if (roleExists)
                        return Json(new { success = false, message = "This User Type already exists." });
                    var newUserType = new UserType
                    {
                        TypeName = vm.NewUserTypeName,
                        Prefix = vm.NewUserPrefix
                    };
                    _context.UserTypes.Add(newUserType);
                    await _context.SaveChangesAsync();
                    await LogActivity("Create Role", "Success", $"New User Type created: {vm.NewUserTypeName}");
                    return Json(new { success = true, message = "New User Type created successfully!" });

                case CrudAction.CreateCourse:
                    if (string.IsNullOrEmpty(vm.NewCourseCode) || string.IsNullOrEmpty(vm.NewCourseName))
                        return Json(new { success = false, message = "Course Code and Name are required." });

                    bool courseExists = await _context.Courses.AnyAsync(c => c.CourseCode == vm.NewCourseCode);
                    if (courseExists)
                        return Json(new { success = false, message = "A course with this code already exists." });

                    var newCourse = new Course
                    {
                        CourseCode = vm.NewCourseCode,
                        CourseName = vm.NewCourseName,
                        Description = vm.NewCourseDescription
                    };

                    _context.Courses.Add(newCourse);
                    await _context.SaveChangesAsync();
                    await LogActivity("Create Course", "Success", $"Created course: {vm.NewCourseCode}");

                    return Json(new { success = true, message = "Course created successfully!" });

                case CrudAction.DeleteCourse:
                    var courseToDelete = await _context.Courses
                        .Include(c => c.CourseAssignments)
                        .FirstOrDefaultAsync(c => c.CourseId == vm.CourseId);

                    if (courseToDelete == null) return NotFound();
                    if (courseToDelete.CourseAssignments.Any())
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Cannot delete course. It is currently assigned to one or more faculty members."
                        });
                    }
                    _context.Courses.Remove(courseToDelete);
                    await _context.SaveChangesAsync();
                    await LogActivity("Delete Course", "Success", $"Deleted course: {courseToDelete.CourseCode}");
                    return Json(new { success = true, message = "Course deleted successfully!" });

                case CrudAction.FetchProfile:
                    int? pId = HttpContext.Session.GetInt32("UserId");
                    var prof = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == pId);
                    if (prof == null) return NotFound();
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            prof.FirstName,
                            prof.LastName,
                            prof.Email,
                            prof.Username,
                            prof.PhoneNo,
                            DateOfBirth = prof.DateOfBirth.ToString("yyyy-MM-dd")
                        }
                    });

                case CrudAction.UpdateProfile:
                    int? currentUserId = HttpContext.Session.GetInt32("UserId");
                    var userToEdit = await _context.Candidates.FindAsync(currentUserId);
                    if (userToEdit == null) return Json(new { success = false, message = "User not found" });
                    userToEdit.FirstName = vm.FirstName;
                    userToEdit.LastName = vm.LastName;
                    userToEdit.Email = vm.Email;
                    userToEdit.PhoneNo = vm.PhoneNo ?? "";
                    userToEdit.DateOfBirth = DateOnly.FromDateTime(vm.DateOfBirth);
                    if (!string.IsNullOrEmpty(vm.Password))
                    {
                        userToEdit.PasswordHash = vm.Password;
                    }
                    await _context.SaveChangesAsync();
                    HttpContext.Session.SetString("Username", userToEdit.FirstName);

                    await LogActivity("Update Profile", "Success", $"User {userToEdit.Username} updated their info");
                    return Json(new { success = true, message = "Profile updated successfully!" });

                case CrudAction.FetchStudentAttendance:
                    int? currentid = HttpContext.Session.GetInt32("UserId");
                    var myData = await _context.Enrollments
                        .Where(e => e.StudentId == currentid)
                        .Select(e => new {
                            CourseCode = e.Assignment.Course.CourseCode,
                            CourseName = e.Assignment.Course.CourseName,
                            TotalMarks = e.Mark ?? 0,
                            AttendanceHistory = e.Attendances.Select(a => new {
                                a.WeekNumber,
                                a.Status
                            }).ToList(),
                            TotalWeeks = 16
                        }).ToListAsync();
                    return Json(new { success = true, data = myData });

                case CrudAction.CreateMenu: // This should be value 12 in your Enum
                    if (string.IsNullOrEmpty(vm.MenuName) || string.IsNullOrEmpty(vm.Url))
                        return Json(new { success = false, message = "Menu Name and URL are required." });

                    var newMenu = new Menu
                    {
                        MenuName = vm.MenuName,
                        Url = vm.Url,
                        Icon = vm.Icon ?? "bi bi-circle"
                    };

                    _context.Menus.Add(newMenu);
                    await _context.SaveChangesAsync();

                    await LogActivity("Create Menu", "Success", $"Created new menu: {vm.MenuName}");
                    return Json(new { success = true, message = "Menu created successfully!" });

                case CrudAction.FetchDashboardStats:
                    int? userId = HttpContext.Session.GetInt32("UserId");
                    int? roleId = HttpContext.Session.GetInt32("UserTypeId");
                    if (roleId == 3)
                    {
                        var adminStats = new
                        {
                            TotalStudents = await _context.Candidates.CountAsync(u => u.UserTypeId == 1),
                            TotalFaculty = await _context.Candidates.CountAsync(u => u.UserTypeId == 2),
                            ActiveCourses = await _context.Courses.CountAsync(),
                            RecentLogs = await _context.SystemLogs.CountAsync(l => l.Timestamp >= DateTime.Now.AddDays(-1))
                        };
                        return Json(new { success = true, stats = adminStats });
                    }
                    else if (roleId == 1)
                    {
                        var studentStats = new
                        {
                            MyEnrollments = await _context.Enrollments.CountAsync(e => e.StudentId == userId),
                            AvgAttendance = 0
                        };
                        return Json(new { success = true, stats = studentStats });
                    }
                    return BadRequest();

                case CrudAction.CreateNotification:
                    var newNotif = new Notification
                    {
                        Title = vm.NotifTitle,
                        Message = vm.NotifMessage,
                        TargetUserTypeId = vm.TargetUserTypeId == 0 ? null : vm.TargetUserTypeId,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(newNotif);
                    await _context.SaveChangesAsync();
                    await LogActivity("Notification Broadcast", "Success", $"Admin published: {vm.NotifTitle}");
                    return Json(new { success = true, message = "Notification published!" });

                case CrudAction.FetchAllNotifications:
                    var allNotifs = await _context.Notifications
                        .Include(n => n.TargetUserType)
                        .OrderByDescending(n => n.CreatedAt)
                        .Select(n => new {
                            id = n.NotifId,
                            title = n.Title,
                            targetName = n.TargetUserType != null ? n.TargetUserType.TypeName : "Public",
                            date = n.CreatedAt.HasValue ? n.CreatedAt.Value.ToString("MMM dd, HH:mm") : "N/A"
                        }).ToListAsync();
                    return Json(new { success = true, data = allNotifs });

                case CrudAction.DeleteNotification:
                    var notifToDelete = await _context.Notifications.FindAsync(vm.NotifId);
                    if (notifToDelete != null)
                    {
                        _context.Notifications.Remove(notifToDelete);
                        await _context.SaveChangesAsync();
                        await LogActivity("Delete Notification", "Success", $"Removed Notif ID: {vm.NotifId}");
                    }
                    return Json(new { success = true });

                case CrudAction.FetchNotifications:
                    int? currentUserRole = HttpContext.Session.GetInt32("UserTypeId");

                    var dashboardNotifs = await _context.Notifications
                        .Where(n => n.TargetUserTypeId == null || n.TargetUserTypeId == currentUserRole)
                        .OrderByDescending(n => n.CreatedAt)
                        .Select(n => new {
                            id = n.NotifId,
                            title = n.Title,
                            message = n.Message, // Must include this for the dashboard p tag
                            date = n.CreatedAt.HasValue ? n.CreatedAt.Value.ToString("MMM dd, HH:mm") : "N/A"
                        }).ToListAsync();
                    return Json(new { success = true, data = dashboardNotifs });

                case (CrudAction)50:
                    var roles = await _context.UserTypes
                        .Select(r => new { id = r.UserTypeId, name = r.TypeName })
                        .ToListAsync();
                    return Json(new { success = true, data = roles });

                case CrudAction.EnrollStudent:
                    bool isAlreadyEnrolled = await _context.Enrollments.AnyAsync(e => e.AssignmentId == vm.AssignmentId && e.StudentId == vm.CandidateId);
                    if (isAlreadyEnrolled) return Json(new { success = false, message = "Student is already enrolled in this section." });

                    var adminEnroll = new Enrollment
                    {
                        AssignmentId = vm.AssignmentId,
                        StudentId = vm.CandidateId,
                        CreatedAt = DateTime.Now
                    };
                    _context.Enrollments.Add(adminEnroll);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Student enrolled successfully!" });

                case CrudAction.FetchEnrolledStudents:
                    var enrolledStudents = await _context.Enrollments
                        .Where(e => e.AssignmentId == vm.AssignmentId)
                        .Select(e => new {
                            e.EnrollmentId,
                            FullName = e.Student.FirstName + " " + e.Student.LastName,
                            e.Student.Username,
                            Date = e.CreatedAt.HasValue ? e.CreatedAt.Value.ToString("MMM dd, yyyy") : "N/A"
                        }).ToListAsync();
                    return Json(new { success = true, data = enrolledStudents });

                case CrudAction.DeleteEnrollment:
                    var enrollment = await _context.Enrollments
                        .Include(e => e.Attendances)
                        .FirstOrDefaultAsync(e => e.EnrollmentId == vm.EnrollmentId);
                    if (enrollment == null) return NotFound();

                    _context.Attendances.RemoveRange(enrollment.Attendances);
                    _context.Enrollments.Remove(enrollment);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Student deregistered." });

                default: return BadRequest();
            }
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException != null
                ? ex.InnerException.Message
                : ex.Message;

            return Json(new { success = false, message = "Database Error: " + errorMessage });
        }
    }

    private async Task<IActionResult> RegisterCandidate(CandidateViewModel vm)
    {
        string finalUsername = await GetUsernameWithPrefix(vm.Username, vm.UserTypeId);
        if (await _context.Candidates.AnyAsync(x => x.Username == finalUsername))
            return Json(new { success = false, message = "Username taken." });

        var c = new Candidate
        {
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Email = vm.Email,
            Username = finalUsername,
            PasswordHash = vm.Password,
            DateOfBirth = DateOnly.FromDateTime(vm.DateOfBirth),
            UserTypeId = vm.UserTypeId == 0 ? 1 : vm.UserTypeId,
            PhoneNo = vm.PhoneNo ?? ""
        };
        _context.Candidates.Add(c);
        await _context.SaveChangesAsync();
        await LogActivity("Register", "Success", $"User {finalUsername} created");
        return Json(new { success = true, message = "Registration successful!" });
    }

    private async Task<IActionResult> LoginCandidate(CandidateViewModel vm)
    {
        string finalUsername = await GetUsernameWithPrefix(vm.Username, vm.UserTypeId);
        var user = await _context.Candidates.FirstOrDefaultAsync(x => x.Username == finalUsername && x.PasswordHash == vm.Password);
        if (user == null) return Json(new { success = false, message = "Invalid credentials." });
        user.OtpCode = new Random().Next(100000, 999999).ToString();
        user.OtpExpiry = DateTime.Now.AddMinutes(5);
        await _context.SaveChangesAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await SendOtpEmail(user.Email, user.OtpCode, user.CandidateId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background Email Fail: {ex.Message}");
            }
        });
        return Json(new { success = true, step = "otp", candidateId = user.CandidateId });
    }

    private async Task<IActionResult> VerifyOtp(CandidateViewModel vm)
    {
        var user = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == vm.CandidateId && x.OtpCode == vm.OtpCode);
        if (user == null || user.OtpExpiry < DateTime.Now) return Json(new { success = false, message = "Invalid OTP." });

        user.OtpCode = null;
        await _context.SaveChangesAsync();
        var menus = await _context.RoleMenuMappings
            .Where(m => m.UserTypeId == user.UserTypeId)
            .Select(m => new {
                MenuName = m.Menu.MenuName,
                Url = m.Menu.Url,
                Icon = m.Menu.Icon
            })
            .ToListAsync();
        HttpContext.Session.SetInt32("UserId", user.CandidateId);
        HttpContext.Session.SetInt32("UserTypeId", user.UserTypeId);
        HttpContext.Session.SetString("Username", user.FirstName);
        HttpContext.Session.SetString("UserMenus", JsonConvert.SerializeObject(menus));
        await HttpContext.Session.CommitAsync();

        return Json(new { success = true, redirectUrl = "/Candidate/Dashboard" });
    }

    private async Task SendOtpEmail(string email, string otp, int userId)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config["EmailSettings:SenderName"], _config["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress("User", email));
            message.Subject = "Your Portal Access Code";
            message.Body = new TextPart("plain") { Text = $"OTP: {otp}" };

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config["EmailSettings:SenderEmail"], _config["EmailSettings:AppPassword"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch { /* Silent fail for local testing */ }
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete(".AspNetCore.Session");
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult UserManagement()
    {
        var roleId = HttpContext.Session.GetInt32("UserTypeId");
        if (roleId != 3)
            return RedirectToAction("Dashboard");

        return View();
    }
}