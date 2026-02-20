using System;
using System.Collections.Generic;

namespace StudentPortal.Models;

public partial class Menu
{
    public int MenuId { get; set; }

    public string MenuName { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? Icon { get; set; }

    public virtual ICollection<RoleMenuMapping> RoleMenuMappings { get; set; } = new List<RoleMenuMapping>();
}
