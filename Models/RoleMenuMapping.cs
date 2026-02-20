using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class RoleMenuMapping
{
    public int MappingId { get; set; }

    public int? UserTypeId { get; set; }

    public int? MenuId { get; set; }

    public virtual Menu? Menu { get; set; }

    public virtual UserType? UserType { get; set; }
}
