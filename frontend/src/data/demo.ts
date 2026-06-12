import type {
  Assessment,
  AuditLog,
  ComplianceDashboard,
  ComplianceGap,
  ContinuityPlan,
  DashboardSummary,
  Incident,
  Notification,
  Recommendation,
  Risk,
  Vendor,
} from "../types";

const now = Date.now();
const date = (days: number) => new Date(now + days * 86_400_000).toISOString();

export const dashboardDemo: DashboardSummary = {
  overallRiskScore: 67,
  riskLevel: "High",
  criticalRisks: 1,
  highRisks: 4,
  complianceReadiness: 64,
  businessContinuityScore: 58,
  financialExposure: 770000,
  vendorRiskScore: 55,
  trend: [
    { label: "Jan", value: 74 },
    { label: "Feb", value: 72 },
    { label: "Mar", value: 70 },
    { label: "Apr", value: 69 },
    { label: "May", value: 66 },
    { label: "Jun", value: 67 },
  ],
  categories: [
    { category: "Cybersecurity", score: 78 },
    { category: "Operational", score: 52 },
    { category: "Financial", score: 41 },
    { category: "Compliance", score: 61 },
    { category: "Vendor", score: 66 },
    { category: "Business Continuity", score: 71 },
    { category: "Data Privacy", score: 58 },
    { category: "Strategic", score: 34 },
  ],
};

export const risksDemo: Risk[] = [
  { id: "r1", title: "Privileged accounts do not enforce MFA", category: "Cybersecurity", score: 82, riskLevel: "Critical", impact: 4, likelihood: 4, owner: "IT Manager", status: "Open", financialExposure: 185000, department: { name: "IT" } },
  { id: "r2", title: "Administrative access reviews are overdue", category: "Cybersecurity", score: 74, riskLevel: "High", impact: 4, likelihood: 3, owner: "Security Analyst", status: "Open", financialExposure: 95000, department: { name: "IT" } },
  { id: "r3", title: "Backup restoration test is overdue", category: "Business Continuity", score: 71, riskLevel: "High", impact: 4, likelihood: 3, owner: "Operations Manager", status: "Open", financialExposure: 240000, department: { name: "Operations" } },
  { id: "r4", title: "POPIA policy is not formally approved", category: "Compliance", score: 61, riskLevel: "High", impact: 3, likelihood: 3, owner: "Compliance Officer", status: "Open", financialExposure: 75000, department: { name: "Compliance" } },
  { id: "r5", title: "Delivery platform dependency lacks an exit plan", category: "Vendor", score: 66, riskLevel: "High", impact: 3, likelihood: 4, owner: "Delivery Manager", status: "Open", financialExposure: 130000, department: { name: "Delivery" } },
  { id: "r6", title: "Leaver access removal is not consistently evidenced", category: "Operational", score: 48, riskLevel: "Medium", impact: 3, likelihood: 2, owner: "HR Manager", status: "In Progress", financialExposure: 45000, department: { name: "HR" } },
];

export const assessmentsDemo: Assessment[] = [
  { id: "a1", title: "Q2 Cybersecurity Control Assessment", status: "Reviewed", score: 68, riskLevel: "High", dueDateUtc: date(7), assignedToName: "Naledi Khumalo", department: { name: "IT" }, riskCategory: { name: "Cybersecurity Risk", type: "Cybersecurity" } },
  { id: "a2", title: "POPIA Readiness Review", status: "InProgress", score: 0, riskLevel: "Low", dueDateUtc: date(12), assignedToName: "Ayesha Patel", department: { name: "Compliance" }, riskCategory: { name: "Compliance Risk", type: "Compliance" } },
  { id: "a3", title: "Critical Vendor Review", status: "Assigned", score: 0, riskLevel: "Low", dueDateUtc: date(18), assignedToName: "Thabo Nkosi", department: { name: "Finance" }, riskCategory: { name: "Vendor Risk", type: "Vendor" } },
  { id: "a4", title: "Kitchen Continuity Assessment", status: "Draft", score: 0, riskLevel: "Low", dueDateUtc: date(25), assignedToName: "Operations Manager", department: { name: "Kitchen" }, riskCategory: { name: "Business Continuity", type: "BusinessContinuity" } },
];

export const recommendationsDemo: Recommendation[] = [
  { id: "rec1", title: "Enforce MFA for privileged access", description: "Enable MFA for administrators and enforce conditional access.", category: "Cybersecurity", priority: "Critical", status: "Open", suggestedOwner: "IT Manager", dueDateUtc: date(14), complianceMapping: "ISO 27001 A.5.17; NIST PR.AA" },
  { id: "rec2", title: "Complete quarterly privileged access review", description: "Certify privileged membership and retain approval evidence.", category: "Cybersecurity", priority: "High", status: "InProgress", suggestedOwner: "Security Analyst", dueDateUtc: date(21), complianceMapping: "CIS 5; ISO 27001 A.5.18" },
  { id: "rec3", title: "Run a full backup restoration test", description: "Validate restoration against approved RTO and RPO targets.", category: "BusinessContinuity", priority: "High", status: "Open", suggestedOwner: "Operations Manager", dueDateUtc: date(14), complianceMapping: "ISO 27001 A.8.13" },
  { id: "rec4", title: "Approve and publish the POPIA policy", description: "Assign an owner, approve the policy, train staff, and review annually.", category: "Compliance", priority: "High", status: "Open", suggestedOwner: "Compliance Officer", dueDateUtc: date(30), complianceMapping: "POPIA Conditions 1-8" },
];

export const incidentsDemo: Incident[] = [
  { id: "i1", title: "Repeated failed login attempts", category: "Cybersecurity", severity: "High", status: "Investigating", owner: "Security Analyst", detectedAtUtc: date(-3), dueDateUtc: date(4), department: { name: "IT" } },
  { id: "i2", title: "Backup test overdue", category: "Business Continuity", severity: "High", status: "Assigned", owner: "Operations Manager", detectedAtUtc: date(-8), dueDateUtc: date(6), department: { name: "Operations" } },
  { id: "i3", title: "Vendor contract expiring", category: "Vendor", severity: "Medium", status: "Assigned", owner: "Finance Manager", detectedAtUtc: date(-5), dueDateUtc: date(12), department: { name: "Finance" } },
  { id: "i4", title: "POPIA policy missing", category: "Compliance", severity: "High", status: "Investigating", owner: "Compliance Officer", detectedAtUtc: date(-12), dueDateUtc: date(18), department: { name: "Compliance" } },
];

export const vendorsDemo: Vendor[] = [
  { id: "v1", name: "Azure Cloud Services", serviceProvided: "Cloud hosting and identity", criticality: "Critical", complianceStatus: "Compliant", securityRating: 69, riskScore: 31, riskLevel: "Medium", owner: "IT Manager", contractExpiryDateUtc: date(31) },
  { id: "v2", name: "Payment Gateway Provider", serviceProvided: "Card payment processing", criticality: "Critical", complianceStatus: "PartiallyCompliant", securityRating: 36, riskScore: 64, riskLevel: "High", owner: "Finance Manager", contractExpiryDateUtc: date(90) },
  { id: "v3", name: "Payroll Software Provider", serviceProvided: "Payroll processing", criticality: "High", complianceStatus: "Compliant", securityRating: 52, riskScore: 48, riskLevel: "Medium", owner: "HR Manager", contractExpiryDateUtc: date(180) },
  { id: "v4", name: "Internet Service Provider", serviceProvided: "Business connectivity", criticality: "Critical", complianceStatus: "PartiallyCompliant", securityRating: 42, riskScore: 58, riskLevel: "High", owner: "IT Manager", contractExpiryDateUtc: date(45) },
  { id: "v5", name: "Delivery Platform Partner", serviceProvided: "Online delivery marketplace", criticality: "High", complianceStatus: "PartiallyCompliant", securityRating: 28, riskScore: 72, riskLevel: "High", owner: "Delivery Manager", contractExpiryDateUtc: date(24) },
];

export const complianceDemo: ComplianceDashboard = {
  readiness: 64,
  passed: 19,
  failed: 6,
  missing: 5,
  frameworks: [
    { category: "POPIA", score: 58 },
    { category: "GDPR", score: 62 },
    { category: "ISO 27001", score: 67 },
    { category: "NIST CSF", score: 71 },
    { category: "CIS Controls", score: 63 },
  ],
};

export const gapsDemo: ComplianceGap[] = [
  { id: "g1", description: "POPIA governance policy is not formally approved.", severity: "High", recommendation: "Approve, publish, train, and review annually.", owner: "Compliance Officer", dueDateUtc: date(30), status: "Open", control: { code: "POPIA-7", title: "Security safeguards", framework: { name: "POPIA" } } },
  { id: "g2", description: "Privileged access review evidence is incomplete.", severity: "High", recommendation: "Complete quarterly access certification.", owner: "Security Analyst", dueDateUtc: date(21), status: "In Progress", control: { code: "ISO-A.5", title: "Organizational controls", framework: { name: "ISO 27001" } } },
  { id: "g3", description: "Critical supplier exit arrangements are not tested.", severity: "Medium", recommendation: "Document and exercise supplier exit plans.", owner: "Risk Manager", dueDateUtc: date(45), status: "Open", control: { code: "NIST-GV", title: "Govern", framework: { name: "NIST CSF" } } },
];

export const continuityDemo: ContinuityPlan[] = [{
  id: "bc1", name: "FoodieBar Business Continuity Plan", owner: "Operations Manager", continuityScore: 58, status: "Active",
  criticalSystems: [
    { id: "s1", name: "Point of Sale", systemOwner: "IT Manager", recoveryTimeObjectiveHours: 2, recoveryPointObjectiveHours: 1, backupFrequency: "Hourly", lastBackupTestDateUtc: date(-72), lastDisasterRecoveryTestDateUtc: date(-140), continuityScore: 62, status: "Needs attention" },
    { id: "s2", name: "Payment Gateway", systemOwner: "Finance Manager", recoveryTimeObjectiveHours: 1, recoveryPointObjectiveHours: 1, backupFrequency: "Provider managed", lastBackupTestDateUtc: date(-30), lastDisasterRecoveryTestDateUtc: date(-95), continuityScore: 71, status: "Ready" },
    { id: "s3", name: "Delivery Platform", systemOwner: "Delivery Manager", recoveryTimeObjectiveHours: 4, recoveryPointObjectiveHours: 2, backupFrequency: "Daily", lastBackupTestDateUtc: date(-45), lastDisasterRecoveryTestDateUtc: date(-190), continuityScore: 54, status: "Needs attention" },
  ],
}];

export const auditDemo: AuditLog[] = [
  { id: "l1", userEmail: "riskmanager@riskguard.local", action: "Risk score calculated", entityType: "Assessment", description: "Overall score calculated at 68.", ipAddress: "127.0.0.1", createdAtUtc: date(-0.1) },
  { id: "l2", userEmail: "security@riskguard.local", action: "Assessment submitted", entityType: "Assessment", description: "Q2 cybersecurity assessment submitted.", ipAddress: "127.0.0.1", createdAtUtc: date(-0.5) },
  { id: "l3", userEmail: "compliance@riskguard.local", action: "Compliance gap created", entityType: "ComplianceGap", description: "POPIA governance gap recorded.", ipAddress: "127.0.0.1", createdAtUtc: date(-1) },
  { id: "l4", userEmail: "admin@riskguard.local", action: "Vendor updated", entityType: "Vendor", description: "Vendor review schedule updated.", ipAddress: "127.0.0.1", createdAtUtc: date(-2) },
];

export const notificationsDemo: Notification[] = [
  { id: "n1", title: "Critical risk alert", message: "Privileged accounts do not enforce MFA.", severity: "Critical", isRead: false, link: "/app/risks", createdAtUtc: date(-0.1) },
  { id: "n2", title: "Assessment requires review", message: "Q2 cybersecurity assessment was submitted.", severity: "High", isRead: false, link: "/app/assessments", createdAtUtc: date(-0.4) },
  { id: "n3", title: "Compliance deadline", message: "POPIA policy remediation is due in 30 days.", severity: "High", isRead: true, link: "/app/compliance/gaps", createdAtUtc: date(-1) },
];
