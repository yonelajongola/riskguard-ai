using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;

namespace RiskGuard.Application.Services;

public sealed class VendorRiskService(IRiskScoringService scoringService) : IVendorRiskService
{
    public RiskCalculationResult Calculate(VendorRiskInput input)
    {
        var answers = new[]
        {
            new WeightedAnswer(input.ContractExpiryRisk, 1, "Contract expiry", "", ""),
            new WeightedAnswer(input.SecurityWeakness, 2, "Security weakness", "", ""),
            new WeightedAnswer(input.ComplianceWeakness, 1.5m, "Compliance weakness", "", ""),
            new WeightedAnswer(input.SingleSupplierDependency, 1.25m, "Supplier dependency", "", ""),
            new WeightedAnswer(input.ServiceReliabilityRisk, 1.5m, "Service reliability", "", ""),
            new WeightedAnswer(input.DataAccessRisk, 2, "Data access", "", "")
        };

        return scoringService.CalculateOverallRisk(answers);
    }
}
