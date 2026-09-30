using System;
using System.IO;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Internship.Commands;
using CompanyTaskManagement.Application.Features.Internship.Queries;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class InternshipController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<InternshipController> _logger;
        private readonly IUserSessionService _sessionService;

        public InternshipController(
            IMediator mediator,
            IWebHostEnvironment environment,
            ILogger<InternshipController> logger,
            IUserSessionService sessionService)
        {
            _mediator = mediator;
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
            var result = await _mediator.Send(new GetInternshipDashboardQuery
            {
                Track = track,
                DoubtStatus = doubtStatus,
                Search = search
            });

            var data = result.Data ?? new InternshipDashboardData();

            ViewBag.ActiveTab = tab;
            ViewBag.SelectedTrack = track ?? "all";
            ViewBag.SelectedDoubtStatus = doubtStatus ?? "all";
            ViewBag.Search = search;
            ViewBag.Employees = data.Employees;

            ViewBag.TotalMaterials = data.TotalMaterials;
            ViewBag.TotalDoubts = data.TotalDoubts;
            ViewBag.OpenDoubts = data.OpenDoubts;
            ViewBag.ResolvedDoubts = data.ResolvedDoubts;
            ViewBag.TotalClarifications = data.TotalClarifications;
            ViewBag.TotalMembers = data.TotalMembers;
            ViewBag.TotalInterns = data.TotalInterns;
            ViewBag.TotalMentors = data.TotalMentors;
            ViewBag.TotalYouTubeVideos = data.TotalYouTubeVideos;
            ViewBag.TotalTests = data.TotalTests;
            ViewBag.PassedTests = data.PassedTests;
            ViewBag.AverageScore = data.AverageScore;

            ViewBag.Tracks = new System.Collections.Generic.List<string>
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

            ViewBag.Materials = data.Materials;
            ViewBag.Doubts = data.Doubts;
            ViewBag.Members = data.Members;
            ViewBag.YouTubeVideos = data.YouTubeVideos;
            ViewBag.TestResults = data.TestResults;

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

            string? uploadedScreenshotPath = null;
            if (screenshotFile != null && screenshotFile.Length > 0)
            {
                uploadedScreenshotPath = await SaveInternshipScreenshotAsync(screenshotFile);
            }

            var result = await _mediator.Send(new AskDoubtCommand
            {
                Title = model.Title,
                Description = model.Description,
                InternName = model.InternName,
                InternEmployeeId = model.InternEmployeeId,
                Track = model.Domain,
                ScreenshotPath = uploadedScreenshotPath
            });

            TempData["Success"] = "Your question has been posted to the Internship Doubt Forum. A mentor will review it shortly!";
            return RedirectToAction(nameof(Index), new { tab = "doubts" });
        }

        // =========================================================
        // GET: /Internship/DoubtDetails/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DoubtDetails(int id)
        {
            var result = await _mediator.Send(new GetDoubtDetailsQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();
            return View(result.Data);
        }

        // =========================================================
        // POST: /Internship/AddClarification
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClarification(int doubtId, string employeeName, int? employeeId, string clarificationText, string? codeSolution, string? helpfulLink)
        {
            if (string.IsNullOrWhiteSpace(clarificationText) || string.IsNullOrWhiteSpace(employeeName))
            {
                TempData["Error"] = "Please enter your name and a helpful explanation/answer.";
                return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
            }

            var result = await _mediator.Send(new AddClarificationCommand
            {
                DoubtId = doubtId,
                EmployeeName = employeeName,
                EmployeeId = employeeId,
                ClarificationText = clarificationText,
                CodeSolution = codeSolution,
                HelpfulLink = helpfulLink
            });

            TempData["Success"] = "Your clarification has been submitted. Thank you for helping out!";
            return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
        }

        // =========================================================
        // POST: /Internship/AcceptClarification (Mentor/Admin accepts solution)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptClarification(int doubtId, int clarificationId)
        {
            var result = await _mediator.Send(new AcceptClarificationCommand
            {
                DoubtId = doubtId,
                ClarificationId = clarificationId
            });

            TempData["Success"] = "Clarification marked as accepted solution! This question is now resolved.";
            return RedirectToAction(nameof(DoubtDetails), new { id = doubtId });
        }

        // =========================================================
        // POST: /Internship/UpvoteDoubt (AJAX)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> UpvoteDoubt(int id)
        {
            var result = await _mediator.Send(new UpvoteDoubtCommand { Id = id });
            if (!result.Succeeded)
            {
                return Json(new { success = false, message = "Doubt not found" });
            }

            return Json(new { success = true, upvotes = result.Data });
        }

        // =========================================================
        // POST: /Internship/AddMaterial (Mentor/Admin upload)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaterial(InternStudyMaterial model, IFormFile? uploadFile)
        {
            if (uploadFile != null && uploadFile.Length > 0)
            {
                model.FilePath = await SaveStudyMaterialFileAsync(uploadFile);
            }

            await _mediator.Send(new AddStudyMaterialCommand { Material = model });
            TempData["Success"] = $"Study material '{model.Title}' added successfully!";
            return RedirectToAction(nameof(Index), new { tab = "materials", track = model.Track });
        }

        // =========================================================
        // POST: /Internship/DeleteMaterial (Admin only)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Only administrators have permission to delete study resources.";
                return RedirectToAction(nameof(Index), new { tab = "materials" });
            }

            await _mediator.Send(new DeleteStudyMaterialCommand { Id = id });
            TempData["Success"] = "Study material removed.";
            return RedirectToAction(nameof(Index), new { tab = "materials" });
        }

        // =========================================================
        // POST: /Internship/AddYouTubeReference
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddYouTubeReference(InternYouTubeReference model)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.YouTubeUrl))
            {
                TempData["Error"] = "Video title and YouTube URL are required.";
                return RedirectToAction(nameof(Index), new { tab = "youtube" });
            }

            await _mediator.Send(new AddYouTubeReferenceCommand { Reference = model });
            TempData["Success"] = $"YouTube video '{model.Title}' added successfully!";
            return RedirectToAction(nameof(Index), new { tab = "youtube", track = model.Track });
        }

        // =========================================================
        // POST: /Internship/DeleteYouTubeReference
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteYouTubeReference(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Only administrators can delete video references.";
                return RedirectToAction(nameof(Index), new { tab = "youtube" });
            }

            await _mediator.Send(new DeleteYouTubeReferenceCommand { Id = id });
            TempData["Success"] = "YouTube reference removed.";
            return RedirectToAction(nameof(Index), new { tab = "youtube" });
        }

        // =========================================================
        // POST: /Internship/AddMember
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(InternshipMember model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["Error"] = "Member name is required.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            await _mediator.Send(new AddInternshipMemberCommand { Member = model });
            TempData["Success"] = $"Member '{model.Name.Trim()}' saved successfully.";
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/DeleteMember
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Only administrators can remove members from the program.";
                return RedirectToAction(nameof(Index), new { tab = "members" });
            }

            await _mediator.Send(new DeleteInternshipMemberCommand { Id = id });
            TempData["Success"] = "Member removed from internship program.";
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/UpdateMemberStatus
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberStatus(int id, string status)
        {
            await _mediator.Send(new UpdateInternshipMemberStatusCommand { Id = id, Status = status });
            TempData["Success"] = "Member status updated.";
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        // =========================================================
        // POST: /Internship/SubmitOnlineTest
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitOnlineTest(string internName, string? internEmail, string topicTitle, string track, int score, int totalQuestions, string? feedbackNotes)
        {
            await _mediator.Send(new SubmitOnlineTestCommand
            {
                InternName = internName,
                InternEmail = internEmail,
                TopicTitle = topicTitle,
                Track = track,
                Score = score,
                TotalQuestions = totalQuestions,
                FeedbackNotes = feedbackNotes
            });

            TempData["Success"] = $"🎉 Congratulations {internName}! Your test result has been recorded successfully.";
            return RedirectToAction(nameof(Index), new { tab = "tests", track = track });
        }

        // =========================================================
        // POST: /Internship/UploadTestResult
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTestResult(InternTestResult model, IFormFile? certificateFile)
        {
            if (certificateFile != null && certificateFile.Length > 0)
            {
                model.CertificateProofPath = await SaveTestReportFileAsync(certificateFile);
            }

            await _mediator.Send(new UploadTestResultCommand { Result = model });
            TempData["Success"] = $"Test result for '{model.InternName}' recorded successfully!";
            return RedirectToAction(nameof(Index), new { tab = "tests" });
        }

        // =========================================================
        // POST: /Internship/DeleteTestResult
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTestResult(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Only administrators can delete test results.";
                return RedirectToAction(nameof(Index), new { tab = "tests" });
            }

            await _mediator.Send(new DeleteTestResultCommand { Id = id });
            TempData["Success"] = "Test result record deleted.";
            return RedirectToAction(nameof(Index), new { tab = "tests" });
        }

        // =========================================================
        // File Upload Helpers
        // =========================================================

        private async Task<string> SaveInternshipScreenshotAsync(IFormFile file)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "internship", "doubts");
            Directory.CreateDirectory(folder);
            var fileName = $"doubt_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var path = Path.Combine(folder, fileName);
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/internship/doubts/{fileName}";
        }

        private async Task<string> SaveStudyMaterialFileAsync(IFormFile file)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "internship", "materials");
            Directory.CreateDirectory(folder);
            var fileName = $"material_{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var path = Path.Combine(folder, fileName);
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/internship/materials/{fileName}";
        }

        private async Task<string> SaveTestReportFileAsync(IFormFile file)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "internship", "tests");
            Directory.CreateDirectory(folder);
            var fileName = $"test_{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var path = Path.Combine(folder, fileName);
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/internship/tests/{fileName}";
        }
    }
}
