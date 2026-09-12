using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public class Team
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Team name is required")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int? TeamLeaderId { get; set; }

        [ForeignKey("TeamLeaderId")]
        public virtual Employee? TeamLeader { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

        public virtual ICollection<TeamTask> TeamTasks { get; set; } = new List<TeamTask>();
    }
}
