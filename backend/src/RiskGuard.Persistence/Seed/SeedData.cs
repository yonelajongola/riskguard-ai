using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence.Identity;

namespace RiskGuard.Persistence.Seed;

public static class SeedData
{
    private static readonly string[] Roles =
    [
        "Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer",
        "Security Analyst", "Department Manager", "Employee"
    ];

    private static readonly (string Name, string Function, CriticalityLevel Criticality, int Employees)[] Departments =
    [
        ("IT", "Technology operations", CriticalityLevel.Critical, 3),
        ("Finance", "Financial control and reporting", CriticalityLevel.Critical, 3),
        ("HR", "People and workforce management", CriticalityLevel.High, 2),
        ("Operations", "Restaurant and service operations", CriticalityLevel.Critical, 5),
        ("Compliance", "Regulatory assurance", CriticalityLevel.High, 1),
        ("Customer Service", "Customer experience", CriticalityLevel.Medium, 3),
        ("Delivery", "Order fulfilment and logistics", CriticalityLevel.High, 4),
        ("Kitchen", "Food production and safety", CriticalityLevel.Critical, 4)
    ];

    public static async Task InitializeAsync(
        RiskGuardDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        await db.Database.MigrateAsync();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureIdentitySucceeded(
                    await roleManager.CreateAsync(new IdentityRole<Guid>(role)),
                    $"create role '{role}'");
            }
        }

        var organization = await EnsureBusinessDataAsync(db);
        var users = await EnsureUsersAsync(db, userManager, organization);
        await EnsureLinkedSeedDataAsync(db, organization, users);
        await EnsureReferenceDataAsync(db);
    }

    private static async Task EnsureReferenceDataAsync(RiskGuardDbContext db)
    {
        var categories = await db.RiskCategories.ToDictionaryAsync(x => x.Type);
        foreach (var type in Enum.GetValues<RiskCategoryType>())
        {
            if (!categories.TryGetValue(type, out var category))
            {
                category = new RiskCategory { Type = type };
                db.RiskCategories.Add(category);
                categories[type] = category;
            }

            category.Name = FormatName(type.ToString());
            category.Description =
                $"Assessment questions and controls for {FormatName(type.ToString()).ToLowerInvariant()}.";
        }
        await db.SaveChangesAsync();

        var existingQuestions = (await db.AssessmentQuestions.AsNoTracking()
                .Select(x => new { x.RiskCategoryId, x.Text })
                .ToListAsync())
            .Select(x => (x.RiskCategoryId, x.Text))
            .ToHashSet();
        foreach (var question in QuestionSeed())
        {
            var category = categories[question.Category];
            if (existingQuestions.Contains((category.Id, question.Text)))
            {
                continue;
            }

            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                RiskCategoryId = category.Id,
                Text = question.Text,
                Weight = question.Weight,
                AnswerType = AnswerType.YesNo,
                RecommendationText = question.Recommendation,
                ComplianceMappings = question.Mapping,
                SeverityImpact = question.Weight >= 2 ? Severity.Critical : Severity.High
            });
        }

        var existingFrameworks = await db.ComplianceFrameworks
            .Include(x => x.Controls)
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var template in FrameworkSeed())
        {
            if (!existingFrameworks.TryGetValue(template.Name, out var framework))
            {
                db.ComplianceFrameworks.Add(template);
                continue;
            }

            framework.Version = template.Version;
            framework.Description = template.Description;
            var controls = framework.Controls.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
            foreach (var templateControl in template.Controls)
            {
                if (!controls.TryGetValue(templateControl.Code, out var control))
                {
                    framework.Controls.Add(new ComplianceControl
                    {
                        Code = templateControl.Code,
                        Title = templateControl.Title,
                        Description = templateControl.Description
                    });
                    continue;
                }

                control.Title = templateControl.Title;
                control.Description = templateControl.Description;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Organization> EnsureBusinessDataAsync(RiskGuardDbContext db)
    {
        var existing = await db.Organizations.FirstOrDefaultAsync(x => x.Name == "FoodieBar");
        if (existing is not null)
        {
            await EnsureEssentialBusinessDataAsync(db, existing);
            return existing;
        }

        var organization = new Organization
        {
            Name = "FoodieBar",
            Industry = "Restaurant / Retail Operations",
            Country = "South Africa",
            EmployeeCount = 25,
            RegistrationNumber = "2022/458921/07",
            PrimaryContact = "Anele Dlamini",
            Email = "risk@foodiebar.co.za",
            Phone = "+27 11 555 0182",
            Address = "14 Market Street, Johannesburg, Gauteng"
        };
        db.Organizations.Add(organization);

        var departments = Departments.Select(x => new Department
        {
            Organization = organization,
            Name = x.Item1,
            BusinessFunction = x.Item2,
            Criticality = x.Item3,
            EmployeeCount = x.Item4,
            ManagerName = $"{x.Item1} Manager",
            RiskOwner = $"{x.Item1} Manager"
        }).ToArray();
        db.Departments.AddRange(departments);

        var categories = Enum.GetValues<RiskCategoryType>().Select(type => new RiskCategory
        {
            Type = type,
            Name = FormatName(type.ToString()),
            Description = $"Assessment questions and controls for {FormatName(type.ToString()).ToLowerInvariant()}."
        }).ToDictionary(x => x.Type);
        db.RiskCategories.AddRange(categories.Values);

        foreach (var question in QuestionSeed())
        {
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                RiskCategory = categories[question.Category],
                Text = question.Text,
                Weight = question.Weight,
                AnswerType = AnswerType.YesNo,
                RecommendationText = question.Recommendation,
                ComplianceMappings = question.Mapping,
                SeverityImpact = question.Weight >= 2 ? Severity.Critical : Severity.High
            });
        }

        var cybersecurity = categories[RiskCategoryType.Cybersecurity];
        var assessment = new Assessment
        {
            Organization = organization,
            Department = departments.First(x => x.Name == "IT"),
            RiskCategory = cybersecurity,
            Title = "Q2 Cybersecurity Control Assessment",
            AssignedToName = "Security Analyst",
            AssignedToUserId = string.Empty,
            Status = AssessmentStatus.Reviewed,
            DueDateUtc = DateTime.UtcNow.AddDays(7),
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-3),
            Score = 68,
            RiskLevel = RiskLevel.High
        };
        db.Assessments.Add(assessment);

        var risks = new[]
        {
            NewRisk(assessment, departments[0], RiskCategoryType.Cybersecurity, "Privileged accounts do not enforce MFA", 4, 4, 82, 185000),
            NewRisk(assessment, departments[0], RiskCategoryType.Cybersecurity, "Administrative access reviews are overdue", 4, 3, 74, 95000),
            NewRisk(assessment, departments[3], RiskCategoryType.BusinessContinuity, "Backup restoration test is overdue", 4, 3, 71, 240000),
            NewRisk(assessment, departments[1], RiskCategoryType.Compliance, "POPIA policy is not formally approved", 3, 3, 61, 75000),
            NewRisk(assessment, departments[6], RiskCategoryType.Vendor, "Delivery platform dependency lacks an exit plan", 3, 4, 66, 130000),
            NewRisk(assessment, departments[2], RiskCategoryType.Operational, "Leaver access removal is not consistently evidenced", 3, 2, 48, 45000)
        };
        db.Risks.AddRange(risks);

        db.RiskScores.Add(new RiskScore
        {
            AssessmentId = assessment.Id,
            OverallScore = 68,
            CategoryScore = 68,
            DepartmentScore = 72,
            ComplianceReadinessScore = 64,
            BusinessContinuityScore = 58,
            VendorRiskScore = 55,
            CybersecurityPostureScore = 32,
            RiskLevel = RiskLevel.High
        });

        db.Recommendations.AddRange(
            RecommendationFor(risks[0], "Enforce MFA for privileged access", Severity.Critical, "IT Manager", 14, "ISO 27001 A.5.17; NIST PR.AA"),
            RecommendationFor(risks[1], "Complete quarterly privileged access review", Severity.High, "Security Analyst", 21, "CIS 5; ISO 27001 A.5.18"),
            RecommendationFor(risks[2], "Run and evidence a full backup restoration test", Severity.High, "Operations Manager", 14, "ISO 27001 A.8.13"),
            RecommendationFor(risks[3], "Approve and publish the POPIA governance policy", Severity.High, "Compliance Officer", 30, "POPIA Conditions 1-8"),
            RecommendationFor(risks[4], "Create a tested delivery platform exit plan", Severity.High, "Delivery Manager", 30, "NIST GV.SC"));

        var frameworks = FrameworkSeed();
        db.ComplianceFrameworks.AddRange(frameworks);

        var popiaSecurity = frameworks.First(x => x.Name == "POPIA").Controls.First(x => x.Code == "POPIA-7");
        db.ComplianceGaps.Add(new ComplianceGap
        {
            Control = popiaSecurity,
            RelatedRisk = risks[3],
            Description = "The organization has no approved POPIA policy and evidence owner.",
            Severity = Severity.High,
            Recommendation = "Approve the policy, publish it, train staff, and schedule an annual review.",
            Owner = "Compliance Officer",
            DueDateUtc = DateTime.UtcNow.AddDays(30)
        });

        db.Incidents.AddRange(
            NewIncident("Repeated failed login attempts", IncidentCategory.Cybersecurity, Severity.High, IncidentStatus.Investigating, "Security Analyst", departments[0], risks[0]),
            NewIncident("Backup test overdue", IncidentCategory.BusinessContinuity, Severity.High, IncidentStatus.Assigned, "Operations Manager", departments[3], risks[2]),
            NewIncident("Vendor contract expiring", IncidentCategory.Vendor, Severity.Medium, IncidentStatus.Assigned, "Finance Manager", departments[1], risks[4]),
            NewIncident("POPIA policy missing", IncidentCategory.Compliance, Severity.High, IncidentStatus.Investigating, "Compliance Officer", departments[4], risks[3]),
            NewIncident("Admin access not reviewed", IncidentCategory.Cybersecurity, Severity.High, IncidentStatus.Mitigated, "IT Manager", departments[0], risks[1]));

        db.Vendors.AddRange(
            NewVendor(organization, "Azure Cloud Services", "Cloud hosting and identity", CriticalityLevel.Critical, 31, 78, ComplianceStatus.Compliant, "IT Manager"),
            NewVendor(organization, "Payment Gateway Provider", "Card payment processing", CriticalityLevel.Critical, 90, 64, ComplianceStatus.PartiallyCompliant, "Finance Manager"),
            NewVendor(organization, "Payroll Software Provider", "Payroll processing", CriticalityLevel.High, 180, 48, ComplianceStatus.Compliant, "HR Manager"),
            NewVendor(organization, "Internet Service Provider", "Business connectivity", CriticalityLevel.Critical, 45, 58, ComplianceStatus.PartiallyCompliant, "IT Manager"),
            NewVendor(organization, "Delivery Platform Partner", "Online delivery marketplace", CriticalityLevel.High, 24, 72, ComplianceStatus.PartiallyCompliant, "Delivery Manager"));

        var continuity = new BusinessContinuityPlan
        {
            OrganizationId = organization.Id,
            Name = "FoodieBar Business Continuity Plan",
            Owner = "Operations Manager",
            ContinuityScore = 58,
            Status = RecordStatus.Active
        };
        foreach (var system in new[]
                 {
                     NewSystem("Point of Sale", "IT Manager", 2, 1, "Hourly", 72, 140, 62, "Revenue loss and order disruption"),
                     NewSystem("Payment Gateway", "Finance Manager", 1, 1, "Provider managed", 30, 95, 71, "Unable to process card payments"),
                     NewSystem("Delivery Platform", "Delivery Manager", 4, 2, "Daily", 45, 190, 54, "Loss of delivery revenue"),
                     NewSystem("Payroll", "HR Manager", 24, 8, "Daily", 12, 280, 68, "Delayed employee payments")
                 })
        {
            continuity.CriticalSystems.Add(system);
        }
        db.BusinessContinuityPlans.Add(continuity);

        db.AuditLogs.AddRange(
            Audit("admin@riskguard.local", "Assessment created", "Assessment", assessment.Id, "Q2 cybersecurity assessment created."),
            Audit("security@riskguard.local", "Assessment submitted", "Assessment", assessment.Id, "Assessment submitted with evidence."),
            Audit("riskmanager@riskguard.local", "Risk score calculated", "RiskScore", assessment.Id, "Overall risk score calculated at 68."),
            Audit("compliance@riskguard.local", "Compliance gap created", "ComplianceGap", popiaSecurity.Id, "POPIA governance gap recorded."),
            Audit("admin@riskguard.local", "Vendor updated", "Vendor", Guid.NewGuid(), "Vendor review schedule updated."));

        await db.SaveChangesAsync();
        return organization;
    }

    private static async Task EnsureEssentialBusinessDataAsync(
        RiskGuardDbContext db,
        Organization organization)
    {
        var existingDepartments = (await db.Departments
                .Where(x => x.OrganizationId == organization.Id)
                .ToListAsync())
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var seed in Departments)
        {
            if (existingDepartments.ContainsKey(seed.Name))
            {
                continue;
            }

            var department = new Department
            {
                OrganizationId = organization.Id,
                Name = seed.Name,
                BusinessFunction = seed.Function,
                Criticality = seed.Criticality,
                EmployeeCount = seed.Employees,
                ManagerName = $"{seed.Name} Manager",
                RiskOwner = $"{seed.Name} Manager"
            };
            db.Departments.Add(department);
            existingDepartments[seed.Name] = department;
        }

        var categories = await db.RiskCategories.ToDictionaryAsync(x => x.Type);
        foreach (var type in Enum.GetValues<RiskCategoryType>())
        {
            if (!categories.TryGetValue(type, out var category))
            {
                category = new RiskCategory { Type = type };
                db.RiskCategories.Add(category);
                categories[type] = category;
            }

            category.Name = FormatName(type.ToString());
            category.Description =
                $"Assessment questions and controls for {FormatName(type.ToString()).ToLowerInvariant()}.";
        }
        await db.SaveChangesAsync();

        var existingQuestions = (await db.AssessmentQuestions.AsNoTracking()
                .Select(x => new { x.RiskCategoryId, x.Text })
                .ToListAsync())
            .Select(x => (x.RiskCategoryId, x.Text))
            .ToHashSet();
        foreach (var question in QuestionSeed())
        {
            var category = categories[question.Category];
            if (existingQuestions.Contains((category.Id, question.Text)))
            {
                continue;
            }

            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                RiskCategoryId = category.Id,
                Text = question.Text,
                Weight = question.Weight,
                AnswerType = AnswerType.YesNo,
                RecommendationText = question.Recommendation,
                ComplianceMappings = question.Mapping,
                SeverityImpact = question.Weight >= 2 ? Severity.Critical : Severity.High
            });
        }
        await db.SaveChangesAsync();

        if (!await db.Assessments.AnyAsync(x =>
                x.OrganizationId == organization.Id &&
                x.Title == "Q2 Cybersecurity Control Assessment"))
        {
            db.Assessments.Add(new Assessment
            {
                OrganizationId = organization.Id,
                DepartmentId = existingDepartments["IT"].Id,
                RiskCategoryId = categories[RiskCategoryType.Cybersecurity].Id,
                Title = "Q2 Cybersecurity Control Assessment",
                AssignedToName = "Security Analyst",
                AssignedToUserId = string.Empty,
                Status = AssessmentStatus.Reviewed,
                DueDateUtc = DateTime.UtcNow.AddDays(7),
                SubmittedAtUtc = DateTime.UtcNow.AddDays(-3),
                Score = 68,
                RiskLevel = RiskLevel.High
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, ApplicationUser>> EnsureUsersAsync(
        RiskGuardDbContext db,
        UserManager<ApplicationUser> userManager,
        Organization organization)
    {
        var departments = await db.Departments
            .Where(x => x.OrganizationId == organization.Id)
            .ToDictionaryAsync(x => x.Name, x => x.Id);
        var users = new[]
        {
            ("admin@riskguard.local", "System", "Administrator", "Admin", "Admin@12345", (string?)null),
            ("executive@riskguard.local", "Lerato", "Mokoena", "Executive", "Executive@12345", (string?)null),
            ("riskmanager@riskguard.local", "Thabo", "Nkosi", "Risk Manager", "Risk@12345", "Compliance"),
            ("security@riskguard.local", "Naledi", "Khumalo", "Security Analyst", "Security@12345", "IT"),
            ("compliance@riskguard.local", "Ayesha", "Patel", "Compliance Officer", "Compliance@12345", "Compliance"),
            ("auditor@riskguard.local", "Mia", "Naidoo", "Auditor", "Auditor@12345", "Compliance"),
            ("manager@riskguard.local", "Zanele", "Mthembu", "Department Manager", "Manager@12345", "Operations"),
            ("employee@riskguard.local", "Sibusiso", "Ndlovu", "Employee", "Employee@12345", "Operations")
        };

        var resultUsers = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in users)
        {
            var user = await userManager.FindByEmailAsync(entry.Item1);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = entry.Item1,
                    Email = entry.Item1,
                    EmailConfirmed = true,
                    FirstName = entry.Item2,
                    LastName = entry.Item3,
                    OrganizationId = organization.Id,
                    DepartmentId = entry.Item6 is not null && departments.TryGetValue(entry.Item6, out var departmentId)
                        ? departmentId
                        : null
                };
                var result = await userManager.CreateAsync(user, entry.Item5);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(user, entry.Item4))
            {
                EnsureIdentitySucceeded(
                    await userManager.AddToRoleAsync(user, entry.Item4),
                    $"assign role '{entry.Item4}' to '{entry.Item1}'");
            }
            var expectedDepartmentId = entry.Item6 is not null && departments.TryGetValue(entry.Item6, out var existingDepartmentId)
                ? existingDepartmentId
                : (Guid?)null;
            if (user.OrganizationId != organization.Id || user.DepartmentId != expectedDepartmentId || !user.IsActive)
            {
                user.OrganizationId = organization.Id;
                user.DepartmentId = expectedDepartmentId;
                user.IsActive = true;
                EnsureIdentitySucceeded(
                    await userManager.UpdateAsync(user),
                    $"update demo user '{entry.Item1}'");
            }
            resultUsers[entry.Item1] = user;
        }
        return resultUsers;
    }

    private static async Task EnsureLinkedSeedDataAsync(
        RiskGuardDbContext db,
        Organization organization,
        IReadOnlyDictionary<string, ApplicationUser> users)
    {
        var securityUser = users["security@riskguard.local"];
        var riskManager = users["riskmanager@riskguard.local"];
        var complianceUser = users["compliance@riskguard.local"];
        var adminUser = users["admin@riskguard.local"];

        var assessment = await db.Assessments
            .Include(x => x.Responses)
            .FirstAsync(x => x.OrganizationId == organization.Id &&
                x.Title == "Q2 Cybersecurity Control Assessment");
        assessment.AssignedToUserId = securityUser.Id.ToString();
        assessment.AssignedToName = securityUser.FullName;

        if (assessment.Responses.Count == 0)
        {
            var questions = await db.AssessmentQuestions
                .Where(x => x.RiskCategoryId == assessment.RiskCategoryId && x.IsActive)
                .OrderBy(x => x.Text)
                .ToListAsync();
            var answers = new[] { "No", "Partially", "No", "Yes", "Partially", "Partially", "No", "Partially", "Partially", "No" };
            foreach (var item in questions.Select((question, index) => new { question, index }))
            {
                var answer = answers[item.index % answers.Length];
                db.AssessmentResponses.Add(new AssessmentResponse
                {
                    AssessmentId = assessment.Id,
                    QuestionId = item.question.Id,
                    Answer = answer,
                    AnswerScore = answer == "No" ? 100 : answer == "Partially" ? 50 : 0,
                    Notes = answer == "Yes"
                        ? "Control implementation was evidenced."
                        : "Remediation evidence is required."
                });
            }
        }

        var departments = await db.Departments
            .Where(x => x.OrganizationId == organization.Id)
            .ToDictionaryAsync(x => x.Name);
        var categories = await db.RiskCategories.ToDictionaryAsync(x => x.Type);
        var additionalAssessments = new[]
        {
            ("POPIA Readiness Review", "Compliance", RiskCategoryType.Compliance, complianceUser, AssessmentStatus.InProgress, 12),
            ("Critical Vendor Review", "Finance", RiskCategoryType.Vendor, riskManager, AssessmentStatus.Assigned, 18),
            ("Kitchen Continuity Assessment", "Kitchen", RiskCategoryType.BusinessContinuity, users["manager@riskguard.local"], AssessmentStatus.Draft, 25)
        };
        foreach (var item in additionalAssessments)
        {
            if (!await db.Assessments.AnyAsync(x => x.OrganizationId == organization.Id && x.Title == item.Item1))
            {
                db.Assessments.Add(new Assessment
                {
                    OrganizationId = organization.Id,
                    DepartmentId = departments[item.Item2].Id,
                    RiskCategoryId = categories[item.Item3].Id,
                    Title = item.Item1,
                    AssignedToUserId = item.Item4.Id.ToString(),
                    AssignedToName = item.Item4.FullName,
                    Status = item.Item5,
                    DueDateUtc = DateTime.UtcNow.Date.AddDays(item.Item6)
                });
            }
        }

        var scoreHistory = await db.RiskScores
            .Where(x => x.AssessmentId == assessment.Id)
            .OrderBy(x => x.CalculatedAtUtc)
            .ToListAsync();
        if (scoreHistory.Count < 6)
        {
            var values = new[] { 74m, 72m, 70m, 69m, 66m, 68m };
            foreach (var item in values.Select((value, index) => new { value, index }).Take(6 - scoreHistory.Count))
            {
                db.RiskScores.Add(new RiskScore
                {
                    AssessmentId = assessment.Id,
                    OverallScore = item.value,
                    CategoryScore = item.value,
                    DepartmentScore = item.value,
                    ComplianceReadinessScore = 100 - item.value,
                    CybersecurityPostureScore = 100 - item.value,
                    RiskLevel = item.value > 75 ? RiskLevel.Critical : item.value > 50 ? RiskLevel.High : RiskLevel.Medium,
                    CalculatedAtUtc = DateTime.UtcNow.Date.AddMonths(item.index - 5)
                });
            }
        }

        var vendors = await db.Vendors.Where(x => x.OrganizationId == organization.Id).ToListAsync();
        foreach (var vendor in vendors)
        {
            if (!await db.VendorAssessments.AnyAsync(x => x.VendorId == vendor.Id))
            {
                db.VendorAssessments.Add(new VendorAssessment
                {
                    VendorId = vendor.Id,
                    ContractExpiryRisk = vendor.ContractExpiryDateUtc < DateTime.UtcNow.AddDays(60) ? 80 : 30,
                    SecurityWeakness = 100 - vendor.SecurityRating,
                    ComplianceWeakness = vendor.ComplianceStatus == ComplianceStatus.Compliant ? 20 : 65,
                    SingleSupplierDependency = vendor.DependencyLevel == CriticalityLevel.Critical ? 85 : 55,
                    ServiceReliabilityRisk = 35,
                    DataAccessRisk = vendor.Criticality == CriticalityLevel.Critical ? 75 : 45,
                    OverallScore = vendor.RiskScore
                });
            }
        }

        if (!await db.Reports.AnyAsync())
        {
            db.Reports.AddRange(
                new Report { Title = "Executive Risk Report", Type = "PDF", FileName = "RiskGuard-Executive-Sample.pdf", PreparedBy = riskManager.FullName },
                new Report { Title = "Risk Register", Type = "Excel", FileName = "RiskGuard-Risk-Register-Sample.xlsx", PreparedBy = riskManager.FullName });
        }
        if (!await db.AiInteractions.AnyAsync())
        {
            db.AiInteractions.Add(new AiInteraction
            {
                UserId = riskManager.Id.ToString(),
                Prompt = "Summarize the highest priority enterprise risks.",
                Response = "Prioritize privileged access, backup restoration, POPIA governance, and vendor exit planning.",
                ResponseType = "Executive summary",
                UsedConfiguredProvider = false
            });
        }

        await EnsureNoticeAsync(db, adminUser.Id, "Critical risk alert", "Privileged accounts do not enforce MFA.",
            NotificationType.CriticalRiskAlert, Severity.Critical, "/app/risks");
        await EnsureNoticeAsync(db, securityUser.Id, "Assessment requires review", "Q2 cybersecurity assessment was submitted.",
            NotificationType.AssessmentAssigned, Severity.High, "/app/assessments");
        await EnsureNoticeAsync(db, complianceUser.Id, "Compliance deadline", "POPIA policy remediation is due in 30 days.",
            NotificationType.ComplianceDeadline, Severity.High, "/app/compliance/gaps");
        await EnsureNoticeAsync(db, adminUser.Id, "Vendor contract expiring", "Delivery Platform Partner expires soon.",
            NotificationType.VendorContractExpiring, Severity.Medium, "/app/vendors");

        await db.SaveChangesAsync();
    }

    private static async Task EnsureNoticeAsync(
        RiskGuardDbContext db,
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Severity severity,
        string link)
    {
        var id = userId.ToString();
        if (!await db.Notifications.AnyAsync(x => x.UserId == id && x.Title == title))
        {
            db.Notifications.Add(Notice(id, title, message, type, severity, link));
        }
    }

    private static RiskItem NewRisk(
        Assessment assessment, Department department, RiskCategoryType category, string title,
        int impact, int likelihood, decimal score, decimal exposure) => new()
    {
        Assessment = assessment,
        Department = department,
        Category = category,
        Title = title,
        Description = $"Control weakness identified during {assessment.Title}.",
        Impact = impact,
        Likelihood = likelihood,
        Score = score,
        RiskLevel = score > 75 ? RiskLevel.Critical : score > 50 ? RiskLevel.High : RiskLevel.Medium,
        Owner = department.RiskOwner,
        FinancialExposure = exposure
    };

    private static Recommendation RecommendationFor(
        RiskItem risk, string title, Severity severity, string owner, int dueDays, string mapping) => new()
    {
        RiskItem = risk,
        AssessmentId = risk.AssessmentId,
        Title = title,
        Description = $"{title}. Document implementation, test effectiveness, and retain evidence.",
        Category = risk.Category,
        Severity = severity,
        Priority = severity,
        SuggestedOwner = owner,
        DueDateUtc = DateTime.UtcNow.AddDays(dueDays),
        BusinessImpact = "Reduces the likelihood and business impact of the related risk.",
        ComplianceMapping = mapping
    };

    private static Incident NewIncident(
        string title, IncidentCategory category, Severity severity, IncidentStatus status,
        string owner, Department department, RiskItem risk) => new()
    {
        Title = title,
        Description = $"{title} requires investigation, documented action, and evidence of closure.",
        Category = category,
        Severity = severity,
        Status = status,
        Owner = owner,
        Department = department,
        RelatedRisk = risk,
        DetectedAtUtc = DateTime.UtcNow.AddDays(-Random.Shared.Next(2, 20)),
        DueDateUtc = DateTime.UtcNow.AddDays(Random.Shared.Next(3, 25)),
        EvidenceNotes = "Evidence collection in progress."
    };

    private static Vendor NewVendor(
        Organization organization, string name, string service, CriticalityLevel criticality,
        int expiresInDays, decimal score, ComplianceStatus compliance, string owner) => new()
    {
        Organization = organization,
        Name = name,
        ServiceProvided = service,
        Criticality = criticality,
        ContractStartDateUtc = DateTime.UtcNow.AddYears(-1),
        ContractExpiryDateUtc = DateTime.UtcNow.AddDays(expiresInDays),
        ComplianceStatus = compliance,
        SecurityRating = (int)(100 - score),
        DependencyLevel = criticality,
        RiskScore = score,
        RiskLevel = score > 75 ? RiskLevel.Critical : score > 50 ? RiskLevel.High : score > 25 ? RiskLevel.Medium : RiskLevel.Low,
        Owner = owner,
        Notes = "Annual assurance review required."
    };

    private static CriticalSystem NewSystem(
        string name, string owner, int rto, int rpo, string backupFrequency,
        int backupDays, int drDays, decimal score, string impact) => new()
    {
        Name = name,
        SystemOwner = owner,
        RecoveryTimeObjectiveHours = rto,
        RecoveryPointObjectiveHours = rpo,
        BackupFrequency = backupFrequency,
        LastBackupTestDateUtc = DateTime.UtcNow.AddDays(-backupDays),
        LastDisasterRecoveryTestDateUtc = DateTime.UtcNow.AddDays(-drDays),
        ContinuityScore = score,
        DowntimeImpact = impact,
        Status = score >= 70 ? "Ready" : score >= 50 ? "Needs attention" : "At risk"
    };

    private static AuditLog Audit(string email, string action, string type, Guid id, string description) => new()
    {
        UserEmail = email,
        Action = action,
        EntityType = type,
        EntityId = id.ToString(),
        IpAddress = "127.0.0.1",
        Description = description,
        CreatedAtUtc = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 96))
    };

    private static Notification Notice(
        string userId, string title, string message, NotificationType type, Severity severity, string link) => new()
    {
        UserId = userId,
        Title = title,
        Message = message,
        Type = type,
        Severity = severity,
        Link = link
    };

    private static List<ComplianceFramework> FrameworkSeed()
    {
        return new List<ComplianceFramework>
        {
            Framework("POPIA", "South African Protection of Personal Information Act",
                ("POPIA-1", "Accountability"), ("POPIA-2", "Processing limitation"),
                ("POPIA-3", "Purpose specification"), ("POPIA-4", "Further processing limitation"),
                ("POPIA-5", "Information quality"), ("POPIA-6", "Openness"),
                ("POPIA-7", "Security safeguards"), ("POPIA-8", "Data subject participation")),
            Framework("GDPR", "EU General Data Protection Regulation",
                ("GDPR-5", "Principles of processing"), ("GDPR-25", "Data protection by design"),
                ("GDPR-32", "Security of processing"), ("GDPR-33", "Breach notification")),
            Framework("ISO 27001", "Information security management system",
                ("ISO-A.5", "Organizational controls"), ("ISO-A.6", "People controls"),
                ("ISO-A.7", "Physical controls"), ("ISO-A.8", "Technological controls")),
            Framework("NIST CSF", "NIST Cybersecurity Framework 2.0",
                ("NIST-GV", "Govern"), ("NIST-ID", "Identify"), ("NIST-PR", "Protect"),
                ("NIST-DE", "Detect"), ("NIST-RS", "Respond"), ("NIST-RC", "Recover")),
            Framework("CIS Controls", "CIS Critical Security Controls v8",
                ("CIS-1", "Enterprise assets"), ("CIS-5", "Account management"),
                ("CIS-6", "Access control"), ("CIS-11", "Data recovery"))
        };
    }

    private static ComplianceFramework Framework(
        string name, string description, params (string Code, string Title)[] controls)
    {
        var framework = new ComplianceFramework { Name = name, Version = "Current", Description = description };
        foreach (var control in controls)
        {
            framework.Controls.Add(new ComplianceControl
            {
                Code = control.Code,
                Title = control.Title,
                Description = $"Control requirements for {control.Title.ToLowerInvariant()}."
            });
        }
        return framework;
    }

    private static string FormatName(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }

    private static IEnumerable<QuestionSeedItem> QuestionSeed()
    {
        return Questions(RiskCategoryType.Cybersecurity, "ISO 27001; NIST CSF; CIS Controls",
            "Implement and evidence the required cybersecurity control.",
            "Is multi-factor authentication enabled for all privileged users?",
            "Are passwords protected by strong complexity requirements?",
            "Are admin accounts reviewed regularly?",
            "Is endpoint protection installed on all business devices?",
            "Are systems patched within an approved timeframe?",
            "Are failed login attempts monitored?",
            "Is security awareness training completed by employees?",
            "Is sensitive data encrypted at rest and in transit?",
            "Are backups protected from ransomware?",
            "Is access removed when employees leave?")
        .Concat(Questions(RiskCategoryType.Operational, "ISO 22301; ISO 27001",
            "Document the process, assign accountable ownership, and monitor performance.",
            "Are critical business processes documented?",
            "Are responsibilities clearly assigned?",
            "Are operational incidents tracked?",
            "Is there a process for service disruption handling?",
            "Are key systems monitored for uptime?",
            "Are manual workarounds documented?",
            "Are operational dependencies reviewed?",
            "Are performance issues reported and resolved?"))
        .Concat(Questions(RiskCategoryType.Financial, "COSO; ISO 31000",
            "Strengthen financial approval, review, and segregation controls.",
            "Are financial approvals documented?",
            "Is segregation of duties enforced?",
            "Are payment changes reviewed?",
            "Are supplier payments verified?",
            "Are financial reports reviewed monthly?",
            "Are suspicious transactions investigated?",
            "Are budgets monitored against actual spend?",
            "Are payroll changes approved?"))
        .Concat(Questions(RiskCategoryType.Compliance, "POPIA; GDPR; ISO 27001",
            "Assign a control owner and retain current evidence of compliance.",
            "Is there a documented POPIA policy?",
            "Is personal information collected lawfully?",
            "Are data subject requests handled properly?",
            "Are compliance controls reviewed regularly?",
            "Is audit evidence stored securely?",
            "Are regulatory deadlines tracked?",
            "Are staff trained on compliance obligations?",
            "Are privacy breaches reported properly?"))
        .Concat(Questions(RiskCategoryType.Vendor, "NIST GV.SC; ISO 27001 A.5.19",
            "Complete supplier due diligence and document an ongoing assurance plan.",
            "Are vendors assessed before onboarding?",
            "Are vendor contracts reviewed regularly?",
            "Do critical vendors provide security evidence?",
            "Are vendor dependencies documented?",
            "Are vendor service levels monitored?",
            "Are vendor contract expiry dates tracked?",
            "Are third-party data-sharing risks reviewed?",
            "Are alternative vendors identified?"))
        .Concat(Questions(RiskCategoryType.BusinessContinuity, "ISO 22301; ISO 27001 A.8.13",
            "Test continuity arrangements and retain evidence against approved recovery objectives.",
            "Are backups performed regularly?",
            "Are backups tested?",
            "Is there a disaster recovery plan?",
            "Is there a business continuity plan?",
            "Are recovery time objectives defined?",
            "Are critical systems identified?",
            "Has a disaster recovery test been performed recently?",
            "Are emergency communication procedures documented?"))
        .Concat(Questions(RiskCategoryType.DataPrivacy, "POPIA; GDPR",
            "Apply privacy-by-design controls and document lawful processing.",
            "Is personal data classification performed?",
            "Is access to personal data restricted?",
            "Is customer data encrypted?",
            "Is retention of personal data controlled?",
            "Are privacy notices available?",
            "Are data breaches recorded?",
            "Is consent managed where required?",
            "Is unnecessary personal data deleted?"))
        .Concat(Questions(RiskCategoryType.Strategic, "ISO 31000",
            "Include the exposure in leadership risk review and define treatment actions.",
            "Are strategic objectives documented?",
            "Are business risks reviewed by leadership?",
            "Are market changes monitored?",
            "Are major business decisions risk-assessed?",
            "Is there dependency on one major customer?",
            "Is there dependency on one major supplier?",
            "Are new projects assessed for risk?",
            "Are strategic risks reported to executives?"));
    }

    private static IEnumerable<QuestionSeedItem> Questions(
        RiskCategoryType category, string mapping, string recommendation, params string[] questions) =>
        questions.Select((text, index) => new QuestionSeedItem(
            category,
            text,
            index is 0 or 2 or 7 ? 2m : 1m,
            recommendation,
            mapping));

    private sealed record QuestionSeedItem(
        RiskCategoryType Category,
        string Text,
        decimal Weight,
        string Recommendation,
        string Mapping);
}
