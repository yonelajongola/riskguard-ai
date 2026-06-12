using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Controllers;
using RiskGuard.Application.Assessments;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;
using RiskGuard.Persistence.Repositories;

namespace RiskGuard.IntegrationTests;

public sealed class AssessmentWorkflowTests
{
    [Fact]
    public async Task DraftAndSubmit_PersistAnswersScoreRiskRecommendationsAndResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RiskGuardDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new RiskGuardDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var organization = new Organization { Name = "Workflow Test", Industry = "Technology", Country = "South Africa" };
        var department = new Department
        {
            Organization = organization,
            Name = "Operations",
            RiskOwner = "Operations Manager"
        };
        var category = new RiskCategory
        {
            Name = "Operational",
            Type = RiskCategoryType.Operational
        };
        var firstQuestion = new AssessmentQuestion
        {
            RiskCategory = category,
            Text = "Is the critical process documented?",
            Weight = 2,
            RecommendationText = "Document and approve the critical process.",
            ComplianceMappings = "ISO 22301"
        };
        var secondQuestion = new AssessmentQuestion
        {
            RiskCategory = category,
            Text = "Is the process owner assigned?",
            Weight = 1,
            RecommendationText = "Assign an accountable process owner.",
            ComplianceMappings = "ISO 31000"
        };
        var userId = Guid.NewGuid();
        var assessment = new Assessment
        {
            Organization = organization,
            Department = department,
            RiskCategory = category,
            Title = "Operational workflow test",
            AssignedToUserId = userId.ToString(),
            AssignedToName = "Risk Owner",
            Status = AssessmentStatus.Assigned
        };
        db.AddRange(organization, department, category, firstQuestion, secondQuestion, assessment);
        await db.SaveChangesAsync();

        var controller = new AssessmentsController(
            db,
            new CreateAssessmentHandler(new AssessmentRepository(db)),
            new RiskScoringService(),
            new RecommendationEngine(),
            new AnswerScoringService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthenticatedContext(userId, organization.Id)
            }
        };

        var questionsResponse = await controller.GetQuestions(assessment.Id);
        questionsResponse.Should().BeOfType<OkObjectResult>();

        var draftResponse = await controller.SaveDraft(
            assessment.Id,
            new SaveAssessmentDraftRequest(
            [
                new AssessmentResponseInput(firstQuestion.Id, "No", "Process documentation is incomplete.")
            ]));
        draftResponse.Should().BeOfType<OkObjectResult>();

        var draft = await db.Assessments.AsNoTracking()
            .Include(x => x.Responses)
            .SingleAsync(x => x.Id == assessment.Id);
        draft.Status.Should().Be(AssessmentStatus.InProgress);
        draft.Responses.Should().ContainSingle();
        draft.Responses.Single().AnswerScore.Should().Be(100);

        var submitResponse = await controller.Submit(
            assessment.Id,
            new SubmitAssessmentRequest(
            [
                new AssessmentResponseInput(firstQuestion.Id, "No", "Process documentation is incomplete."),
                new AssessmentResponseInput(secondQuestion.Id, "Yes", "Owner assignment is evidenced.")
            ]));
        var submitResult = submitResponse.Result.Should().BeOfType<OkObjectResult>().Subject;
        var result = submitResult.Value.Should().BeOfType<AssessmentResultDto>().Subject;

        result.Status.Should().Be(AssessmentStatus.Submitted);
        result.OverallRiskScore.Should().Be(66.67m);
        result.RiskLevel.Should().Be(RiskLevel.High);
        result.Answers.Should().HaveCount(2);
        result.Recommendations.Should().ContainSingle();

        var risk = await db.Risks.AsNoTracking().SingleAsync(x => x.AssessmentId == assessment.Id);
        risk.Score.Should().Be(66.67m);
        risk.Impact.Should().Be(3);
        (await db.RiskScores.CountAsync(x => x.AssessmentId == assessment.Id)).Should().Be(1);

        var mediumAssessment = new Assessment
        {
            OrganizationId = organization.Id,
            DepartmentId = department.Id,
            RiskCategoryId = category.Id,
            Title = "Medium risk workflow test",
            AssignedToUserId = userId.ToString(),
            AssignedToName = "Risk Owner",
            Status = AssessmentStatus.Assigned
        };
        db.Assessments.Add(mediumAssessment);
        await db.SaveChangesAsync();
        var mediumResponse = await controller.Submit(
            mediumAssessment.Id,
            new SubmitAssessmentRequest(
            [
                new AssessmentResponseInput(firstQuestion.Id, "Yes", "Critical process is documented."),
                new AssessmentResponseInput(secondQuestion.Id, "No", "A process owner is not assigned.")
            ]));
        var mediumResult = mediumResponse.Result.Should().BeOfType<OkObjectResult>()
            .Subject.Value.Should().BeOfType<AssessmentResultDto>().Subject;
        mediumResult.RiskLevel.Should().Be(RiskLevel.Medium);
        mediumResult.Recommendations.Should().BeEmpty();

        var framework = new ComplianceFramework { Name = "ISO 22301", Version = "Current" };
        var control = new ComplianceControl
        {
            Framework = framework,
            Code = "ISO-8.4",
            Title = "Business continuity procedures"
        };
        db.ComplianceGaps.Add(new ComplianceGap
        {
            Control = control,
            RiskItemId = risk.Id,
            Description = "The critical process is not fully documented.",
            Severity = Severity.High,
            Recommendation = "Complete and approve the process documentation.",
            Owner = "Operations Manager",
            DueDateUtc = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var resultsResponse = await controller.GetResults(assessment.Id);
        var persistedResult = resultsResponse.Result.Should().BeOfType<OkObjectResult>()
            .Subject.Value.Should().BeOfType<AssessmentResultDto>().Subject;
        persistedResult.ComplianceGaps.Should().ContainSingle();
        persistedResult.ComplianceGaps.Single().Framework.Should().Be("ISO 22301");
    }

    private static DefaultHttpContext AuthenticatedContext(Guid userId, Guid organizationId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "admin@workflow.test"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("organization_id", organizationId.ToString())
        ], "Test"));
        return context;
    }
}
