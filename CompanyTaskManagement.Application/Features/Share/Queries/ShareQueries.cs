using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Share.Queries
{
    public class ShareDashboardDto
    {
        public List<ColleaguePost> Posts { get; set; } = new();
        public int TotalPosts { get; set; }
        public int TotalFeedbacks { get; set; }
        public int TotalLikes { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public string? SelectedCategory { get; set; }
        public string? SelectedStatus { get; set; }
        public string? Search { get; set; }
        public string Sort { get; set; } = "latest";
    }

    public class PostFeedbackDto
    {
        public int Id { get; set; }
        public string ColleagueName { get; set; } = string.Empty;
        public string? ColleagueRole { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string? ScreenshotPath { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public string SentimentClass { get; set; } = string.Empty;
        public string AuthorInitials { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class PostReplyDto
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string ReplyText { get; set; } = string.Empty;
        public string AuthorInitials { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class ReactionCountDto
    {
        public int Type { get; set; }
        public int Count { get; set; }
    }

    public class GetShareDashboardQuery : IRequest<ShareDashboardDto>
    {
        public string? Category { get; set; }
        public string? Status { get; set; }
        public string? Search { get; set; }
        public string Sort { get; set; } = "latest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetPostFeedbacksQuery : IRequest<List<PostFeedbackDto>>
    {
        public int PostId { get; set; }
        public GetPostFeedbacksQuery(int postId) => PostId = postId;
    }

    public class GetPostRepliesQuery : IRequest<List<PostReplyDto>>
    {
        public int PostId { get; set; }
        public GetPostRepliesQuery(int postId) => PostId = postId;
    }

    public class GetPostReactionsQuery : IRequest<List<ReactionCountDto>>
    {
        public int PostId { get; set; }
        public GetPostReactionsQuery(int postId) => PostId = postId;
    }

    internal class ShareQueriesHandler :
        IRequestHandler<GetShareDashboardQuery, ShareDashboardDto>,
        IRequestHandler<GetPostFeedbacksQuery, List<PostFeedbackDto>>,
        IRequestHandler<GetPostRepliesQuery, List<PostReplyDto>>,
        IRequestHandler<GetPostReactionsQuery, List<ReactionCountDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public ShareQueriesHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ShareDashboardDto> Handle(GetShareDashboardQuery request, CancellationToken cancellationToken)
        {
            var query = _unitOfWork.Repository<ColleaguePost>().Entities
                .Include(p => p.Feedbacks)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.Category) && int.TryParse(request.Category, out int catVal))
            {
                query = query.Where(p => (int)p.Category == catVal);
            }

            if (!string.IsNullOrEmpty(request.Status) && int.TryParse(request.Status, out int statusVal))
            {
                query = query.Where(p => (int)p.Status == statusVal);
            }

            if (!string.IsNullOrEmpty(request.Search))
            {
                var term = request.Search.ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(term)) ||
                    p.AuthorName.ToLower().Contains(term));
            }

            query = request.Sort switch
            {
                "most_liked" => query.OrderByDescending(p => p.LikesCount).ThenByDescending(p => p.CreatedAt),
                "most_feedback" => query.OrderByDescending(p => p.Feedbacks.Count).ThenByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt)
            };

            var totalCount = await query.CountAsync(cancellationToken);
            var posts = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var totalPosts = await _unitOfWork.Repository<ColleaguePost>().Entities.CountAsync(cancellationToken);
            var totalFeedbacks = await _unitOfWork.Repository<ColleagueFeedback>().Entities.CountAsync(cancellationToken);
            var totalLikes = await _unitOfWork.Repository<ColleaguePost>().Entities.SumAsync(p => (int?)p.LikesCount, cancellationToken) ?? 0;

            return new ShareDashboardDto
            {
                Posts = posts,
                TotalPosts = totalPosts,
                TotalFeedbacks = totalFeedbacks,
                TotalLikes = totalLikes,
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
                TotalCount = totalCount,
                SelectedCategory = request.Category,
                SelectedStatus = request.Status,
                Search = request.Search,
                Sort = request.Sort
            };
        }

        public async Task<List<PostFeedbackDto>> Handle(GetPostFeedbacksQuery request, CancellationToken cancellationToken)
        {
            var rawFeedbacks = await _unitOfWork.Repository<ColleagueFeedback>().Entities
                .Where(f => f.PostId == request.PostId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync(cancellationToken);

            return rawFeedbacks.Select(f => new PostFeedbackDto
            {
                Id = f.Id,
                ColleagueName = f.ColleagueName,
                ColleagueRole = f.ColleagueRole,
                Rating = f.Rating,
                Comment = f.Comment,
                ScreenshotPath = f.ScreenshotPath,
                SentimentLabel = GetSentimentLabel(f.Sentiment),
                SentimentClass = GetSentimentClass(f.Sentiment),
                AuthorInitials = !string.IsNullOrEmpty(f.ColleagueName) ? f.ColleagueName[..1].ToUpper() : "?",
                CreatedAt = f.CreatedAt.ToString("MMM dd, yyyy")
            }).ToList();
        }

        public async Task<List<PostReplyDto>> Handle(GetPostRepliesQuery request, CancellationToken cancellationToken)
        {
            var rawReplies = await _unitOfWork.Repository<ColleagueReply>().Entities
                .Where(r => r.PostId == request.PostId)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

            return rawReplies.Select(r => new PostReplyDto
            {
                Id = r.Id,
                AuthorName = r.AuthorName,
                AuthorRole = r.AuthorRole,
                ReplyText = r.ReplyText,
                AuthorInitials = !string.IsNullOrEmpty(r.AuthorName) ? r.AuthorName[..1].ToUpper() : "?",
                CreatedAt = r.CreatedAt.ToString("MMM dd, hh:mm tt")
            }).ToList();
        }

        public async Task<List<ReactionCountDto>> Handle(GetPostReactionsQuery request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<ColleagueReaction>().Entities
                .Where(r => r.PostId == request.PostId)
                .GroupBy(r => r.Reaction)
                .Select(g => new ReactionCountDto
                {
                    Type = (int)g.Key,
                    Count = g.Count()
                })
                .ToListAsync(cancellationToken);
        }

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
}
