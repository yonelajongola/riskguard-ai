using RiskGuard.Domain.Common;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Domain.Entities;

public sealed class ComplianceFramework : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<ComplianceControl> Controls { get; set; } = [];
}

public sealed class ComplianceControl : BaseEntity
{
    public Guid FrameworkId { get; set; }
    public ComplianceFramework? Framework { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<ComplianceGap> Gaps { get; set; } = [];
}

public sealed class ComplianceGap : BaseEntity
{
    public Guid ControlId { get; set; }
    public ComplianceControl? Control { get; set; }
    public Guid? RiskItemId { get; set; }
    public RiskItem? RelatedRisk { get; set; }
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime DueDateUtc { get; set; }
    public string Status { get; set; } = "Open";
}

public sealed class Incident : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentCategory Category { get; set; }
    public Severity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Detected;
    public string Owner { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? RiskItemId { get; set; }
    public RiskItem? RelatedRisk { get; set; }
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueDateUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string EvidenceNotes { get; set; } = string.Empty;
    public ICollection<IncidentComment> Comments { get; set; } = [];
}

public sealed class IncidentComment : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed class Vendor : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServiceProvided { get; set; } = string.Empty;
    public CriticalityLevel Criticality { get; set; }
    public DateTime ContractStartDateUtc { get; set; }
    public DateTime ContractExpiryDateUtc { get; set; }
    public ComplianceStatus ComplianceStatus { get; set; }
    public int SecurityRating { get; set; }
    public CriticalityLevel DependencyLevel { get; set; }
    public decimal RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public ICollection<VendorAssessment> Assessments { get; set; } = [];
}

public sealed class VendorAssessment : BaseEntity
{
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public decimal ContractExpiryRisk { get; set; }
    public decimal SecurityWeakness { get; set; }
    public decimal ComplianceWeakness { get; set; }
    public decimal SingleSupplierDependency { get; set; }
    public decimal ServiceReliabilityRisk { get; set; }
    public decimal DataAccessRisk { get; set; }
    public decimal OverallScore { get; set; }
}

public sealed class BusinessContinuityPlan : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public decimal ContinuityScore { get; set; }
    public RecordStatus Status { get; set; } = RecordStatus.Active;
    public ICollection<CriticalSystem> CriticalSystems { get; set; } = [];
}

public sealed class CriticalSystem : BaseEntity
{
    public Guid BusinessContinuityPlanId { get; set; }
    public BusinessContinuityPlan? Plan { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemOwner { get; set; } = string.Empty;
    public int RecoveryTimeObjectiveHours { get; set; }
    public int RecoveryPointObjectiveHours { get; set; }
    public string BackupFrequency { get; set; } = string.Empty;
    public DateTime? LastBackupTestDateUtc { get; set; }
    public DateTime? LastDisasterRecoveryTestDateUtc { get; set; }
    public string DowntimeImpact { get; set; } = string.Empty;
    public decimal ContinuityScore { get; set; }
    public string Status { get; set; } = "Ready";
}

public sealed class Report : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageUrl { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
}

public sealed class AuditLog : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Severity Severity { get; set; }
    public bool IsRead { get; set; }
    public string Link { get; set; } = string.Empty;
}

public sealed class AiInteraction : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string ResponseType { get; set; } = string.Empty;
    public bool UsedConfiguredProvider { get; set; }
}

public sealed class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
