using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class Notification
{
    public int NotifId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public int? TargetUserTypeId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual UserType? TargetUserType { get; set; }
}
