namespace RiskGuard.Domain.Enums;

public enum RiskCategoryType
{
    Cybersecurity,
    Operational,
    Financial,
    Compliance,
    Vendor,
    BusinessContinuity,
    DataPrivacy,
    Strategic
}

public enum RiskLevel { Low, Medium, High, Critical }
public enum AssessmentStatus { Draft, Assigned, InProgress, Submitted, Reviewed, Approved, Archived }
public enum AnswerType { YesNo, MultipleChoice, Numeric, RatingScale, Text, EvidenceUpload }
public enum RecommendationStatus { Open, InProgress, Completed, AcceptedRisk, Deferred }
public enum IncidentStatus { Detected, Assigned, Investigating, Mitigated, Resolved, Closed }
public enum IncidentCategory { Cybersecurity, Operational, Compliance, Financial, Vendor, Privacy, BusinessContinuity }
public enum Severity { Low, Medium, High, Critical }
public enum RecordStatus { Active, Inactive, Archived }
public enum CriticalityLevel { Low, Medium, High, Critical }
public enum ComplianceStatus { NotAssessed, NonCompliant, PartiallyCompliant, Compliant }
public enum NotificationType
{
    CriticalRiskAlert,
    AssessmentAssigned,
    AssessmentOverdue,
    IncidentAssigned,
    ComplianceDeadline,
    VendorContractExpiring,
    ReportGenerated,
    RecommendationOverdue
}
