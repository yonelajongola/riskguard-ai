using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;

namespace RiskGuard.Persistence.Repositories;

public sealed class AssessmentRepository(RiskGuardDbContext dbContext) : IAssessmentRepository
{
    public async Task<Assessment> AddAsync(Assessment assessment, CancellationToken cancellationToken)
    {
        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return assessment;
    }
}
