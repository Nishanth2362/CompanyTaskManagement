namespace CompanyTaskManagement.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "noreply@companytask.com";
        public string SenderPassword { get; set; } = string.Empty;
        public string HrEmailAddress { get; set; } = "hr@companytask.com";
        public bool UseSimulationMode { get; set; } = true;
    }
}
