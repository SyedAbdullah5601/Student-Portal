using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace StudentPortal.Models;

public partial class UserType
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserTypeId { get; set; }
    public string TypeName { get; set; } = null!;
    public string? Prefix { get; set; }
    public virtual ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<RoleMenuMapping> RoleMenuMappings { get; set; } = new List<RoleMenuMapping>();
}
