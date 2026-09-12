using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public class ColleagueReply
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        [ForeignKey("PostId")]
        public virtual ColleaguePost? Post { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? AuthorRole { get; set; } = "Team Member";

        [Required(ErrorMessage = "Reply cannot be empty")]
        [StringLength(1000)]
        public string ReplyText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
