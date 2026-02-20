using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class CourseAssignment
{
    public int AssignmentId { get; set; }

    public int? CourseId { get; set; }

    public int? FacultyId { get; set; }

    public string SectionName { get; set; } = null!;

    public virtual Course? Course { get; set; }

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual Candidate? Faculty { get; set; }
}
