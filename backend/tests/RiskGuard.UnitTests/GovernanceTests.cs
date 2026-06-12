using FluentAssertions;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class GovernanceTests
{
    [Fact]
    public void ComplianceGapFactory_CreatesOpenOwnedGap()
    {
        var factory = new ComplianceGapFactory();

        var gap = factory.Create(
            Guid.NewGuid(),
            null,
            "  Required policy evidence is missing. ",
            Severity.High,
            "Approve and publish the policy.",
            "Compliance Officer",
            DateTime.UtcNow.AddDays(30));

        gap.Status.Should().Be("Open");
        gap.Description.Should().Be("Required policy evidence is missing.");
        gap.Owner.Should().Be("Compliance Officer");
    }

    [Fact]
    public void IncidentWorkflow_UpdatesResolutionDateForResolvedIncident()
    {
        var workflow = new IncidentWorkflowService();
        var incident = new Incident { Status = IncidentStatus.Investigating };

        workflow.Transition(incident, IncidentStatus.Resolved);

        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void IncidentWorkflow_RejectsSkippingRequiredStages()
    {
        var workflow = new IncidentWorkflowService();
        var incident = new Incident { Status = IncidentStatus.Detected };

        var action = () => workflow.Transition(incident, IncidentStatus.Closed);

        action.Should().Throw<InvalidOperationException>();
    }
}
