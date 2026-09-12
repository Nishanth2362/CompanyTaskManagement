using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class InternshipController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<InternshipController> _logger;
        private readonly IUserSessionService _sessionService;

        public InternshipController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<InternshipController> logger, IUserSessionService sessionService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _sessionService = sessionService;
        }

        // =========================================================
        // GET: /Internship
        // Central Internship Hub (Study Materials & Doubt Forum)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(string tab = "materials", string? track = null, string? doubtStatus = null, string? search = null)
        {
            // Load Study Materials
            var materialsQuery = _context.InternStudyMaterials.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(track) && track != "all")
            {
                materialsQuery = materialsQuery.Where(m => m.Track == track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                materialsQuery = materialsQuery.Where(m => m.Title.ToLower().Contains(s) || m.Description.ToLower().Contains(s) || (m.Tags != null && m.Tags.ToLower().Contains(s)));
            }

            var materials = await materialsQuery.OrderBy(m => m.Track).ThenBy(m => m.Difficulty).ToListAsync();

            // Load Doubts
            var doubtsQuery = _context.InternDoubts
                .Include(d => d.Clarifications)
                .Include(d => d.InternEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(doubtStatus) && doubtStatus != "all")
            {
                doubtsQuery = doubtsQuery.Where(d => d.Status == doubtStatus);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                doubtsQuery = doubtsQuery.Where(d => d.Title.ToLower().Contains(s) || d.Description.ToLower().Contains(s) || d.InternName.ToLower().Contains(s));
            }

            var doubts = await doubtsQuery.OrderByDescending(d => d.CreatedAt).ToListAsync();

            // Load Internship Members
            var membersQuery = _context.InternshipMembers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                membersQuery = membersQuery.Where(m => m.Name.ToLower().Contains(s) || m.Domain.ToLower().Contains(s) || m.Role.ToLower().Contains(s));
            }
            var members = await membersQuery.OrderBy(m => m.Role).ThenBy(m => m.Name).ToListAsync();

            // Load YouTube Video References
            var youTubeQuery = _context.InternYouTubeReferences.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(track) && track != "all")
            {
                youTubeQuery = youTubeQuery.Where(y => y.Track == track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                youTubeQuery = youTubeQuery.Where(y => y.Title.ToLower().Contains(s) || 
                                                       y.Description.ToLower().Contains(s) || 
                                                       (y.ChannelOrMentorName != null && y.ChannelOrMentorName.ToLower().Contains(s)) ||
                                                       (y.Tags != null && y.Tags.ToLower().Contains(s)));
            }
            var youTubeVideos = await youTubeQuery.OrderByDescending(y => y.CreatedAt).ToListAsync();

            // Load Topic Test Results
            var testsQuery = _context.InternTestResults.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(track) && track != "all")
            {
                testsQuery = testsQuery.Where(t => t.Track == track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                testsQuery = testsQuery.Where(t => t.InternName.ToLower().Contains(s) || 
                                                   t.TopicTitle.ToLower().Contains(s) || 
                                                   t.Track.ToLower().Contains(s) ||
                                                   (t.FeedbackNotes != null && t.FeedbackNotes.ToLower().Contains(s)));
            }
            var testResults = await testsQuery.OrderByDescending(t => t.TakenAt).ToListAsync();

            // Employees list for mentorship dropdowns
            var employees = await _context.Employees.OrderBy(e => e.Name).AsNoTracking().ToListAsync();

            ViewBag.ActiveTab = tab;
            ViewBag.SelectedTrack = track ?? "all";
            ViewBag.SelectedDoubtStatus = doubtStatus ?? "all";
            ViewBag.Search = search;
            ViewBag.Employees = employees;

            ViewBag.TotalMaterials = await _context.InternStudyMaterials.CountAsync();
            ViewBag.TotalDoubts = doubts.Count;
            ViewBag.OpenDoubts = doubts.Count(d => d.Status == "Open");
            ViewBag.ResolvedDoubts = doubts.Count(d => d.Status == "Resolved" || d.Clarifications.Any(c => c.IsAcceptedSolution));
            ViewBag.TotalClarifications = await _context.InternDoubtClarifications.CountAsync();
            ViewBag.TotalMembers = members.Count;
            ViewBag.TotalInterns = members.Count(m => m.Role == "Intern" || m.Role == "Graduate Trainee");
            ViewBag.TotalMentors = members.Count(m => m.Role == "Technical Mentor" || m.Role == "Senior Lead");
            ViewBag.TotalYouTubeVideos = await _context.InternYouTubeReferences.CountAsync();
            ViewBag.TotalTests = await _context.InternTestResults.CountAsync();
            ViewBag.PassedTests = testResults.Count(t => t.IsPassed);
            ViewBag.AverageScore = testResults.Any() ? Math.Round(testResults.Average(t => t.ScorePercentage), 1) : 0;

            ViewBag.Tracks = new List<string>
            {
                "Backend .NET / C#",
                "Frontend & UI/UX",
                "SQL & Database Design",
                "Git, DevOps & Cloud",
                "Software Architecture",
                "QA & Testing"
            };

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();

            ViewBag.Materials = materials;
            ViewBag.Doubts = doubts;
            ViewBag.Members = members;
            ViewBag.YouTubeVideos = youTubeVideos;
            ViewBag.TestResults = testResults;

            return View();
        }

        // =========================================================
        // POST: /Internship/AskDoubt
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AskDoubt(InternDoubt model, IFormFile? screenshotFile)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Description) || string.IsNullOrWhiteSpace(model.InternName))
            {
                TempData["Error"] = "Please fill in all required fields (Name, Title, Description).";
                return RedirectToAction(nameof(Index), new { tab = "doubts" });
            }

            if (screenshotFile != null && screenshotFile.Length > 0)
            {
                model.ScreenshotPath = await SaveInternshipScreenshotAsync(screenshotFile);
            }

            model.CreatedAt = DateTime.Now;
            model.Status = "Open";
            model.Upvotes = 0;

            _context.InternDoubts.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your question has been posted to the Internship Doubt Forum. A mentor will review it shortly!";
            return RedirectToAction(nameof(Index), new { tab = "doubts" });
        }

        // =========================================================
        // GET: /Internship/DoubtDetails/1
        // View Doubt Q&A Thread
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DoubtDetails(int id)
        {
            var doubt = await _context.InternDoubts
                .Include(d => d.Clarifications.OrderByDescending(c => c.IsAcceptedSolution).ThenByDescending(c => c.AnsweredAt))
                    .ThenInclude(c => c.Employee)
                .Include(d => d.InternEmployee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doubt == null)
            {
                return NotFound();
            }

            ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).AsNoTracking().ToListAsync();
            return View(doubt);
        }

        // =========================================================
        // POST: /Internship/AddClarification
        // Employee / Mentor Clarification / Answer
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClarification(int doubtId, string employeeName, int? employeeId, string clarificationText, string? codeSolution, string? helpfulLink)
        {
            var doubt = await _context.InternDoubts.FindAsync(doubtId);
            if (doubt == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(clarificationText))
            {
                TempData["Error"] = "Clarification answer cannot be empty.";
                return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
            }

            if (string.IsNullOrWhiteSpace(employeeName) && employeeId.HasValue)
            {
                var emp = await _context.Employees.FindAsync(employeeId.Value);
                if (emp != null) employeeName = emp.Name;
            }

            if (string.IsNullOrWhiteSpace(employeeName))
            {
                employeeName = "Senior Team Mentor";
            }

            var clarification = new InternDoubtClarification
            {
                InternDoubtId = doubtId,
                ClarifiedByEmployeeName = employeeName,
                EmployeeId = employeeId,
                ClarificationText = clarificationText,
                CodeSolution = codeSolution,
                HelpfulLink = helpfulLink,
                IsAcceptedSolution = false,
                AnsweredAt = DateTime.Now
            };

            _context.InternDoubtClarifications.Add(clarification);

            if (doubt.Status == "Open")
            {
                doubt.Status = "Clarified";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Thank you! Your clarification has been posted successfully.";
            return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
        }

        // =========================================================
        // POST: /Internship/AcceptClarification
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptClarification(int doubtId, int clarificationId)
        {
            var doubt = await _context.InternDoubts
                .Include(d => d.Clarifications)
                .FirstOrDefaultAsync(d => d.Id == doubtId);

            if (doubt == null)
            {
                return NotFound();
            }

            foreach (var c in doubt.Clarifications)
            {
                c.IsAcceptedSolution = (c.Id == clarificationId);
            }

            doubt.Status = "Resolved";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Clarification marked as Accepted Solution & Doubt marked as Resolved!";
            return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
        }

        // =========================================================
        // AJAX: POST: /Internship/UpvoteDoubt/1
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> UpvoteDoubt(int id)
        {
            var doubt = await _context.InternDoubts.FindAsync(id);
            if (doubt == null)
            {
                return Json(new { success = false });
            }

            doubt.Upvotes += 1;
            await _context.SaveChangesAsync();

            return Json(new { success = true, upvotes = doubt.Upvotes });
        }

        // =========================================================
        // POST: /Internship/AddMaterial
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaterial(InternStudyMaterial model, IFormFile? uploadFile)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["Error"] = "Please provide both a Title and Description for the study resource.";
                return RedirectToAction(nameof(Index), new { tab = "materials" });
            }

            if (uploadFile != null && uploadFile.Length > 0)
            {
                model.FilePath = await SaveStudyMaterialFileAsync(uploadFile);
            }

            model.CreatedAt = DateTime.Now;
            _context.InternStudyMaterials.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Study resource \"{model.Title}\" added successfully with attached materials.";
            return RedirectToAction(nameof(Index), new { tab = "materials" });
        }

        // =========================================================
        // POST: /Internship/DeleteMaterial/1
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            var material = await _context.InternStudyMaterials.FindAsync(id);
            if (material != null)
            {
                _context.InternStudyMaterials.Remove(material);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Study material removed.";
            }

            return RedirectToAction(nameof(Index), new { tab = "materials" });
        }

        // =========================================================
        // POST: /Internship/AddYouTubeReference
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddYouTubeReference(InternYouTubeReference model)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.YouTubeUrl) || string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["Error"] = "Please fill in all required fields (Video Title, YouTube URL, and Key Takeaways/Description).";
                return RedirectToAction(nameof(Index), new { tab = "youtube" });
            }

            var videoId = InternYouTubeReference.ExtractVideoId(model.YouTubeUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                TempData["Error"] = "Invalid YouTube URL format. Please provide a standard YouTube video link (e.g., https://www.youtube.com/watch?v=... or https://youtu.be/...).";
                return RedirectToAction(nameof(Index), new { tab = "youtube" });
            }

            model.YouTubeVideoId = videoId;
            model.CreatedAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(model.ChannelOrMentorName))
            {
                model.ChannelOrMentorName = "Auxinzio Mentors";
            }

            _context.InternYouTubeReferences.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"YouTube video reference \"{model.Title}\" added to the Internship Academy library!";
            return RedirectToAction(nameof(Index), new { tab = "youtube" });
        }

        // =========================================================
        // POST: /Internship/DeleteYouTubeReference/1
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteYouTubeReference(int id)
        {
            var video = await _context.InternYouTubeReferences.FindAsync(id);
            if (video != null)
            {
                _context.InternYouTubeReferences.Remove(video);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Video reference \"{video.Title}\" removed.";
            }

            return RedirectToAction(nameof(Index), new { tab = "youtube" });
        }

        // =========================================================
        // POST: /Internship/AddMember (ADMIN ONLY)
        // Add Intern / Employee Mentor to Cohort
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(InternshipMember model)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to add or register interns.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["Error"] = "Member name is required.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            if (model.JoinedDate == default)
            {
                model.JoinedDate = DateTime.Today;
            }

            var trimmedName = model.Name.Trim();
            model.Name = trimmedName;

            _context.InternshipMembers.Add(model);

            // Synchronize with Employees directory as well
            var existingEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower());
            if (existingEmp == null)
            {
                var newEmp = new Employee
                {
                    Name = trimmedName,
                    Email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email.Trim() : $"{trimmedName.ToLower().Replace(" ", ".")}@auxinz.io",
                    Department = "Internship",
                    Designation = $"{model.Role} ({model.Domain})",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _context.Employees.Add(newEmp);
            }
            else
            {
                if (existingEmp.Department == "Internship" || string.IsNullOrWhiteSpace(existingEmp.Department))
                {
                    existingEmp.Department = "Internship";
                    existingEmp.Designation = $"{model.Role} ({model.Domain})";
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{model.Role} \"{model.Name}\" registered to the internship cohort & directory successfully by Administrator.";
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/DeleteMember/1 (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to remove interns.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            var member = await _context.InternshipMembers.FindAsync(id);
            if (member != null)
            {
                _context.InternshipMembers.Remove(member);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Member \"{member.Name}\" removed from cohort.";
            }

            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/UpdateMemberStatus (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberStatus(int id, string status)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to modify intern status.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            var member = await _context.InternshipMembers.FindAsync(id);
            if (member != null)
            {
                member.Status = status;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Status updated to \"{status}\" for {member.Name}.";
            }

            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/SubmitOnlineTest
        // Submits online topic assessment quiz & grades instantly
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitOnlineTest(string internName, string? internEmail, string topicTitle, string track, int score, int totalQuestions, string? feedbackNotes)
        {
            if (string.IsNullOrWhiteSpace(internName) || string.IsNullOrWhiteSpace(topicTitle))
            {
                TempData["Error"] = "Intern name and Topic Title are required to submit the test.";
                return RedirectToAction(nameof(Index), new { tab = "tests" });
            }

            if (totalQuestions <= 0) totalQuestions = 5;
            if (score < 0) score = 0;
            if (score > totalQuestions) score = totalQuestions;

            double percentage = Math.Round((double)score / totalQuestions * 100, 1);
            bool isPassed = percentage >= 60;

            string gradeBadge = percentage switch
            {
                100 => "Flawless Distinction (A+)",
                >= 80 => "Excellence (A)",
                >= 60 => "Good Standing (B)",
                _ => "Needs Practice"
            };

            var testResult = new InternTestResult
            {
                InternName = internName.Trim(),
                InternEmail = internEmail?.Trim(),
                TopicTitle = topicTitle.Trim(),
                Track = string.IsNullOrWhiteSpace(track) ? "Backend .NET / C#" : track.Trim(),
                Score = score,
                TotalQuestions = totalQuestions,
                ScorePercentage = percentage,
                IsPassed = isPassed,
                GradeBadge = gradeBadge,
                FeedbackNotes = !string.IsNullOrWhiteSpace(feedbackNotes) 
                    ? feedbackNotes.Trim() 
                    : (isPassed ? "Completed online assessment with flying colors!" : "Attempted assessment — review study materials and retry."),
                TakenAt = DateTime.Now
            };

            _context.InternTestResults.Add(testResult);

            // Increment completed modules count for intern member if exists
            var member = await _context.InternshipMembers.FirstOrDefaultAsync(m => m.Name.ToLower() == internName.ToLower().Trim());
            if (member != null && isPassed)
            {
                member.CompletedModulesCount += 1;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"🎉 Test Result Recorded! {internName} scored {score}/{totalQuestions} ({percentage}%) on '{topicTitle}'.";
            return RedirectToAction(nameof(Index), new { tab = "tests" });
        }

        // =========================================================
        // POST: /Internship/UploadTestResult
        // Uploads certificate or external assessment scorecard
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTestResult(InternTestResult model, IFormFile? certificateFile)
        {
            if (string.IsNullOrWhiteSpace(model.InternName) || string.IsNullOrWhiteSpace(model.TopicTitle))
            {
                TempData["Error"] = "Intern name and topic title are required.";
                return RedirectToAction(nameof(Index), new { tab = "tests" });
            }

            if (model.TotalQuestions <= 0) model.TotalQuestions = 100;
            if (model.Score < 0) model.Score = 0;

            model.ScorePercentage = Math.Round((double)model.Score / model.TotalQuestions * 100, 1);
            model.IsPassed = model.ScorePercentage >= 60;
            model.GradeBadge = model.ScorePercentage switch
            {
                100 => "Flawless Distinction (A+)",
                >= 80 => "Excellence (A)",
                >= 60 => "Good Standing (B)",
                _ => "Needs Practice"
            };

            if (certificateFile != null && certificateFile.Length > 0)
            {
                model.CertificateProofPath = await SaveInternshipScreenshotAsync(certificateFile);
            }

            model.TakenAt = DateTime.Now;

            _context.InternTestResults.Add(model);

            // Update member's completed modules
            var member = await _context.InternshipMembers.FirstOrDefaultAsync(m => m.Name.ToLower() == model.InternName.ToLower().Trim());
            if (member != null && model.IsPassed)
            {
                member.CompletedModulesCount += 1;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Test scorecard for '{model.TopicTitle}' uploaded successfully!";
            return RedirectToAction(nameof(Index), new { tab = "tests" });
        }

        // =========================================================
        // POST: /Internship/DeleteTestResult
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTestResult(int id)
        {
            var test = await _context.InternTestResults.FindAsync(id);
            if (test != null)
            {
                _context.InternTestResults.Remove(test);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Assessment record deleted.";
            }
            return RedirectToAction(nameof(Index), new { tab = "tests" });
        }

        private async Task<string> SaveStudyMaterialFileAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "study_materials");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"study_{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/study_materials/{uniqueFileName}";
        }

        private async Task<string> SaveInternshipScreenshotAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "internship");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"doubt_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/internship/{uniqueFileName}";
        }
    }
}
