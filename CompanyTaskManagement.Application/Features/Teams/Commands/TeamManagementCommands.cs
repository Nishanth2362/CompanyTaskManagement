using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Teams.Commands
{
    // 1. Create Team
    public class CreateTeamCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? TeamLeaderId { get; set; }
    }

    // 2. Assign Leader
    public class AssignLeaderCommand : IRequest<Result<int>>
    {
        public int TeamId { get; set; }
        public int? TeamLeaderId { get; set; }
    }

    // 3. Add Team Member
    public class AddTeamMemberCommand : IRequest<Result<int>>
    {
        public int TeamId { get; set; }
        public int EmployeeId { get; set; }
    }

    // 4. Remove Team Member
    public class RemoveTeamMemberCommand : IRequest<Result<int>>
    {
        public int TeamId { get; set; }
        public int EmployeeId { get; set; }
    }

    // 5. Assign Task
    public class AssignTeamTaskCommand : IRequest<Result<int>>
    {
        public int TeamId { get; set; }
        public int AssignedToEmployeeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string? TaskDurationType { get; set; }
        public int? ProjectId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int? AssignedByLeaderId { get; set; }
    }

    // 6. Update Task Status
    public class UpdateTeamTaskStatusCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public int Status { get; set; }
        public string? IncompleteReason { get; set; }
    }

    // 7. Provide Justification
    public class ProvideJustificationCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public string JustificationReason { get; set; } = string.Empty;
    }

    // 8. Grant Extra Time
    public class GrantExtraTimeCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public DateTime? NewEndDate { get; set; }
        public string? LeaderSuggestion { get; set; }
    }

    // 9. Delete Team Task
    public class DeleteTeamTaskCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
    }

    // 10. Submit Review
    public class SubmitTeamLeaderReviewCommand : IRequest<Result<int>>
    {
        public int? TeamLeaderId { get; set; }
        public int EmployeeId { get; set; }
        public string Category { get; set; } = "🌟 Outstanding Performance";
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
        public string? ReviewerRole { get; set; }
        public string? ReviewerName { get; set; }
    }

    internal class TeamManagementCommandHandler :
        IRequestHandler<CreateTeamCommand, Result<int>>,
        IRequestHandler<AssignLeaderCommand, Result<int>>,
        IRequestHandler<AddTeamMemberCommand, Result<int>>,
        IRequestHandler<RemoveTeamMemberCommand, Result<int>>,
        IRequestHandler<AssignTeamTaskCommand, Result<int>>,
        IRequestHandler<UpdateTeamTaskStatusCommand, Result<int>>,
        IRequestHandler<ProvideJustificationCommand, Result<int>>,
        IRequestHandler<GrantExtraTimeCommand, Result<int>>,
        IRequestHandler<DeleteTeamTaskCommand, Result<int>>,
        IRequestHandler<SubmitTeamLeaderReviewCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<TeamManagementCommandHandler> _logger;

        public TeamManagementCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<TeamManagementCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // 1. Create Team
        public async Task<Result<int>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return await Result<int>.FailAsync("Team name cannot be empty.");
            }

            var team = new Team
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                TeamLeaderId = request.TeamLeaderId > 0 ? request.TeamLeaderId : null,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Team>().AddAsync(team);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(team.Id, $"Team '{team.Name}' created successfully!");
        }

        // 2. Assign Leader
        public async Task<Result<int>> Handle(AssignLeaderCommand request, CancellationToken cancellationToken)
        {
            var team = await _unitOfWork.Repository<Team>().GetByIdAsync(request.TeamId);
            if (team == null)
            {
                return await Result<int>.FailAsync("Team not found.");
            }

            team.TeamLeaderId = (request.TeamLeaderId.HasValue && request.TeamLeaderId.Value > 0) ? request.TeamLeaderId.Value : null;
            await _unitOfWork.Repository<Team>().UpdateAsync(team);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(team.Id, "Team Leader updated successfully.");
        }

        // 3. Add Member
        public async Task<Result<int>> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
        {
            var team = await _unitOfWork.Repository<Team>().GetByIdAsync(request.TeamId);
            if (team == null)
            {
                return await Result<int>.FailAsync("Team not found.");
            }

            var exists = await _unitOfWork.Repository<TeamMember>().Entities
                .AnyAsync(tm => tm.TeamId == request.TeamId && tm.EmployeeId == request.EmployeeId, cancellationToken);
            if (exists)
            {
                return await Result<int>.FailAsync("Employee is already a member of this team.");
            }

            var member = new TeamMember
            {
                TeamId = request.TeamId,
                EmployeeId = request.EmployeeId,
                AssignedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<TeamMember>().AddAsync(member);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(member.Id, "Employee added to team successfully.");
        }

        // 4. Remove Member
        public async Task<Result<int>> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _unitOfWork.Repository<TeamMember>().Entities
                .FirstOrDefaultAsync(tm => tm.TeamId == request.TeamId && tm.EmployeeId == request.EmployeeId, cancellationToken);

            if (member != null)
            {
                await _unitOfWork.Repository<TeamMember>().DeleteAsync(member);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(request.EmployeeId, "Employee removed from team.");
            }

            return await Result<int>.FailAsync("Team member not found.");
        }

        // 5. Assign Task
        public async Task<Result<int>> Handle(AssignTeamTaskCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return await Result<int>.FailAsync("Task title and description are required.");
            }

            var teamTask = new TeamTask
            {
                TeamId = request.TeamId,
                AssignedToEmployeeId = request.AssignedToEmployeeId,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Priority = (TaskPriority)request.Priority,
                TaskDurationType = request.TaskDurationType,
                ProjectId = request.ProjectId,
                StartDate = request.StartDate ?? DateTime.Now,
                EndDate = request.EndDate ?? request.DueDate,
                DueDate = request.DueDate ?? request.EndDate,
                Status = TeamTaskStatus.Pending,
                AssignedByLeaderId = request.AssignedByLeaderId > 0 ? request.AssignedByLeaderId : null,
                CreatedAt = DateTime.Now
            };

            await _unitOfWork.Repository<TeamTask>().AddAsync(teamTask);
            await _unitOfWork.Commit(cancellationToken);

            // Sync with main Tasks
            var mainTask = new TaskItem
            {
                TaskName = teamTask.Title,
                Description = teamTask.Description,
                Priority = teamTask.Priority,
                Status = TaskStatus.ToDo,
                ProjectId = teamTask.ProjectId,
                StartDate = teamTask.StartDate,
                EndDate = teamTask.EndDate,
                DueDate = teamTask.DueDate,
                Progress = 0,
                CreatedAt = DateTime.Now
            };

            await _unitOfWork.Repository<TaskItem>().AddAsync(mainTask);
            await _unitOfWork.Commit(cancellationToken);

            if (teamTask.AssignedToEmployeeId > 0)
            {
                mainTask.TaskEmployees.Add(new TaskEmployee
                {
                    TaskId = mainTask.Id,
                    EmployeeId = teamTask.AssignedToEmployeeId
                });
                await _unitOfWork.Commit(cancellationToken);
            }

            return await Result<int>.SuccessAsync(teamTask.Id, $"Task '{teamTask.Title}' assigned successfully.");
        }

        // 6. Update Task Status
        public async Task<Result<int>> Handle(UpdateTeamTaskStatusCommand request, CancellationToken cancellationToken)
        {
            var task = await _unitOfWork.Repository<TeamTask>().GetByIdAsync(request.TaskId);
            if (task == null)
            {
                return await Result<int>.FailAsync("Task not found.");
            }

            var newStatus = (TeamTaskStatus)request.Status;
            task.Status = newStatus;

            if (newStatus == TeamTaskStatus.NotCompleted)
            {
                if (string.IsNullOrWhiteSpace(request.IncompleteReason))
                {
                    return await Result<int>.FailAsync("Reason is required when marking a task as Not Completed.");
                }
                task.IncompleteReason = request.IncompleteReason.Trim();

                var mainTask = await _unitOfWork.Repository<TaskItem>().Entities
                    .FirstOrDefaultAsync(t => t.TaskName == task.Title, cancellationToken);
                if (mainTask != null)
                {
                    mainTask.DelayReason = request.IncompleteReason.Trim();
                    await _unitOfWork.Repository<TaskItem>().UpdateAsync(mainTask);
                }
            }
            else if (newStatus == TeamTaskStatus.Completed)
            {
                task.CompletedAt = DateTime.UtcNow;
                task.IncompleteReason = null;

                var mainTask = await _unitOfWork.Repository<TaskItem>().Entities
                    .FirstOrDefaultAsync(t => t.TaskName == task.Title, cancellationToken);
                if (mainTask != null)
                {
                    mainTask.Status = TaskStatus.Completed;
                    mainTask.Progress = 100;
                    mainTask.DelayReason = null;
                    mainTask.EndDate = DateTime.Now;
                    await _unitOfWork.Repository<TaskItem>().UpdateAsync(mainTask);
                }
            }
            else if (newStatus == TeamTaskStatus.InProgress)
            {
                task.IncompleteReason = null;
                var mainTask = await _unitOfWork.Repository<TaskItem>().Entities
                    .FirstOrDefaultAsync(t => t.TaskName == task.Title, cancellationToken);
                if (mainTask != null)
                {
                    mainTask.Status = TaskStatus.InProgress;
                    if (mainTask.Progress < 50) mainTask.Progress = 50;
                    mainTask.DelayReason = null;
                    await _unitOfWork.Repository<TaskItem>().UpdateAsync(mainTask);
                }
            }

            await _unitOfWork.Repository<TeamTask>().UpdateAsync(task);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(task.TeamId, $"Task status updated to '{newStatus}'.");
        }

        // 7. Provide Justification
        public async Task<Result<int>> Handle(ProvideJustificationCommand request, CancellationToken cancellationToken)
        {
            var task = await _unitOfWork.Repository<TeamTask>().GetByIdAsync(request.TaskId);
            if (task == null)
            {
                return await Result<int>.FailAsync("Task not found.");
            }

            task.Status = TeamTaskStatus.NotCompleted;
            task.IncompleteReason = request.JustificationReason.Trim();

            var mainTask = await _unitOfWork.Repository<TaskItem>().Entities
                .FirstOrDefaultAsync(t => t.TaskName == task.Title, cancellationToken);
            if (mainTask != null)
            {
                mainTask.DelayReason = request.JustificationReason.Trim();
                if (mainTask.Status != TaskStatus.Completed)
                {
                    mainTask.Status = TaskStatus.InProgress;
                }
                await _unitOfWork.Repository<TaskItem>().UpdateAsync(mainTask);
            }

            await _unitOfWork.Repository<TeamTask>().UpdateAsync(task);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(task.TeamId, $"Justification submitted for task '{task.Title}'.");
        }

        // 8. Grant Extra Time
        public async Task<Result<int>> Handle(GrantExtraTimeCommand request, CancellationToken cancellationToken)
        {
            var task = await _unitOfWork.Repository<TeamTask>().Entities
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                return await Result<int>.FailAsync("Task not found.");
            }

            if (request.NewEndDate.HasValue)
            {
                task.EndDate = request.NewEndDate.Value;
                task.DueDate = request.NewEndDate.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.LeaderSuggestion))
            {
                task.LeaderSuggestion = request.LeaderSuggestion.Trim();
            }

            if (task.Status == TeamTaskStatus.NotCompleted)
            {
                task.Status = TeamTaskStatus.InProgress;
            }

            var mainTask = await _unitOfWork.Repository<TaskItem>().Entities
                .FirstOrDefaultAsync(t => t.TaskName == task.Title, cancellationToken);
            if (mainTask != null)
            {
                if (request.NewEndDate.HasValue)
                {
                    mainTask.EndDate = request.NewEndDate.Value;
                    mainTask.DueDate = request.NewEndDate.Value;
                }
                mainTask.Status = TaskStatus.InProgress;
                await _unitOfWork.Repository<TaskItem>().UpdateAsync(mainTask);
            }

            await _unitOfWork.Repository<TeamTask>().UpdateAsync(task);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(task.TeamId, $"Extra time granted for task '{task.Title}'.");
        }

        // 9. Delete Team Task
        public async Task<Result<int>> Handle(DeleteTeamTaskCommand request, CancellationToken cancellationToken)
        {
            var task = await _unitOfWork.Repository<TeamTask>().GetByIdAsync(request.TaskId);
            if (task == null)
            {
                return await Result<int>.FailAsync("Task not found.");
            }

            var teamId = task.TeamId;
            await _unitOfWork.Repository<TeamTask>().DeleteAsync(task);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(teamId, "Team task deleted successfully.");
        }

        // 10. Submit Review
        public async Task<Result<int>> Handle(SubmitTeamLeaderReviewCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Comment))
            {
                return await Result<int>.FailAsync("Review comment is required.");
            }

            var emp = await _unitOfWork.Repository<Employee>().GetByIdAsync(request.EmployeeId);
            if (emp == null)
            {
                return await Result<int>.FailAsync("Employee not found.");
            }

            var leaderName = !string.IsNullOrWhiteSpace(request.ReviewerName) ? request.ReviewerName : "Team Leader";

            var review = new TeamLeaderReview
            {
                TeamLeaderId = request.TeamLeaderId,
                LeaderName = leaderName,
                EmployeeId = request.EmployeeId,
                EmployeeName = emp.Name,
                ReviewerName = leaderName,
                ReviewerRole = request.ReviewerRole ?? "Team Leader",
                Category = request.Category,
                Rating = Math.Clamp(request.Rating, 1, 5),
                Comments = request.Comment.Trim(),
                Feedback = request.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<TeamLeaderReview>().AddAsync(review);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(review.Id, $"Review submitted for {emp.Name}!");
        }
    }
}
