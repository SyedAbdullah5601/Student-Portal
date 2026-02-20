using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class Course
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<CourseAssignment> CourseAssignments { get; set; } = new List<CourseAssignment>();
}
