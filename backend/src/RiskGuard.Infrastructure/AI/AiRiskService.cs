using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Application.Services;

namespace RiskGuard.Infrastructure.AI;

public sealed class AiRiskService(
    HttpClient httpClient,
    IConfiguration configuration,
    MockAiRiskService mock,
    ILogger<AiRiskService> logger) : IAiRiskService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Key) &&
        !string.IsNullOrWhiteSpace(Deployment);

    public async Task<AiChatResponse> GenerateAsync(
        AiGenerationRequest request,
        AiRiskContext context,
        CancellationToken cancellationToken)
    {
        var sanitized = request with { Prompt = AiPromptSecurity.Sanitize(request.Prompt) };
        if (!IsConfigured)
        {
            return await mock.GenerateAsync(sanitized, context, cancellationToken);
        }

        try
        {
            var url = $"{Endpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(Deployment!)}/chat/completions?api-version=2024-10-21";
            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Headers.Add("api-key", Key);
            message.Content = JsonContent.Create(new
            {
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = """
                            You are RiskGuard AI, an enterprise risk copilot. Use only the supplied context.
                            Never invent risks, controls, incidents, vendors, scores, evidence, or compliance status.
                            Do not request or expose passwords, access tokens, API keys, connection strings, or secrets.
                            Return one JSON object with these exact properties:
                            title (string), summary (string), keyFindings (string array),
                            recommendedActions (string array), riskPriority (string),
                            businessImpact (string), nextSteps (string array).
                            Keep findings and actions concise, specific, and suitable for accountable human review.
                            """
                    },
                    new
                    {
                        role = "user",
                        content = JsonSerializer.Serialize(new
                        {
                            request = new
                            {
                                sanitized.Prompt,
                                sanitized.ResponseType,
                                sanitized.PromptCategory,
                                sanitized.AssessmentId
                            },
                            riskContext = context
                        }, JsonOptions)
                    }
                },
                response_format = new { type = "json_object" },
                temperature = 0.15,
                max_tokens = 1400
            });

            using var response = await httpClient.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            var payload = string.IsNullOrWhiteSpace(content)
                ? null
                : JsonSerializer.Deserialize<ProviderResponse>(StripCodeFence(content), JsonOptions);
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.Title) ||
                string.IsNullOrWhiteSpace(payload.Summary))
            {
                throw new JsonException("Azure OpenAI returned an incomplete structured response.");
            }

            return new AiChatResponse(
                payload.Title,
                payload.Summary,
                payload.KeyFindings ?? [],
                payload.RecommendedActions ?? [],
                payload.RiskPriority ?? context.Summary.RiskLevel.ToString(),
                payload.BusinessImpact ?? "Review the generated analysis with the accountable risk owner.",
                payload.NextSteps ?? [],
                sanitized.ResponseType,
                false,
                DateTime.UtcNow,
                context.Summary);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Azure OpenAI request failed; using the safe local AI provider.");
            return await mock.GenerateAsync(sanitized, context, cancellationToken);
        }
    }

    private string? Endpoint => Resolve(configuration["AzureOpenAI:Endpoint"], "AZURE_OPENAI_ENDPOINT");
    private string? Key => Resolve(configuration["AzureOpenAI:Key"], "AZURE_OPENAI_KEY");
    private string? Deployment => Resolve(configuration["AzureOpenAI:Deployment"], "AZURE_OPENAI_DEPLOYMENT");

    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }
        var firstNewLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && lastFence > firstNewLine
            ? trimmed[(firstNewLine + 1)..lastFence].Trim()
            : trimmed;
    }

    private static string? Resolve(string? configuredValue, string environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(configuredValue) ||
            configuredValue.Equals(environmentVariable, StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(environmentVariable);
        }
        return configuredValue;
    }

    private sealed record ProviderResponse(
        string Title,
        string Summary,
        IReadOnlyCollection<string>? KeyFindings,
        IReadOnlyCollection<string>? RecommendedActions,
        string? RiskPriority,
        string? BusinessImpact,
        IReadOnlyCollection<string>? NextSteps);
}
