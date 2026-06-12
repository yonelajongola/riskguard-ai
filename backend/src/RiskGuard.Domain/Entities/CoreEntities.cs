using RiskGuard.Domain.Common;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Domain.Entities;

public sealed class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string PrimaryContact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public RecordStatus Status { get; set; } = RecordStatus.Active;
    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<Assessment> Assessments { get; set; } = [];
    public ICollection<Vendor> Vendors { get; set; } = [];
}

public sealed class Department : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public string BusinessFunction { get; set; } = string.Empty;
    public CriticalityLevel Criticality { get; set; }
    public string RiskOwner { get; set; } = string.Empty;
    public RecordStatus Status { get; set; } = RecordStatus.Active;
}

public sealed class RiskCategory : BaseEntity
{
    public RiskCategoryType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<AssessmentQuestion> Questions { get; set; } = [];
}

public sealed class AssessmentQuestion : BaseEntity
{
    public Guid RiskCategoryId { get; set; }
    public RiskCategory? RiskCategory { get; set; }
    public string Text { get; set; } = string.Empty;
    public decimal Weight { get; set; } = 1;
    public AnswerType AnswerType { get; set; } = AnswerType.YesNo;
    public string ScoreMappingJson { get; set; } = "{\"Yes\":0,\"Partially\":50,\"No\":100,\"Not applicable\":0}";
    public string RecommendationText { get; set; } = string.Empty;
    public string ComplianceMappings { get; set; } = string.Empty;
    public Severity SeverityImpact { get; set; } = Severity.High;
    public bool IsActive { get; set; } = true;
}

public sealed class Assessment : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid RiskCategoryId { get; set; }
    public RiskCategory? RiskCategory { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedToName { get; set; } = string.Empty;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? SubmittedAtUtc { get; set; }
    public decimal Score { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public ICollection<AssessmentResponse> Responses { get; set; } = [];
    public ICollection<RiskItem> Risks { get; set; } = [];
}

public sealed class AssessmentResponse : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public Guid QuestionId { get; set; }
    public AssessmentQuestion? Question { get; set; }
    public string Answer { get; set; } = string.Empty;
    public decimal AnswerScore { get; set; }
    public string EvidenceUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class RiskItem : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public RiskCategoryType Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Impact { get; set; } = 1;
    public int Likelihood { get; set; } = 1;
    public decimal Score { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public decimal FinancialExposure { get; set; }
    public ICollection<Recommendation> Recommendations { get; set; } = [];
}

public sealed class RiskScore : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal CategoryScore { get; set; }
    public decimal DepartmentScore { get; set; }
    public decimal ComplianceReadinessScore { get; set; }
    public decimal BusinessContinuityScore { get; set; }
    public decimal VendorRiskScore { get; set; }
    public decimal CybersecurityPostureScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Recommendation : BaseEntity
{
    public Guid? RiskItemId { get; set; }
    public RiskItem? RiskItem { get; set; }
    public Guid? AssessmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskCategoryType Category { get; set; }
    public Severity Severity { get; set; }
    public Severity Priority { get; set; }
    public string SuggestedOwner { get; set; } = string.Empty;
    public DateTime DueDateUtc { get; set; }
    public string BusinessImpact { get; set; } = string.Empty;
    public string ComplianceMapping { get; set; } = string.Empty;
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Open;
}
