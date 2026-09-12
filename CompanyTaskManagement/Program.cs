using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Configure HTTPS redirection options to prevent 'Failed to determine https port' warnings
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 7222;
});

// Add HttpContextAccessor and Session for Role-Based Access Control
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<IUserSessionService, UserSessionService>();

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    );
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Add Email Service & Background Worker
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddHostedService<OverdueTaskNotifierService>();

var app = builder.Build();

// Automatically update DB schema & seed initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Ensure database exists
        context.Database.EnsureCreated();

        // Safely add missing columns to existing Tasks table if not already present
        var alterSql = @"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Priority')
                ALTER TABLE [Tasks] ADD [Priority] INT NOT NULL DEFAULT 1;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Status')
                ALTER TABLE [Tasks] ADD [Status] INT NOT NULL DEFAULT 0;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'DueDate')
                ALTER TABLE [Tasks] ADD [DueDate] DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'CreatedAt')
                ALTER TABLE [Tasks] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE();

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Progress')
                ALTER TABLE [Tasks] ADD [Progress] INT NOT NULL DEFAULT 0;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'DelayReason')
                ALTER TABLE [Tasks] ADD [DelayReason] NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ErrorDetails')
                ALTER TABLE [Tasks] ADD [ErrorDetails] NVARCHAR(1000) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ErrorScreenshotPath')
                ALTER TABLE [Tasks] ADD [ErrorScreenshotPath] NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ProjectId')
                ALTER TABLE [Tasks] ADD [ProjectId] INT NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'StartDate')
                ALTER TABLE [Tasks] ADD [StartDate] DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'EndDate')
                ALTER TABLE [Tasks] ADD [EndDate] DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Email')
                ALTER TABLE [Employees] ADD [Email] NVARCHAR(150) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Designation')
                ALTER TABLE [Employees] ADD [Designation] NVARCHAR(100) NULL DEFAULT 'Software Engineer';

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Department')
                ALTER TABLE [Employees] ADD [Department] NVARCHAR(100) NULL DEFAULT 'Engineering';

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Phone')
                ALTER TABLE [Employees] ADD [Phone] NVARCHAR(50) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'CreatedAt')
                ALTER TABLE [Employees] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE();

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'IsActive')
                ALTER TABLE [Employees] ADD [IsActive] BIT NOT NULL DEFAULT 1;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ColleagueFeedbacks]') AND name = 'ScreenshotPath')
                ALTER TABLE [ColleagueFeedbacks] ADD [ScreenshotPath] NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TaskActivityLogs')
            BEGIN
                CREATE TABLE [TaskActivityLogs] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TaskId] INT NOT NULL,
                    [TaskName] NVARCHAR(200) NOT NULL,
                    [EmployeeId] INT NULL,
                    [EmployeeName] NVARCHAR(150) NULL,
                    [ActionType] NVARCHAR(100) NOT NULL,
                    [OldStatus] NVARCHAR(50) NULL,
                    [NewStatus] NVARCHAR(50) NULL,
                    [Progress] INT NOT NULL DEFAULT 0,
                    [DelayReason] NVARCHAR(MAX) NULL,
                    [ErrorDetails] NVARCHAR(MAX) NULL,
                    [ErrorScreenshotPath] NVARCHAR(500) NULL,
                    [LoggedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HrComplaints')
            BEGIN
                CREATE TABLE [HrComplaints] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TicketNumber] NVARCHAR(50) NOT NULL,
                    [Subject] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [Category] NVARCHAR(100) NOT NULL,
                    [Priority] NVARCHAR(50) NOT NULL,
                    [IsAnonymous] BIT NOT NULL DEFAULT 0,
                    [EmployeeId] INT NULL,
                    [SubmitterName] NVARCHAR(150) NULL,
                    [SubmitterEmail] NVARCHAR(150) NULL,
                    [Department] NVARCHAR(100) NULL,
                    [AttachmentPath] NVARCHAR(500) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Submitted',
                    [HrResponseNotes] NVARCHAR(MAX) NULL,
                    [HrInvestigatorName] NVARCHAR(150) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    [ResolvedAt] DATETIME2 NULL
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternStudyMaterials')
            BEGIN
                CREATE TABLE [InternStudyMaterials] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Title] NVARCHAR(200) NOT NULL,
                    [Track] NVARCHAR(100) NOT NULL,
                    [Difficulty] NVARCHAR(50) NOT NULL,
                    [ContentType] NVARCHAR(50) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [ResourceUrl] NVARCHAR(500) NULL,
                    [FilePath] NVARCHAR(500) NULL,
                    [EstimatedMinutes] INT NOT NULL DEFAULT 30,
                    [Tags] NVARCHAR(200) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternDoubts')
            BEGIN
                CREATE TABLE [InternDoubts] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Title] NVARCHAR(250) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [CodeSnippet] NVARCHAR(MAX) NULL,
                    [Domain] NVARCHAR(100) NOT NULL,
                    [Urgency] NVARCHAR(50) NOT NULL DEFAULT 'Normal',
                    [InternName] NVARCHAR(150) NOT NULL,
                    [InternEmployeeId] INT NULL,
                    [ScreenshotPath] NVARCHAR(500) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Open',
                    [Upvotes] INT NOT NULL DEFAULT 0,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternDoubtClarifications')
            BEGIN
                CREATE TABLE [InternDoubtClarifications] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [InternDoubtId] INT NOT NULL,
                    [ClarifiedByEmployeeName] NVARCHAR(150) NOT NULL,
                    [EmployeeId] INT NULL,
                    [ClarificationText] NVARCHAR(MAX) NOT NULL,
                    [CodeSolution] NVARCHAR(MAX) NULL,
                    [HelpfulLink] NVARCHAR(500) NULL,
                    [IsAcceptedSolution] BIT NOT NULL DEFAULT 0,
                    [AnsweredAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternshipMembers')
            BEGIN
                CREATE TABLE [InternshipMembers] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Name] NVARCHAR(150) NOT NULL,
                    [Email] NVARCHAR(150) NULL,
                    [Role] NVARCHAR(100) NOT NULL DEFAULT 'Intern',
                    [Domain] NVARCHAR(100) NOT NULL DEFAULT 'Backend .NET / C#',
                    [MentorId] INT NULL,
                    [MentorName] NVARCHAR(150) NULL,
                    [JoinedDate] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
                    [Notes] NVARCHAR(500) NULL,
                    [CompletedModulesCount] INT NOT NULL DEFAULT 0
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternYouTubeReferences')
            BEGIN
                CREATE TABLE [InternYouTubeReferences] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Title] NVARCHAR(200) NOT NULL,
                    [YouTubeUrl] NVARCHAR(500) NOT NULL,
                    [YouTubeVideoId] NVARCHAR(50) NOT NULL,
                    [Track] NVARCHAR(100) NOT NULL DEFAULT 'Backend .NET / C#',
                    [Difficulty] NVARCHAR(50) NOT NULL DEFAULT 'Beginner',
                    [ChannelOrMentorName] NVARCHAR(150) NULL,
                    [DurationMinutes] INT NOT NULL DEFAULT 30,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [Tags] NVARCHAR(200) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InternTestResults')
            BEGIN
                CREATE TABLE [InternTestResults] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [InternName] NVARCHAR(150) NOT NULL,
                    [InternEmail] NVARCHAR(150) NULL,
                    [TopicTitle] NVARCHAR(200) NOT NULL,
                    [Track] NVARCHAR(100) NOT NULL DEFAULT 'Backend .NET / C#',
                    [Score] INT NOT NULL DEFAULT 0,
                    [TotalQuestions] INT NOT NULL DEFAULT 5,
                    [ScorePercentage] FLOAT NOT NULL DEFAULT 0,
                    [IsPassed] BIT NOT NULL DEFAULT 1,
                    [GradeBadge] NVARCHAR(100) NOT NULL DEFAULT 'Passed',
                    [FeedbackNotes] NVARCHAR(1000) NULL,
                    [CertificateProofPath] NVARCHAR(500) NULL,
                    [TakenAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Projects')
            BEGIN
                CREATE TABLE [Projects] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [ProjectName] NVARCHAR(150) NOT NULL,
                    [ClientCompany] NVARCHAR(150) NULL,
                    [Description] NVARCHAR(2000) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'In Progress',
                    [Priority] NVARCHAR(50) NOT NULL DEFAULT 'High',
                    [StartDate] DATETIME2 NULL,
                    [TargetEndDate] DATETIME2 NULL,
                    [Budget] DECIMAL(18,2) NULL,
                    [LeadManagerName] NVARCHAR(100) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColleaguePosts')
            BEGIN
                CREATE TABLE [ColleaguePosts] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Title] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [LiveUrl] NVARCHAR(500) NULL,
                    [RepositoryUrl] NVARCHAR(500) NULL,
                    [Category] INT NOT NULL DEFAULT 1,
                    [Status] INT NOT NULL DEFAULT 1,
                    [AuthorName] NVARCHAR(100) NOT NULL,
                    [AuthorEmail] NVARCHAR(150) NULL,
                    [AuthorDepartment] NVARCHAR(100) NULL DEFAULT 'Engineering',
                    [Tags] NVARCHAR(200) NULL,
                    [ThumbnailUrl] NVARCHAR(500) NULL,
                    [IsPinned] BIT NOT NULL DEFAULT 0,
                    [LikesCount] INT NOT NULL DEFAULT 0,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    [UpdatedAt] DATETIME2 NULL
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColleagueFeedbacks')
            BEGIN
                CREATE TABLE [ColleagueFeedbacks] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [PostId] INT NOT NULL,
                    [ColleagueName] NVARCHAR(100) NOT NULL,
                    [ColleagueEmail] NVARCHAR(150) NULL,
                    [ColleagueRole] NVARCHAR(100) NULL DEFAULT 'Team Member',
                    [Sentiment] INT NOT NULL DEFAULT 2,
                    [Rating] INT NOT NULL DEFAULT 5,
                    [Comment] NVARCHAR(MAX) NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    [HelpfulVotes] INT NOT NULL DEFAULT 0,
                    [ScreenshotPath] NVARCHAR(500) NULL,
                    CONSTRAINT [FK_ColleagueFeedbacks_ColleaguePosts] FOREIGN KEY ([PostId]) REFERENCES [ColleaguePosts]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColleaguePostLikes')
            BEGIN
                CREATE TABLE [ColleaguePostLikes] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [PostId] INT NOT NULL,
                    [UserIdentifier] NVARCHAR(100) NOT NULL,
                    [LikedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_ColleaguePostLikes_ColleaguePosts] FOREIGN KEY ([PostId]) REFERENCES [ColleaguePosts]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColleagueReactions')
            BEGIN
                CREATE TABLE [ColleagueReactions] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [PostId] INT NOT NULL,
                    [UserIdentifier] NVARCHAR(100) NOT NULL,
                    [ReactorName] NVARCHAR(100) NOT NULL DEFAULT 'Anonymous',
                    [Reaction] INT NOT NULL DEFAULT 1,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_ColleagueReactions_ColleaguePosts] FOREIGN KEY ([PostId]) REFERENCES [ColleaguePosts]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColleagueReplies')
            BEGIN
                CREATE TABLE [ColleagueReplies] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [PostId] INT NOT NULL,
                    [AuthorName] NVARCHAR(100) NOT NULL,
                    [AuthorRole] NVARCHAR(100) NULL DEFAULT 'Team Member',
                    [ReplyText] NVARCHAR(1000) NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_ColleagueReplies_ColleaguePosts] FOREIGN KEY ([PostId]) REFERENCES [ColleaguePosts]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Teams')
            BEGIN
                CREATE TABLE [Teams] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Name] NVARCHAR(150) NOT NULL,
                    [Description] NVARCHAR(500) NULL,
                    [TeamLeaderId] INT NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_Teams_Employees_TeamLeaderId] FOREIGN KEY ([TeamLeaderId]) REFERENCES [Employees]([Id]) ON DELETE SET NULL
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TeamMembers')
            BEGIN
                CREATE TABLE [TeamMembers] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TeamId] INT NOT NULL,
                    [EmployeeId] INT NOT NULL,
                    [AssignedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_TeamMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TeamTasks')
            BEGIN
                CREATE TABLE [TeamTasks] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TeamId] INT NOT NULL,
                    [AssignedByLeaderId] INT NULL,
                    [AssignedToEmployeeId] INT NOT NULL,
                    [Title] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [Priority] INT NOT NULL DEFAULT 1,
                    [Status] INT NOT NULL DEFAULT 0,
                    [IncompleteReason] NVARCHAR(1000) NULL,
                    [DueDate] DATETIME2 NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    [CompletedAt] DATETIME2 NULL,
                    CONSTRAINT [FK_TeamTasks_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_TeamTasks_Employees_AssignedByLeaderId] FOREIGN KEY ([AssignedByLeaderId]) REFERENCES [Employees]([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_TeamTasks_Employees_AssignedToEmployeeId] FOREIGN KEY ([AssignedToEmployeeId]) REFERENCES [Employees]([Id]) ON DELETE NO ACTION
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TeamLeaderReviews')
            BEGIN
                CREATE TABLE [TeamLeaderReviews] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TeamLeaderId] INT NULL,
                    [LeaderName] NVARCHAR(100) NOT NULL,
                    [EmployeeId] INT NULL,
                    [EmployeeName] NVARCHAR(100) NOT NULL,
                    [Category] NVARCHAR(100) NULL DEFAULT '🌟 Outstanding Performance',
                    [Rating] INT NOT NULL DEFAULT 5,
                    [Comments] NVARCHAR(2000) NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [FK_TeamLeaderReviews_Employees_TeamLeaderId] FOREIGN KEY ([TeamLeaderId]) REFERENCES [Employees]([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_TeamLeaderReviews_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees]([Id]) ON DELETE NO ACTION
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MindForgeQuestions')
            BEGIN
                CREATE TABLE [MindForgeQuestions] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [GameType] NVARCHAR(100) NOT NULL,
                    [QuestionText] NVARCHAR(MAX) NOT NULL,
                    [ScrambledOrSnippet] NVARCHAR(MAX) NULL,
                    [CorrectAnswer] NVARCHAR(500) NOT NULL,
                    [OptionA] NVARCHAR(500) NULL,
                    [OptionB] NVARCHAR(500) NULL,
                    [OptionC] NVARCHAR(500) NULL,
                    [OptionD] NVARCHAR(500) NULL,
                    [Explanation] NVARCHAR(MAX) NULL,
                    [Difficulty] NVARCHAR(50) NOT NULL DEFAULT 'Medium',
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );

                INSERT INTO [MindForgeQuestions] ([GameType], [QuestionText], [ScrambledOrSnippet], [CorrectAnswer], [OptionA], [OptionB], [OptionC], [OptionD], [Explanation], [Difficulty]) VALUES
                ('WordScramble', 'High-performance web framework for modern cloud apps', 'TESPANOR', 'ASP NET CORE', NULL, NULL, NULL, NULL, 'ASP.NET Core is cross-platform and high-performance.', 'Medium'),
                ('WordScramble', 'Key Microsoft programming language for .NET development', 'PAHRCS', 'CSHARP', NULL, NULL, NULL, NULL, 'C# is the primary language for .NET ecosystem.', 'Easy'),
                ('WordScramble', 'High-speed caching and in-memory key-value data store', 'SIRED', 'REDIS', NULL, NULL, NULL, NULL, 'Redis provides fast distributed caching.', 'Medium'),
                ('WordScramble', 'Object-relational mapper for .NET data access', 'TYITEN', 'ENTITY FRAMEWORK', NULL, NULL, NULL, NULL, 'Entity Framework Core simplifies SQL operations.', 'Medium'),
                ('WordScramble', 'Asynchronous programming keyword in C#', 'IWAAT', 'AWAIT', NULL, NULL, NULL, NULL, 'await yields execution until task completes.', 'Easy'),
                ('DotNetQuiz', 'What is the output of the following async code snippet?', 'async Task<int> CalculateAsync()\n{\n    await Task.Delay(10);\n    return 42;\n}', '42', '0', '42', 'Task<int>', 'Compiler Error', 'Awaiting Task.Delay returns the result 42.', 'Medium'),
                ('DotNetQuiz', 'Which LINQ method defers execution until enumerated?', 'var q = db.Tasks.Where(t => t.Progress > 50);', 'Where', 'ToList()', 'Count()', 'Where', 'FirstOrDefault()', 'Where builds an IQueryable with deferred execution.', 'Medium'),
                ('DotNetQuiz', 'What keyword handles resource disposal automatically?', 'using var stream = File.OpenRead(path);', 'using', 'using', 'try-finally', 'dispose', 'auto', 'C# 8 using declarations dispose objects at scope exit.', 'Easy'),
                ('MathChallenge', 'What is 15 * 8 - 35?', NULL, '85', '75', '85', '95', '105', '15 * 8 = 120, 120 - 35 = 85.', 'Easy'),
                ('MathChallenge', 'What is the square root of 256 + 14?', NULL, '30', '28', '30', '32', '34', 'sqrt(256) = 16, 16 + 14 = 30.', 'Medium');
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MindForgeScores')
            BEGIN
                CREATE TABLE [MindForgeScores] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [EmployeeId] INT NULL,
                    [EmployeeName] NVARCHAR(150) NOT NULL,
                    [GameType] NVARCHAR(100) NOT NULL,
                    [Score] INT NOT NULL DEFAULT 0,
                    [TimeTakenSeconds] INT NOT NULL DEFAULT 0,
                    [PlayedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamTasks]') AND name = 'StartDate')
                ALTER TABLE [TeamTasks] ADD [StartDate] DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamTasks]') AND name = 'EndDate')
                ALTER TABLE [TeamTasks] ADD [EndDate] DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamTasks]') AND name = 'LeaderSuggestion')
                ALTER TABLE [TeamTasks] ADD [LeaderSuggestion] NVARCHAR(1000) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamTasks]') AND name = 'TaskDurationType')
                ALTER TABLE [TeamTasks] ADD [TaskDurationType] NVARCHAR(50) NULL DEFAULT 'Full Day';

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamTasks]') AND name = 'ProjectId')
                ALTER TABLE [TeamTasks] ADD [ProjectId] INT NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'LeaderName')
                ALTER TABLE [TeamLeaderReviews] ADD [LeaderName] NVARCHAR(100) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'EmployeeId')
                ALTER TABLE [TeamLeaderReviews] ADD [EmployeeId] INT NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'EmployeeName')
                ALTER TABLE [TeamLeaderReviews] ADD [EmployeeName] NVARCHAR(100) NULL;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'Category')
                ALTER TABLE [TeamLeaderReviews] ADD [Category] NVARCHAR(100) NULL DEFAULT '🌟 Outstanding Performance';

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'Comments')
                ALTER TABLE [TeamLeaderReviews] ADD [Comments] NVARCHAR(2000) NULL;

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'ReviewerName')
                ALTER TABLE [TeamLeaderReviews] ALTER COLUMN [ReviewerName] NVARCHAR(100) NULL;

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TeamLeaderReviews]') AND name = 'Feedback')
                ALTER TABLE [TeamLeaderReviews] ALTER COLUMN [Feedback] NVARCHAR(MAX) NULL;
        ";

        context.Database.ExecuteSqlRaw(alterSql);

        // Seed initial Teams of Auxinzio if empty
        if (!context.Teams.Any())
        {
            var emp1 = context.Employees.FirstOrDefault(e => e.Id == 1); // Mujimal
            var emp2 = context.Employees.FirstOrDefault(e => e.Id == 2); // Anas Ahamad
            var emp3 = context.Employees.FirstOrDefault(e => e.Id == 3); // John Christopher
            var emp4 = context.Employees.FirstOrDefault(e => e.Id == 4); // Srithar
            var emp5 = context.Employees.FirstOrDefault(e => e.Id == 5); // Karthikeyan
            var emp6 = context.Employees.FirstOrDefault(e => e.Id == 6); // Santhosh
            var emp7 = context.Employees.FirstOrDefault(e => e.Id == 7); // John Thomas

            var team1 = new Team
            {
                Name = "Frontend Innovators",
                Description = "Crafting cutting-edge responsive web applications and interactive UI components.",
                TeamLeaderId = emp1?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            var team2 = new Team
            {
                Name = "Backend Architects",
                Description = "Engineers powering high-performance microservices, EF Core ORM, and ASP.NET Core APIs.",
                TeamLeaderId = emp5?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            var team3 = new Team
            {
                Name = "QA Champions & Reliability",
                Description = "Ensuring top-notch quality assurance, automated unit/integration tests, and continuous delivery.",
                TeamLeaderId = emp3?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            context.Teams.AddRange(team1, team2, team3);
            context.SaveChanges();

            if (emp2 != null) context.TeamMembers.Add(new TeamMember { TeamId = team1.Id, EmployeeId = emp2.Id });
            if (emp4 != null) context.TeamMembers.Add(new TeamMember { TeamId = team1.Id, EmployeeId = emp4.Id });
            if (emp6 != null) context.TeamMembers.Add(new TeamMember { TeamId = team2.Id, EmployeeId = emp6.Id });
            if (emp7 != null) context.TeamMembers.Add(new TeamMember { TeamId = team2.Id, EmployeeId = emp7.Id });
            if (emp4 != null) context.TeamMembers.Add(new TeamMember { TeamId = team3.Id, EmployeeId = emp4.Id });
            if (emp6 != null) context.TeamMembers.Add(new TeamMember { TeamId = team3.Id, EmployeeId = emp6.Id });
            context.SaveChanges();

            // Seed Sample Team Tasks
            if (emp2 != null && emp1 != null)
            {
                context.TeamTasks.Add(new TeamTask
                {
                    TeamId = team1.Id,
                    AssignedByLeaderId = emp1.Id,
                    AssignedToEmployeeId = emp2.Id,
                    Title = "Implement Glassmorphism Dashboard Cards",
                    Description = "Build sleek glassmorphism UI stats cards with gradient borders.",
                    Priority = TaskPriority.High,
                    Status = TeamTaskStatus.Completed,
                    DueDate = DateTime.UtcNow.AddDays(2),
                    CompletedAt = DateTime.UtcNow.AddDays(-1)
                });

                context.TeamTasks.Add(new TeamTask
                {
                    TeamId = team1.Id,
                    AssignedByLeaderId = emp1.Id,
                    AssignedToEmployeeId = emp2.Id,
                    Title = "Optimize Core Web Vitals for Mobile View",
                    Description = "Reduce initial bundle size and optimize image assets.",
                    Priority = TaskPriority.Urgent,
                    Status = TeamTaskStatus.NotCompleted,
                    IncompleteReason = "Delayed while awaiting asset exports from external UI designer.",
                    DueDate = DateTime.UtcNow.AddDays(-1)
                });
            }

            if (emp6 != null && emp5 != null)
            {
                context.TeamTasks.Add(new TeamTask
                {
                    TeamId = team2.Id,
                    AssignedByLeaderId = emp5.Id,
                    AssignedToEmployeeId = emp6.Id,
                    Title = "Configure SQL Query Splitting & Indexing",
                    Description = "Optimize EF Core queries using SplitQuery and add missing indexes.",
                    Priority = TaskPriority.High,
                    Status = TeamTaskStatus.InProgress,
                    DueDate = DateTime.UtcNow.AddDays(3)
                });
            }

            // Seed Sample Team Leader Reviews & Employee Appreciations
            if (emp1 != null && emp2 != null)
            {
                context.TeamLeaderReviews.Add(new TeamLeaderReview
                {
                    TeamLeaderId = emp1.Id,
                    LeaderName = emp1.Name,
                    EmployeeId = emp2.Id,
                    EmployeeName = emp2.Name,
                    Category = "🌟 Outstanding Performance & Praise",
                    Rating = 5,
                    Comments = "Anas demonstrates excellent code quality and proactively resolves frontend UI challenges.",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                });
            }

            if (emp5 != null && emp6 != null)
            {
                context.TeamLeaderReviews.Add(new TeamLeaderReview
                {
                    TeamLeaderId = emp5.Id,
                    LeaderName = emp5.Name,
                    EmployeeId = emp6.Id,
                    EmployeeName = emp6.Name,
                    Category = "🚀 Key Achievement & Milestone",
                    Rating = 5,
                    Comments = "Santhosh did a fantastic job automating testing pipelines for backend APIs ahead of schedule.",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                });
            }

            context.SaveChanges();
        }

        var updateTaskDatesSql = @"
            -- Default all task start dates to 10:00 AM on created date
            UPDATE [Tasks] 
            SET [StartDate] = DATEADD(minute, 600, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2));

            -- Default all task end dates to 7:00 PM on created date
            UPDATE [Tasks] 
            SET [EndDate] = DATEADD(minute, 1140, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2));

            -- Set varied realistic working hours matching user specifications
            UPDATE [Tasks] SET [StartDate] = DATEADD(minute, 870, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)), [EndDate] = DATEADD(minute, 1140, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)) WHERE [Id] % 5 = 1; -- 2:30 PM to 7:00 PM (4:30)
            UPDATE [Tasks] SET [StartDate] = DATEADD(minute, 600, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)), [EndDate] = DATEADD(minute, 780, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)) WHERE [Id] % 5 = 2; -- 10:00 AM to 1:00 PM (3:00)
            UPDATE [Tasks] SET [StartDate] = DATEADD(minute, 840, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)), [EndDate] = DATEADD(minute, 1140, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)) WHERE [Id] % 5 = 3; -- 2:00 PM to 7:00 PM (5:00)
            UPDATE [Tasks] SET [StartDate] = DATEADD(minute, 640, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)), [EndDate] = DATEADD(minute, 840, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)) WHERE [Id] % 5 = 4; -- 10:40 AM to 2:00 PM (3:20)
            UPDATE [Tasks] SET [StartDate] = DATEADD(minute, 900, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)), [EndDate] = DATEADD(minute, 1140, CAST(CAST([CreatedAt] AS DATE) AS DATETIME2)) WHERE [Id] % 5 = 0; -- 3:00 PM to 7:00 PM (4:00)

            IF EXISTS (SELECT 1 FROM [Projects] WHERE [Id] = 1)
            BEGIN
                UPDATE [Tasks] SET [ProjectId] = 1 WHERE [ProjectId] IS NULL;
            END
        ";
        context.Database.ExecuteSqlRaw(updateTaskDatesSql);

        // Safely update company name to Auxinzio in database
        var updateCompanySql = @"
            IF EXISTS (SELECT * FROM [Companies] WHERE [Name] IN ('Auxin.io', 'Auxin', 'Auxizio', 'AUXINZ', 'AUXIN'))
            BEGIN
                UPDATE [Companies] SET [Name] = 'Auxinzio' WHERE [Name] IN ('Auxin.io', 'Auxin', 'Auxizio', 'AUXINZ', 'AUXIN');
            END
            IF EXISTS (SELECT * FROM [Companies] WHERE [Id] = 3 AND [Name] <> 'Auxinzio')
            BEGIN
                UPDATE [Companies] SET [Name] = 'Auxinzio' WHERE [Id] = 3;
            END
        ";
        context.Database.ExecuteSqlRaw(updateCompanySql);

        var updateDeptSql = @"
            UPDATE [Employees] SET [Department] = 'Frontend' WHERE [Name] = 'Mujimal' OR [Department] = 'Frontend Developer';
            UPDATE [Employees] SET [Department] = 'Backend' WHERE [Department] IN ('Engineering', 'Infrastructure', 'Backend Specialist') AND [Name] <> 'Mujimal';
            UPDATE [Employees] SET [Department] = 'UI / UX Designer' WHERE [Department] IN ('Product & Design', 'Design', 'Product');
            UPDATE [Employees] SET [Department] = 'Tester' WHERE [Department] IN ('Quality Assurance', 'Operations', 'QA');
            UPDATE [Employees] SET [Department] = 'Intern' WHERE [Department] IN ('Internship', 'Interns');
        ";
        context.Database.ExecuteSqlRaw(updateDeptSql);



        // Seed initial study materials if none exist
        if (!context.InternStudyMaterials.Any())
        {
            context.InternStudyMaterials.AddRange(
                new InternStudyMaterial
                {
                    Title = "ASP.NET Core MVC & Dependency Injection Mastery",
                    Track = "Backend .NET / C#",
                    Difficulty = "Beginner",
                    ContentType = "Interactive Guide",
                    Description = "Master controllers, action results, routing, model binding, dependency injection lifecycle, and middleware in ASP.NET Core.",
                    ResourceUrl = "https://learn.microsoft.com/en-us/aspnet/core/mvc/overview",
                    EstimatedMinutes = 45,
                    Tags = "C#, MVC, DI, Architecture"
                },
                new InternStudyMaterial
                {
                    Title = "Entity Framework Core Relational Data Modeling & Migrations",
                    Track = "SQL & Database Design",
                    Difficulty = "Intermediate",
                    ContentType = "Code Sample",
                    Description = "Learn fluent API configurations, many-to-many navigation mappings, eager loading with Include/ThenInclude, and performance indexing.",
                    ResourceUrl = "https://learn.microsoft.com/en-us/ef/core/",
                    EstimatedMinutes = 60,
                    Tags = "EF Core, SQL Server, Linq, Performance"
                },
                new InternStudyMaterial
                {
                    Title = "Modern Glassmorphism & High-Performance CSS Layouts",
                    Track = "Frontend & UI/UX",
                    Difficulty = "Beginner",
                    ContentType = "Cheat Sheet",
                    Description = "Comprehensive cheatsheet for CSS custom properties, backdrop-filter, flexbox, CSS grid, micro-animations, and responsive layouts.",
                    ResourceUrl = "https://developer.mozilla.org/en-US/docs/Web/CSS",
                    EstimatedMinutes = 30,
                    Tags = "CSS3, UI/UX, Glassmorphism, Responsive"
                },
                new InternStudyMaterial
                {
                    Title = "Git Branching Strategy, Pull Requests & Code Reviews",
                    Track = "Git, DevOps & Cloud",
                    Difficulty = "Beginner",
                    ContentType = "Official Documentation",
                    Description = "Learn enterprise Git branch management (feature/fix branches, conventional commit messages, rebasing, merge conflict resolution).",
                    ResourceUrl = "https://git-scm.com/doc",
                    EstimatedMinutes = 35,
                    Tags = "Git, GitHub, CI/CD, Version Control"
                },
                new InternStudyMaterial
                {
                    Title = "RESTful API Design Principles & Clean Architecture",
                    Track = "Software Architecture",
                    Difficulty = "Intermediate",
                    ContentType = "Architecture Sheet",
                    Description = "Standard HTTP status codes, idempotency, repository pattern, DTO view models, and error response standards.",
                    ResourceUrl = "https://restfulapi.net/",
                    EstimatedMinutes = 50,
                    Tags = "REST, Architecture, Clean Code, API"
                },
                new InternStudyMaterial
                {
                    Title = "Unit Testing & Integration Testing in C# with xUnit",
                    Track = "QA & Testing",
                    Difficulty = "Intermediate",
                    ContentType = "Video Tutorial",
                    Description = "Learn how to write unit tests using xUnit, mock external services with Moq, and verify database integrity.",
                    ResourceUrl = "https://xunit.net/",
                    EstimatedMinutes = 40,
                    Tags = "Testing, xUnit, Moq, QA"
                }
            );
            context.SaveChanges();
        }

        // Seed initial sample doubt if none exist
        if (!context.InternDoubts.Any())
        {
            var seedDoubt = new InternDoubt
            {
                Title = "How to handle Many-to-Many entity updates in EF Core without duplicate key error?",
                Description = "When editing a task with multiple assigned employees, calling RemoveRange and AddRange sometimes throws an exception if the entity tracking isn't cleared. What is the recommended pattern?",
                CodeSnippet = @"// Current Attempt:
_context.TaskEmployees.RemoveRange(task.TaskEmployees);
foreach(var empId in model.SelectedEmployeeIds) {
    _context.TaskEmployees.Add(new TaskEmployee { TaskId = task.Id, EmployeeId = empId });
}",
                Domain = "SQL Server / EF Core",
                Urgency = "Help Needed Today",
                InternName = "Alex Rivera (Intern)",
                Status = "Clarified",
                Upvotes = 5,
                CreatedAt = DateTime.Now.AddDays(-2)
            };

            context.InternDoubts.Add(seedDoubt);
            context.SaveChanges();

            context.InternDoubtClarifications.Add(new InternDoubtClarification
            {
                InternDoubtId = seedDoubt.Id,
                ClarifiedByEmployeeName = "Karthikeyan",
                ClarificationText = "Great question Alex! In EF Core, make sure you include the navigation collection `Include(t => t.TaskEmployees)` when retrieving the tracked entity. Then RemoveRange the tracked collection and add new items before calling `SaveChangesAsync()`. Alternatively, you can use `.AsNoTracking()` if you intend to reattach.",
                CodeSolution = @"// Recommended pattern:
var task = await _context.Tasks.Include(t => t.TaskEmployees).FirstOrDefaultAsync(t => t.Id == id);
_context.TaskEmployees.RemoveRange(task.TaskEmployees);
foreach (var empId in model.SelectedEmployeeIds)
{
    _context.TaskEmployees.Add(new TaskEmployee { TaskId = task.Id, EmployeeId = empId });
}
await _context.SaveChangesAsync();",
                HelpfulLink = "https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete",
                IsAcceptedSolution = true,
                AnsweredAt = DateTime.Now.AddDays(-1)
            });
            context.SaveChanges();
        }

        // Seed initial internship members if none exist
        if (!context.InternshipMembers.Any())
        {
            context.InternshipMembers.AddRange(
                new InternshipMember
                {
                    Name = "Alex Rivera",
                    Email = "alex.rivera@intern.auxinz.io",
                    Role = "Intern",
                    Domain = "Backend .NET / C#",
                    MentorName = "Karthikeyan",
                    JoinedDate = DateTime.Today.AddDays(-30),
                    Status = "Active",
                    CompletedModulesCount = 4,
                    Notes = "Working on MVC controllers, database migrations, and repository patterns."
                },
                new InternshipMember
                {
                    Name = "Priya Sharma",
                    Email = "priya.sharma@intern.auxinz.io",
                    Role = "Intern",
                    Domain = "Frontend & UI/UX",
                    MentorName = "Srithar",
                    JoinedDate = DateTime.Today.AddDays(-20),
                    Status = "On Track",
                    CompletedModulesCount = 3,
                    Notes = "Focusing on responsive design systems, CSS architecture, and Razor view integration."
                },
                new InternshipMember
                {
                    Name = "David Chen",
                    Email = "david.chen@intern.auxinz.io",
                    Role = "Graduate Trainee",
                    Domain = "SQL & Database Design",
                    MentorName = "John Christopher",
                    JoinedDate = DateTime.Today.AddDays(-45),
                    Status = "Active",
                    CompletedModulesCount = 5,
                    Notes = "Specializing in EF Core query optimization and relational schema modeling."
                },
                new InternshipMember
                {
                    Name = "Karthikeyan",
                    Email = "karthikeyan@auxinz.io",
                    Role = "Technical Mentor",
                    Domain = "Full Stack Architecture",
                    JoinedDate = DateTime.Today.AddDays(-365),
                    Status = "Active",
                    CompletedModulesCount = 10,
                    Notes = "Lead mentor for Backend .NET & ASP.NET Core MVC tracks."
                },
                new InternshipMember
                {
                    Name = "Srithar",
                    Email = "srithar@auxinz.io",
                    Role = "Technical Mentor",
                    Domain = "Frontend & UI/UX",
                    JoinedDate = DateTime.Today.AddDays(-300),
                    Status = "Active",
                    CompletedModulesCount = 8,
                    Notes = "Lead mentor for UI engineering and frontend optimization."
                }
            );
            context.SaveChanges();
        }

        // Seed initial curated YouTube references for interns if none exist
        if (!context.InternYouTubeReferences.Any())
        {
            context.InternYouTubeReferences.AddRange(
                new InternYouTubeReference
                {
                    Title = "ASP.NET Core MVC Full Course (.NET 8/9/10) - Complete Beginner to Pro",
                    YouTubeUrl = "https://www.youtube.com/watch?v=hZ1DASYd9rk",
                    YouTubeVideoId = "hZ1DASYd9rk",
                    Track = "Backend .NET / C#",
                    Difficulty = "Beginner",
                    ChannelOrMentorName = "freeCodeCamp / Karthikeyan (Mentor Pick)",
                    DurationMinutes = 180,
                    Description = "Comprehensive walkthrough covering controllers, Razor views, model binding, dependency injection, routing, repository pattern, and Entity Framework Core integration.",
                    Tags = "ASP.NET Core, C#, MVC, Backend, Web API",
                    CreatedAt = DateTime.Now.AddDays(-10)
                },
                new InternYouTubeReference
                {
                    Title = "Entity Framework Core Masterclass: Relationships, Migrations & Performance",
                    YouTubeUrl = "https://www.youtube.com/watch?v=0k57_jW4Ld8",
                    YouTubeVideoId = "0k57_jW4Ld8",
                    Track = "SQL & Database Design",
                    Difficulty = "Intermediate",
                    ChannelOrMentorName = "Amigoscode & Microsoft Developer",
                    DurationMinutes = 75,
                    Description = "Deep dive into DbContext lifecycle, Fluent API configurations, Many-to-Many entity navigation, Split Queries, and query optimization in production SQL Server.",
                    Tags = "EF Core, SQL Server, Linq, Performance, Migrations",
                    CreatedAt = DateTime.Now.AddDays(-8)
                },
                new InternYouTubeReference
                {
                    Title = "Modern Responsive CSS, Flexbox & Grid Masterclass",
                    YouTubeUrl = "https://www.youtube.com/watch?v=1PnVor36_40",
                    YouTubeVideoId = "1PnVor36_40",
                    Track = "Frontend & UI/UX",
                    Difficulty = "Beginner",
                    ChannelOrMentorName = "Kevin Powell / Srithar (Mentor Pick)",
                    DurationMinutes = 60,
                    Description = "Learn CSS custom properties, backdrop filters, responsive CSS Grid, Flexbox centering, modern clamp() typography, and fluid micro-animations.",
                    Tags = "CSS3, UI/UX, Flexbox, Responsive, Glassmorphism",
                    CreatedAt = DateTime.Now.AddDays(-6)
                },
                new InternYouTubeReference
                {
                    Title = "Git & GitHub Crash Course for Developers & Team Pull Requests",
                    YouTubeUrl = "https://www.youtube.com/watch?v=RGOj5yH7evk",
                    YouTubeVideoId = "RGOj5yH7evk",
                    Track = "Git, DevOps & Cloud",
                    Difficulty = "Beginner",
                    ChannelOrMentorName = "Traversy Media",
                    DurationMinutes = 45,
                    Description = "Branching workflows, resolving merge conflicts, crafting meaningful commit messages, interactive rebasing, and collaborating on enterprise PRs.",
                    Tags = "Git, GitHub, DevOps, Version Control, CI/CD",
                    CreatedAt = DateTime.Now.AddDays(-4)
                },
                new InternYouTubeReference
                {
                    Title = "Clean Architecture & Design Patterns in C# .NET",
                    YouTubeUrl = "https://www.youtube.com/watch?v=t8B5s5_8x08",
                    YouTubeVideoId = "t8B5s5_8x08",
                    Track = "Software Architecture",
                    Difficulty = "Advanced",
                    ChannelOrMentorName = "Nick Chapsas / Architecture Lead",
                    DurationMinutes = 50,
                    Description = "Understand Onion/Clean Architecture, Domain-Driven Design (DDD) principles, CQRS fundamentals, and dependency inversion in enterprise .NET apps.",
                    Tags = "Architecture, Clean Code, Design Patterns, DDD, CQRS",
                    CreatedAt = DateTime.Now.AddDays(-2)
                }
            );
            context.SaveChanges();
        }

        if (!context.Projects.Any())
        {
            context.Projects.AddRange(
                new Project
                {
                    ProjectName = "Auxinzio Enterprise Task & AI Suite",
                    ClientCompany = "Auxinzio",
                    Description = "Next-generation multi-company task management, role-based workflows, automated email escalations, and real-time delivery tracking.",
                    Status = "In Progress",
                    Priority = "Critical",
                    StartDate = DateTime.Today.AddDays(-14),
                    TargetEndDate = DateTime.Today.AddDays(45),
                    Budget = 75000,
                    LeadManagerName = "Srithar",
                    CreatedAt = DateTime.Now.AddDays(-14)
                },
                new Project
                {
                    ProjectName = "Ameobatronics Robotics Automation Engine",
                    ClientCompany = "Ameobatronics",
                    Description = "Industrial IoT and firmware coordination platform for warehouse automation and smart robotic arm controllers.",
                    Status = "In Progress",
                    Priority = "High",
                    StartDate = DateTime.Today.AddDays(-30),
                    TargetEndDate = DateTime.Today.AddDays(60),
                    Budget = 120000,
                    LeadManagerName = "Mujimal",
                    CreatedAt = DateTime.Now.AddDays(-30)
                },
                new Project
                {
                    ProjectName = "Printa Cloud Printing & Ledger System",
                    ClientCompany = "Printa",
                    Description = "High-throughput cloud printing broker, automated digital invoicing, queue management, and ink consumption analytics.",
                    Status = "Planning",
                    Priority = "Medium",
                    StartDate = DateTime.Today.AddDays(5),
                    TargetEndDate = DateTime.Today.AddDays(90),
                    Budget = 45000,
                    LeadManagerName = "John Christopher",
                    CreatedAt = DateTime.Now.AddDays(-5)
                },
                new Project
                {
                    ProjectName = "Visitor Pass Smart Check-in Portal",
                    ClientCompany = "Auxinzio",
                    Description = "Touchless QR visitor pass registration, badge generation, NDA acknowledgment, and SMS security notifications.",
                    Status = "Completed",
                    Priority = "Medium",
                    StartDate = DateTime.Today.AddDays(-60),
                    TargetEndDate = DateTime.Today.AddDays(-5),
                    Budget = 30000,
                    LeadManagerName = "Anas Ahamad",
                    CreatedAt = DateTime.Now.AddDays(-60)
                }
            );
            context.SaveChanges();
        }

        // Seed initial intern test results if none exist
        if (!context.InternTestResults.Any())
        {
            context.InternTestResults.AddRange(
                new InternTestResult
                {
                    InternName = "Kavitha R.",
                    InternEmail = "kavitha.r@intern.auxinz.io",
                    TopicTitle = "ASP.NET Core MVC & Dependency Injection Mastery",
                    Track = "Backend .NET / C#",
                    Score = 5,
                    TotalQuestions = 5,
                    ScorePercentage = 100,
                    IsPassed = true,
                    GradeBadge = "Flawless Distinction (A+)",
                    FeedbackNotes = "Demonstrated outstanding comprehension of MVC lifecycle, scoped services, and middleware order.",
                    TakenAt = DateTime.Now.AddDays(-2)
                },
                new InternTestResult
                {
                    InternName = "Rahul Sharma",
                    InternEmail = "rahul.s@intern.auxinz.io",
                    TopicTitle = "Entity Framework Core Relational Data Modeling & Migrations",
                    Track = "SQL & Database Design",
                    Score = 4,
                    TotalQuestions = 5,
                    ScorePercentage = 80,
                    IsPassed = true,
                    GradeBadge = "Excellence (A)",
                    FeedbackNotes = "Great grasp on Fluent API foreign key cascades and eager loading navigation.",
                    TakenAt = DateTime.Now.AddDays(-1)
                },
                new InternTestResult
                {
                    InternName = "Deepak Kumar",
                    InternEmail = "deepak.k@intern.auxinz.io",
                    TopicTitle = "Modern Glassmorphism & High-Performance CSS Layouts",
                    Track = "Frontend & UI/UX",
                    Score = 5,
                    TotalQuestions = 5,
                    ScorePercentage = 100,
                    IsPassed = true,
                    GradeBadge = "Flawless Distinction (A+)",
                    FeedbackNotes = "Perfect score in responsive CSS grid, theme variables, and flexbox alignment.",
                    TakenAt = DateTime.Now.AddHours(-18)
                }
            );
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization warning: {ex.Message}");
    }
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

// Default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Task}/{action=Index}/{id?}");

app.Run();