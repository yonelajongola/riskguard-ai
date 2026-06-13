using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string FirstName, string LastName, string OrganizationName, string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UpdateProfileRequest(string FirstName, string LastName, string PhoneNumber);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);
public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles,
    Guid? OrganizationId,
    Guid? DepartmentId,
    bool IsActive);

public sealed record CreateOrganizationRequest(
    string Name,
    string Industry,
    string Country,
    int EmployeeCount,
    string RegistrationNumber,
    string PrimaryContact,
    string Email,
    string Phone,
    string Address);

public sealed record CreateDepartmentRequest(
    Guid OrganizationId,
    string Name,
    string ManagerName,
    int EmployeeCount,
    string BusinessFunction,
    CriticalityLevel Criticality,
    string RiskOwner);

public sealed record CreateAssessmentRequest(
    Guid OrganizationId,
    Guid? DepartmentId,
    Guid RiskCategoryId,
    string Title,
    string AssignedToUserId,
    string AssignedToName,
    DateTime DueDateUtc);

public sealed record AssessmentResponseInput(Guid QuestionId, string Answer, string? Notes);
public sealed record SaveAssessmentDraftRequest(IReadOnlyCollection<AssessmentResponseInput> Responses);
public sealed record SubmitAssessmentRequest(IReadOnlyCollection<AssessmentResponseInput> Responses);
public sealed record WeightedAnswer(decimal Score, decimal Weight, string Question, string Recommendation, string ComplianceMapping);
public sealed record RiskCalculationResult(decimal Score, RiskLevel Level, string Color);
public sealed record AssessmentResultDto(
    Guid AssessmentId,
    string Title,
    AssessmentStatus Status,
    decimal OverallRiskScore,
    decimal CategoryScore,
    RiskLevel RiskLevel,
    DateTime? SubmittedAtUtc,
    string Organization,
    string Department,
    string Category,
    Guid? RiskId,
    string? RiskTitle,
    IReadOnlyCollection<AssessmentAnswerResultDto> Answers,
    IReadOnlyCollection<AssessmentRecommendationResultDto> Recommendations,
    IReadOnlyCollection<AssessmentComplianceGapResultDto> ComplianceGaps);
public sealed record AssessmentAnswerResultDto(
    Guid QuestionId,
    string Question,
    string Answer,
    decimal AnswerScore,
    decimal Weight,
    string Notes,
    string ComplianceMappings);
public sealed record AssessmentRecommendationResultDto(
    Guid Id,
    string Title,
    string Description,
    Severity Priority,
    string SuggestedOwner,
    DateTime DueDateUtc,
    string ComplianceMapping,
    RecommendationStatus Status);
public sealed record AssessmentComplianceGapResultDto(
    Guid Id,
    string Framework,
    string Control,
    string Description,
    Severity Severity,
    string Recommendation,
    string Owner,
    DateTime DueDateUtc,
    string Status);

public sealed record CreateIncidentRequest(
    string Title,
    string Description,
    IncidentCategory Category,
    Severity Severity,
    string Owner,
    Guid? DepartmentId,
    Guid? RiskItemId,
    DateTime? DueDateUtc,
    string EvidenceNotes);

public sealed record IncidentStatusRequest(IncidentStatus Status);
public sealed record IncidentCommentRequest(string Comment);

public sealed record CreateVendorRequest(
    Guid OrganizationId,
    string Name,
    string ServiceProvided,
    CriticalityLevel Criticality,
    DateTime ContractStartDateUtc,
    DateTime ContractExpiryDateUtc,
    ComplianceStatus ComplianceStatus,
    int SecurityRating,
    CriticalityLevel DependencyLevel,
    string Owner,
    string Notes);

public sealed record VendorRiskInput(
    decimal ContractExpiryRisk,
    decimal SecurityWeakness,
    decimal ComplianceWeakness,
    decimal SingleSupplierDependency,
    decimal ServiceReliabilityRisk,
    decimal DataAccessRisk);

public sealed record CriticalSystemRequest(
    string Name,
    string SystemOwner,
    int RecoveryTimeObjectiveHours,
    int RecoveryPointObjectiveHours,
    string BackupFrequency,
    DateTime? LastBackupTestDateUtc,
    DateTime? LastDisasterRecoveryTestDateUtc,
    string DowntimeImpact,
    decimal ContinuityScore,
    string Status);

public sealed record RecoveryTestRequest(
    DateTime TestedAtUtc,
    decimal ContinuityScore,
    string Status,
    string Notes);

public sealed record AiChatRequest(
    string Prompt,
    string ResponseType = "Risk explanation",
    Guid? AssessmentId = null);
public sealed record AiRiskSummaryRequest(
    Guid? AssessmentId = null,
    string? Focus = null);
public sealed record AiRecommendationRequest(
    Guid? AssessmentId = null,
    string? Focus = null);
public sealed record AiMitigationPlanRequest(
    Guid? AssessmentId = null,
    string? Focus = null);
public sealed record AiComplianceSummaryRequest(
    Guid? AssessmentId = null,
    string? Framework = null);
public sealed record AiChatResponse(
    string Title,
    string Summary,
    IReadOnlyCollection<string> KeyFindings,
    IReadOnlyCollection<string> RecommendedActions,
    string RiskPriority,
    string BusinessImpact,
    IReadOnlyCollection<string> NextSteps,
    string ResponseType,
    bool IsMock,
    DateTime GeneratedAtUtc,
    AiRiskContextSummary Context);
public sealed record AiRiskContextSummary(
    decimal OverallRiskScore,
    RiskLevel RiskLevel,
    int CriticalRisks,
    int HighRisks,
    decimal ComplianceReadiness,
    int OpenComplianceGaps,
    int OpenIncidents,
    int HighRiskVendors,
    decimal BusinessContinuityScore,
    IReadOnlyCollection<CategoryScore> CategoryScores);
public sealed record AiRecentInsightDto(
    Guid Id,
    string Title,
    string Summary,
    string ResponseType,
    bool IsMock,
    DateTime GeneratedAtUtc);
public sealed record AiProviderStatus(bool IsConfigured, string Mode);
public sealed record AiGenerationRequest(
    string Prompt,
    string ResponseType,
    string PromptCategory,
    Guid? AssessmentId);
public sealed record AiRiskContext(
    string Organization,
    AiRiskContextSummary Summary,
    IReadOnlyCollection<AiRiskItemContext> TopRisks,
    IReadOnlyCollection<AiRecommendationContext> Recommendations,
    IReadOnlyCollection<AiComplianceGapContext> ComplianceGaps,
    IReadOnlyCollection<AiIncidentContext> Incidents,
    IReadOnlyCollection<AiVendorContext> Vendors,
    IReadOnlyCollection<AiContinuityContext> ContinuityFindings,
    AiAssessmentContext? Assessment);
public sealed record AiRiskItemContext(
    string Title,
    string Category,
    decimal Score,
    RiskLevel Level,
    string Department,
    string Owner);
public sealed record AiRecommendationContext(
    string Title,
    Severity Priority,
    string Owner,
    DateTime DueDateUtc,
    RecommendationStatus Status);
public sealed record AiComplianceGapContext(
    string Framework,
    string Control,
    string Description,
    Severity Severity,
    string Owner,
    string Status);
public sealed record AiIncidentContext(
    string Title,
    Severity Severity,
    IncidentStatus Status,
    string Owner);
public sealed record AiVendorContext(
    string Name,
    string Service,
    decimal RiskScore,
    RiskLevel RiskLevel,
    ComplianceStatus ComplianceStatus);
public sealed record AiContinuityContext(
    string Name,
    decimal ContinuityScore,
    string Status,
    int OverdueTests);
public sealed record AiAssessmentContext(
    Guid Id,
    string Title,
    string Category,
    string Department,
    AssessmentStatus Status,
    decimal Score,
    RiskLevel RiskLevel,
    IReadOnlyCollection<AiAssessmentAnswerContext> Answers);
public sealed record AiAssessmentAnswerContext(
    string Question,
    string Answer,
    decimal Score,
    decimal Weight);

public sealed record DashboardSummary(
    decimal OverallRiskScore,
    RiskLevel RiskLevel,
    int CriticalRisks,
    int HighRisks,
    decimal ComplianceReadiness,
    decimal BusinessContinuityScore,
    decimal FinancialExposure,
    decimal VendorRiskScore,
    IReadOnlyCollection<ChartPoint> Trend,
    IReadOnlyCollection<CategoryScore> Categories);

public sealed record ChartPoint(string Label, decimal Value);
public sealed record CategoryScore(string Category, decimal Score);
public sealed record HeatMapItem(Guid Id, string Title, int Impact, int Likelihood, RiskLevel Level, string Department);
public sealed record ComplianceDashboard(decimal Readiness, int Passed, int Failed, int Missing, IReadOnlyCollection<CategoryScore> Frameworks);
public sealed record ContinuityDashboard(decimal Readiness, int CriticalSystems, int TestsOverdue, decimal DowntimeExposure);
