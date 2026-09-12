using System;
using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class InternTestResult
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Intern name is required")]
        [StringLength(150)]
        public string InternName { get; set; } = string.Empty;

        [StringLength(150)]
        public string? InternEmail { get; set; }

        [Required(ErrorMessage = "Topic title is required")]
        [StringLength(200)]
        public string TopicTitle { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Track { get; set; } = "Backend .NET / C#";

        public int Score { get; set; }

        public int TotalQuestions { get; set; } = 5;

        public double ScorePercentage { get; set; }

        public bool IsPassed { get; set; }

        [StringLength(100)]
        public string GradeBadge { get; set; } = "Passed";

        [StringLength(1000)]
        public string? FeedbackNotes { get; set; }

        [StringLength(500)]
        public string? CertificateProofPath { get; set; }

        public DateTime TakenAt { get; set; } = DateTime.Now;
    }
}
