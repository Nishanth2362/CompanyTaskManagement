using System.Collections.Generic;
using CompanyTaskManagement.Models;

namespace CompanyTaskManagement.View_Model
{
    public class MotivationalQuoteItem
    {
        public string Quote { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Category { get; set; } = "Growth & Resilience";
        public string Icon { get; set; } = "bi-lightbulb-fill";
    }

    public class EmployeeAppreciationViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int TotalAssignedTasks { get; set; }
        public int CompletedTasksCount { get; set; }
        public int PerfectTasksCount { get; set; }
        public double CompletionRate { get; set; }
        public double PerfectRate { get; set; }
        public int Rank { get; set; }
        public string AwardTitle { get; set; } = "Outstanding Contributor";
        public string AwardDescription { get; set; } = string.Empty;
        public string BadgeGradient { get; set; } = "linear-gradient(135deg, #f59e0b, #d97706)";
        public bool IsStarPerformer { get; set; }
        public List<string> AssignedCompanies { get; set; } = new List<string>();
        public List<string> PerfectlyCompletedTasks { get; set; } = new List<string>();

        // Motivational & Growth Mindset Properties (No discouragement, all members appreciated)
        public string MotivationalQuote { get; set; } = string.Empty;
        public string MotivationalAuthor { get; set; } = string.Empty;
        public string GrowthMindsetNote { get; set; } = string.Empty;
        public string EncouragementTag { get; set; } = "Valued Team Pillar";
    }

    public class WeeklyAppreciationPageViewModel
    {
        public List<EmployeeAppreciationViewModel> Leaderboard { get; set; } = new List<EmployeeAppreciationViewModel>();
        public EmployeeAppreciationViewModel? StarPerformer { get; set; }
        public int TotalSystemTasks { get; set; }
        public int TotalCompletedTasks { get; set; }
        public int TotalPerfectTasks { get; set; }
        public double OverallSystemPerfectionRate { get; set; }
        public List<TaskActivityLog> AllActivityLogs { get; set; } = new List<TaskActivityLog>();
        public List<MotivationalQuoteItem> DailyInspirations { get; set; } = new List<MotivationalQuoteItem>();
    }
}
