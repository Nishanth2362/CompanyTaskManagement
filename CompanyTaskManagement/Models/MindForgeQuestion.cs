using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class MindForgeQuestion
    {
        public int Id { get; set; }

        [Required]
        public string GameType { get; set; } = "WordScramble"; // WordScramble, DotNetQuiz, MathChallenge, MemoryMatch

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        public string? ScrambledOrSnippet { get; set; }

        [Required]
        public string CorrectAnswer { get; set; } = string.Empty;

        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }

        public string? Explanation { get; set; }
        public string Difficulty { get; set; } = "Medium";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
