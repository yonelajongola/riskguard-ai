using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Assessments;

public sealed record CreateAssessmentCommand(CreateAssessmentRequest Request);

public sealed class CreateAssessmentHandler(IAssessmentRepository repository)
{
    public Task<Assessment> Handle(CreateAssessmentCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var assessment = new Assessment
        {
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            RiskCategoryId = request.RiskCategoryId,
            Title = request.Title.Trim(),
            AssignedToUserId = request.AssignedToUserId,
            AssignedToName = request.AssignedToName.Trim(),
            DueDateUtc = request.DueDateUtc,
            Status = string.IsNullOrWhiteSpace(request.AssignedToUserId)
                ? AssessmentStatus.Draft
                : AssessmentStatus.Assigned
        };

        return repository.AddAsync(assessment, cancellationToken);
    }
}
