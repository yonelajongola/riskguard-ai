using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Enums;
using RiskGuard.Infrastructure.AI;

namespace RiskGuard.UnitTests;

public sealed class AiRiskServiceTests
{
    [Fact]
    public async Task Mock_provider_uses_available_risk_context()
    {
        var service = new MockAiRiskService();

        var response = await service.GenerateAsync(
            new AiGenerationRequest("What is our biggest risk?", "Risk explanation", "General risk", null),
            Context(),
            CancellationToken.None);

        response.IsMock.Should().BeTrue();
        response.Title.Should().Contain("Privileged access");
        response.Summary.Should().Contain("82");
        response.Context.CriticalRisks.Should().Be(1);
    }

    [Fact]
    public void Prompt_security_redacts_tokens_passwords_and_api_keys()
    {
        const string prompt =
            "Review password=SuperSecret! token:abc123456789012 api_key=sk-test-value Bearer abcdefghijklmnopqrstuvwxyz";

        var sanitized = AiPromptSecurity.Sanitize(prompt);

        sanitized.Should().NotContain("SuperSecret");
        sanitized.Should().NotContain("abc123456789012");
        sanitized.Should().NotContain("sk-test-value");
        sanitized.Should().NotContain("abcdefghijklmnopqrstuvwxyz");
        sanitized.Should().Contain("[REDACTED");
    }

    [Fact]
    public async Task Configured_provider_returns_structured_response()
    {
        var providerPayload = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"title\":\"Azure analysis\",\"summary\":\"Grounded provider response.\",\"keyFindings\":[\"Finding\"],\"recommendedActions\":[\"Action\"],\"riskPriority\":\"High\",\"businessImpact\":\"Impact\",\"nextSteps\":[\"Next\"]}"
                  }
                }
              ]
            }
            """;
        var handler = new StubHandler(providerPayload);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "https://riskguard.openai.azure.com",
                ["AzureOpenAI:Key"] = "test-key",
                ["AzureOpenAI:Deployment"] = "risk-model"
            })
            .Build();
        var service = new AiRiskService(
            new HttpClient(handler),
            configuration,
            new MockAiRiskService(),
            NullLogger<AiRiskService>.Instance);

        var response = await service.GenerateAsync(
            new AiGenerationRequest("Summarize risk.", "Technical analysis", "General risk", null),
            Context(),
            CancellationToken.None);

        service.IsConfigured.Should().BeTrue();
        response.IsMock.Should().BeFalse();
        response.Title.Should().Be("Azure analysis");
        handler.RequestUri.Should().Contain("/openai/deployments/risk-model/chat/completions");
        handler.ApiKey.Should().Be("test-key");
    }

    private static AiRiskContext Context()
    {
        var summary = new AiRiskContextSummary(
            72,
            RiskLevel.High,
            1,
            2,
            64,
            3,
            2,
            1,
            58,
            [new CategoryScore("Cybersecurity", 82)]);
        return new AiRiskContext(
            "FoodieBar",
            summary,
            [new AiRiskItemContext("Privileged access weakness", "Cybersecurity", 82, RiskLevel.Critical, "IT", "Security Analyst")],
            [new AiRecommendationContext("Enforce MFA", Severity.Critical, "Security Analyst", DateTime.UtcNow.AddDays(14), RecommendationStatus.Open)],
            [],
            [],
            [],
            [],
            null);
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            ApiKey = request.Headers.GetValues("api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}
