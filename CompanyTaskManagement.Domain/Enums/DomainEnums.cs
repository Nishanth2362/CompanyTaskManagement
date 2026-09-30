namespace CompanyTaskManagement.Domain.Enums
{
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Urgent = 3
    }

    public enum TaskStatus
    {
        ToDo = 0,
        InProgress = 1,
        InReview = 2,
        Completed = 3
    }

    public enum TeamTaskStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        NotCompleted = 3
    }

    public enum UserRole
    {
        Admin = 0,
        HR = 1,
        Employee = 2
    }
}
