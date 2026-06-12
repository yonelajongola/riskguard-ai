import { useMemo, useState, type FormEvent, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  Archive,
  ArrowRight,
  Bot,
  Building2,
  CalendarClock,
  CheckCircle2,
  ChevronDown,
  CircleAlert,
  Clock3,
  Copy,
  Download,
  FileChartColumn,
  FileCheck2,
  FileSpreadsheet,
  Filter,
  KeyRound,
  ListFilter,
  LockKeyhole,
  Mail,
  MessageSquareText,
  MoreHorizontal,
  Network,
  Plus,
  RefreshCw,
  Search,
  Send,
  ServerCog,
  Settings,
  ShieldCheck,
  Siren,
  SlidersHorizontal,
  Sparkles,
  UserCog,
  Users,
} from "lucide-react";
import { Link } from "react-router-dom";
import {
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
import {
  auditDemo,
  complianceDemo,
  continuityDemo,
  gapsDemo,
  incidentsDemo,
  notificationsDemo,
  recommendationsDemo,
  risksDemo,
  vendorsDemo,
} from "../data/demo";
import type {
  AuditLog,
  ComplianceDashboard,
  ComplianceGap,
  ContinuityPlan,
  HeatMapItem,
  Incident,
  Notification,
  Recommendation,
  Risk,
  UserSummary,
  Vendor,
} from "../types";
import { Badge, Card, formatDate, formatMoney, MetricCard, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { useAuth } from "../context/AuthContext";

export function RisksPage() {
  const {isDemo}=useAuth();
  const query = useQuery({queryKey:["risks"],queryFn:()=>api<Risk[]>("/risks",{},risksDemo)});
  const [search,setSearch]=useState("");
  const risks=(query.data??(isDemo?risksDemo:[])).filter((risk)=>risk.title.toLowerCase().includes(search.toLowerCase()));
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Loading current risks..."/><Card>Loading risk register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership and treatment status."/><div className="form-error">{query.error.message}</div></div>;
  const critical=risks.filter(risk=>risk.riskLevel==="Critical").length;
  const high=risks.filter(risk=>risk.riskLevel==="High").length;
  const exposure=risks.reduce((total,risk)=>total+risk.financialExposure,0);
  return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership, treatment status, and estimated financial impact." actions={<><button className="button button-secondary"><Download size={16}/> Export</button><button className="button button-primary"><Plus size={16}/> Add risk</button></>}/>
    <div className="metric-grid metric-grid-compact"><MetricCard label="Total risks" value={`${risks.length}`} detail="Current workspace" icon={<Siren/>} tone="blue"/><MetricCard label="Critical" value={`${critical}`} detail="Outside tolerance" icon={<CircleAlert/>} tone="red"/><MetricCard label="High" value={`${high}`} detail="Treatment required" icon={<Activity/>} tone="orange"/><MetricCard label="Gross exposure" value={formatMoney(exposure)} detail="Estimated financial impact" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="table-toolbar"><div className="inline-search"><Search size={16}/><input value={search} onChange={(e)=>setSearch(e.target.value)} placeholder="Search risk register..."/></div><div className="toolbar-actions"><button className="button button-secondary"><ListFilter size={15}/> All categories</button><button className="button button-secondary"><SlidersHorizontal size={15}/> Filters</button></div></div>
      <div className="table-wrap"><table><thead><tr><th>Risk statement</th><th>Category</th><th>Department</th><th>Owner</th><th>Score</th><th>Status</th><th>Exposure</th></tr></thead><tbody>{risks.map(risk=><tr key={risk.id}><td><div className="table-primary"><span className={`severity-bar severity-${risk.riskLevel.toLowerCase()}`}/><span><strong>{risk.title}</strong><small>ID · {risk.id.toUpperCase()}</small></span></div></td><td>{risk.category}</td><td>{risk.department?.name}</td><td><span className="owner-cell"><i>{initials(risk.owner)}</i>{risk.owner}</span></td><td><span className="score-cell"><strong>{risk.score}</strong><RiskBadge level={risk.riskLevel}/></span></td><td><StatusBadge status={risk.status}/></td><td>{formatMoney(risk.financialExposure)}</td></tr>)}</tbody></table></div>
    </Card></div>;
}

export function HeatMapPage() {
  const {isDemo}=useAuth();
  const query=useQuery({
    queryKey:["risk-heatmap"],
    queryFn:()=>api<HeatMapItem[]>("/risks/heatmap",{},risksDemo.map(risk=>({
      id:risk.id,
      title:risk.title,
      impact:risk.impact,
      likelihood:risk.likelihood,
      level:risk.riskLevel,
      department:risk.department?.name??"Enterprise",
    }))),
  });
  const likelihood=["Rare","Possible","Likely","Almost certain"];
  const impact=["Critical","High","Medium","Low"];
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Impact x likelihood" title="Enterprise risk heat map" description="Loading risk concentration..."/><Card>Loading heat map...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Impact x likelihood" title="Enterprise risk heat map" description="Risk concentration by severity and department."/><div className="form-error">{query.error.message}</div></div>;
  const heatItems=query.data??[];
  return <div className="page-stack"><PageHeader eyebrow="Impact × likelihood" title="Enterprise risk heat map" description="See concentration, severity, and accountable departments across the current risk profile." actions={<button className="button button-secondary"><Filter size={15}/> Filter view</button>}/>
    <div className="heatmap-layout"><Card className="heatmap-card"><div className="heatmap-y-label">Business impact</div><div className="heatmap-grid">
      {impact.map((impactLabel,row)=><div className="heat-row" key={impactLabel}><span className="axis-label">{impactLabel}</span>{likelihood.map((likelihoodLabel,col)=>{const severity=matrixSeverity(row,col);const items=heatItems.filter(risk=>risk.impact===4-row&&risk.likelihood===col+1);return <div className={`heat-cell heat-${severity}`} key={likelihoodLabel}><span>{items.length}</span>{items.map((risk)=><button key={risk.id} title={risk.title}>{risk.level.slice(0,1)}</button>)}</div>})}</div>)}
      <div className="heat-x-row"><span/>{likelihood.map(item=><span key={item}>{item}</span>)}</div><div className="heat-x-label">Likelihood</div>
    </div></Card><Card className="heatmap-side"><div className="card-head"><div><span className="card-kicker">Selected zone</span><h2>High impact · likely</h2></div><Badge tone="critical">2 risks</Badge></div>{risksDemo.slice(0,2).map(risk=><div className="heat-risk" key={risk.id}><div><RiskBadge level={risk.riskLevel}/><strong>{risk.title}</strong><span>{risk.department?.name} · {risk.owner}</span></div><b>{risk.score}</b></div>)}<div className="heat-legend"><strong>Severity legend</strong>{["Low","Medium","High","Critical"].map(x=><span key={x}><i className={`heat-${x.toLowerCase()}`}/>{x}</span>)}</div></Card></div>
  </div>;
}

export function RecommendationsPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["recommendations"],queryFn:()=>api<Recommendation[]>("/recommendations",{},recommendationsDemo)});
  const items=query.data??(isDemo?recommendationsDemo:[]);
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Loading treatment actions..."/><Card>Loading recommendations...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Prioritized mitigation actions and ownership."/><div className="form-error">{query.error.message}</div></div>;
  return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Prioritized mitigation actions with ownership, deadlines, business impact, and control mappings." actions={<button className="button button-primary"><Plus size={16}/> Add recommendation</button>}/>
    <div className="recommendation-grid">{["Open","InProgress","Completed"].map((status)=><div className="kanban-column" key={status}><div className="kanban-head"><span>{status==="InProgress"?"In progress":status}</span><b>{items.filter(x=>x.status===status).length || (status==="Completed"?2:status==="InProgress"?1:3)}</b></div>{items.filter(x=>status==="Completed"?false:x.status===status).map(item=><Card className="recommendation-card" key={item.id}><div><Badge tone={item.priority.toLowerCase() as "critical"|"high"}>{item.priority}</Badge><button className="icon-button"><MoreHorizontal size={17}/></button></div><h3>{item.title}</h3><p>{item.description}</p><div className="recommendation-meta"><span><CalendarClock size={14}/>{formatDate(item.dueDateUtc)}</span><span><UserCog size={14}/>{item.suggestedOwner}</span></div><div className="mapping-tags">{item.complianceMapping.split(";").map(x=><small key={x}>{x}</small>)}</div></Card>)}{status==="Completed"?<Card className="completed-summary"><CheckCircle2 size={30}/><strong>2 actions completed</strong><span>This quarter · 11 risk points reduced</span></Card>:null}</div>)}</div>
  </div>;
}

export function CompliancePage() {
  const {isDemo}=useAuth();
  const dash=useQuery({queryKey:["compliance-dashboard"],queryFn:()=>api<ComplianceDashboard>("/compliance/dashboard",{},complianceDemo)});
  const gaps=useQuery({queryKey:["compliance-gaps"],queryFn:()=>api<ComplianceGap[]>("/compliance/gaps",{},gapsDemo)});
  const loading=dash.isLoading||gaps.isLoading;
  const error=dash.error??gaps.error;
  if(loading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Loading compliance readiness..."/><Card>Loading compliance data...</Card></div>;
  if(error&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Readiness, control performance, and remediation."/><div className="form-error">{error.message}</div></div>;
  const data=dash.data??complianceDemo;
  return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Readiness, control performance, evidence status, and remediation across five frameworks." actions={<button className="button button-primary"><FileCheck2 size={16}/> Generate compliance report</button>}/>
    <div className="metric-grid"><MetricCard label="Overall readiness" value={`${data.readiness}%`} detail="Across 5 frameworks" icon={<ShieldCheck/>} tone="blue" trend={4}/><MetricCard label="Controls passed" value={`${data.passed}`} detail="Evidence accepted" icon={<CheckCircle2/>} tone="green"/><MetricCard label="Failed controls" value={`${data.failed}`} detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Evidence missing" value={`${data.missing}`} detail="Audit attention" icon={<Archive/>} tone="orange"/></div>
    <div className="dashboard-grid"><Card className="span-2"><div className="card-head"><div><span className="card-kicker">Framework readiness</span><h2>Assurance coverage</h2></div></div><div className="framework-readiness">{data.frameworks.map((item,index)=><div key={item.category}><span className="framework-icon">{item.category.slice(0,2)}</span><span><strong>{item.category}</strong><small>{[8,4,12,9,10][index]} controls assessed</small></span><ProgressBar value={item.score} tone="blue"/><b>{item.score}%</b></div>)}</div></Card><Card className="compliance-donut"><div className="card-head"><div><span className="card-kicker">Control results</span><h2>Current status</h2></div></div><div className="donut-chart"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={[{name:"Passed",value:data.passed},{name:"Failed",value:data.failed},{name:"Missing",value:data.missing}]} dataKey="value" innerRadius={55} outerRadius={80} paddingAngle={4}><Cell fill="#22c55e"/><Cell fill="#ef4444"/><Cell fill="#eab308"/></Pie></PieChart></ResponsiveContainer><div><strong>{data.passed+data.failed+data.missing}</strong><span>Controls</span></div></div></Card></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Remediation register</span><h2>Compliance gaps</h2></div><Link to="/app/compliance/gaps">View all <ArrowRight size={15}/></Link></div><div className="table-wrap"><table><thead><tr><th>Framework / control</th><th>Gap</th><th>Severity</th><th>Owner</th><th>Due date</th><th>Status</th></tr></thead><tbody>{(gaps.data??gapsDemo).map(gap=><tr key={gap.id}><td><strong>{gap.control?.framework?.name}</strong><small className="block-muted">{gap.control?.code} · {gap.control?.title}</small></td><td className="wide-cell">{gap.description}</td><td><Badge tone={gap.severity.toLowerCase() as "high"|"medium"}>{gap.severity}</Badge></td><td>{gap.owner}</td><td>{formatDate(gap.dueDateUtc)}</td><td><StatusBadge status={gap.status}/></td></tr>)}</tbody></table></div></Card>
  </div>;
}

export function IncidentsPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["incidents"],queryFn:()=>api<Incident[]>("/incidents",{},incidentsDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Loading incidents..."/><Card>Loading incident register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Track investigation, mitigation, and closure."/><div className="form-error">{query.error.message}</div></div>;
  return <RegisterPage title="Incident management" eyebrow="Case and event response" description="Track detection, investigation, mitigation, evidence, and closure against related risks." action="Create incident" metrics={[
    ["Open incidents","4","3 high severity",Siren,"red"],["Investigating","2","Active response",Search,"orange"],["Mean time to resolve","3.2d","12% improvement",Clock3,"blue"],["Closed this quarter","7","Evidence retained",CheckCircle2,"green"]
  ]}><table><thead><tr><th>Incident</th><th>Category</th><th>Department</th><th>Owner</th><th>Detected</th><th>Severity</th><th>Status</th></tr></thead><tbody>{(query.data??incidentsDemo).map(item=><tr key={item.id}><td><Link className="cell-link" to={`/app/incidents/${item.id}`}><span className="type-icon type-danger"><Siren size={17}/></span><span><strong>{item.title}</strong><small>INC-{item.id.toUpperCase()}</small></span></Link></td><td>{item.category}</td><td>{item.department?.name}</td><td>{item.owner}</td><td>{formatDate(item.detectedAtUtc)}</td><td><Badge tone={item.severity.toLowerCase() as "high"|"medium"}>{item.severity}</Badge></td><td><StatusBadge status={item.status}/></td></tr>)}</tbody></table></RegisterPage>;
}

export function VendorsPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["vendors"],queryFn:()=>api<Vendor[]>("/vendors",{},vendorsDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Loading vendor exposure..."/><Card>Loading vendors...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers and dependencies."/><div className="form-error">{query.error.message}</div></div>;
  const data=query.data??(isDemo?vendorsDemo:[]);
  return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers, contract exposure, security posture, compliance, and business dependency." actions={<button className="button button-primary"><Plus size={16}/> Add vendor</button>}/>
    <div className="metric-grid"><MetricCard label="Total vendors" value={`${data.length}`} detail="5 active contracts" icon={<Network/>} tone="blue"/><MetricCard label="Critical vendors" value="3" detail="High business dependency" icon={<Building2/>} tone="purple"/><MetricCard label="High-risk vendors" value="3" detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Expiring soon" value="2" detail="Within 45 days" icon={<CalendarClock/>} tone="orange"/></div>
    <div className="vendor-grid">{data.map(vendor=><Card className="vendor-card" interactive key={vendor.id}><div className="vendor-head"><span className="vendor-logo">{vendor.name.split(" ").map(x=>x[0]).join("").slice(0,2)}</span><div><h3>{vendor.name}</h3><p>{vendor.serviceProvided}</p></div><button className="icon-button"><MoreHorizontal size={17}/></button></div><div className="vendor-score"><div><span>Risk score</span><strong>{vendor.riskScore}</strong></div><RiskBadge level={vendor.riskLevel}/></div><ProgressBar value={vendor.riskScore}/><div className="vendor-meta"><span><strong>{vendor.criticality}</strong>Criticality</span><span><strong>{vendor.securityRating}%</strong>Security rating</span><span><strong>{formatDate(vendor.contractExpiryDateUtc)}</strong>Contract expiry</span></div><div className="vendor-foot"><span className="owner-cell"><i>{initials(vendor.owner)}</i>{vendor.owner}</span><Badge tone={vendor.complianceStatus==="Compliant"?"success":"medium"}>{splitWords(vendor.complianceStatus)}</Badge></div></Card>)}</div>
  </div>;
}

export function ContinuityPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["continuity"],queryFn:()=>api<ContinuityPlan[]>("/continuity",{},continuityDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Loading recovery readiness..."/><Card>Loading continuity plan...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing."/><div className="form-error">{query.error.message}</div></div>;
  const plan=(query.data??(isDemo?continuityDemo:[]))[0];
  if(!plan)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing."/><Card>No continuity plan has been configured.</Card></div>;
  return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, disaster recovery testing, and downtime exposure." actions={<button className="button button-primary"><RefreshCw size={16}/> Record recovery test</button>}/>
    <div className="metric-grid"><MetricCard label="Continuity score" value={`${plan.continuityScore}%`} detail="Needs improvement" icon={<Activity/>} tone="orange"/><MetricCard label="Backup readiness" value="68%" detail="1 test overdue" icon={<Archive/>} tone="blue"/><MetricCard label="DR readiness" value="54%" detail="2 exercises overdue" icon={<ServerCog/>} tone="red"/><MetricCard label="Downtime exposure" value="R315k" detail="Per 24 hours" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Recovery inventory</span><h2>Critical systems</h2></div><button className="button button-secondary"><Plus size={15}/> Add system</button></div><div className="table-wrap"><table><thead><tr><th>Critical system</th><th>Owner</th><th>RTO</th><th>RPO</th><th>Backup frequency</th><th>Last DR test</th><th>Readiness</th><th>Status</th></tr></thead><tbody>{plan.criticalSystems.map(system=><tr key={system.id}><td><div className="table-primary"><span className="type-icon"><ServerCog size={17}/></span><span><strong>{system.name}</strong><small>Critical business service</small></span></div></td><td>{system.systemOwner}</td><td>{system.recoveryTimeObjectiveHours}h</td><td>{system.recoveryPointObjectiveHours}h</td><td>{system.backupFrequency}</td><td>{formatDate(system.lastDisasterRecoveryTestDateUtc)}</td><td><div className="readiness-cell"><ProgressBar value={system.continuityScore} tone="blue"/><strong>{system.continuityScore}%</strong></div></td><td><StatusBadge status={system.status}/></td></tr>)}</tbody></table></div></Card>
  </div>;
}

export function ReportsPage() {
  const [message,setMessage]=useState("");
  async function get(path:string,name:string){setMessage("");try{await downloadReport(path,name)}catch(error){setMessage(error instanceof Error?error.message:"Report unavailable.")}}
  const reports=[["Executive risk report","Board-ready exposure, trends, findings, and actions.","PDF",FileChartColumn,"/reports/executive/pdf"],["Full risk register","Risk ownership, scoring, treatment, and financial exposure.","Excel",FileSpreadsheet,"/reports/risks/excel"],["Compliance report","Framework readiness, controls, evidence, and gaps.","PDF",FileCheck2,"/reports/compliance/pdf"],["Incident register","Case status, ownership, timelines, and related risks.","CSV",Siren,"/reports/incidents/csv"],["Vendor risk report","Supplier criticality, ratings, contracts, and dependencies.","PDF",Network,"/reports/vendors/pdf"],["Audit activity log","Immutable history of material system and user actions.","CSV",LockKeyhole,"/reports/auditlogs/csv"]];
  return <div className="page-stack"><PageHeader eyebrow="Management and assurance outputs" title="Reports and exports" description="Generate professional, traceable outputs for executives, boards, auditors, and control owners."/>
    {message?<div className="attention-banner attention-info"><CircleAlert/><div><strong>Report service</strong><span>{message}</span></div></div>:null}
    <div className="report-grid">{reports.map(([title,detail,type,Icon,path])=><Card className="report-card" interactive key={title as string}><span className="report-icon"><Icon size={23}/></span><Badge tone="info">{type as string}</Badge><h3>{title as string}</h3><p>{detail as string}</p><div><button className="button button-secondary" onClick={()=>get(path as string,`${(title as string).replaceAll(" ","-")}.${(type as string).toLowerCase()==="excel"?"xlsx":(type as string).toLowerCase()}`)}><Download size={16}/> Generate report</button><button className="icon-button"><MoreHorizontal size={17}/></button></div></Card>)}</div>
    <Card className="schedule-card"><div><span className="type-icon"><CalendarClock/></span><div><h3>Scheduled executive reporting</h3><p>Automatically prepare the board summary on the first business day of each month.</p></div></div><Badge tone="success">Active</Badge><button className="button button-secondary">Manage schedule</button></Card>
  </div>;
}

export function CopilotPage() {
  const [messages,setMessages]=useState<{role:"user"|"assistant";content:string}[]>([{role:"assistant",content:"Good morning. I have the current FoodieBar risk context ready. Ask about exposure, compliance gaps, priorities, or a mitigation plan."}]);
  const [prompt,setPrompt]=useState("");
  const [loading,setLoading]=useState(false);
  async function send(value=prompt){if(!value.trim())return;setMessages(current=>[...current,{role:"user",content:value}]);setPrompt("");setLoading(true);const fallback={content:"The highest-priority exposure is privileged access without enforced multi-factor authentication. Enable MFA for administrators within 14 days, complete the overdue access review, and retain configuration evidence. Next, validate backup restoration and approve the POPIA governance policy.",responseType:"Executive summary",isMock:true,generatedAtUtc:new Date().toISOString()};try{const response=await api<typeof fallback>("/ai/copilot-chat",{method:"POST",body:JSON.stringify({prompt:value,context:"FoodieBar enterprise risk dashboard",responseType:"Executive summary"})},fallback);setMessages(current=>[...current,{role:"assistant",content:response.content}])}catch(error){setMessages(current=>[...current,{role:"assistant",content:error instanceof Error?error.message:"Copilot request failed."}])}finally{setLoading(false)}}
  return <div className="page-stack copilot-page"><PageHeader eyebrow="Grounded risk assistance" title="AI Copilot" description="Ask questions against the current risk context. Validate generated guidance with the accountable owner." actions={<Badge tone="info"><Sparkles size={13}/> Safe local fallback enabled</Badge>}/>
    <div className="copilot-layout"><Card className="copilot-chat"><div className="chat-head"><div><span className="copilot-orb"><Bot/></span><span><strong>RiskGuard Copilot</strong><small>Enterprise risk analyst</small></span></div><button className="icon-button"><MoreHorizontal/></button></div><div className="chat-body">{messages.map((message,index)=><div className={`message message-${message.role}`} key={index}>{message.role==="assistant"?<span className="mini-orb"><Bot size={16}/></span>:null}<div><p>{message.content}</p>{message.role==="assistant"?<button className="copy-action" onClick={()=>navigator.clipboard.writeText(message.content)}><Copy size={13}/> Copy response</button>:null}</div></div>)}{loading?<div className="message message-assistant"><span className="mini-orb"><Bot size={16}/></span><div className="typing"><i/><i/><i/></div></div>:null}</div><form className="chat-input" onSubmit={(e:FormEvent)=>{e.preventDefault();send()}}><textarea value={prompt} onChange={e=>setPrompt(e.target.value)} placeholder="Ask about risks, controls, priorities, or reports..."/><div><span>AI guidance requires human review</span><button className="send-button" disabled={loading}><Send size={17}/></button></div></form></Card>
      <div className="copilot-side"><Card><span className="card-kicker">Suggested prompts</span><div className="prompt-list">{["What is our biggest risk?","What should management fix first?","Explain our POPIA gaps.","Generate a 30-day mitigation plan.","Summarize this for the board."].map(item=><button key={item} onClick={()=>send(item)}><MessageSquareText size={15}/>{item}<ArrowRight size={14}/></button>)}</div></Card><Card className="context-panel"><span className="card-kicker">Active risk context</span><h3>FoodieBar · Enterprise</h3><div><span>Overall risk <strong>67 · High</strong></span><span>Open findings <strong>6</strong></span><span>Compliance <strong>64%</strong></span><span>Continuity <strong>58%</strong></span></div><small>Updated from the latest reviewed assessment.</small></Card></div>
    </div>
  </div>;
}

export function AuditPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["audit"],queryFn:()=>api<AuditLog[]>("/audit-logs",{},auditDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Loading audit evidence..."/><Card>Loading audit log...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Read-only evidence of important system and user actions."/><div className="form-error">{query.error.message}</div></div>;
  return <RegisterPage title="Audit trail" eyebrow="Immutable activity history" description="Read-only evidence of authentication, scoring, workflow, reporting, and administrative actions." action="Export CSV" actionIcon={<Download size={16}/>} metrics={[
    ["Events this month","148","Across all modules",Activity,"blue"],["User actions","92","14 active users",Users,"purple"],["System events","56","Scoring and reports",Settings,"orange"],["Failed logins","3","No lockouts triggered",KeyRound,"red"]
  ]}><table><thead><tr><th>Timestamp</th><th>User</th><th>Action</th><th>Entity</th><th>Description</th><th>IP address</th></tr></thead><tbody>{(query.data??(isDemo?auditDemo:[])).map(log=><tr key={log.id}><td>{new Date(log.createdAtUtc).toLocaleString("en-ZA")}</td><td><span className="owner-cell"><i>{initials(log.userEmail)}</i>{log.userEmail}</span></td><td><strong>{log.action}</strong></td><td><Badge tone="neutral">{log.entityType}</Badge></td><td className="wide-cell">{log.description}</td><td><code>{log.ipAddress}</code></td></tr>)}</tbody></table></RegisterPage>;
}

export function NotificationsPage() {
  const {isDemo}=useAuth();
  const queryClient=useQueryClient();
  const query=useQuery({queryKey:["notifications"],queryFn:()=>api<Notification[]>("/notifications",{},notificationsDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Loading notifications..."/><Card>Loading notifications...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Assignments, deadlines, and important alerts."/><div className="form-error">{query.error.message}</div></div>;
  async function readAll(){if(isDemo)return;await api("/notifications/read-all",{method:"POST"});await queryClient.invalidateQueries({queryKey:["notifications"]})}
  async function read(id:string){if(isDemo)return;await api(`/notifications/${id}/read`,{method:"POST"});await queryClient.invalidateQueries({queryKey:["notifications"]})}
  return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Critical alerts, assignments, deadlines, report events, and overdue actions." actions={<button className="button button-secondary" onClick={readAll}>Mark all as read</button>}/><Card className="notification-page-list">{(query.data??notificationsDemo).map(item=><Link to={item.link} onClick={()=>!item.isRead&&read(item.id)} className={`notification-page-item ${item.isRead?"read":""}`} key={item.id}><span className={`notice-icon notice-${item.severity.toLowerCase()}`}><CircleAlert size={18}/></span><span><strong>{item.title}</strong><p>{item.message}</p><small>{new Date(item.createdAtUtc).toLocaleString("en-ZA")}</small></span><ArrowRight size={17}/></Link>)}</Card></div>;
}

export function UsersPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["users"],queryFn:()=>api<UserSummary[]>("/users",{},[])});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Loading workspace users..."/><Card>Loading users...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Manage users, roles, and account status."/><div className="form-error">{query.error.message}</div></div>;
  const users=query.data??[];
  return <RegisterPage title="User management" eyebrow="Identity and access" description="Manage users, roles, departments, account status, and least-privilege access." action="Invite user" metrics={[
    ["Active users",`${users.length}`,"Current workspace",Users,"blue"],["Administrators",`${users.filter(user=>user.roles.includes("Admin")).length}`,"Privileged accounts",KeyRound,"red"],["Roles in use",`${new Set(users.flatMap(user=>user.roles)).size}`,"Least-privilege assignments",ShieldCheck,"orange"],["Directory status","Healthy","Identity API available",LockKeyhole,"green"]
  ]}><table><thead><tr><th>User</th><th>Email</th><th>Role</th><th>Department</th><th>Status</th><th></th></tr></thead><tbody>{users.map((user)=><tr key={user.id}><td><span className="owner-cell"><i>{initials(user.fullName)}</i><strong>{user.fullName}</strong></span></td><td>{user.email}</td><td><Badge tone="info">{user.roles.join(", ")}</Badge></td><td>{user.departmentId?"Assigned":"Enterprise"}</td><td><Badge tone="success">Active</Badge></td><td><button className="icon-button" aria-label={`Manage ${user.fullName}`}><MoreHorizontal size={17}/></button></td></tr>)}</tbody></table></RegisterPage>;
}

export function SettingsPage() {
  const [tab,setTab]=useState("Organization");
  return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Organization profile, risk methodology, security, integrations, and notification preferences."/><div className="settings-layout"><Card className="settings-nav">{["Organization","Risk methodology","Security","Integrations","Notifications","Profile"].map(item=><button className={tab===item?"active":""} key={item} onClick={()=>setTab(item)}>{settingIcon(item)}{item}</button>)}</Card><Card className="settings-content"><div className="settings-head"><div><h2>{tab}</h2><p>Configure {tab.toLowerCase()} preferences for the RiskGuard workspace.</p></div><button className="button button-primary">Save changes</button></div>{tab==="Organization"?<><div className="organization-banner"><span className="org-avatar org-avatar-large">FB</span><div><strong>FoodieBar</strong><span>Restaurant / Retail Operations · South Africa</span></div><button className="button button-secondary">Change logo</button></div><div className="form-grid"><label>Company name<input defaultValue="FoodieBar"/></label><label>Registration number<input defaultValue="2022/458921/07"/></label><label>Industry<input defaultValue="Restaurant / Retail Operations"/></label><label>Employee count<input type="number" defaultValue="25"/></label><label>Primary contact<input defaultValue="Anele Dlamini"/></label><label>Contact email<input defaultValue="risk@foodiebar.co.za"/></label></div><label className="field-label">Business address<textarea defaultValue="14 Market Street, Johannesburg, Gauteng"/></label></>:<SettingsPlaceholder tab={tab}/>}</Card></div></div>;
}

function SettingsPlaceholder({tab}:{tab:string}) {return <div className="settings-options">{[1,2,3].map((item)=><div key={item}><span><strong>{tab} option {item}</strong><small>Enterprise-ready configuration with secure environment defaults.</small></span><button className="toggle active"><i/></button></div>)}</div>}

function RegisterPage({title,eyebrow,description,action,actionIcon,metrics,children}:{title:string;eyebrow:string;description:string;action:string;actionIcon?:ReactNode;metrics:[string,string,string,typeof Activity,string][];children:ReactNode}) {
  return <div className="page-stack"><PageHeader eyebrow={eyebrow} title={title} description={description} actions={<button className="button button-primary">{actionIcon??<Plus size={16}/>} {action}</button>}/><div className="metric-grid metric-grid-compact">{metrics.map(([label,value,detail,Icon,tone])=><MetricCard key={label} label={label} value={value} detail={detail} icon={<Icon/>} tone={tone as "blue"|"red"|"orange"|"green"|"purple"}/>)}</div><Card className="table-card"><div className="table-toolbar"><div className="inline-search"><Search size={16}/><input placeholder={`Search ${title.toLowerCase()}...`}/></div><div className="toolbar-actions"><button className="button button-secondary"><Filter size={15}/> Filter</button><button className="icon-button"><MoreHorizontal size={18}/></button></div></div><div className="table-wrap">{children}</div></Card></div>
}

function StatusBadge({status}:{status:string}) {
  const clean=splitWords(status);
  const tone=status.toLowerCase().includes("closed")||status.toLowerCase().includes("complete")||status.toLowerCase()==="active"||status.toLowerCase()==="ready"?"success":status.toLowerCase().includes("progress")||status.toLowerCase().includes("investigat")||status.toLowerCase().includes("attention")?"info":status.toLowerCase().includes("open")||status.toLowerCase().includes("overdue")||status.toLowerCase().includes("risk")?"high":"neutral";
  return <Badge tone={tone}>{clean}</Badge>;
}

function matrixSeverity(row:number,col:number){const score=(4-row)*(col+1);return score>=12?"critical":score>=8?"high":score>=4?"medium":"low"}
function initials(value:string){return value.split(/[\s@.]+/).filter(Boolean).map(x=>x[0]).join("").slice(0,2).toUpperCase()}
function splitWords(value:string){return value.replace(/([a-z])([A-Z])/g,"$1 $2")}
function settingIcon(value:string){const Icon=value==="Organization"?Building2:value==="Security"?LockKeyhole:value==="Integrations"?Network:value==="Notifications"?Mail:value==="Profile"?UserCog:SlidersHorizontal;return <Icon size={17}/>}
