using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class Enrollment
{
    public int EnrollmentId { get; set; }

    public int? AssignmentId { get; set; }

    public int? StudentId { get; set; }

    public int? Mark { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CourseAssignment? Assignment { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual Candidate? Student { get; set; }
}
