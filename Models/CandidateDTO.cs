public enum CrudAction
{
    Select = 1,
    Insert = 2,
    Login = 3,
    Verify = 5,
    VerifyOtp = 6,
    FetchLogs = 7,
    ResendOtp = 8,
    FetchAllMenus = 10,
    SaveMenuMapping = 11,
    CreateMenu = 12,
    AdminCreateUser = 13,
    FetchFacultyCourses = 14,
    SaveAttendance = 15,
    FetchAttendanceSheet = 16,
    FetchStudentAttendance = 17,
    FetchGradeSheet = 18,
    SaveGrades = 19,
    FetchAllUsers = 20,
    DeleteUser = 21,
    UpdateUser = 22,
    FetchCourseDetails = 23,
    FetchAssignments = 24,
    SaveAssignment = 25,
    FetchCourses = 26,
    DeleteAssignment = 27,
    FetchDashboardStats = 28,
    CreateUserType = 29,
    CreateCourse = 30,
    DeleteCourse = 31,
    FetchProfile = 35,
    UpdateProfile = 40,
    FetchNotifications = 41,    
    CreateNotification = 42,     
    DeleteNotification = 43,      
    FetchAllNotifications = 44,
    EnrollStudent = 45,
    FetchEnrolledStudents = 46,
    DeleteEnrollment = 47
}

public class CandidateViewModel
{
    public CrudAction Operaid { get; set; }
    public int? CandidateId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? PhoneNo { get; set; }
    public int UserTypeId { get; set; }
    public string? OtpCode { get; set; }
    public int? MenuId { get; set; }
    public string? MenuName { get; set; }
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public int? AssignmentId { get; set; }
    public int? EnrollmentId { get; set; }
    public int? WeekNumber { get; set; }
    public string? Status { get; set; }
    public string? CourseName { get; set; }
    public int PresentCount { get; set; }
    public int TotalClasses { get; set; }
    public double AttendancePercentage { get; set; }
    public decimal? Marks { get; set; }
    public int? CourseId { get; set; }
    public string? SectionName { get; set; }
    public List<int>? SelectedMenuIds { get; set; }
    public string? NewUserTypeName { get; set; }
    public string? NewUserPrefix { get; set; }
    public string? NewCourseCode { get; set; }
    public string? NewCourseDescription { get; set; }
    public string? NewCourseName { get; set; }
    public string? BulkData { get; set; }
    public List<int>? PresentWeeks { get; set; }
    public int? NotifId { get; set; }
    public string? NotifTitle { get; set; }
    public string? NotifMessage { get; set; }
    public int? TargetUserTypeId { get; set; }
}
public class BulkAttendanceUpdate
{
    public int EnrollmentId { get; set; }
    public decimal Marks { get; set; }
    public List<int> PresentWeeks { get; set; } = new();
}