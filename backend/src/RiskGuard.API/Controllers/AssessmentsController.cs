using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.Assessments;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;
using RiskGuard.Persistence.Identity;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/assessments")]
public sealed class AssessmentsController(
    RiskGuardDbContext db,
    CreateAssessmentHandler createAssessment,
    IRiskScoringService scoring,
    IRecommendationEngine recommendations,
    IAnswerScoringService answerScoring) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] AssessmentStatus? status) 
    {
        var query = db.Assessments.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.Department)
            .Include(x => x.RiskCategory)
            .AsQueryable();
        var organizationId = User.OrganizationId();
        if (!organizationId.HasValue)
        {
            return Ok(Array.Empty<Assessment>());
        }
        query = query.Where(x => x.OrganizationId == organizationId.Value);
        if (User.IsInRole("Employee"))
        {
            query = query.Where(x => x.AssignedToUserId == User.UserId());
        }
        else if (User.IsInRole("Department Manager") && User.DepartmentId().HasValue)
        {
            var departmentId = User.DepartmentId()!.Value;
            query = query.Where(x => x.DepartmentId == departmentId || x.AssignedToUserId == User.UserId());
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id)
    {
        var assessment = await db.Assessments.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Organization)
            .Include(x => x.Department)
            .Include(x => x.RiskCategory)
            .Include(x => x.Responses)
            .ThenInclude(x => x.Question)
            .Include(x => x.Risks)
            .ThenInclude(x => x.Recommendations)
            .FirstOrDefaultAsync(x => x.Id == id);
        return assessment is null || !CanAccess(assessment) ? NotFound() : Ok(assessment);
    }

    [HttpGet("{id:guid}/questions")]
    public async Task<ActionResult> GetQuestions(Guid id)
    {
        var assessment = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (assessment is null || !CanAccess(assessment))
        {
            return NotFound();
        }

        return Ok(await db.AssessmentQuestions.AsNoTracking()
            .Where(x => x.RiskCategoryId == assessment.RiskCategoryId && x.IsActive)
            .OrderBy(x => x.Text)
            .ToListAsync());
    }

    [HttpGet("{id:guid}/results")]
    public async Task<ActionResult<AssessmentResultDto>> GetResults(Guid id)
    {
        var assessment = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (assessment is null || !CanAccess(assessment))
        {
            return NotFound();
        }
        if (assessment.Status is not (AssessmentStatus.Submitted or AssessmentStatus.Reviewed or AssessmentStatus.Approved))
        {
            return Conflict(new { message = "Results are available after the assessment is submitted." });
        }

        return Ok(await BuildResultAsync(id));
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateAssessmentRequest request)
    {
        if (User.OrganizationId() != request.OrganizationId)
        {
            return Forbid();
        }
        var assignee = await ValidateReferencesAsync(request);
        if (assignee is null)
        {
            return BadRequest(new { message = "The category, department, or assignee is invalid for this workspace." });
        }

        var canonicalRequest = request with { AssignedToName = assignee.FullName };
        var assessment = await createAssessment.Handle(new CreateAssessmentCommand(canonicalRequest), HttpContext.RequestAborted);
        db.Notifications.Add(new Notification
        {
            UserId = assignee.Id.ToString(),
            Title = "Assessment assigned",
            Message = $"{assessment.Title} is due {assessment.DueDateUtc:dd MMM yyyy}.",
            Type = NotificationType.AssessmentAssigned,
            Severity = Severity.Medium,
            Link = $"/app/assessments/{assessment.Id}"
        });
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Assessment created", "Assessment", assessment.Id, assessment.Title);
        return CreatedAtAction(nameof(Get), new { id = assessment.Id }, assessment);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateAssessmentRequest request)
    {
        var assessment = await db.Assessments.FindAsync(id);
        if (assessment is null || !CanAccess(assessment)) return NotFound();
        if (assessment.Status is AssessmentStatus.Submitted or AssessmentStatus.Reviewed or AssessmentStatus.Approved)
            return Conflict(new { message = "Submitted, reviewed, or approved assessments cannot be edited." });
        if (User.OrganizationId() != request.OrganizationId)
        {
            return Forbid();
        }
        var assignee = await ValidateReferencesAsync(request);
        if (assignee is null)
        {
            return BadRequest(new { message = "The category, department, or assignee is invalid for this workspace." });
        }
        assessment.Title = request.Title.Trim();
        assessment.DepartmentId = request.DepartmentId;
        assessment.RiskCategoryId = request.RiskCategoryId;
        assessment.AssignedToUserId = request.AssignedToUserId;
        assessment.AssignedToName = assignee.FullName;
        assessment.DueDateUtc = request.DueDateUtc;
        assessment.Status = AssessmentStatus.Assigned;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult> SaveDraft(Guid id, SaveAssessmentDraftRequest request)
    {
        if (request.Responses is null)
        {
            return BadRequest(new { message = "Responses are required." });
        }
        var assessment = await db.Assessments
            .Include(x => x.Responses)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (assessment is null)
        {
            return NotFound();
        }
        if (!CanSubmit(assessment))
        {
            return Forbid();
        }
        if (assessment.Status is AssessmentStatus.Submitted or AssessmentStatus.Reviewed or AssessmentStatus.Approved)
        {
            return Conflict(new { message = "Submitted, reviewed, or approved assessments cannot be edited." });
        }

        var questions = await ActiveQuestionsAsync(assessment.RiskCategoryId);
        var validation = ScoreResponses(request.Responses, questions, requireAll: false);
        if (validation.Error is not null)
        {
            return BadRequest(new { message = validation.Error });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        ReplaceResponses(assessment, validation.Responses);
        assessment.Status = validation.Responses.Count == 0
            ? AssessmentStatus.Assigned
            : AssessmentStatus.InProgress;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await db.WriteAuditAsync(
            User,
            HttpContext,
            "Assessment draft saved",
            "Assessment",
            assessment.Id,
            $"{validation.Responses.Count} draft responses saved.");

        return Ok(new
        {
            assessment.Id,
            assessment.Status,
            SavedResponses = validation.Responses.Count,
            UpdatedAtUtc = assessment.UpdatedAtUtc
        });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<AssessmentResultDto>> Submit(Guid id, SubmitAssessmentRequest request)
    {
        if (request.Responses is null)
        {
            return BadRequest(new { message = "Responses are required." });
        }
        var assessment = await db.Assessments
            .Include(x => x.RiskCategory)
            .Include(x => x.Department)
            .Include(x => x.Responses)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (assessment is null) return NotFound();
        if (!CanSubmit(assessment)) return Forbid();
        if (assessment.Status is AssessmentStatus.Submitted or AssessmentStatus.Reviewed or AssessmentStatus.Approved)
            return Conflict(new { message = "Assessment was already submitted." });

        var questions = await ActiveQuestionsAsync(assessment.RiskCategoryId);
        if (questions.Count == 0)
        {
            return Conflict(new { message = "This assessment category has no active questions." });
        }
        var validation = ScoreResponses(request.Responses, questions, requireAll: true);
        if (validation.Error is not null)
        {
            return BadRequest(new { message = validation.Error });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        ReplaceResponses(assessment, validation.Responses);
        assessment.Status = AssessmentStatus.Submitted;
        assessment.SubmittedAtUtc = DateTime.UtcNow;
        var result = await CalculateInternalAsync(assessment, questions.Values);
        await transaction.CommitAsync();
        await db.WriteAuditAsync(User, HttpContext, "Assessment submitted", "Assessment", assessment.Id, $"{assessment.Title} submitted at {result.Score:0.##}.");
        return Ok(await BuildResultAsync(assessment.Id));
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("{id:guid}/calculate")]
    public async Task<ActionResult<RiskCalculationResult>> Calculate(Guid id)
    {
        var assessment = await db.Assessments
            .Include(x => x.RiskCategory)
            .Include(x => x.Department)
            .Include(x => x.Responses)
            .ThenInclude(x => x.Question)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (assessment is null) return NotFound();
        if (!CanAccess(assessment)) return NotFound();
        if (assessment.Responses.Count == 0)
        {
            return Conflict(new { message = "The assessment has no responses to calculate." });
        }
        var result = await CalculateInternalAsync(assessment, assessment.Responses.Select(x => x.Question!).Where(x => x is not null));
        await db.WriteAuditAsync(User, HttpContext, "Risk score calculated", "Assessment", assessment.Id, $"Score {result.Score:0.##}.");
        return Ok(result);
    }

    private async Task<RiskCalculationResult> CalculateInternalAsync(
        Assessment assessment,
        IEnumerable<AssessmentQuestion> questions)
    {
        var questionMap = questions.ToDictionary(x => x.Id);
        var weighted = assessment.Responses
            .Where(response => questionMap.ContainsKey(response.QuestionId))
            .Select(response =>
            {
                var question = questionMap[response.QuestionId];
                return new WeightedAnswer(
                    response.AnswerScore,
                    question.Weight,
                    question.Text,
                    question.RecommendationText,
                    question.ComplianceMappings);
            }).ToArray();
        var result = scoring.CalculateOverallRisk(weighted);
        assessment.Score = result.Score;
        assessment.RiskLevel = result.Level;

        var risk = await db.Risks.FirstOrDefaultAsync(x => x.AssessmentId == assessment.Id && x.Title == $"{assessment.Title} exposure");
        if (risk is null)
        {
            risk = new RiskItem
            {
                AssessmentId = assessment.Id,
                DepartmentId = assessment.DepartmentId,
                Category = assessment.RiskCategory?.Type ?? RiskCategoryType.Operational,
                Title = $"{assessment.Title} exposure",
                Description = "Consolidated risk generated from the assessment result.",
                Impact = RiskDimension(result.Score),
                Likelihood = RiskDimension(result.Score),
                Owner = assessment.Department?.RiskOwner ?? assessment.AssignedToName,
                Status = "Open"
            };
            db.Risks.Add(risk);
        }
        risk.Score = result.Score;
        risk.RiskLevel = result.Level;

        var existing = await db.Recommendations.Where(x => x.AssessmentId == assessment.Id).ToListAsync();
        db.Recommendations.RemoveRange(existing);
        var generated = result.Level is RiskLevel.High or RiskLevel.Critical
            ? recommendations.GenerateRecommendations(
                assessment.Id,
                assessment.RiskCategory?.Type ?? RiskCategoryType.Operational,
                weighted,
                assessment.Department?.RiskOwner ?? assessment.AssignedToName)
            : [];
        foreach (var recommendation in generated)
        {
            recommendation.RiskItem = risk;
        }
        db.Recommendations.AddRange(generated);

        db.RiskScores.Add(new RiskScore
        {
            AssessmentId = assessment.Id,
            OverallScore = result.Score,
            CategoryScore = result.Score,
            DepartmentScore = result.Score,
            ComplianceReadinessScore = 100 - result.Score,
            BusinessContinuityScore = assessment.RiskCategory?.Type == RiskCategoryType.BusinessContinuity ? 100 - result.Score : 0,
            VendorRiskScore = assessment.RiskCategory?.Type == RiskCategoryType.Vendor ? result.Score : 0,
            CybersecurityPostureScore = assessment.RiskCategory?.Type == RiskCategoryType.Cybersecurity ? 100 - result.Score : 0,
            RiskLevel = result.Level
        });
        await db.SaveChangesAsync();
        return result;
    }

    private async Task<Dictionary<Guid, AssessmentQuestion>> ActiveQuestionsAsync(Guid categoryId) =>
        await db.AssessmentQuestions
            .Where(x => x.RiskCategoryId == categoryId && x.IsActive)
            .OrderBy(x => x.Text)
            .ToDictionaryAsync(x => x.Id);

    private async Task<ApplicationUser?> ValidateReferencesAsync(CreateAssessmentRequest request)
    {
        var categoryExists = await db.RiskCategories.AnyAsync(x => x.Id == request.RiskCategoryId);
        var departmentValid = !request.DepartmentId.HasValue ||
            await db.Departments.AnyAsync(x =>
                x.Id == request.DepartmentId.Value &&
                x.OrganizationId == request.OrganizationId &&
                x.Status == RecordStatus.Active);
        if (!categoryExists || !departmentValid ||
            !Guid.TryParse(request.AssignedToUserId, out var assigneeId))
        {
            return null;
        }

        return await db.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == assigneeId &&
            x.OrganizationId == request.OrganizationId &&
            x.IsActive);
    }

    private (string? Error, List<ScoredResponse> Responses) ScoreResponses(
        IReadOnlyCollection<AssessmentResponseInput> inputs,
        IReadOnlyDictionary<Guid, AssessmentQuestion> questions,
        bool requireAll)
    {
        if (inputs.Select(x => x.QuestionId).Distinct().Count() != inputs.Count)
        {
            return ("Each question can only be answered once.", []);
        }
        if (inputs.Any(x => !questions.ContainsKey(x.QuestionId)))
        {
            return ("One or more answers do not belong to this assessment.", []);
        }
        if (requireAll && inputs.Count != questions.Count)
        {
            return ("Every active question in this assessment category must be answered exactly once.", []);
        }
        if (inputs.Any(x =>
                string.IsNullOrWhiteSpace(x.Answer) ||
                x.Answer.Length > 200 ||
                (x.Notes?.Length ?? 0) > 4000))
        {
            return ("Answers are required and notes cannot exceed 4,000 characters.", []);
        }

        var result = new List<ScoredResponse>(inputs.Count);
        foreach (var input in inputs)
        {
            var question = questions[input.QuestionId];
            if (!answerScoring.TryCalculate(question, input.Answer, out var score))
            {
                return ($"'{input.Answer}' is not a valid answer for '{question.Text}'.", []);
            }
            result.Add(new ScoredResponse(input, score));
        }
        return (null, result);
    }

    private void ReplaceResponses(Assessment assessment, IReadOnlyCollection<ScoredResponse> responses)
    {
        db.AssessmentResponses.RemoveRange(assessment.Responses);
        assessment.Responses.Clear();
        foreach (var item in responses)
        {
            db.AssessmentResponses.Add(new AssessmentResponse
            {
                AssessmentId = assessment.Id,
                Assessment = assessment,
                QuestionId = item.Input.QuestionId,
                Answer = item.Input.Answer.Trim(),
                AnswerScore = item.Score,
                Notes = item.Input.Notes?.Trim() ?? string.Empty
            });
        }
    }

    private async Task<AssessmentResultDto> BuildResultAsync(Guid id)
    {
        var assessment = await db.Assessments.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Organization)
            .Include(x => x.Department)
            .Include(x => x.RiskCategory)
            .Include(x => x.Responses)
            .ThenInclude(x => x.Question)
            .Include(x => x.Risks)
            .ThenInclude(x => x.Recommendations)
            .SingleAsync(x => x.Id == id);
        var latestScore = await db.RiskScores.AsNoTracking()
            .Where(x => x.AssessmentId == id)
            .OrderByDescending(x => x.CalculatedAtUtc)
            .FirstOrDefaultAsync();
        var risk = assessment.Risks.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        var riskIds = assessment.Risks.Select(x => x.Id).ToArray();
        var gaps = riskIds.Length == 0
            ? []
            : await db.ComplianceGaps.AsNoTracking()
                .Include(x => x.Control)
                .ThenInclude(x => x!.Framework)
                .Where(x => x.RiskItemId.HasValue && riskIds.Contains(x.RiskItemId.Value))
                .OrderByDescending(x => x.Severity)
                .ToListAsync();

        return new AssessmentResultDto(
            assessment.Id,
            assessment.Title,
            assessment.Status,
            latestScore?.OverallScore ?? assessment.Score,
            latestScore?.CategoryScore ?? assessment.Score,
            latestScore?.RiskLevel ?? assessment.RiskLevel,
            assessment.SubmittedAtUtc,
            assessment.Organization?.Name ?? string.Empty,
            assessment.Department?.Name ?? "Enterprise",
            assessment.RiskCategory?.Name ?? string.Empty,
            risk?.Id,
            risk?.Title,
            assessment.Responses
                .OrderBy(x => x.Question?.Text)
                .Select(x => new AssessmentAnswerResultDto(
                    x.QuestionId,
                    x.Question?.Text ?? string.Empty,
                    x.Answer,
                    x.AnswerScore,
                    x.Question?.Weight ?? 0,
                    x.Notes,
                    x.Question?.ComplianceMappings ?? string.Empty))
                .ToArray(),
            assessment.Risks
                .SelectMany(x => x.Recommendations)
                .OrderByDescending(x => x.Priority)
                .Select(x => new AssessmentRecommendationResultDto(
                    x.Id,
                    x.Title,
                    x.Description,
                    x.Priority,
                    x.SuggestedOwner,
                    x.DueDateUtc,
                    x.ComplianceMapping,
                    x.Status))
                .ToArray(),
            gaps.Select(x => new AssessmentComplianceGapResultDto(
                    x.Id,
                    x.Control?.Framework?.Name ?? string.Empty,
                    x.Control is null ? string.Empty : $"{x.Control.Code} - {x.Control.Title}",
                    x.Description,
                    x.Severity,
                    x.Recommendation,
                    x.Owner,
                    x.DueDateUtc,
                    x.Status))
                .ToArray());
    }

    private static int RiskDimension(decimal score) => score switch
    {
        > 75 => 4,
        > 50 => 3,
        > 25 => 2,
        _ => 1
    };

    private bool CanAccess(Assessment assessment)
    {
        if (assessment.OrganizationId != User.OrganizationId())
        {
            return false;
        }
        if (User.IsInRole("Employee"))
        {
            return assessment.AssignedToUserId == User.UserId();
        }
        if (User.IsInRole("Department Manager"))
        {
            return assessment.DepartmentId == User.DepartmentId() ||
                   assessment.AssignedToUserId == User.UserId();
        }
        return true;
    }

    private bool CanSubmit(Assessment assessment) =>
        CanAccess(assessment) &&
        (User.IsRiskProfessional() ||
         assessment.AssignedToUserId == User.UserId() ||
         User.IsInRole("Department Manager"));

    private sealed record ScoredResponse(AssessmentResponseInput Input, decimal Score);
}
