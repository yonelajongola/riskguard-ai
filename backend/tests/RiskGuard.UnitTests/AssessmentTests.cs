using FluentAssertions;
using Moq;
using RiskGuard.Application.Assessments;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class AssessmentTests
{
    [Fact]
    public async Task CreateAssessmentHandler_AssignsStatusWhenUserIsProvided()
    {
        var repository = new Mock<IAssessmentRepository>();
        repository
            .Setup(x => x.AddAsync(It.IsAny<Assessment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Assessment assessment, CancellationToken _) => assessment);
        var request = new CreateAssessmentRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Quarterly review",
            "user-1", "Risk Owner", DateTime.UtcNow.AddDays(14));

        var result = await new CreateAssessmentHandler(repository.Object)
            .Handle(new CreateAssessmentCommand(request), CancellationToken.None);

        result.Status.Should().Be(AssessmentStatus.Assigned);
        result.Title.Should().Be("Quarterly review");
        repository.Verify(x => x.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }
}
