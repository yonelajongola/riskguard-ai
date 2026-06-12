import { useMemo, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowRight,
  Bot,
  BriefcaseBusiness,
  Building2,
  Check,
  CircleAlert,
  ClipboardCheck,
  Copy,
  FileChartColumn,
  LoaderCircle,
  MessageSquareText,
  Send,
  ShieldAlert,
  Sparkles,
  Target,
} from "lucide-react";
import { Badge, Card, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { useAuth } from "../context/AuthContext";
import { dashboardDemo } from "../data/demo";
import { api } from "../lib/api";
import type {
  AiChatResponse,
  AiProviderStatus,
  AiRecentInsight,
  DashboardSummary,
} from "../types";

type ChatMessage =
  | { role: "user"; content: string }
  | { role: "assistant"; response: AiChatResponse };

type SuggestedPrompt = {
  title: string;
  prompt: string;
  endpoint: string;
  responseType: string;
  icon: typeof Target;
  roles?: string[];
};

const suggestions: SuggestedPrompt[] = [
  { title: "Biggest Risk", prompt: "What is our biggest risk?", endpoint: "/ai/risk-summary", responseType: "Risk explanation", icon: Target },
  { title: "Executive Summary", prompt: "Generate an executive board summary.", endpoint: "/ai/executive-summary", responseType: "Executive summary", icon: FileChartColumn, roles: ["Admin", "Executive", "Risk Manager", "Auditor"] },
  { title: "Compliance Gaps", prompt: "Explain our compliance gaps.", endpoint: "/ai/compliance-summary", responseType: "Compliance summary", icon: ClipboardCheck, roles: ["Admin", "Risk Manager", "Compliance Officer", "Auditor"] },
  { title: "Cybersecurity Exposure", prompt: "What is our cybersecurity exposure?", endpoint: "/ai/copilot-chat", responseType: "Technical analysis", icon: ShieldAlert, roles: ["Admin", "Risk Manager", "Security Analyst"] },
  { title: "Vendor Risk", prompt: "Explain vendor risk.", endpoint: "/ai/copilot-chat", responseType: "Vendor risk explanation", icon: BriefcaseBusiness, roles: ["Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst", "Department Manager"] },
  { title: "Business Continuity", prompt: "Explain business continuity risk.", endpoint: "/ai/copilot-chat", responseType: "Business continuity recommendation", icon: Building2, roles: ["Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst", "Department Manager"] },
  { title: "Mitigation Plan", prompt: "Generate a mitigation plan.", endpoint: "/ai/mitigation-plan", responseType: "Mitigation plan", icon: Check, roles: ["Admin", "Risk Manager", "Security Analyst", "Compliance Officer", "Department Manager"] },
  { title: "Board Report", prompt: "Generate an executive board report summary.", endpoint: "/ai/copilot-chat", responseType: "Board report summary", icon: Sparkles, roles: ["Admin", "Executive", "Risk Manager", "Auditor"] },
];

const demoResponse: AiChatResponse = {
  title: "Priority risk: privileged access controls",
  summary: "Privileged access without enforced multi-factor authentication is the leading recorded exposure and requires accountable treatment.",
  keyFindings: [
    "Cybersecurity is the highest-scoring category.",
    "Administrative access review evidence is overdue.",
    "Open compliance and recovery findings increase the residual exposure.",
  ],
  recommendedActions: [
    "Enforce MFA for all privileged accounts.",
    "Complete and evidence the quarterly access review.",
    "Validate backup restoration after access controls are stabilized.",
  ],
  riskPriority: "Critical",
  businessImpact: "Account compromise could disrupt operations, expose information, and reduce regulatory confidence.",
  nextSteps: ["Confirm the treatment owner.", "Approve a 14-day due date.", "Recalculate risk after evidence review."],
  responseType: "Risk explanation",
  isMock: true,
  generatedAtUtc: new Date().toISOString(),
  context: {
    overallRiskScore: dashboardDemo.overallRiskScore,
    riskLevel: dashboardDemo.riskLevel,
    criticalRisks: dashboardDemo.criticalRisks,
    highRisks: dashboardDemo.highRisks,
    complianceReadiness: dashboardDemo.complianceReadiness,
    openComplianceGaps: 4,
    openIncidents: 4,
    highRiskVendors: 3,
    businessContinuityScore: dashboardDemo.businessContinuityScore,
    categoryScores: dashboardDemo.categories,
  },
};

export function CopilotPage() {
  const { user, isDemo } = useAuth();
  const queryClient = useQueryClient();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [prompt, setPrompt] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState<string>();

  const status = useQuery({
    queryKey: ["ai-status"],
    queryFn: () => api<AiProviderStatus>("/ai/status", {}, { isConfigured: false, mode: "Demo mock" }),
  });
  const recent = useQuery({
    queryKey: ["ai-recent"],
    queryFn: () => api<AiRecentInsight[]>("/ai/recent?take=6", {}, []),
  });
  const dashboard = useQuery({
    queryKey: ["dashboard-summary"],
    queryFn: () => api<DashboardSummary>("/risks/dashboard-summary", {}, dashboardDemo),
  });

  const latest = [...messages].reverse().find((message) => message.role === "assistant");
  const lastUserMessage = [...messages].reverse().find(
    (message): message is Extract<ChatMessage, { role: "user" }> => message.role === "user",
  );
  const activeContext = latest?.role === "assistant" ? latest.response.context : undefined;
  const summary = dashboard.data ?? dashboardDemo;
  const providerMode = status.data?.mode ?? (isDemo ? "Demo mock" : "Checking provider");
  const recentItems = useMemo(() => {
    const current = messages
      .filter((message): message is Extract<ChatMessage, { role: "assistant" }> => message.role === "assistant")
      .map((message, index) => ({
        id: `session-${index}`,
        title: message.response.title,
        summary: message.response.summary,
        responseType: message.response.responseType,
        isMock: message.response.isMock,
        generatedAtUtc: message.response.generatedAtUtc,
      }));
    return [...current.reverse(), ...(recent.data ?? [])].slice(0, 6);
  }, [messages, recent.data]);

  async function send(value = prompt, suggestion?: SuggestedPrompt) {
    const clean = value.trim();
    if (!clean || loading) return;
    setMessages((current) => [...current, { role: "user", content: clean }]);
    setPrompt("");
    setError("");
    setLoading(true);
    const endpoint = suggestion?.endpoint ?? "/ai/copilot-chat";
    const responseType = suggestion?.responseType ?? "Risk explanation";
    const body = endpoint === "/ai/copilot-chat"
      ? { prompt: clean, responseType }
      : endpoint === "/ai/compliance-summary"
        ? { framework: null }
        : { focus: clean };
    try {
      const response = await api<AiChatResponse>(
        endpoint,
        { method: "POST", body: JSON.stringify(body) },
        { ...demoResponse, title: suggestion?.title ?? demoResponse.title, responseType },
      );
      setMessages((current) => [...current, { role: "assistant", response }]);
      await queryClient.invalidateQueries({ queryKey: ["ai-recent"] });
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "AI Copilot could not generate a response.");
    } finally {
      setLoading(false);
    }
  }

  async function copyResponse(response: AiChatResponse) {
    const text = [
      response.title,
      response.summary,
      `Key findings:\n${response.keyFindings.map((item) => `- ${item}`).join("\n")}`,
      `Recommended actions:\n${response.recommendedActions.map((item) => `- ${item}`).join("\n")}`,
      `Business impact: ${response.businessImpact}`,
      `Next steps:\n${response.nextSteps.map((item) => `- ${item}`).join("\n")}`,
    ].join("\n\n");
    await navigator.clipboard.writeText(text);
    setCopied(response.generatedAtUtc);
    window.setTimeout(() => setCopied(undefined), 1_500);
  }

  return (
    <div className="page-stack ai-page">
      <PageHeader
        eyebrow="Grounded enterprise risk assistance"
        title="AI Copilot"
        description="Understand assessments, exposure, compliance, incidents, vendors, and resilience using the risk data available to your role."
        actions={<Badge tone={status.data?.isConfigured ? "success" : "info"}><Sparkles size={13} /> {providerMode}</Badge>}
      />

      <div className="ai-prompt-grid">
        {suggestions.map((item) => {
          const allowed = !item.roles || item.roles.some((role) => user?.roles.includes(role));
          return (
            <button
              className="ai-prompt-card"
              disabled={!allowed || loading}
              key={item.title}
              onClick={() => send(item.prompt, item)}
              title={allowed ? item.prompt : "Your role cannot request this AI response."}
            >
              <span><item.icon size={18} /></span>
              <strong>{item.title}</strong>
              <small>{allowed ? item.prompt : "Restricted for your role"}</small>
              <ArrowRight size={15} />
            </button>
          );
        })}
      </div>

      <div className="ai-workspace">
        <Card className="ai-chat-panel">
          <div className="ai-chat-head">
            <span className="ai-avatar"><Bot size={20} /></span>
            <div><strong>RiskGuard Copilot</strong><small>Responses are grounded in your accessible workspace data</small></div>
            <Badge tone={status.data?.isConfigured ? "success" : "neutral"}>{status.data?.isConfigured ? "Azure OpenAI" : "Mock mode"}</Badge>
          </div>

          <div className="ai-chat-stream" aria-live="polite">
            {messages.length === 0 ? (
              <div className="ai-empty">
                <span><MessageSquareText size={26} /></span>
                <strong>Start with a risk question</strong>
                <p>Choose a suggested prompt or ask Copilot to explain the current risk position in plain language.</p>
              </div>
            ) : messages.map((message, index) => message.role === "user" ? (
              <div className="ai-user-message" key={index}>{message.content}</div>
            ) : (
              <AiResponseCard
                response={message.response}
                copied={copied === message.response.generatedAtUtc}
                onCopy={() => copyResponse(message.response)}
                key={index}
              />
            ))}
            {loading ? (
              <div className="ai-loading"><LoaderCircle size={19} className="spin" /><span><strong>Analyzing risk context</strong><small>Reviewing available scores, findings, incidents, and actions...</small></span></div>
            ) : null}
          </div>

          {error ? <div className="ai-error"><CircleAlert size={17} /><span>{error}</span><button onClick={() => send(lastUserMessage?.content ?? prompt)}>Retry</button></div> : null}

          <form className="ai-composer" onSubmit={(event: FormEvent) => { event.preventDefault(); send(); }}>
            <textarea
              value={prompt}
              onChange={(event) => setPrompt(event.target.value)}
              placeholder="Ask: Which department needs urgent attention?"
              maxLength={2000}
              rows={3}
            />
            <div><small>Do not enter passwords, tokens, API keys, or confidential secrets.</small><button className="send-button" disabled={loading || !prompt.trim()}><Send size={17} /></button></div>
          </form>
        </Card>

        <aside className="ai-side">
          <Card className="ai-context-card">
            <div className="card-head"><div><span className="card-kicker">Risk context</span><h2>Current posture</h2></div><RiskBadge level={activeContext?.riskLevel ?? summary.riskLevel} /></div>
            <div className="ai-score"><strong>{activeContext?.overallRiskScore ?? summary.overallRiskScore}</strong><span>/ 100 overall risk</span></div>
            <ProgressBar value={activeContext?.overallRiskScore ?? summary.overallRiskScore} />
            <div className="ai-context-metrics">
              <span><small>Critical risks</small><strong>{activeContext?.criticalRisks ?? summary.criticalRisks}</strong></span>
              <span><small>High risks</small><strong>{activeContext?.highRisks ?? summary.highRisks}</strong></span>
              <span><small>Compliance</small><strong>{activeContext?.complianceReadiness ?? summary.complianceReadiness}%</strong></span>
              <span><small>Continuity</small><strong>{activeContext?.businessContinuityScore ?? summary.businessContinuityScore}%</strong></span>
              <span><small>Open incidents</small><strong>{activeContext?.openIncidents ?? "..."}</strong></span>
              <span><small>Vendor alerts</small><strong>{activeContext?.highRiskVendors ?? "..."}</strong></span>
            </div>
            <small className="ai-context-note">Context is filtered to your organization, department, assignments, and role.</small>
          </Card>

          <Card className="ai-recent-card">
            <div className="card-head"><div><span className="card-kicker">Recent AI insights</span><h2>Your history</h2></div></div>
            {recent.isLoading && !isDemo ? <div className="ai-side-loading"><LoaderCircle size={17} className="spin" /> Loading insights...</div> : null}
            {recent.isError && !isDemo ? <div className="form-error">{recent.error.message}</div> : null}
            {!recent.isLoading && recentItems.length === 0 ? <p className="ai-recent-empty">Generated insights will appear here.</p> : null}
            <div className="ai-recent-list">{recentItems.map((item) => (
              <article key={item.id}>
                <span><strong>{item.title}</strong><small>{item.responseType} · {new Date(item.generatedAtUtc).toLocaleDateString("en-ZA")}</small></span>
                {item.isMock ? <Badge tone="neutral">Mock</Badge> : <Badge tone="success">Azure</Badge>}
              </article>
            ))}</div>
          </Card>
        </aside>
      </div>
    </div>
  );
}

function AiResponseCard({
  response,
  copied,
  onCopy,
}: {
  response: AiChatResponse;
  copied: boolean;
  onCopy: () => void;
}) {
  return (
    <article className="ai-response">
      <header>
        <span className="ai-avatar"><Bot size={17} /></span>
        <div><small>{response.responseType}</small><h2>{response.title}</h2></div>
        <RiskBadge level={response.riskPriority} />
      </header>
      <p className="ai-summary">{response.summary}</p>
      <div className="ai-response-grid">
        <section><h3>Key findings</h3><ul>{response.keyFindings.map((item) => <li key={item}>{item}</li>)}</ul></section>
        <section className="ai-actions"><h3>Recommended actions</h3>{response.recommendedActions.map((item, index) => <div key={item}><b>{index + 1}</b><span>{item}</span></div>)}</section>
      </div>
      <div className="ai-impact"><strong>Business impact</strong><p>{response.businessImpact}</p></div>
      <div className="ai-next"><strong>Next steps</strong>{response.nextSteps.map((item) => <span key={item}><Check size={14} />{item}</span>)}</div>
      <footer><span>{response.isMock ? "Safe mock response" : "Azure OpenAI response"} · {new Date(response.generatedAtUtc).toLocaleTimeString("en-ZA", { hour: "2-digit", minute: "2-digit" })}</span><button onClick={onCopy}>{copied ? <Check size={14} /> : <Copy size={14} />}{copied ? "Copied" : "Copy response"}</button></footer>
    </article>
  );
}
