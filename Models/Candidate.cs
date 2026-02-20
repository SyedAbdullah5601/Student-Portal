using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class Candidate
{
    public int CandidateId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string PhoneNo { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public int UserTypeId { get; set; }

    public string? OtpCode { get; set; }

    public DateTime? OtpExpiry { get; set; }

    public string? ProfilePicturePath { get; set; }

    public virtual ICollection<CourseAssignment> CourseAssignments { get; set; } = new List<CourseAssignment>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();

    public virtual UserType UserType { get; set; } = null!;
}
