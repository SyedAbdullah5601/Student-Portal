using Google.Authenticator;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Newtonsoft.Json;
using StudentPortal.Models;
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
                UserId = userId ?? HttpContext.Session.GetInt32("UserId"),
                UserRole = role ?? HttpContext.Session.GetInt32("UserTypeId")?.ToString(),
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet] public IActionResult Login() => View();
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet] public IActionResult Register() => View();

    [HttpGet]
    public IActionResult Dashboard()
    {
        ViewBag.UserRole = HttpContext.Session.GetInt32("UserTypeId");
        ViewBag.UserId = HttpContext.Session.GetInt32("UserId");
        return View();
    }

    [HttpGet] public IActionResult Attendance() => View();
    [HttpGet] public IActionResult Logs() => View();
    [HttpGet] public IActionResult AdminManagement() => View();
    [HttpGet] public IActionResult UserManagement() => (HttpContext.Session.GetInt32("UserTypeId") == 3) ? View() : RedirectToAction("Dashboard");
    [HttpGet] public IActionResult Notifications() => (HttpContext.Session.GetInt32("UserTypeId") == 3) ? View() : RedirectToAction("Dashboard");
    [HttpGet] public IActionResult AssignCourses() => View();
    [HttpGet] public IActionResult Enroll() => View();
    [HttpGet] public IActionResult Grades() => View();
    [HttpGet] public IActionResult MyCourses() => View();
    [HttpGet] public IActionResult MyClasses() => View();
    [HttpGet] public IActionResult Profile() => View();

    [HttpGet]
    public IActionResult StudentManagement(int assignmentId)
    {
        ViewBag.AssignmentId = assignmentId;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (userId.HasValue)
        {
            var user = await _context.Candidates.FindAsync(userId.Value);
            if (user != null)
            {
                user.CurrentSessionId = null;
                await _context.SaveChangesAsync();
                await LogActivity("Logout", "Success", $"User {user.Username} logged out");
            }
        }
        HttpContext.Session.Clear();
        Response.Cookies.Delete(".AspNetCore.Session");
        return RedirectToAction("Login");
    }
    #endregion

    [HttpPost]
    public async Task<IActionResult> HandleAction([FromBody] CandidateViewModel vm)
    {
        try
        {
            switch (vm.Operaid)
            {
                case CrudAction.Login:
                    return await LoginCandidate(vm);

                case CrudAction.VerifyAuth:
                    return await VerifyAuthenticator(vm);

                case CrudAction.Select:
                    var student = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == vm.CandidateId);
                    if (student == null) return NotFound();
                    return Json(new { success = true, data = new { student.FirstName, student.LastName, student.Email, student.Username, student.PhoneNo, student.UserTypeId, DateOfBirth = student.DateOfBirth.ToString("yyyy-MM-dd") } });

                case CrudAction.Verify:
                    string vUsername = await GetUsernameWithPrefix(vm.Username, vm.UserTypeId);
                    return Json(new { isAvailable = !await _context.Candidates.AnyAsync(x => x.Username == vUsername) });

                case CrudAction.Insert:
                    // If there's no session and they have a name, it's a self-registration
                    if (HttpContext.Session.GetInt32("UserId") == null && !string.IsNullOrEmpty(vm.FirstName))
                    {
                        return await RegisterCandidate(vm);
                    }

                    int currentId = HttpContext.Session.GetInt32("UserId") ?? 0;
                    if (await _context.Enrollments.AnyAsync(e => e.AssignmentId == vm.AssignmentId && e.StudentId == currentId))
                        return Json(new { success = false, message = "Already enrolled." });

                    _context.Enrollments.Add(new Enrollment { AssignmentId = vm.AssignmentId, StudentId = currentId, CreatedAt = DateTime.Now });
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Successfully enrolled!" });

                case CrudAction.AdminCreateUser:
                    return await RegisterCandidate(vm);

                case CrudAction.FetchLogs:
                    var logs = await _context.SystemLogs.OrderByDescending(l => l.Timestamp).Take(100).ToListAsync();
                    return Json(new { success = true, data = logs });

                case CrudAction.FetchAllMenus:
                    return Json(new
                    {
                        success = true,
                        menus = await _context.Menus.ToListAsync(),
                        roles = await _context.UserTypes.Select(r => new { r.UserTypeId, r.TypeName }).ToListAsync(),
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

                case CrudAction.FetchAttendanceSheet:
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

                case CrudAction.SaveGrades:
                    if (string.IsNullOrEmpty(vm.BulkData)) return BadRequest();
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
                            var studentAttendances = userToDelete.Enrollments.SelectMany(e => e.Attendances ?? new List<Attendance>()).ToList();
                            _context.Attendances.RemoveRange(studentAttendances);
                            _context.Enrollments.RemoveRange(userToDelete.Enrollments);
                            foreach (var assignment in userToDelete.CourseAssignments)
                            {
                                var studentInClassAttendances = assignment.Enrollments.SelectMany(e => e.Attendances ?? new List<Attendance>()).ToList();
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
                            return Json(new { success = false, message = "Cannot change Role: User has active links." });
                        }
                        userToUpdate.UserTypeId = vm.UserTypeId;
                    }
                    userToUpdate.FirstName = vm.FirstName;
                    userToUpdate.LastName = vm.LastName;
                    userToUpdate.Email = vm.Email;
                    userToUpdate.PhoneNo = vm.PhoneNo ?? "";
                    await _context.SaveChangesAsync();
                    await LogActivity("Admin Update", "Success", $"Updated user: {userToUpdate.Username}");
                    return Json(new { success = true });

                case CrudAction.FetchCourses:
                    return Json(new { success = true, data = await _context.Courses.ToListAsync() });

                case CrudAction.FetchFacultyCourses:
                    int? currentTeacherId = HttpContext.Session.GetInt32("UserId");
                    var assignments = await _context.CourseAssignments
                        .Where(a => a.FacultyId == currentTeacherId)
                        .Select(a => new {
                            a.AssignmentId,
                            courseCode = a.Course.CourseCode,
                            courseName = a.Course.CourseName,
                            sectionName = a.SectionName,
                            studentCount = a.Enrollments.Count()
                        }).ToListAsync();
                    return Json(new { success = true, data = assignments });

                case CrudAction.FetchAssignments:
                    var assignedList = await _context.CourseAssignments
                        .Include(a => a.Course).Include(a => a.Faculty)
                        .Select(a => new {
                            a.AssignmentId,
                            CourseCode = a.Course.CourseCode,
                            CourseName = a.Course.CourseName,
                            FacultyName = a.Faculty.FirstName + " " + a.Faculty.LastName,
                            a.SectionName
                        }).ToListAsync();
                    return Json(new { success = true, data = assignedList });

                case CrudAction.SaveAssignment:
                    if (await _context.CourseAssignments.AnyAsync(a => a.CourseId == vm.CourseId && a.FacultyId == vm.CandidateId && a.SectionName == vm.SectionName))
                        return Json(new { success = false, message = "Already assigned." });

                    _context.CourseAssignments.Add(new CourseAssignment { CourseId = vm.CourseId, FacultyId = vm.CandidateId, SectionName = vm.SectionName ?? "A" });
                    await _context.SaveChangesAsync();
                    await LogActivity("Course Assignment", "Success", $"Assigned Faculty {vm.CandidateId} to Course {vm.CourseId}");
                    return Json(new { success = true });

                case CrudAction.DeleteAssignment:
                    var assignDelete = await _context.CourseAssignments.Include(a => a.Enrollments).FirstOrDefaultAsync(a => a.AssignmentId == vm.AssignmentId);
                    if (assignDelete == null) return NotFound();
                    if (assignDelete.Enrollments.Any()) return Json(new { success = false, message = "Students are enrolled." });
                    _context.CourseAssignments.Remove(assignDelete);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                case CrudAction.CreateUserType:
                    var newUserType = new UserType { TypeName = vm.NewUserTypeName, Prefix = vm.NewUserPrefix };
                    _context.UserTypes.Add(newUserType);
                    await _context.SaveChangesAsync();
                    await LogActivity("Create Role", "Success", $"Created: {vm.NewUserTypeName}");
                    return Json(new { success = true });

                case CrudAction.CreateCourse:
                    var newCourse = new Course { CourseCode = vm.NewCourseCode, CourseName = vm.NewCourseName, Description = vm.NewCourseDescription };
                    _context.Courses.Add(newCourse);
                    await _context.SaveChangesAsync();
                    await LogActivity("Create Course", "Success", $"Created: {vm.NewCourseCode}");
                    return Json(new { success = true });

                case CrudAction.DeleteCourse:
                    var cDel = await _context.Courses.Include(c => c.CourseAssignments).FirstOrDefaultAsync(c => c.CourseId == vm.CourseId);
                    if (cDel == null) return NotFound();
                    if (cDel.CourseAssignments.Any()) return Json(new { success = false, message = "Course is assigned." });
                    _context.Courses.Remove(cDel);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                case CrudAction.FetchProfile:
                    int? pId = HttpContext.Session.GetInt32("UserId");
                    var prof = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == pId);
                    if (prof == null) return NotFound();
                    return Json(new { success = true, data = new { prof.FirstName, prof.LastName, prof.Email, prof.Username, prof.PhoneNo, DateOfBirth = prof.DateOfBirth.ToString("yyyy-MM-dd") } });

                case CrudAction.UpdateProfile:
                    int? currentUserId = HttpContext.Session.GetInt32("UserId");
                    var userToEdit = await _context.Candidates.FindAsync(currentUserId);
                    if (userToEdit == null) return Json(new { success = false, message = "User not found" });
                    userToEdit.FirstName = vm.FirstName;
                    userToEdit.LastName = vm.LastName;
                    userToEdit.Email = vm.Email;
                    userToEdit.PhoneNo = vm.PhoneNo ?? "";
                    userToEdit.DateOfBirth = DateOnly.FromDateTime(vm.DateOfBirth);
                    if (!string.IsNullOrEmpty(vm.Password)) userToEdit.PasswordHash = vm.Password;
                    await _context.SaveChangesAsync();
                    HttpContext.Session.SetString("Username", userToEdit.FirstName);
                    await LogActivity("Update Profile", "Success", $"User {userToEdit.Username} updated info");
                    return Json(new { success = true });

                case CrudAction.FetchStudentAttendance:
                    int? curId = HttpContext.Session.GetInt32("UserId");
                    var myData = await _context.Enrollments.Where(e => e.StudentId == curId)
                        .Select(e => new {
                            CourseCode = e.Assignment.Course.CourseCode,
                            CourseName = e.Assignment.Course.CourseName,
                            TotalMarks = e.Mark ?? 0,
                            AttendanceHistory = e.Attendances.Select(a => new { a.WeekNumber, a.Status }).ToList(),
                            TotalWeeks = 16
                        }).ToListAsync();
                    return Json(new { success = true, data = myData });

                case CrudAction.CreateMenu:
                    _context.Menus.Add(new Menu { MenuName = vm.MenuName, Url = vm.Url, Icon = vm.Icon ?? "bi bi-circle" });
                    await _context.SaveChangesAsync();
                    await LogActivity("Create Menu", "Success", $"Created menu: {vm.MenuName}");
                    return Json(new { success = true });

                case CrudAction.FetchDashboardStats:
                    int? uId = HttpContext.Session.GetInt32("UserId");
                    int? rId = HttpContext.Session.GetInt32("UserTypeId");
                    if (rId == 3)
                    {
                        return Json(new
                        {
                            success = true,
                            stats = new
                            {
                                TotalStudents = await _context.Candidates.CountAsync(u => u.UserTypeId == 1),
                                TotalFaculty = await _context.Candidates.CountAsync(u => u.UserTypeId == 2),
                                ActiveCourses = await _context.Courses.CountAsync(),
                                RecentLogs = await _context.SystemLogs.CountAsync(l => l.Timestamp >= DateTime.Now.AddDays(-1))
                            }
                        });
                    }
                    else if (rId == 1)
                    {
                        return Json(new { success = true, stats = new { MyEnrollments = await _context.Enrollments.CountAsync(e => e.StudentId == uId), AvgAttendance = 0 } });
                    }
                    return BadRequest();

                case CrudAction.CreateNotification:
                    _context.Notifications.Add(new Notification { Title = vm.NotifTitle, Message = vm.NotifMessage, TargetUserTypeId = vm.TargetUserTypeId == 0 ? null : vm.TargetUserTypeId, CreatedAt = DateTime.Now });
                    await _context.SaveChangesAsync();
                    await LogActivity("Notification Broadcast", "Success", $"Published: {vm.NotifTitle}");
                    return Json(new { success = true });

                case CrudAction.FetchAllNotifications:
                    var allNotifs = await _context.Notifications.Include(n => n.TargetUserType).OrderByDescending(n => n.CreatedAt)
                        .Select(n => new { id = n.NotifId, title = n.Title, targetName = n.TargetUserType != null ? n.TargetUserType.TypeName : "Public", date = n.CreatedAt.HasValue ? n.CreatedAt.Value.ToString("MMM dd, HH:mm") : "N/A" }).ToListAsync();
                    return Json(new { success = true, data = allNotifs });

                case CrudAction.DeleteNotification:
                    var nDel = await _context.Notifications.FindAsync(vm.NotifId);
                    if (nDel != null) { _context.Notifications.Remove(nDel); await _context.SaveChangesAsync(); }
                    return Json(new { success = true });

                case CrudAction.FetchNotifications:
                    int? role = HttpContext.Session.GetInt32("UserTypeId");
                    var dNotifs = await _context.Notifications.Where(n => n.TargetUserTypeId == null || n.TargetUserTypeId == role).OrderByDescending(n => n.CreatedAt)
                        .Select(n => new { id = n.NotifId, title = n.Title, message = n.Message, date = n.CreatedAt.HasValue ? n.CreatedAt.Value.ToString("MMM dd, HH:mm") : "N/A" }).ToListAsync();
                    return Json(new { success = true, data = dNotifs });

                case (CrudAction)50:
                    return Json(new { success = true, data = await _context.UserTypes.Select(r => new { id = r.UserTypeId, name = r.TypeName }).ToListAsync() });

                case CrudAction.EnrollStudent:
                    if (await _context.Enrollments.AnyAsync(e => e.AssignmentId == vm.AssignmentId && e.StudentId == vm.CandidateId))
                        return Json(new { success = false, message = "Already enrolled." });
                    _context.Enrollments.Add(new Enrollment { AssignmentId = vm.AssignmentId, StudentId = vm.CandidateId, CreatedAt = DateTime.Now });
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                case CrudAction.FetchEnrolledStudents:
                    var enrolled = await _context.Enrollments.Where(e => e.AssignmentId == vm.AssignmentId)
                        .Select(e => new { e.EnrollmentId, FullName = e.Student.FirstName + " " + e.Student.LastName, e.Student.Username, Date = e.CreatedAt.HasValue ? e.CreatedAt.Value.ToString("MMM dd, yyyy") : "N/A" }).ToListAsync();
                    return Json(new { success = true, data = enrolled });

                case CrudAction.DeleteEnrollment:
                    var enroll = await _context.Enrollments.Include(e => e.Attendances).FirstOrDefaultAsync(e => e.EnrollmentId == vm.EnrollmentId);
                    if (enroll == null) return NotFound();
                    _context.Attendances.RemoveRange(enroll.Attendances);
                    _context.Enrollments.Remove(enroll);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });

                default: return BadRequest();
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Database Error: " + (ex.InnerException?.Message ?? ex.Message) });
        }
    }

    #region Private Action Logic
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

    private TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();

    private async Task<IActionResult> LoginCandidate(CandidateViewModel vm)
    {
        string finalUsername = await GetUsernameWithPrefix(vm.Username, vm.UserTypeId);
        var user = await _context.Candidates.FirstOrDefaultAsync(x => x.Username == finalUsername && x.PasswordHash == vm.Password);

        if (user == null) return Json(new { success = false, message = "Invalid credentials." });
        if (!string.IsNullOrEmpty(user.CurrentSessionId))
        {
            return Json(new { success = false, message = "User is already logged in on another device." });
        }

        if (!user.IsTwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                user.TwoFactorSecret = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
                await _context.SaveChangesAsync();
            }
            var setupInfo = tfa.GenerateSetupCode("Student Portal", user.Email, user.TwoFactorSecret, false, 3);
            return Json(new
            {
                success = true,
                step = "setup",
                candidateId = user.CandidateId,
                qrCodeUrl = setupInfo.QrCodeSetupImageUrl,
                manualKey = setupInfo.ManualEntryKey
            });
        }
        return Json(new { success = true, step = "verify_2fa", candidateId = user.CandidateId });
    }

    private async Task<IActionResult> VerifyAuthenticator(CandidateViewModel vm)
    {
        var user = await _context.Candidates.FirstOrDefaultAsync(x => x.CandidateId == vm.CandidateId);
        if (user == null) return Json(new { success = false, message = "User not found." });
        if (!string.IsNullOrEmpty(user.LastUsedOtp) && user.LastUsedOtp == vm.OtpCode)
        {
            return Json(new { success = false, message = "This code has already been used. Please wait for the next 30-second cycle." });
        }
        bool isValid = tfa.ValidateTwoFactorPIN(user.TwoFactorSecret, vm.OtpCode, false);
        if (!isValid) return Json(new { success = false, message = "Invalid Authenticator code." });
        if (!user.IsTwoFactorEnabled) user.IsTwoFactorEnabled = true;
        user.LastUsedOtp = vm.OtpCode;
        string newSessionId = Guid.NewGuid().ToString();
        user.CurrentSessionId = newSessionId;
        await _context.SaveChangesAsync();
        var menus = await _context.RoleMenuMappings
            .Where(m => m.UserTypeId == user.UserTypeId)
            .Select(m => new { m.Menu.MenuName, m.Menu.Url, m.Menu.Icon }).ToListAsync();
        HttpContext.Session.SetInt32("UserId", user.CandidateId);
        HttpContext.Session.SetInt32("UserTypeId", user.UserTypeId);
        HttpContext.Session.SetString("UserSessionGuid", newSessionId);
        HttpContext.Session.SetString("Username", user.FirstName);
        HttpContext.Session.SetString("UserMenus", JsonConvert.SerializeObject(menus));
        HttpContext.Session.SetString("UserAgent", Request.Headers["User-Agent"].ToString());
        return Json(new { success = true, redirectUrl = "/Candidate/Dashboard" });
    }
    #endregion
}