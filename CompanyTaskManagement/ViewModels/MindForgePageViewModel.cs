using CompanyTaskManagement.Models;

namespace CompanyTaskManagement.ViewModels
{
    public class MindForgePageViewModel
    {
        public List<MindForgeScore> TopLeaderboardScores { get; set; } = new List<MindForgeScore>();
        public int TotalGamesPlayed { get; set; }
        public int HighScoreToday { get; set; }
    }

    public class SubmitMindForgeScoreDto
    {
        public string GameType { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TimeTakenSeconds { get; set; }
    }
}
