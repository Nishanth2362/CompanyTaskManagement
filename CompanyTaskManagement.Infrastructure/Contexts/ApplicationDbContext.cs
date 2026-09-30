using CompanyTaskManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Infrastructure.Contexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<TaskCompany> TaskCompanies { get; set; }
        public DbSet<TaskEmployee> TaskEmployees { get; set; }
        public DbSet<TaskActivityLog> TaskActivityLogs { get; set; }
        public DbSet<HrComplaint> HrComplaints { get; set; }
        public DbSet<InternStudyMaterial> InternStudyMaterials { get; set; }
        public DbSet<InternDoubt> InternDoubts { get; set; }
        public DbSet<InternDoubtClarification> InternDoubtClarifications { get; set; }
        public DbSet<InternshipMember> InternshipMembers { get; set; }
        public DbSet<InternYouTubeReference> InternYouTubeReferences { get; set; }
        public DbSet<InternTestResult> InternTestResults { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ColleaguePost> ColleaguePosts { get; set; }
        public DbSet<ColleagueFeedback> ColleagueFeedbacks { get; set; }
        public DbSet<ColleaguePostLike> ColleaguePostLikes { get; set; }
        public DbSet<ColleagueReaction> ColleagueReactions { get; set; }
        public DbSet<ColleagueReply> ColleagueReplies { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<TeamTask> TeamTasks { get; set; }
        public DbSet<TeamLeaderReview> TeamLeaderReviews { get; set; }
        public DbSet<MindForgeScore> MindForgeScores { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserAccount
            modelBuilder.Entity<UserAccount>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Project precision
            modelBuilder.Entity<Project>()
                .Property(p => p.Budget)
                .HasPrecision(18, 2);

            // Performance Indexes on TaskItem
            modelBuilder.Entity<TaskItem>()
                .HasIndex(t => t.Status);
            modelBuilder.Entity<TaskItem>()
                .HasIndex(t => t.Priority);
            modelBuilder.Entity<TaskItem>()
                .HasIndex(t => t.CreatedAt);
            modelBuilder.Entity<TaskItem>()
                .HasIndex(t => t.ProjectId);

            // Performance Indexes on TeamTask
            modelBuilder.Entity<TeamTask>()
                .HasIndex(tt => tt.TeamId);
            modelBuilder.Entity<TeamTask>()
                .HasIndex(tt => tt.AssignedToEmployeeId);
            modelBuilder.Entity<TeamTask>()
                .HasIndex(tt => tt.Status);

            // TaskCompany relationship
            modelBuilder.Entity<TaskCompany>()
                .HasKey(tc => new { tc.TaskId, tc.CompanyId });

            modelBuilder.Entity<TaskCompany>()
                .HasOne(tc => tc.Task)
                .WithMany(t => t.TaskCompanies)
                .HasForeignKey(tc => tc.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskCompany>()
                .HasOne(tc => tc.Company)
                .WithMany(c => c.TaskCompanies)
                .HasForeignKey(tc => tc.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskEmployee relationship
            modelBuilder.Entity<TaskEmployee>()
                .HasKey(te => new { te.TaskId, te.EmployeeId });

            modelBuilder.Entity<TaskEmployee>()
                .HasOne(te => te.Task)
                .WithMany(t => t.TaskEmployees)
                .HasForeignKey(te => te.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskEmployee>()
                .HasOne(te => te.Employee)
                .WithMany(e => e.TaskEmployees)
                .HasForeignKey(te => te.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Initial Seed
            modelBuilder.Entity<Company>().HasData(
                new Company { Id = 1, Name = "Ameobatronics" },
                new Company { Id = 2, Name = "Vigo Solutions" },
                new Company { Id = 3, Name = "Auxinzio" }
            );
        }
    }
}
