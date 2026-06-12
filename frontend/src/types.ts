export type RiskLevel = "Low" | "Medium" | "High" | "Critical";

export interface User {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  organizationId?: string;
  departmentId?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: User;
}

export interface ChartPoint {
  label: string;
  value: number;
}

export interface CategoryScore {
  category: string;
  score: number;
}

export interface DashboardSummary {
  overallRiskScore: number;
  riskLevel: RiskLevel;
  criticalRisks: number;
  highRisks: number;
  complianceReadiness: number;
  businessContinuityScore: number;
  financialExposure: number;
  vendorRiskScore: number;
  trend: ChartPoint[];
  categories: CategoryScore[];
}

export interface Risk {
  id: string;
  title: string;
  category: string;
  score: number;
  riskLevel: RiskLevel;
  impact: number;
  likelihood: number;
  owner: string;
  status: string;
  financialExposure: number;
  department?: { name: string };
}

export interface HeatMapItem {
  id: string;
  title: string;
  impact: number;
  likelihood: number;
  level: RiskLevel;
  department: string;
}

export interface Assessment {
  id: string;
  title: string;
  status: string;
  score: number;
  riskLevel: RiskLevel;
  dueDateUtc: string;
  assignedToName: string;
  department?: { name: string };
  riskCategory?: { name: string; type: string };
  responses?: AssessmentResponse[];
}

export interface AssessmentResponse {
  id: string;
  questionId: string;
  answer: string;
  answerScore: number;
  notes: string;
}

export interface AssessmentResult {
  assessmentId: string;
  title: string;
  status: string;
  overallRiskScore: number;
  categoryScore: number;
  riskLevel: RiskLevel;
  submittedAtUtc?: string;
  organization: string;
  department: string;
  category: string;
  riskId?: string;
  riskTitle?: string;
  answers: AssessmentResultAnswer[];
  recommendations: AssessmentResultRecommendation[];
  complianceGaps: AssessmentResultComplianceGap[];
}

export interface AssessmentResultAnswer {
  questionId: string;
  question: string;
  answer: string;
  answerScore: number;
  weight: number;
  notes: string;
  complianceMappings: string;
}

export interface AssessmentResultRecommendation {
  id: string;
  title: string;
  description: string;
  priority: string;
  suggestedOwner: string;
  dueDateUtc: string;
  complianceMapping: string;
  status: string;
}

export interface AssessmentResultComplianceGap {
  id: string;
  framework: string;
  control: string;
  description: string;
  severity: string;
  recommendation: string;
  owner: string;
  dueDateUtc: string;
  status: string;
}

export interface AssessmentQuestion {
  id: string;
  text: string;
  weight: number;
  answerType: string;
  scoreMappingJson: string;
  recommendationText: string;
  complianceMappings: string;
  riskCategory?: RiskCategory;
}

export interface RiskCategory {
  id: string;
  name: string;
  type: string;
  description: string;
}

export interface Department {
  id: string;
  organizationId: string;
  name: string;
}

export interface UserSummary extends User {
  organizationId?: string;
  departmentId?: string;
}

export interface Recommendation {
  id: string;
  title: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  suggestedOwner: string;
  dueDateUtc: string;
  complianceMapping: string;
}

export interface Incident {
  id: string;
  title: string;
  category: string;
  severity: string;
  status: string;
  owner: string;
  detectedAtUtc: string;
  dueDateUtc?: string;
  department?: { name: string };
}

export interface Vendor {
  id: string;
  name: string;
  serviceProvided: string;
  criticality: string;
  complianceStatus: string;
  securityRating: number;
  riskScore: number;
  riskLevel: RiskLevel;
  owner: string;
  contractExpiryDateUtc: string;
}

export interface ComplianceDashboard {
  readiness: number;
  passed: number;
  failed: number;
  missing: number;
  frameworks: CategoryScore[];
}

export interface ComplianceGap {
  id: string;
  description: string;
  severity: string;
  recommendation: string;
  owner: string;
  dueDateUtc: string;
  status: string;
  control?: { code: string; title: string; framework?: { name: string } };
}

export interface ContinuityPlan {
  id: string;
  name: string;
  owner: string;
  continuityScore: number;
  status: string;
  criticalSystems: CriticalSystem[];
}

export interface CriticalSystem {
  id: string;
  name: string;
  systemOwner: string;
  recoveryTimeObjectiveHours: number;
  recoveryPointObjectiveHours: number;
  backupFrequency: string;
  lastBackupTestDateUtc?: string;
  lastDisasterRecoveryTestDateUtc?: string;
  continuityScore: number;
  status: string;
}

export interface AuditLog {
  id: string;
  userEmail: string;
  action: string;
  entityType: string;
  description: string;
  ipAddress: string;
  createdAtUtc: string;
}

export interface Notification {
  id: string;
  title: string;
  message: string;
  severity: string;
  isRead: boolean;
  link: string;
  createdAtUtc: string;
}

export interface AiRiskContextSummary {
  overallRiskScore: number;
  riskLevel: RiskLevel;
  criticalRisks: number;
  highRisks: number;
  complianceReadiness: number;
  openComplianceGaps: number;
  openIncidents: number;
  highRiskVendors: number;
  businessContinuityScore: number;
  categoryScores: CategoryScore[];
}

export interface AiChatResponse {
  title: string;
  summary: string;
  keyFindings: string[];
  recommendedActions: string[];
  riskPriority: string;
  businessImpact: string;
  nextSteps: string[];
  responseType: string;
  isMock: boolean;
  generatedAtUtc: string;
  context: AiRiskContextSummary;
}

export interface AiRecentInsight {
  id: string;
  title: string;
  summary: string;
  responseType: string;
  isMock: boolean;
  generatedAtUtc: string;
}

export interface AiProviderStatus {
  isConfigured: boolean;
  mode: string;
}
