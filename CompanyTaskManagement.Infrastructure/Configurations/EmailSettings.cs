namespace CompanyTaskManagement.Infrastructure.Configurations
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = "system@auxinz.io";
        public string SenderPassword { get; set; } = "";
        public string CcEmailAddress { get; set; } = "";
        public string BccEmailAddress { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        public bool UseSimulationMode { get; set; } = true;
    }
}
