using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class ShareController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _env;

        public ShareController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env)
        {
            _context = context;
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
            string sort = "latest")
        {
            var query = _context.ColleaguePosts
                .Include(p => p.Feedbacks)
                .AsQueryable();

            // Category filter
            if (!string.IsNullOrEmpty(category) && int.TryParse(category, out int catVal))
            {
                query = query.Where(p => (int)p.Category == catVal);
            }

            // Status filter
            if (!string.IsNullOrEmpty(status) && int.TryParse(status, out int statusVal))
            {
                query = query.Where(p => (int)p.Status == statusVal);
            }

            // Search
            if (!string.IsNullOrEmpty(search))
            {
                var term = search.ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(term)) ||
                    p.AuthorName.ToLower().Contains(term));
            }

            // Sort
            query = sort switch
            {
                "most_liked" => query.OrderByDescending(p => p.LikesCount).ThenByDescending(p => p.CreatedAt),
                "most_feedback" => query.OrderByDescending(p => p.Feedbacks.Count).ThenByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt)
            };

            var posts = await query.ToListAsync();

            // Stats for hero
            ViewBag.TotalPosts = await _context.ColleaguePosts.CountAsync();
            ViewBag.TotalFeedbacks = await _context.ColleagueFeedbacks.CountAsync();
            ViewBag.TotalLikes = await _context.ColleaguePosts.SumAsync(p => (int?)p.LikesCount) ?? 0;

            ViewBag.SelectedCategory = category;
            ViewBag.SelectedStatus = status;
            ViewBag.Search = search;
            ViewBag.Sort = sort;

            return View(posts);
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

            post.CreatedAt = DateTime.UtcNow;
            post.Status = PostStatus.SeekingFeedback;
            post.LikesCount = 0;

            _context.ColleaguePosts.Add(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Your post \"{post.Title}\" has been shared with your colleagues!";
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

            var post = await _context.ColleaguePosts.FindAsync(req.PostId);
            if (post == null) return Json(new { success = false, message = "Post not found." });

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

            var feedback = new ColleagueFeedback
            {
                PostId = req.PostId,
                ColleagueName = req.ColleagueName.Trim(),
                ColleagueEmail = req.ColleagueEmail?.Trim(),
                ColleagueRole = req.ColleagueRole?.Trim() ?? "Team Member",
                Sentiment = (FeedbackSentiment)req.Sentiment,
                Rating = Math.Clamp(req.Rating, 1, 5),
                Comment = req.Comment.Trim(),
                ScreenshotPath = screenshotPath,
                CreatedAt = DateTime.UtcNow
            };

            _context.ColleagueFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            var feedbackCount = await _context.ColleagueFeedbacks.CountAsync(f => f.PostId == req.PostId);

            return Json(new
            {
                success = true,
                message = "Your feedback has been submitted!",
                feedbackCount,
                sentimentLabel = GetSentimentLabel(feedback.Sentiment),
                authorInitials = feedback.ColleagueName.Length > 0 ? feedback.ColleagueName[0].ToString().ToUpper() : "?",
                authorName = feedback.ColleagueName,
                authorRole = feedback.ColleagueRole,
                rating = feedback.Rating,
                comment = feedback.Comment,
                screenshotPath = feedback.ScreenshotPath,
                createdAt = "Just now"
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

            var post = await _context.ColleaguePosts.FindAsync(req.PostId);
            if (post == null) return Json(new { success = false });

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userIdentifier = ip;

            var existingLike = await _context.ColleaguePostLikes
                .FirstOrDefaultAsync(l => l.PostId == req.PostId && l.UserIdentifier == userIdentifier);

            bool liked;
            if (existingLike != null)
            {
                _context.ColleaguePostLikes.Remove(existingLike);
                post.LikesCount = Math.Max(0, post.LikesCount - 1);
                liked = false;
            }
            else
            {
                _context.ColleaguePostLikes.Add(new ColleaguePostLike
                {
                    PostId = req.PostId,
                    UserIdentifier = userIdentifier,
                    LikedAt = DateTime.UtcNow
                });
                post.LikesCount++;
                liked = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, liked, likesCount = post.LikesCount });
        }

        // =========================================================
        // POST: /Share/UpdateStatus (Admin)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int postId, PostStatus newStatus)
        {
            var post = await _context.ColleaguePosts.FindAsync(postId);
            if (post == null)
            {
                TempData["Error"] = "Post not found.";
                return RedirectToAction(nameof(Index));
            }

            post.Status = newStatus;
            post.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Post status updated to \"{newStatus}\".";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // POST: /Share/Delete (Admin)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int postId)
        {
            var post = await _context.ColleaguePosts.FindAsync(postId);
            if (post == null)
            {
                TempData["Error"] = "Post not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.ColleaguePosts.Remove(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Post has been removed.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // GET: /Share/GetFeedbacks/{postId}
        // AJAX: Load feedbacks for a specific post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetFeedbacks(int postId)
        {
            var feedbacks = await _context.ColleagueFeedbacks
                .Where(f => f.PostId == postId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    f.Id,
                    f.ColleagueName,
                    f.ColleagueRole,
                    f.Rating,
                    f.Comment,
                    f.ScreenshotPath,
                    SentimentLabel = GetSentimentLabel((FeedbackSentiment)f.Sentiment),
                    SentimentClass = GetSentimentClass((FeedbackSentiment)f.Sentiment),
                    AuthorInitials = f.ColleagueName.Substring(0, 1).ToUpper(),
                    CreatedAt = f.CreatedAt.ToString("MMM dd, yyyy")
                })
                .ToListAsync();

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

            var post = await _context.ColleaguePosts.FindAsync(req.PostId);
            if (post == null) return Json(new { success = false, message = "Post not found." });

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var identifier = $"{req.ReactorName.Trim().ToLower()}_{ip}";

            // Remove any existing reaction by this user on this post (one reaction per user)
            var existing = await _context.ColleagueReactions
                .FirstOrDefaultAsync(r => r.PostId == req.PostId && r.UserIdentifier == identifier);

            bool toggled = false;
            if (existing != null && existing.Reaction == (ReactionType)req.ReactionType)
            {
                // Same reaction → toggle off
                _context.ColleagueReactions.Remove(existing);
                toggled = false;
            }
            else
            {
                if (existing != null)
                    _context.ColleagueReactions.Remove(existing);

                _context.ColleagueReactions.Add(new ColleagueReaction
                {
                    PostId = req.PostId,
                    UserIdentifier = identifier,
                    ReactorName = req.ReactorName.Trim(),
                    Reaction = (ReactionType)req.ReactionType,
                    CreatedAt = DateTime.UtcNow
                });
                toggled = true;
            }

            await _context.SaveChangesAsync();

            // Return updated counts for all reaction types
            var counts = await _context.ColleagueReactions
                .Where(r => r.PostId == req.PostId)
                .GroupBy(r => r.Reaction)
                .Select(g => new { Type = (int)g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(new { success = true, toggled, counts });
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

            var post = await _context.ColleaguePosts.FindAsync(req.PostId);
            if (post == null) return Json(new { success = false, message = "Post not found." });

            var reply = new ColleagueReply
            {
                PostId = req.PostId,
                AuthorName = req.AuthorName.Trim(),
                AuthorRole = req.AuthorRole?.Trim() ?? "Team Member",
                ReplyText = req.ReplyText.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ColleagueReplies.Add(reply);
            await _context.SaveChangesAsync();

            int replyCount = await _context.ColleagueReplies.CountAsync(r => r.PostId == req.PostId);

            return Json(new
            {
                success = true,
                replyCount,
                authorInitials = reply.AuthorName[0].ToString().ToUpper(),
                authorName = reply.AuthorName,
                authorRole = reply.AuthorRole,
                replyText = reply.ReplyText,
                createdAt = "Just now"
            });
        }

        // =========================================================
        // GET: /Share/GetReplies
        // AJAX: Load replies for a post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetReplies(int postId)
        {
            var replies = await _context.ColleagueReplies
                .Where(r => r.PostId == postId)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.AuthorName,
                    r.AuthorRole,
                    r.ReplyText,
                    AuthorInitials = r.AuthorName.Substring(0, 1).ToUpper(),
                    CreatedAt = r.CreatedAt.ToString("MMM dd, hh:mm tt")
                })
                .ToListAsync();

            return Json(new { success = true, replies });
        }

        // =========================================================
        // GET: /Share/GetReactions
        // AJAX: Load reaction counts for a post
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetReactions(int postId)
        {
            var counts = await _context.ColleagueReactions
                .Where(r => r.PostId == postId)
                .GroupBy(r => r.Reaction)
                .Select(g => new { Type = (int)g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(new { success = true, counts });
        }

        // =========================================================
        // Helpers
        // =========================================================
        private static string GetSentimentLabel(FeedbackSentiment s) => s switch
        {
            FeedbackSentiment.Impressive => "🌟 Highly Impressive",
            FeedbackSentiment.Suggestion => "💡 Suggestion",
            FeedbackSentiment.BugFound => "🐞 Bug Found",
            FeedbackSentiment.ReadyToShip => "🚀 Ready to Ship",
            FeedbackSentiment.DesignFeedback => "🎨 Design Feedback",
            _ => "💬 Feedback"
        };

        private static string GetSentimentClass(FeedbackSentiment s) => s switch
        {
            FeedbackSentiment.Impressive => "bg-warning text-dark",
            FeedbackSentiment.Suggestion => "bg-info text-dark",
            FeedbackSentiment.BugFound => "bg-danger text-white",
            FeedbackSentiment.ReadyToShip => "bg-success text-white",
            FeedbackSentiment.DesignFeedback => "bg-primary text-white",
            _ => "bg-secondary text-white"
        };
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
