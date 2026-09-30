using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Share.Queries;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Share.Commands
{
    // DTOs
    public class FeedbackResultDto
    {
        public int FeedbackCount { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public string AuthorInitials { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string? ScreenshotPath { get; set; }
        public string CreatedAt { get; set; } = "Just now";
    }

    public class ToggleLikeResultDto
    {
        public bool Liked { get; set; }
        public int LikesCount { get; set; }
    }

    public class AddReactionResultDto
    {
        public bool Toggled { get; set; }
        public List<ReactionCountDto> Counts { get; set; } = new();
    }

    public class AddReplyResultDto
    {
        public int ReplyCount { get; set; }
        public string AuthorInitials { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string ReplyText { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = "Just now";
    }

    // Commands
    public class CreateColleaguePostCommand : IRequest<Result<int>>
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LiveUrl { get; set; }
        public string? RepositoryUrl { get; set; }
        public PostCategory Category { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorEmail { get; set; }
        public string? AuthorDepartment { get; set; }
        public string? Tags { get; set; }
    }

    public class AddFeedbackCommand : IRequest<Result<FeedbackResultDto>>
    {
        public int PostId { get; set; }
        public string ColleagueName { get; set; } = string.Empty;
        public string? ColleagueEmail { get; set; }
        public string? ColleagueRole { get; set; }
        public int Sentiment { get; set; } = 2;
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
        public string? ScreenshotPath { get; set; }
    }

    public class TogglePostLikeCommand : IRequest<Result<ToggleLikeResultDto>>
    {
        public int PostId { get; set; }
        public string UserIdentifier { get; set; } = string.Empty;
    }

    public class UpdatePostStatusCommand : IRequest<Result<int>>
    {
        public int PostId { get; set; }
        public PostStatus NewStatus { get; set; }
    }

    public class DeletePostCommand : IRequest<Result<int>>
    {
        public int PostId { get; set; }
        public DeletePostCommand(int postId) => PostId = postId;
    }

    public class AddReactionCommand : IRequest<Result<AddReactionResultDto>>
    {
        public int PostId { get; set; }
        public string ReactorName { get; set; } = string.Empty;
        public int ReactionType { get; set; } = 1;
        public string UserIdentifier { get; set; } = string.Empty;
    }

    public class AddReplyCommand : IRequest<Result<AddReplyResultDto>>
    {
        public int PostId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string ReplyText { get; set; } = string.Empty;
    }

    // Handler
    internal class ShareCommandsHandler :
        IRequestHandler<CreateColleaguePostCommand, Result<int>>,
        IRequestHandler<AddFeedbackCommand, Result<FeedbackResultDto>>,
        IRequestHandler<TogglePostLikeCommand, Result<ToggleLikeResultDto>>,
        IRequestHandler<UpdatePostStatusCommand, Result<int>>,
        IRequestHandler<DeletePostCommand, Result<int>>,
        IRequestHandler<AddReactionCommand, Result<AddReactionResultDto>>,
        IRequestHandler<AddReplyCommand, Result<AddReplyResultDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public ShareCommandsHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(CreateColleaguePostCommand request, CancellationToken cancellationToken)
        {
            var post = new ColleaguePost
            {
                Title = request.Title,
                Description = request.Description,
                LiveUrl = request.LiveUrl,
                RepositoryUrl = request.RepositoryUrl,
                Category = request.Category,
                AuthorName = request.AuthorName,
                AuthorEmail = request.AuthorEmail,
                AuthorDepartment = request.AuthorDepartment ?? "Engineering",
                Tags = request.Tags,
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.SeekingFeedback,
                LikesCount = 0
            };

            await _unitOfWork.Repository<ColleaguePost>().AddAsync(post);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(post.Id, $"Your post \"{post.Title}\" has been shared with your colleagues!");
        }

        public async Task<Result<FeedbackResultDto>> Handle(AddFeedbackCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<FeedbackResultDto>.FailAsync("Post not found.");

            var feedback = new ColleagueFeedback
            {
                PostId = request.PostId,
                ColleagueName = request.ColleagueName.Trim(),
                ColleagueEmail = request.ColleagueEmail?.Trim(),
                ColleagueRole = request.ColleagueRole?.Trim() ?? "Team Member",
                Sentiment = (FeedbackSentiment)request.Sentiment,
                Rating = Math.Clamp(request.Rating, 1, 5),
                Comment = request.Comment.Trim(),
                ScreenshotPath = request.ScreenshotPath,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<ColleagueFeedback>().AddAsync(feedback);
            await _unitOfWork.Commit(cancellationToken);

            var feedbackCount = await _unitOfWork.Repository<ColleagueFeedback>().Entities.CountAsync(f => f.PostId == request.PostId, cancellationToken);

            var result = new FeedbackResultDto
            {
                FeedbackCount = feedbackCount,
                SentimentLabel = GetSentimentLabel(feedback.Sentiment),
                AuthorInitials = !string.IsNullOrEmpty(feedback.ColleagueName) ? feedback.ColleagueName[..1].ToUpper() : "?",
                AuthorName = feedback.ColleagueName,
                AuthorRole = feedback.ColleagueRole,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                ScreenshotPath = feedback.ScreenshotPath,
                CreatedAt = "Just now"
            };

            return await Result<FeedbackResultDto>.SuccessAsync(result, "Your feedback has been submitted!");
        }

        public async Task<Result<ToggleLikeResultDto>> Handle(TogglePostLikeCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<ToggleLikeResultDto>.FailAsync("Post not found.");

            var existingLike = await _unitOfWork.Repository<ColleaguePostLike>().Entities
                .FirstOrDefaultAsync(l => l.PostId == request.PostId && l.UserIdentifier == request.UserIdentifier, cancellationToken);

            bool liked;
            if (existingLike != null)
            {
                await _unitOfWork.Repository<ColleaguePostLike>().DeleteAsync(existingLike);
                post.LikesCount = Math.Max(0, post.LikesCount - 1);
                liked = false;
            }
            else
            {
                await _unitOfWork.Repository<ColleaguePostLike>().AddAsync(new ColleaguePostLike
                {
                    PostId = request.PostId,
                    UserIdentifier = request.UserIdentifier,
                    LikedAt = DateTime.UtcNow
                });
                post.LikesCount++;
                liked = true;
            }

            await _unitOfWork.Repository<ColleaguePost>().UpdateAsync(post);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<ToggleLikeResultDto>.SuccessAsync(new ToggleLikeResultDto
            {
                Liked = liked,
                LikesCount = post.LikesCount
            });
        }

        public async Task<Result<int>> Handle(UpdatePostStatusCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<int>.FailAsync("Post not found.");

            post.Status = request.NewStatus;
            post.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<ColleaguePost>().UpdateAsync(post);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(post.Id, $"Post status updated to \"{request.NewStatus}\".");
        }

        public async Task<Result<int>> Handle(DeletePostCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<int>.FailAsync("Post not found.");

            await _unitOfWork.Repository<ColleaguePost>().DeleteAsync(post);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(post.Id, "Post has been removed.");
        }

        public async Task<Result<AddReactionResultDto>> Handle(AddReactionCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<AddReactionResultDto>.FailAsync("Post not found.");

            var existing = await _unitOfWork.Repository<ColleagueReaction>().Entities
                .FirstOrDefaultAsync(r => r.PostId == request.PostId && r.UserIdentifier == request.UserIdentifier, cancellationToken);

            bool toggled;
            if (existing != null && existing.Reaction == (ReactionType)request.ReactionType)
            {
                await _unitOfWork.Repository<ColleagueReaction>().DeleteAsync(existing);
                toggled = false;
            }
            else
            {
                if (existing != null)
                {
                    await _unitOfWork.Repository<ColleagueReaction>().DeleteAsync(existing);
                }

                await _unitOfWork.Repository<ColleagueReaction>().AddAsync(new ColleagueReaction
                {
                    PostId = request.PostId,
                    UserIdentifier = request.UserIdentifier,
                    ReactorName = request.ReactorName.Trim(),
                    Reaction = (ReactionType)request.ReactionType,
                    CreatedAt = DateTime.UtcNow
                });
                toggled = true;
            }

            await _unitOfWork.Commit(cancellationToken);

            var counts = await _unitOfWork.Repository<ColleagueReaction>().Entities
                .Where(r => r.PostId == request.PostId)
                .GroupBy(r => r.Reaction)
                .Select(g => new ReactionCountDto { Type = (int)g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return await Result<AddReactionResultDto>.SuccessAsync(new AddReactionResultDto
            {
                Toggled = toggled,
                Counts = counts
            });
        }

        public async Task<Result<AddReplyResultDto>> Handle(AddReplyCommand request, CancellationToken cancellationToken)
        {
            var post = await _unitOfWork.Repository<ColleaguePost>().GetByIdAsync(request.PostId);
            if (post == null) return await Result<AddReplyResultDto>.FailAsync("Post not found.");

            var reply = new ColleagueReply
            {
                PostId = request.PostId,
                AuthorName = request.AuthorName.Trim(),
                AuthorRole = request.AuthorRole?.Trim() ?? "Team Member",
                ReplyText = request.ReplyText.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<ColleagueReply>().AddAsync(reply);
            await _unitOfWork.Commit(cancellationToken);

            var replyCount = await _unitOfWork.Repository<ColleagueReply>().Entities.CountAsync(r => r.PostId == request.PostId, cancellationToken);

            var result = new AddReplyResultDto
            {
                ReplyCount = replyCount,
                AuthorInitials = !string.IsNullOrEmpty(reply.AuthorName) ? reply.AuthorName[..1].ToUpper() : "?",
                AuthorName = reply.AuthorName,
                AuthorRole = reply.AuthorRole,
                ReplyText = reply.ReplyText,
                CreatedAt = "Just now"
            };

            return await Result<AddReplyResultDto>.SuccessAsync(result);
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
    }
}
