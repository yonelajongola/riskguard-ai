using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Interfaces;

public interface IRiskScoringService
{
    RiskCalculationResult CalculateOverallRisk(IEnumerable<WeightedAnswer> answers);
    decimal CalculateCategoryRisk(IEnumerable<WeightedAnswer> answers);
    RiskLevel GetRiskLevel(decimal score);
    string GetRiskColor(decimal score);
    decimal CalculateTrend(decimal currentScore, decimal previousScore);
}

public interface IAnswerScoringService
{
    bool TryCalculate(AssessmentQuestion question, string answer, out decimal score);
}

public interface IRecommendationEngine
{
    IReadOnlyCollection<Recommendation> GenerateRecommendations(
        Guid assessmentId,
        RiskCategoryType category,
        IEnumerable<WeightedAnswer> answers,
        string suggestedOwner);
}

public interface IVendorRiskService
{
    RiskCalculationResult Calculate(VendorRiskInput input);
}

public interface IIncidentWorkflowService
{
    bool CanTransition(IncidentStatus current, IncidentStatus next);
    void Transition(Incident incident, IncidentStatus next);
}

public interface IComplianceGapFactory
{
    ComplianceGap Create(
        Guid controlId,
        Guid? riskItemId,
        string description,
        Severity severity,
        string recommendation,
        string owner,
        DateTime dueDateUtc);
}

public interface ITokenService
{
    string CreateAccessToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles,
        Guid? organizationId,
        Guid? departmentId,
        DateTime expiresAtUtc);
    string CreateRefreshToken();
    string HashToken(string token);
}

public interface IAiRiskService
{
    bool IsConfigured { get; }
    Task<AiChatResponse> GenerateAsync(
        AiGenerationRequest request,
        AiRiskContext context,
        CancellationToken cancellationToken);
}

public interface IReportService
{
    byte[] GenerateExecutivePdf(
        string companyName,
        string reportTitle,
        DashboardSummary summary,
        string preparedBy);
    byte[] GenerateAssessmentPdf(
        string companyName,
        Assessment assessment,
        IReadOnlyCollection<ComplianceGap> complianceGaps,
        string preparedBy);
    byte[] GenerateRiskRegisterExcel(IEnumerable<RiskItem> risks);
    byte[] GenerateCsv<T>(IEnumerable<T> records);
}

public interface IAssessmentRepository
{
    Task<Assessment> AddAsync(Assessment assessment, CancellationToken cancellationToken);
}

public interface IEmailService
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}
