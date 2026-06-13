import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import {
  Activity,
  ArrowRight,
  Banknote,
  Bot,
  CheckCircle2,
  CircleAlert,
  CloudCog,
  Download,
  Fingerprint,
  KeyRound,
  LockKeyhole,
  ServerCog,
  ShieldAlert,
  ShieldCheck,
  Siren,
  TriangleAlert,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Link } from "react-router-dom";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { api, downloadReport } from "../lib/api";
import { dashboardDemo, recommendationsDemo, risksDemo } from "../data/demo";
import type { DashboardSummary, Recommendation, Risk } from "../types";
import { Badge, Card, formatMoney, MetricCard, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { useAuth } from "../context/AuthContext";

const colors = ["#2f8cff", "#8b5cf6", "#f97316", "#ef4444", "#14b8a6", "#eab308", "#ec4899", "#64748b"];

export function DashboardPage() {
  const { isDemo, user } = useAuth();
  const canCreateAssessment = !isDemo && user?.roles.some((role) =>
    ["Admin", "Risk Manager", "Compliance Officer", "Security Analyst"].includes(role));
  const [range, setRange] = useState("6 months");
  const summary = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api<DashboardSummary>("/risks/dashboard-summary", {}, dashboardDemo),
  });
  const risks = useQuery({
    queryKey: ["risks"],
    queryFn: () => api<Risk[]>("/risks", {}, risksDemo),
  });
  const recommendations = useQuery({
    queryKey: ["recommendations"],
    queryFn: () => api<Recommendation[]>("/recommendations", {}, recommendationsDemo),
  });
  const loading = summary.isLoading || risks.isLoading || recommendations.isLoading;
  const error = summary.error ?? risks.error ?? recommendations.error;
  if (loading && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Enterprise risk posture" title="Command center" description="Loading current risk data..."/><Card>Loading dashboard...</Card></div>;
  }
  if (error && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Enterprise risk posture" title="Command center" description="A decision-ready view of current exposure and priority actions."/><div className="form-error">{error.message}</div></div>;
  }
  const data = summary.data ?? dashboardDemo;
  const trend = range === "3 months" ? data.trend.slice(-3) : data.trend;
  const topRisks = (risks.data ?? risksDemo).slice(0, 5);

  return (
    <div className="page-stack">
      <PageHeader
        eyebrow="Enterprise risk posture"
        title="Command center"
        description="A decision-ready view of current exposure, control health, and priority actions."
        actions={<><label className="button button-secondary"><Activity size={16}/><select aria-label="Dashboard date range" value={range} onChange={(event)=>setRange(event.target.value)}><option>3 months</option><option>6 months</option><option>All available</option></select></label>{canCreateAssessment?<Link className="button button-primary" to="/app/assessments/new">New assessment</Link>:null}</>}
      />
      <div className="attention-banner">
        <span className="attention-icon"><ShieldAlert size={22} /></span>
        <div><strong>{data.criticalRisks > 0 ? `${data.criticalRisks} critical risk${data.criticalRisks === 1 ? "" : "s"} require executive attention` : "No critical risks are currently recorded"}</strong><span>{data.highRisks} high-risk items remain in the treatment queue.</span></div>
        <Link to="/app/risks">Review critical risk <ArrowRight size={16} /></Link>
      </div>
      <div className="metric-grid">
        <MetricCard label="Overall risk score" value={`${Math.round(data.overallRiskScore)}`} detail={`${data.riskLevel} exposure`} icon={<ShieldAlert />} tone="orange" trend={-4} />
        <MetricCard label="Critical risks" value={`${data.criticalRisks}`} detail={`${data.highRisks} additional high risks`} icon={<Siren />} tone="red" />
        <MetricCard label="Compliance readiness" value={`${Math.round(data.complianceReadiness)}%`} detail="Across 5 frameworks" icon={<ShieldCheck />} tone="blue" trend={4} />
        <MetricCard label="Financial exposure" value={formatMoney(data.financialExposure)} detail="Estimated gross exposure" icon={<Banknote />} tone="purple" trend={-8} />
      </div>
      <div className="dashboard-grid dashboard-grid-main">
        <Card className="chart-card span-2">
          <div className="card-head"><div><span className="card-kicker">Risk trajectory</span><h2>Enterprise exposure trend</h2></div><Badge tone="success">Improving 9.5%</Badge></div>
          <div className="chart-large">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={trend}>
                <defs><linearGradient id="riskArea" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor="#2f8cff" stopOpacity={0.35}/><stop offset="95%" stopColor="#2f8cff" stopOpacity={0}/></linearGradient></defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
                <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fill: "var(--muted)", fontSize: 12 }} />
                <YAxis domain={[0, 100]} axisLine={false} tickLine={false} tick={{ fill: "var(--muted)", fontSize: 12 }} />
                <Tooltip contentStyle={tooltipStyle} />
                <Area type="monotone" dataKey="value" stroke="#2f8cff" strokeWidth={3} fill="url(#riskArea)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card className="posture-card">
          <div className="card-head"><div><span className="card-kicker">Control health</span><h2>Posture index</h2></div><span className="score-delta">+4.2</span></div>
          <div className="posture-gauge"><svg viewBox="0 0 180 100"><path d="M20,90 A70,70 0 0 1 160,90" pathLength="100" className="gauge-track"/><path d="M20,90 A70,70 0 0 1 160,90" pathLength="100" className="gauge-value" strokeDasharray="64 100"/></svg><div><strong>64</strong><span>Developing</span></div></div>
          <div className="posture-lines"><div><span>Cybersecurity</span><strong>32%</strong></div><ProgressBar value={32} tone="blue" /><div><span>Continuity</span><strong>58%</strong></div><ProgressBar value={58} tone="blue" /><div><span>Compliance</span><strong>64%</strong></div><ProgressBar value={64} tone="blue" /></div>
        </Card>
      </div>
      <div className="dashboard-grid">
        <Card className="chart-card span-2">
          <div className="card-head"><div><span className="card-kicker">Exposure by domain</span><h2>Risk category comparison</h2></div><Link to="/app/risks">Full register <ArrowRight size={15} /></Link></div>
          <div className="chart-medium">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data.categories} layout="vertical" margin={{ left: 10, right: 20 }}>
                <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="var(--border)" />
                <XAxis type="number" domain={[0, 100]} hide />
                <YAxis type="category" dataKey="category" width={115} axisLine={false} tickLine={false} tick={{ fill: "var(--text-2)", fontSize: 11 }} />
                <Tooltip contentStyle={tooltipStyle} />
                <Bar dataKey="score" radius={[0, 6, 6, 0]} barSize={12}>
                  {data.categories.map((item, index) => <Cell key={item.category} fill={item.score > 75 ? "#ef4444" : item.score > 50 ? "#f97316" : colors[index]} />)}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card className="ai-brief-card">
          <div className="ai-card-label"><Bot size={17} /> AI EXECUTIVE BRIEF</div>
          <h2>Exposure is improving, but privileged access remains outside tolerance.</h2>
          <p>Resolve MFA and access review findings first. Together they account for the largest controllable cybersecurity exposure.</p>
          <div className="ai-impact"><span>Potential reduction</span><strong>−16 points</strong></div>
          <Link className="button button-ai" to="/app/copilot">Open AI Copilot <ArrowRight size={16} /></Link>
        </Card>
      </div>
      <div className="dashboard-grid">
        <Card className="table-card span-2">
          <div className="card-head"><div><span className="card-kicker">Priority queue</span><h2>Top enterprise risks</h2></div><Link to="/app/risks">View all <ArrowRight size={15} /></Link></div>
          <div className="table-wrap"><table><thead><tr><th>Risk</th><th>Department</th><th>Owner</th><th>Score</th><th>Exposure</th></tr></thead><tbody>
            {topRisks.map((risk) => <tr key={risk.id}><td><div className="table-primary"><span className={`severity-bar severity-${risk.riskLevel.toLowerCase()}`} /><span><strong>{risk.title}</strong><small>{risk.category}</small></span></div></td><td>{risk.department?.name ?? "Enterprise"}</td><td>{risk.owner}</td><td><div className="score-cell"><strong>{risk.score}</strong><RiskBadge level={risk.riskLevel} /></div></td><td>{formatMoney(risk.financialExposure)}</td></tr>)}
          </tbody></table></div>
        </Card>
        <Card className="action-card">
          <div className="card-head"><div><span className="card-kicker">Action plan</span><h2>Next best actions</h2></div></div>
          <div className="action-list">{(recommendations.data ?? recommendationsDemo).slice(0, 4).map((item, index) => <Link to="/app/recommendations" className="action-item" key={item.id}><span className={`action-rank rank-${index + 1}`}>{index + 1}</span><span><strong>{item.title}</strong><small>{item.suggestedOwner} · {item.priority}</small></span><ArrowRight size={16} /></Link>)}</div>
        </Card>
      </div>
    </div>
  );
}

export function ExecutiveDashboard() {
  const { isDemo } = useAuth();
  const [message, setMessage] = useState("");
  const summary = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api<DashboardSummary>("/risks/dashboard-summary", {}, dashboardDemo),
  });
  const data = summary.data ?? dashboardDemo;
  if (summary.isLoading && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Board and executive view" title="Executive risk outlook" description="Loading executive risk data..."/><Card>Loading dashboard...</Card></div>;
  }
  if (summary.isError && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Board and executive view" title="Executive risk outlook" description="Strategic exposure, financial impact, and management priorities."/><div className="form-error">{summary.error.message}</div></div>;
  }
  async function report() {
    setMessage("");
    try { await downloadReport("/reports/executive/pdf", "RiskGuard-Executive-Report.pdf"); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Report unavailable."); }
  }
  return (
    <div className="page-stack">
      <PageHeader eyebrow="Board and executive view" title="Executive risk outlook" description="Strategic exposure, financial impact, control confidence, and management priorities." actions={!isDemo ? <button type="button" className="button button-primary" onClick={report}><Download size={16} /> Board report</button> : undefined} />
      {message ? <div className="form-error">{message}</div> : null}
      <div className="executive-hero">
        <div><span>CURRENT ENTERPRISE POSITION</span><h2>High exposure, with a clear path back to tolerance.</h2><p>Five management actions are projected to reduce residual risk by 24% this quarter.</p></div>
        <div className="executive-score"><strong>{Math.round(data.overallRiskScore)}</strong><RiskBadge level={data.riskLevel} /><small>Target: below 50</small></div>
      </div>
      <div className="metric-grid">
        <MetricCard label="Critical risks" value={`${data.criticalRisks}`} detail={`${data.highRisks} high risks`} icon={<Siren />} tone="red" />
        <MetricCard label="Compliance readiness" value={`${Math.round(data.complianceReadiness)}%`} detail="Mapped frameworks" icon={<ShieldCheck />} tone="blue" />
        <MetricCard label="Continuity readiness" value={`${Math.round(data.businessContinuityScore)}%`} detail="Recovery capability" icon={<ServerCog />} tone="orange" />
        <MetricCard label="Financial exposure" value={formatMoney(data.financialExposure)} detail="Estimated gross exposure" icon={<Banknote />} tone="purple" />
      </div>
      <Card className="chart-card">
        <div className="card-head"><div><span className="card-kicker">Board trend</span><h2>Enterprise risk trajectory</h2></div><Badge tone={data.overallRiskScore > 50 ? "high" : "success"}>{data.riskLevel}</Badge></div>
        <div className="chart-large"><ResponsiveContainer width="100%" height="100%"><AreaChart data={data.trend}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" /><XAxis dataKey="label" axisLine={false} tickLine={false} /><YAxis domain={[0,100]} axisLine={false} tickLine={false} /><Tooltip contentStyle={tooltipStyle}/><Area type="monotone" dataKey="value" stroke="#2f8cff" strokeWidth={3} fill="#2f8cff22"/></AreaChart></ResponsiveContainer></div>
      </Card>
    </div>
  );
}

export function SecurityDashboard() {
  const statusData = [{ name: "Investigating", value: 3 }, { name: "Mitigated", value: 2 }, { name: "Closed", value: 7 }];
  return (
    <div className="page-stack">
      <PageHeader eyebrow="Cyber defense posture" title="Security operations" description="Identity, endpoint, vulnerability, and incident signals translated into business exposure." actions={<Link className="button button-primary" to="/app/incidents">Open incidents</Link>} />
      <div className="metric-grid">
        <MetricCard label="Cyber risk score" value="78" detail="Critical threshold" icon={<ShieldAlert />} tone="red" trend={3} />
        <MetricCard label="MFA coverage" value="71%" detail="9 accounts uncovered" icon={<Fingerprint />} tone="orange" />
        <MetricCard label="Open incidents" value="5" detail="3 under investigation" icon={<CircleAlert />} tone="purple" />
        <MetricCard label="Critical findings" value="2" detail="Identity and backups" icon={<TriangleAlert />} tone="red" />
      </div>
      <div className="dashboard-grid">
        <Card className="span-2">
          <div className="card-head"><div><span className="card-kicker">Security control telemetry</span><h2>Control coverage</h2></div><Badge tone="info">Demo integration data</Badge></div>
          <div className="control-grid">
            {securityControls.map(([name,value,Icon])=><div className="control-tile" key={name}><span><Icon size={19}/></span><div><strong>{name}</strong><small>{value}% coverage</small></div><ProgressBar value={value} tone={value < 60 ? "critical" : value < 80 ? "high" : "blue"} /></div>)}
          </div>
        </Card>
        <Card>
          <div className="card-head"><div><span className="card-kicker">Case workload</span><h2>Incident status</h2></div></div>
          <div className="donut-chart"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={statusData} dataKey="value" innerRadius={55} outerRadius={80} paddingAngle={4}>{statusData.map((_, i)=><Cell key={i} fill={["#f97316","#2f8cff","#22c55e"][i]}/>)}</Pie><Tooltip contentStyle={tooltipStyle}/></PieChart></ResponsiveContainer><div><strong>12</strong><span>Total cases</span></div></div>
          <div className="legend">{statusData.map((item,i)=><span key={item.name}><i style={{background:["#f97316","#2f8cff","#22c55e"][i]}}/>{item.name}<strong>{item.value}</strong></span>)}</div>
        </Card>
      </div>
    </div>
  );
}

export function OperationsDashboard() {
  return (
    <div className="page-stack">
      <PageHeader eyebrow="Operational resilience" title="Operations and continuity" description="Downtime, recovery, service dependency, and critical-system readiness." />
      <div className="metric-grid">
        <MetricCard label="Operational risk" value="52" detail="High end of medium" icon={<Activity />} tone="orange" trend={-6} />
        <MetricCard label="Continuity score" value="58%" detail="2 tests overdue" icon={<ServerCog />} tone="blue" />
        <MetricCard label="Downtime exposure" value="R315k" detail="Per 24-hour event" icon={<Banknote />} tone="purple" />
        <MetricCard label="Critical systems" value="4" detail="1 currently at risk" icon={<CloudCog />} tone="red" />
      </div>
      <div className="dashboard-grid">
        <Card className="span-2">
          <div className="card-head"><div><span className="card-kicker">Recovery readiness</span><h2>Critical system resilience</h2></div><Link to="/app/continuity">Continuity plan <ArrowRight size={15}/></Link></div>
          <div className="system-readiness">
            {[["Payment Gateway",71,"1h RTO","Ready"],["Payroll",68,"24h RTO","Ready"],["Point of Sale",62,"2h RTO","Needs attention"],["Delivery Platform",54,"4h RTO","DR test overdue"]].map(([name,score,rto,status])=><div key={name as string}><span className="system-icon"><ServerCog size={18}/></span><span><strong>{name as string}</strong><small>{rto as string} · {status as string}</small></span><ProgressBar value={score as number} tone="blue"/><b>{score as number}%</b></div>)}
          </div>
        </Card>
        <Card className="action-card">
          <div className="card-head"><div><span className="card-kicker">Continuity actions</span><h2>Recovery priorities</h2></div></div>
          <div className="action-list">{["Test delivery platform recovery","Validate point-of-sale restore","Update emergency contacts","Review ISP failover"].map((item,index)=><Link to="/app/continuity" className="action-item" key={item}><span className="action-rank">{index+1}</span><span><strong>{item}</strong><small>{index < 2 ? "Overdue" : "Due this month"}</small></span><ArrowRight size={16}/></Link>)}</div>
        </Card>
      </div>
    </div>
  );
}

const tooltipStyle = {
  background: "var(--surface-raised)",
  border: "1px solid var(--border)",
  borderRadius: "10px",
  color: "var(--text)",
  fontSize: "12px",
  boxShadow: "var(--shadow-lg)",
};

const securityControls: [string, number, LucideIcon][] = [
  ["Privileged MFA", 71, KeyRound],
  ["Endpoint protection", 92, ShieldCheck],
  ["Patch compliance", 78, ServerCog],
  ["Encryption coverage", 86, LockKeyhole],
  ["Backup isolation", 54, CloudCog],
  ["Awareness training", 81, CheckCircle2],
];
