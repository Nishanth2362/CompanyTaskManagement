using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public enum PostCategory
    {
        [Display(Name = "Project Live Demo")]
        ProjectDemo = 1,

        [Display(Name = "Company Update & News")]
        CompanyUpdate = 2,

        [Display(Name = "Tool & Resource")]
        ToolOrResource = 3,

        [Display(Name = "Design & Prototype")]
        DesignPrototype = 4,

        [Display(Name = "Code & Tech Review")]
        CodeReview = 5
    }

    public enum PostStatus
    {
        [Display(Name = "Seeking Feedback")]
        SeekingFeedback = 1,

        [Display(Name = "Under Review")]
        UnderReview = 2,

        [Display(Name = "Ready / Live")]
        Live = 3,

        [Display(Name = "Archived")]
        Archived = 4
    }

    public class ColleaguePost
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; } = string.Empty;

        [Url(ErrorMessage = "Please enter a valid URL")]
        [StringLength(500)]
        public string? LiveUrl { get; set; }

        [Url(ErrorMessage = "Please enter a valid repository URL")]
        [StringLength(500)]
        public string? RepositoryUrl { get; set; }

        public PostCategory Category { get; set; } = PostCategory.ProjectDemo;

        public PostStatus Status { get; set; } = PostStatus.SeekingFeedback;

        [Required(ErrorMessage = "Author name is required")]
        [StringLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(150)]
        public string? AuthorEmail { get; set; }

        [StringLength(100)]
        public string? AuthorDepartment { get; set; } = "Engineering";

        [StringLength(200)]
        public string? Tags { get; set; } // Comma-separated tags e.g. "ASP.NET, AI, Vue, Tailwind"

        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        public bool IsPinned { get; set; } = false;

        public int LikesCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual ICollection<ColleagueFeedback> Feedbacks { get; set; } = new List<ColleagueFeedback>();
        public virtual ICollection<ColleagueReply> Replies { get; set; } = new List<ColleagueReply>();
        public virtual ICollection<ColleagueReaction> Reactions { get; set; } = new List<ColleagueReaction>();
    }
}
