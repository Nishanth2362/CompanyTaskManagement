using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public enum FeedbackSentiment
    {
        [Display(Name = "🌟 Highly Impressive")]
        Impressive = 1,

        [Display(Name = "💡 Suggestion / Idea")]
        Suggestion = 2,

        [Display(Name = "🐞 Bug / Issue Found")]
        BugFound = 3,

        [Display(Name = "🚀 Ready for Production")]
        ReadyToShip = 4,

        [Display(Name = "🎨 UI & Design Feedback")]
        DesignFeedback = 5
    }

    public class ColleagueFeedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        [ForeignKey("PostId")]
        public virtual ColleaguePost? Post { get; set; }

        [Required(ErrorMessage = "Colleague name is required")]
        [StringLength(100)]
        public string ColleagueName { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(150)]
        public string? ColleagueEmail { get; set; }

        [StringLength(100)]
        public string? ColleagueRole { get; set; } = "Team Member";

        public FeedbackSentiment Sentiment { get; set; } = FeedbackSentiment.Suggestion;

        [Range(1, 5)]
        public int Rating { get; set; } = 5; // 1 to 5 Stars

        [Required(ErrorMessage = "Feedback comment cannot be empty")]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int HelpfulVotes { get; set; } = 0;

        [StringLength(500)]
        public string? ScreenshotPath { get; set; } // Path to uploaded screenshot image
    }
}
