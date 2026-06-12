using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Services;

public sealed class IncidentWorkflowService : IIncidentWorkflowService
{
    public bool CanTransition(IncidentStatus current, IncidentStatus next)
    {
        if (current == next) return true;
        return current switch
        {
            IncidentStatus.Detected => next is IncidentStatus.Assigned or IncidentStatus.Investigating,
            IncidentStatus.Assigned => next is IncidentStatus.Investigating,
            IncidentStatus.Investigating => next is IncidentStatus.Mitigated or IncidentStatus.Resolved,
            IncidentStatus.Mitigated => next is IncidentStatus.Resolved or IncidentStatus.Investigating,
            IncidentStatus.Resolved => next is IncidentStatus.Closed or IncidentStatus.Investigating,
            _ => false
        };
    }

    public void Transition(Incident incident, IncidentStatus next)
    {
        if (!CanTransition(incident.Status, next))
        {
            throw new InvalidOperationException($"Incident cannot move from {incident.Status} to {next}.");
        }

        incident.Status = next;
        incident.UpdatedAtUtc = DateTime.UtcNow;
        if (next is IncidentStatus.Resolved or IncidentStatus.Closed)
        {
            incident.ResolvedAtUtc = DateTime.UtcNow;
        }
    }
}

public sealed class ComplianceGapFactory : IComplianceGapFactory
{
    public ComplianceGap Create(
        Guid controlId,
        Guid? riskItemId,
        string description,
        Severity severity,
        string recommendation,
        string owner,
        DateTime dueDateUtc)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("A compliance control is required.", nameof(controlId));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A gap description is required.", nameof(description));
        if (dueDateUtc.Date < DateTime.UtcNow.Date) throw new ArgumentException("The due date cannot be in the past.", nameof(dueDateUtc));

        return new ComplianceGap
        {
            ControlId = controlId,
            RiskItemId = riskItemId,
            Description = description.Trim(),
            Severity = severity,
            Recommendation = recommendation.Trim(),
            Owner = owner.Trim(),
            DueDateUtc = dueDateUtc,
            Status = "Open"
        };
    }
}
