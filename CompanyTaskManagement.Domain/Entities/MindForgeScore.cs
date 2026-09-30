using CompanyTaskManagement.Domain.Contract;
using CompanyTaskManagement.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Domain.Entities
{
    public class MindForgeScore : IEntity<int>
    {
        public int Id { get; set; }

        public int? EmployeeId { get; set; }

        [Required]
        public string EmployeeName { get; set; } = "Anonymous Gamer";

        [Required]
        public string GameType { get; set; } = "MemoryMatch"; // MemoryMatch, WordScramble, DotNetQuiz, MathChallenge

        public int Score { get; set; } = 0;

        public int TimeTakenSeconds { get; set; } = 0;

        public DateTime PlayedAt { get; set; } = DateTime.Now;
    }
}


