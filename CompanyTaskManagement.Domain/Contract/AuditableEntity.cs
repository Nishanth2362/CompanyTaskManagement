using System;

namespace CompanyTaskManagement.Domain.Contract
{
    public abstract class AuditableEntity<TId> : IAuditableEntity<TId>
    {
        public TId Id { get; set; } = default!;
        public string? CreatedBy { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime? CreatedOn { get; set; } = DateTime.UtcNow;
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? IPAddress { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? TenantId { get; set; } = string.Empty;
    }
}
