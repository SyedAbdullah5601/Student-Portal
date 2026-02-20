using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class SystemLog
{
    public int LogId { get; set; }

    public DateTime? Timestamp { get; set; }

    public int? UserId { get; set; }

    public string? UserRole { get; set; }

    public string Action { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public string? ControllerName { get; set; }

    public string? ActionName { get; set; }

    public virtual Candidate? User { get; set; }
}
