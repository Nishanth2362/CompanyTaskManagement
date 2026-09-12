using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public enum ReactionType
    {
        OK = 1,
        NotOK = 2,
        NeedsWork = 3,
        Approved = 4,
        HasIssues = 5
    }

    public class ColleagueReaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        [ForeignKey("PostId")]
        public virtual ColleaguePost? Post { get; set; }

        [Required]
        [StringLength(100)]
        public string UserIdentifier { get; set; } = string.Empty; // IP or name

        [StringLength(100)]
        public string ReactorName { get; set; } = "Anonymous";

        public ReactionType Reaction { get; set; } = ReactionType.OK;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
