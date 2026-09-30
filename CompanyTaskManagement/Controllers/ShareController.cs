using System;
using System.IO;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Share.Commands;
using CompanyTaskManagement.Application.Features.Share.Queries;
using CompanyTaskManagement.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class ShareController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _env;

        public ShareController(IMediator mediator, IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env)
        {
            _mediator = mediator;
            _httpContextAccessor = httpContextAccessor;
            _env = env;
        }

        // =========================================================
        // GET: /Share
        // Main showcase feed
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? category = null,
            string? status = null,
            string? search = null,
            string sort = "latest",
            int page = 1,
            int pageSize = 10)
        {
            var result = await _mediator.Send(new GetShareDashboardQuery
            {
                Category = category,
                Status = status,
                Search = search,
                Sort = sort,
                Page = page,
                PageSize = pageSize
            });

            ViewBag.TotalPosts = result.TotalPosts;
            ViewBag.TotalFeedbacks = result.TotalFeedbacks;
            ViewBag.TotalLikes = result.TotalLikes;

            ViewBag.CurrentPage = result.CurrentPage;
            ViewBag.PageSize = result.PageSize;
            ViewBag.TotalPages = result.TotalPages;

            ViewBag.SelectedCategory = result.SelectedCategory;
            ViewBag.SelectedStatus = result.SelectedStatus;
            ViewBag.Search = result.Search;
            ViewBag.Sort = result.Sort;

            return View(result.Posts);
        }

        // =========================================================
        // POST: /Share/Create
        // Create a new post / link share
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,LiveUrl,RepositoryUrl,Category,AuthorName,AuthorEmail,AuthorDepartment,Tags")] ColleaguePost post)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new CreateColleaguePostCommand
            {
                Title = post.Title,
                Description = post.Description,
                LiveUrl = post.LiveUrl,
                RepositoryUrl = post.RepositoryUrl,
                Category = post.Category,
                AuthorName = post.AuthorName,
                AuthorEmail = post.AuthorEmail,
                AuthorDepartment = post.AuthorDepartment,
                Tags = post.Tags
            });

            if (result.Succeeded)
            {
                TempData["Success"] = result.Messages.Count > 0 ? result.Messages[0] : "Your post has been shared!";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // POST: /Share/AddFeedback
        // AJAX: Submit feedback on a post
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> AddFeedback([FromForm] AddFeedbackRequest req)
        {
            if (req == null || req.PostId <= 0 || string.IsNullOrWhiteSpace(req.ColleagueName) || string.IsNullOrWhiteSpace(req.Comment))
            {
                return Json(new { success = false, message = "Invalid feedback data." });
            }

            string? screenshotPath = null;
            if (req.Screenshot != null && req.Screenshot.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "feedbacks");
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + req.Screenshot.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await req.Screenshot.CopyToAsync(fileStream);
                }
                screenshotPath = "/uploads/feedbacks/" + uniqueFileName;
            }

            var result = await _mediator.Send(new AddFeedbackCommand
            {
                PostId = req.PostId,
                ColleagueName = req.ColleagueName,
                ColleagueEmail = req.ColleagueEmail,
                ColleagueRole = req.ColleagueRole,
                Sentiment = req.Sentiment,
                Rating = req.Rating,
                Comment = req.Comment,
                ScreenshotPath = screenshotPath
            });

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join("; ", result.Messages) });
            }

            var d = result.Data;
            return Json(new
            {
                success = true,
                message = "Your feedback has been submitted!",
                feedbackCount = d.FeedbackCount,
                sentimentLabel = d.SentimentLabel,
                authorInitials = d.AuthorInitials,
                authorName = d.AuthorName,
                authorRole = d.AuthorRole,
                rating = d.Rating,
                comment = d.Comment,
                screenshotPath = d.ScreenshotPath,
                createdAt = d.CreatedAt
            });
        }

        // =========================================================
        // POST: /Share/ToggleLike
        // AJAX: Upvote / like a post
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> ToggleLike([FromBody] LikeRequest req)
        {
            if (req == null || req.PostId <= 0)
                return Json(new { success = false });

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var result = await _mediator.Send(new TogglePostLikeCommand
            {
                PostId = req.PostId,
                UserIdentifier = ip
            });

            if (!result.Succeeded) return Json(new { success = false });

            return Json(new { success = true, liked = result.Data.Liked, likesCount = result.Data.LikesCount });
        }

        // =========================================================
        // POST: /Share/UpdateStatus (Admin)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int postId, PostStatus newStatus)
        {
            var result = await _mediator.Send(new UpdatePostStatusCommand
            {
                PostId = postId,
                NewStatus = newStatus
            });

            if (result.Succeeded)
            {
                TempData["Success"] = result.Messages.Count > 0 ? result.Messages[0] : "Status updated.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // POST: /Share/Delete (Admin)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int postId)
        {
            var result = await _mediator.Send(new DeletePostCommand(postId));

            if (result.Succeeded)
            {
                TempData["Success"] = result.Messages.Count > 0 ? result.Messages[0] : "Post removed.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // GET: /Share/GetFeedbacks/{postId}
        // AJAX: Load feedbacks for a specific post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetFeedbacks(int postId)
        {
            var feedbacks = await _mediator.Send(new GetPostFeedbacksQuery(postId));
            return Json(new { success = true, feedbacks });
        }

        // =========================================================
        // POST: /Share/AddReaction
        // AJAX: Quick reaction (OK / Not OK / Needs Work / Approved / Has Issues)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> AddReaction([FromBody] AddReactionRequest req)
        {
            if (req == null || req.PostId <= 0 || string.IsNullOrWhiteSpace(req.ReactorName))
                return Json(new { success = false, message = "Invalid request." });

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var identifier = $"{req.ReactorName.Trim().ToLower()}_{ip}";

            var result = await _mediator.Send(new AddReactionCommand
            {
                PostId = req.PostId,
                ReactorName = req.ReactorName,
                ReactionType = req.ReactionType,
                UserIdentifier = identifier
            });

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join("; ", result.Messages) });
            }

            return Json(new { success = true, toggled = result.Data.Toggled, counts = result.Data.Counts });
        }

        // =========================================================
        // POST: /Share/AddReply
        // AJAX: Post a quick reply/comment on a post
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> AddReply([FromBody] AddReplyRequest req)
        {
            if (req == null || req.PostId <= 0 || string.IsNullOrWhiteSpace(req.AuthorName) || string.IsNullOrWhiteSpace(req.ReplyText))
                return Json(new { success = false, message = "Name and reply text are required." });

            var result = await _mediator.Send(new AddReplyCommand
            {
                PostId = req.PostId,
                AuthorName = req.AuthorName,
                AuthorRole = req.AuthorRole,
                ReplyText = req.ReplyText
            });

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join("; ", result.Messages) });
            }

            var d = result.Data;
            return Json(new
            {
                success = true,
                replyCount = d.ReplyCount,
                authorInitials = d.AuthorInitials,
                authorName = d.AuthorName,
                authorRole = d.AuthorRole,
                replyText = d.ReplyText,
                createdAt = d.CreatedAt
            });
        }

        // =========================================================
        // GET: /Share/GetReplies
        // AJAX: Load replies for a post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetReplies(int postId)
        {
            var replies = await _mediator.Send(new GetPostRepliesQuery(postId));
            return Json(new { success = true, replies });
        }

        // =========================================================
        // GET: /Share/GetReactions
        // AJAX: Load reaction counts for a post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetReactions(int postId)
        {
            var counts = await _mediator.Send(new GetPostReactionsQuery(postId));
            return Json(new { success = true, counts });
        }
    }

    // =========================================================
    // Request DTOs for AJAX endpoints
    // =========================================================
    public class AddFeedbackRequest
    {
        public int PostId { get; set; }
        public string ColleagueName { get; set; } = string.Empty;
        public string? ColleagueEmail { get; set; }
        public string? ColleagueRole { get; set; }
        public int Sentiment { get; set; } = 2;
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
        public IFormFile? Screenshot { get; set; }
    }

    public class LikeRequest
    {
        public int PostId { get; set; }
    }

    public class AddReactionRequest
    {
        public int PostId { get; set; }
        public string ReactorName { get; set; } = string.Empty;
        public int ReactionType { get; set; } = 1; // 1=OK, 2=NotOK, 3=NeedsWork, 4=Approved, 5=HasIssues
    }

    public class AddReplyRequest
    {
        public int PostId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string ReplyText { get; set; } = string.Empty;
    }
}
