using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public class ColleaguePostLike
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        [ForeignKey("PostId")]
        public virtual ColleaguePost? Post { get; set; }

        [Required]
        [StringLength(100)]
        public string UserIdentifier { get; set; } = string.Empty; // IP or Session or Email

        public DateTime LikedAt { get; set; } = DateTime.UtcNow;
    }
}
