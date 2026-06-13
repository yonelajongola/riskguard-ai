import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  Archive,
  ArrowRight,
  Building2,
  CalendarClock,
  CheckCircle2,
  ChevronDown,
  CircleAlert,
  Clock3,
  Download,
  FileChartColumn,
  FileCheck2,
  FileSpreadsheet,
  Filter,
  KeyRound,
  ListFilter,
  LockKeyhole,
  Mail,
  MoreHorizontal,
  Network,
  Plus,
  RefreshCw,
  Search,
  ServerCog,
  Settings,
  ShieldCheck,
  Siren,
  SlidersHorizontal,
  UserCog,
  Users,
} from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
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
  Organization,
  Recommendation,
  Risk,
  UserSummary,
  Vendor,
} from "../types";
import { Badge, Card, ComingSoonButton, formatDate, formatMoney, MetricCard, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { useAuth } from "../context/AuthContext";

export function RisksPage() {
  const {isDemo}=useAuth();
  const query = useQuery({queryKey:["risks"],queryFn:()=>api<Risk[]>("/risks",{},risksDemo)});
  const [search,setSearch]=useState("");
  const [exporting,setExporting]=useState(false);
  const [message,setMessage]=useState("");
  const risks=(query.data??(isDemo?risksDemo:[])).filter((risk)=>risk.title.toLowerCase().includes(search.toLowerCase()));
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Loading current risks..."/><Card>Loading risk register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership and treatment status."/><div className="form-error">{query.error.message}</div></div>;
  const critical=risks.filter(risk=>risk.riskLevel==="Critical").length;
  const high=risks.filter(risk=>risk.riskLevel==="High").length;
  const exposure=risks.reduce((total,risk)=>total+risk.financialExposure,0);
  async function exportExcel(){setExporting(true);setMessage("");try{await downloadReport("/reports/risks/excel","RiskGuard-Risk-Register.xlsx")}catch(error){setMessage(error instanceof Error?error.message:"Risk register export failed.")}finally{setExporting(false)}}
  return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership, treatment status, and estimated financial impact." actions={<><button type="button" className="button button-secondary" disabled={exporting} onClick={exportExcel}><Download size={16}/> {exporting?"Exporting...":"Export Excel"}</button><ComingSoonButton className="button button-primary"><Plus size={16}/> Add risk</ComingSoonButton></>}/>
    {message?<div className="form-error">{message}</div>:null}
    <div className="metric-grid metric-grid-compact"><MetricCard label="Total risks" value={`${risks.length}`} detail="Current workspace" icon={<Siren/>} tone="blue"/><MetricCard label="Critical" value={`${critical}`} detail="Outside tolerance" icon={<CircleAlert/>} tone="red"/><MetricCard label="High" value={`${high}`} detail="Treatment required" icon={<Activity/>} tone="orange"/><MetricCard label="Gross exposure" value={formatMoney(exposure)} detail="Estimated financial impact" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="table-toolbar"><div className="inline-search"><Search size={16}/><input value={search} onChange={(e)=>setSearch(e.target.value)} placeholder="Search risk register..."/></div><div className="toolbar-actions"><ComingSoonButton><ListFilter size={15}/> Categories</ComingSoonButton><ComingSoonButton><SlidersHorizontal size={15}/> Filters</ComingSoonButton></div></div>
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
  return <div className="page-stack"><PageHeader eyebrow="Impact × likelihood" title="Enterprise risk heat map" description="See concentration, severity, and accountable departments across the current risk profile." actions={<ComingSoonButton><Filter size={15}/> Filter view</ComingSoonButton>}/>
    <div className="heatmap-layout"><Card className="heatmap-card"><div className="heatmap-y-label">Business impact</div><div className="heatmap-grid">
      {impact.map((impactLabel,row)=><div className="heat-row" key={impactLabel}><span className="axis-label">{impactLabel}</span>{likelihood.map((likelihoodLabel,col)=>{const severity=matrixSeverity(row,col);const items=heatItems.filter(risk=>risk.impact===4-row&&risk.likelihood===col+1);return <div className={`heat-cell heat-${severity}`} key={likelihoodLabel}><span>{items.length}</span>{items.map((risk)=><button type="button" disabled key={risk.id} title={`${risk.title} - Detail view coming soon`}>{risk.level.slice(0,1)}</button>)}</div>})}</div>)}
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
  return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Prioritized mitigation actions with ownership, deadlines, business impact, and control mappings." actions={<ComingSoonButton className="button button-primary"><Plus size={16}/> Add recommendation</ComingSoonButton>}/>
    <div className="recommendation-grid">{["Open","InProgress","Completed"].map((status)=><div className="kanban-column" key={status}><div className="kanban-head"><span>{status==="InProgress"?"In progress":status}</span><b>{items.filter(x=>x.status===status).length}</b></div>{items.filter(x=>x.status===status).map(item=><Card className="recommendation-card" key={item.id}><div><Badge tone={item.priority.toLowerCase() as "critical"|"high"}>{item.priority}</Badge><ComingSoonButton className="icon-button"><MoreHorizontal size={17}/></ComingSoonButton></div><h3>{item.title}</h3><p>{item.description}</p><div className="recommendation-meta"><span><CalendarClock size={14}/>{formatDate(item.dueDateUtc)}</span><span><UserCog size={14}/>{item.suggestedOwner}</span></div><div className="mapping-tags">{item.complianceMapping.split(";").map(x=><small key={x}>{x}</small>)}</div></Card>)}{items.filter(x=>x.status===status).length===0?<Card className="completed-summary"><CheckCircle2 size={30}/><strong>No {status==="InProgress"?"in-progress":status.toLowerCase()} actions</strong><span>The API returned no recommendations in this state.</span></Card>:null}</div>)}</div>
  </div>;
}

export function CompliancePage() {
  const {isDemo}=useAuth();
  const [reporting,setReporting]=useState(false);
  const [reportError,setReportError]=useState("");
  const dash=useQuery({queryKey:["compliance-dashboard"],queryFn:()=>api<ComplianceDashboard>("/compliance/dashboard",{},complianceDemo)});
  const gaps=useQuery({queryKey:["compliance-gaps"],queryFn:()=>api<ComplianceGap[]>("/compliance/gaps",{},gapsDemo)});
  const loading=dash.isLoading||gaps.isLoading;
  const error=dash.error??gaps.error;
  if(loading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Loading compliance readiness..."/><Card>Loading compliance data...</Card></div>;
  if(error&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Readiness, control performance, and remediation."/><div className="form-error">{error.message}</div></div>;
  const data=dash.data??complianceDemo;
  async function downloadCompliance(){setReporting(true);setReportError("");try{await downloadReport("/reports/compliance/pdf","RiskGuard-Compliance-Report.pdf")}catch(requestError){setReportError(requestError instanceof Error?requestError.message:"Compliance report failed.")}finally{setReporting(false)}}
  return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Readiness, control performance, evidence status, and remediation across five frameworks." actions={<button type="button" className="button button-primary" disabled={reporting} onClick={downloadCompliance}><FileCheck2 size={16}/> {reporting?"Generating...":"Download compliance report"}</button>}/>
    {reportError?<div className="form-error">{reportError}</div>:null}
    <div className="metric-grid"><MetricCard label="Overall readiness" value={`${data.readiness}%`} detail="Across 5 frameworks" icon={<ShieldCheck/>} tone="blue" trend={4}/><MetricCard label="Controls passed" value={`${data.passed}`} detail="Evidence accepted" icon={<CheckCircle2/>} tone="green"/><MetricCard label="Failed controls" value={`${data.failed}`} detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Evidence missing" value={`${data.missing}`} detail="Audit attention" icon={<Archive/>} tone="orange"/></div>
    <div className="dashboard-grid"><Card className="span-2"><div className="card-head"><div><span className="card-kicker">Framework readiness</span><h2>Assurance coverage</h2></div></div><div className="framework-readiness">{data.frameworks.map((item,index)=><div key={item.category}><span className="framework-icon">{item.category.slice(0,2)}</span><span><strong>{item.category}</strong><small>{[8,4,12,9,10][index]} controls assessed</small></span><ProgressBar value={item.score} tone="blue"/><b>{item.score}%</b></div>)}</div></Card><Card className="compliance-donut"><div className="card-head"><div><span className="card-kicker">Control results</span><h2>Current status</h2></div></div><div className="donut-chart"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={[{name:"Passed",value:data.passed},{name:"Failed",value:data.failed},{name:"Missing",value:data.missing}]} dataKey="value" innerRadius={55} outerRadius={80} paddingAngle={4}><Cell fill="#22c55e"/><Cell fill="#ef4444"/><Cell fill="#eab308"/></Pie></PieChart></ResponsiveContainer><div><strong>{data.passed+data.failed+data.missing}</strong><span>Controls</span></div></div></Card></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Remediation register</span><h2>Compliance gaps</h2></div><Link to="/app/compliance/gaps">View all <ArrowRight size={15}/></Link></div><div className="table-wrap"><table><thead><tr><th>Framework / control</th><th>Gap</th><th>Severity</th><th>Owner</th><th>Due date</th><th>Status</th></tr></thead><tbody>{(gaps.data??gapsDemo).map(gap=><tr key={gap.id}><td><strong>{gap.control?.framework?.name}</strong><small className="block-muted">{gap.control?.code} · {gap.control?.title}</small></td><td className="wide-cell">{gap.description}</td><td><Badge tone={gap.severity.toLowerCase() as "high"|"medium"}>{gap.severity}</Badge></td><td>{gap.owner}</td><td>{formatDate(gap.dueDateUtc)}</td><td><StatusBadge status={gap.status}/></td></tr>)}</tbody></table></div></Card>
  </div>;
}

export function IncidentsPage() {
  const {isDemo}=useAuth();
  const {id}=useParams();
  const detail=useQuery({queryKey:["incident",id],enabled:Boolean(id),queryFn:()=>api<Incident>(`/incidents/${id}`,{},incidentsDemo.find((item)=>item.id===id)??incidentsDemo[0])});
  const query=useQuery({queryKey:["incidents"],queryFn:()=>api<Incident[]>("/incidents",{},incidentsDemo)});
  if(id){
    if(detail.isLoading)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident details" description="Loading incident..."/><Card>Loading incident...</Card></div>;
    if(detail.isError)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident details" description="The requested incident could not be loaded."/><div className="form-error">{detail.error.message}</div><Link className="button button-secondary" to="/app/incidents">Back to incidents</Link></div>;
    const incident=detail.data;
    if(!incident)return null;
    return <div className="page-stack"><PageHeader eyebrow="Case and event response" title={incident.title} description={incident.description||"Incident details and current workflow status."} actions={<Link className="button button-secondary" to="/app/incidents">Back to incidents</Link>}/><div className="metric-grid metric-grid-compact"><MetricCard label="Severity" value={incident.severity} detail="Current classification" icon={<Siren/>} tone="red"/><MetricCard label="Status" value={splitWords(incident.status)} detail="Workflow state" icon={<Activity/>} tone="blue"/><MetricCard label="Owner" value={incident.owner} detail={incident.department?.name||"Enterprise"} icon={<UserCog/>} tone="purple"/><MetricCard label="Detected" value={formatDate(incident.detectedAtUtc)} detail="Event timestamp" icon={<Clock3/>} tone="orange"/></div><Card className="form-card"><div className="card-head"><div><span className="card-kicker">Evidence and response</span><h2>Incident record</h2></div><Badge tone={incident.severity.toLowerCase() as "high"|"medium"}>{incident.severity}</Badge></div><p className="muted">{incident.evidenceNotes||"No evidence notes have been recorded."}</p></Card></div>;
  }
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Loading incidents..."/><Card>Loading incident register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Track investigation, mitigation, and closure."/><div className="form-error">{query.error.message}</div></div>;
  return <RegisterPage title="Incident management" eyebrow="Case and event response" description="Track detection, investigation, mitigation, evidence, and closure against related risks." action="Create incident" metrics={[
    ["Open incidents","4","3 high severity",Siren,"red"],["Investigating","2","Active response",Search,"orange"],["Mean time to resolve","3.2d","12% improvement",Clock3,"blue"],["Closed this quarter","7","Evidence retained",CheckCircle2,"green"]
  ]}><table><thead><tr><th>Incident</th><th>Category</th><th>Department</th><th>Owner</th><th>Detected</th><th>Severity</th><th>Status</th></tr></thead><tbody>{(query.data??incidentsDemo).map(item=><tr key={item.id}><td><Link className="cell-link" to={`/app/incidents/${item.id}`}><span className="type-icon type-danger"><Siren size={17}/></span><span><strong>{item.title}</strong><small>INC-{item.id.toUpperCase()}</small></span></Link></td><td>{item.category}</td><td>{item.department?.name}</td><td>{item.owner}</td><td>{formatDate(item.detectedAtUtc)}</td><td><Badge tone={item.severity.toLowerCase() as "high"|"medium"}>{item.severity}</Badge></td><td><StatusBadge status={item.status}/></td></tr>)}</tbody></table></RegisterPage>;
}

export function VendorsPage() {
  const {isDemo}=useAuth();
  const {id}=useParams();
  const detail=useQuery({queryKey:["vendor",id],enabled:Boolean(id),queryFn:()=>api<Vendor>(`/vendors/${id}`,{},vendorsDemo.find((item)=>item.id===id)??vendorsDemo[0])});
  const query=useQuery({queryKey:["vendors"],queryFn:()=>api<Vendor[]>("/vendors",{},vendorsDemo)});
  if(id){
    if(detail.isLoading)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor details" description="Loading vendor..."/><Card>Loading vendor...</Card></div>;
    if(detail.isError)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor details" description="The requested vendor could not be loaded."/><div className="form-error">{detail.error.message}</div><Link className="button button-secondary" to="/app/vendors">Back to vendors</Link></div>;
    const vendor=detail.data;
    if(!vendor)return null;
    return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title={vendor.name} description={vendor.serviceProvided} actions={<Link className="button button-secondary" to="/app/vendors">Back to vendors</Link>}/><div className="metric-grid metric-grid-compact"><MetricCard label="Risk score" value={`${vendor.riskScore}`} detail={vendor.riskLevel} icon={<CircleAlert/>} tone="red"/><MetricCard label="Security rating" value={`${vendor.securityRating}%`} detail="Current rating" icon={<ShieldCheck/>} tone="blue"/><MetricCard label="Criticality" value={vendor.criticality} detail="Business dependency" icon={<Building2/>} tone="purple"/><MetricCard label="Contract expiry" value={formatDate(vendor.contractExpiryDateUtc)} detail={vendor.complianceStatus} icon={<CalendarClock/>} tone="orange"/></div><Card className="form-card"><div className="card-head"><div><span className="card-kicker">Vendor record</span><h2>Assurance notes</h2></div><RiskBadge level={vendor.riskLevel}/></div><p className="muted">{vendor.notes||"No vendor notes have been recorded."}</p></Card></div>;
  }
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Loading vendor exposure..."/><Card>Loading vendors...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers and dependencies."/><div className="form-error">{query.error.message}</div></div>;
  const data=query.data??(isDemo?vendorsDemo:[]);
  return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers, contract exposure, security posture, compliance, and business dependency." actions={<ComingSoonButton className="button button-primary"><Plus size={16}/> Add vendor</ComingSoonButton>}/>
    <div className="metric-grid"><MetricCard label="Total vendors" value={`${data.length}`} detail="5 active contracts" icon={<Network/>} tone="blue"/><MetricCard label="Critical vendors" value="3" detail="High business dependency" icon={<Building2/>} tone="purple"/><MetricCard label="High-risk vendors" value="3" detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Expiring soon" value="2" detail="Within 45 days" icon={<CalendarClock/>} tone="orange"/></div>
    <div className="vendor-grid">{data.map(vendor=><Card className="vendor-card" key={vendor.id}><div className="vendor-head"><span className="vendor-logo">{vendor.name.split(" ").map(x=>x[0]).join("").slice(0,2)}</span><div><h3>{vendor.name}</h3><p>{vendor.serviceProvided}</p></div><Link className="icon-button" aria-label={`View ${vendor.name}`} to={`/app/vendors/${vendor.id}`}><ArrowRight size={17}/></Link></div><div className="vendor-score"><div><span>Risk score</span><strong>{vendor.riskScore}</strong></div><RiskBadge level={vendor.riskLevel}/></div><ProgressBar value={vendor.riskScore}/><div className="vendor-meta"><span><strong>{vendor.criticality}</strong>Criticality</span><span><strong>{vendor.securityRating}%</strong>Security rating</span><span><strong>{formatDate(vendor.contractExpiryDateUtc)}</strong>Contract expiry</span></div><div className="vendor-foot"><span className="owner-cell"><i>{initials(vendor.owner)}</i>{vendor.owner}</span><Badge tone={vendor.complianceStatus==="Compliant"?"success":"medium"}>{splitWords(vendor.complianceStatus)}</Badge></div></Card>)}</div>
  </div>;
}

export function ContinuityPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["continuity"],queryFn:()=>api<ContinuityPlan[]>("/continuity",{},continuityDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Loading recovery readiness..."/><Card>Loading continuity plan...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing."/><div className="form-error">{query.error.message}</div></div>;
  const plan=(query.data??(isDemo?continuityDemo:[]))[0];
  if(!plan)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing."/><Card>No continuity plan has been configured.</Card></div>;
  return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, disaster recovery testing, and downtime exposure." actions={<ComingSoonButton className="button button-primary"><RefreshCw size={16}/> Record recovery test</ComingSoonButton>}/>
    <div className="metric-grid"><MetricCard label="Continuity score" value={`${plan.continuityScore}%`} detail="Needs improvement" icon={<Activity/>} tone="orange"/><MetricCard label="Backup readiness" value="68%" detail="1 test overdue" icon={<Archive/>} tone="blue"/><MetricCard label="DR readiness" value="54%" detail="2 exercises overdue" icon={<ServerCog/>} tone="red"/><MetricCard label="Downtime exposure" value="R315k" detail="Per 24 hours" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Recovery inventory</span><h2>Critical systems</h2></div><ComingSoonButton><Plus size={15}/> Add system</ComingSoonButton></div><div className="table-wrap"><table><thead><tr><th>Critical system</th><th>Owner</th><th>RTO</th><th>RPO</th><th>Backup frequency</th><th>Last DR test</th><th>Readiness</th><th>Status</th></tr></thead><tbody>{plan.criticalSystems.map(system=><tr key={system.id}><td><div className="table-primary"><span className="type-icon"><ServerCog size={17}/></span><span><strong>{system.name}</strong><small>Critical business service</small></span></div></td><td>{system.systemOwner}</td><td>{system.recoveryTimeObjectiveHours}h</td><td>{system.recoveryPointObjectiveHours}h</td><td>{system.backupFrequency}</td><td>{formatDate(system.lastDisasterRecoveryTestDateUtc)}</td><td><div className="readiness-cell"><ProgressBar value={system.continuityScore} tone="blue"/><strong>{system.continuityScore}%</strong></div></td><td><StatusBadge status={system.status}/></td></tr>)}</tbody></table></div></Card>
  </div>;
}

export function ReportsPage() {
  const {user}=useAuth();
  const [message,setMessage]=useState("");
  const [activePath,setActivePath]=useState("");
  const reports=[
    {title:"Executive risk report",detail:"Board-ready exposure, trends, findings, and actions.",type:"PDF",Icon:FileChartColumn,path:"/reports/executive/pdf",file:"RiskGuard-Executive-Risk-Report.pdf"},
    {title:"Full risk register",detail:"Risk ownership, scoring, treatment, and financial exposure.",type:"Excel",Icon:FileSpreadsheet,path:"/reports/risks/excel",file:"RiskGuard-Risk-Register.xlsx"},
    {title:"Compliance report",detail:"Framework readiness, controls, evidence, and gaps.",type:"PDF",Icon:FileCheck2,path:"/reports/compliance/pdf",file:"RiskGuard-Compliance-Report.pdf"},
    {title:"Incident register",detail:"Case status, ownership, timelines, and related risks.",type:"CSV",Icon:Siren,path:"/reports/incidents/csv",file:"RiskGuard-Incident-Register.csv"},
    {title:"Vendor risk report",detail:"Supplier criticality, ratings, contracts, and dependencies.",type:"PDF",Icon:Network,path:"/reports/vendors/pdf",file:"RiskGuard-Vendor-Risk-Report.pdf"},
    {title:"Audit activity log",detail:"Immutable history of material system and user actions.",type:"CSV",Icon:LockKeyhole,path:"/reports/auditlogs/csv",file:"RiskGuard-Audit-Activity.csv",roles:["Admin","Auditor"]},
  ];
  async function get(path:string,name:string){
    setMessage("");
    setActivePath(path);
    try{await downloadReport(path,name)}
    catch(error){setMessage(error instanceof Error?error.message:"Report unavailable.")}
    finally{setActivePath("")}
  }
  return <div className="page-stack"><PageHeader eyebrow="Management and assurance outputs" title="Reports and exports" description="Generate professional, traceable outputs for executives, boards, auditors, and control owners."/>
    {message?<div className="attention-banner attention-info"><CircleAlert/><div><strong>Report service</strong><span>{message}</span></div></div>:null}
    <div className="report-grid">{reports.map(({title,detail,type,Icon,path,file,roles})=>{const allowed=!roles||roles.some((role)=>user?.roles.includes(role));return <Card className="report-card" key={title}><span className="report-icon"><Icon size={23}/></span><Badge tone="info">{type}</Badge><h3>{title}</h3><p>{detail}</p><div><button type="button" className="button button-secondary" disabled={Boolean(activePath)||!allowed} title={allowed?`Generate ${title}`:"Your role cannot export the audit log."} onClick={()=>get(path,file)}><Download size={16}/> {!allowed?"Restricted":activePath===path?"Preparing...":type==="PDF"?"Download PDF":`Export ${type}`}</button><ComingSoonButton className="icon-button"><MoreHorizontal size={17}/></ComingSoonButton></div></Card>})}</div>
    <Card className="schedule-card"><div><span className="type-icon"><CalendarClock/></span><div><h3>Scheduled executive reporting</h3><p>Automated report scheduling is not configured in this deployment.</p></div></div><Badge tone="neutral">Not configured</Badge><ComingSoonButton>Manage schedule</ComingSoonButton></Card>
  </div>;
}

export function AuditPage() {
  const {isDemo}=useAuth();
  const [exporting,setExporting]=useState(false);
  const [exportError,setExportError]=useState("");
  const query=useQuery({queryKey:["audit"],queryFn:()=>api<AuditLog[]>("/audit-logs",{},auditDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Loading audit evidence..."/><Card>Loading audit log...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Read-only evidence of important system and user actions."/><div className="form-error">{query.error.message}</div></div>;
  async function exportAudit(){setExporting(true);setExportError("");try{await downloadReport("/reports/auditlogs/csv","RiskGuard-Audit-Activity.csv")}catch(error){setExportError(error instanceof Error?error.message:"Audit export failed.")}finally{setExporting(false)}}
  return <><RegisterPage title="Audit trail" eyebrow="Immutable activity history" description="Read-only evidence of authentication, scoring, workflow, reporting, and administrative actions." action={exporting?"Exporting...":"Export CSV"} actionIcon={<Download size={16}/>} onAction={exportAudit} actionDisabled={exporting} metrics={[
    ["Events this month","148","Across all modules",Activity,"blue"],["User actions","92","14 active users",Users,"purple"],["System events","56","Scoring and reports",Settings,"orange"],["Failed logins","3","No lockouts triggered",KeyRound,"red"]
  ]}><table><thead><tr><th>Timestamp</th><th>User</th><th>Action</th><th>Entity</th><th>Description</th><th>IP address</th></tr></thead><tbody>{(query.data??(isDemo?auditDemo:[])).map(log=><tr key={log.id}><td>{new Date(log.createdAtUtc).toLocaleString("en-ZA")}</td><td><span className="owner-cell"><i>{initials(log.userEmail)}</i>{log.userEmail}</span></td><td><strong>{log.action}</strong></td><td><Badge tone="neutral">{log.entityType}</Badge></td><td className="wide-cell">{log.description}</td><td><code>{log.ipAddress}</code></td></tr>)}</tbody></table></RegisterPage>{exportError?<div className="form-error">{exportError}</div>:null}</>;
}

export function NotificationsPage() {
  const {isDemo}=useAuth();
  const navigate=useNavigate();
  const queryClient=useQueryClient();
  const [updating,setUpdating]=useState("");
  const [actionError,setActionError]=useState("");
  const query=useQuery({queryKey:["notifications"],queryFn:()=>api<Notification[]>("/notifications",{},notificationsDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Loading notifications..."/><Card>Loading notifications...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Assignments, deadlines, and important alerts."/><div className="form-error">{query.error.message}</div></div>;
  async function readAll(){if(isDemo)return;setUpdating("all");setActionError("");try{await api("/notifications/read-all",{method:"POST"});await queryClient.invalidateQueries({queryKey:["notifications"]})}catch(error){setActionError(error instanceof Error?error.message:"Notifications could not be updated.")}finally{setUpdating("")}}
  async function openNotification(item:Notification){setUpdating(item.id);setActionError("");try{if(!isDemo&&!item.isRead){await api(`/notifications/${item.id}/read`,{method:"POST"});await queryClient.invalidateQueries({queryKey:["notifications"]})}navigate(item.link)}catch(error){setActionError(error instanceof Error?error.message:"Notification could not be opened.")}finally{setUpdating("")}}
  const items=query.data??(isDemo?notificationsDemo:[]);
  const hasUnread=items.some((item)=>!item.isRead);
  return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Critical alerts, assignments, deadlines, report events, and overdue actions." actions={<button type="button" className="button button-secondary" disabled={isDemo||!hasUnread||updating==="all"} onClick={readAll}>{updating==="all"?"Updating...":"Mark all as read"}</button>}/>{actionError?<div className="form-error">{actionError}</div>:null}<Card className="notification-page-list">{items.map(item=><button type="button" onClick={()=>openNotification(item)} disabled={updating===item.id} className={`notification-page-item ${item.isRead?"read":""}`} key={item.id}><span className={`notice-icon notice-${item.severity.toLowerCase()}`}><CircleAlert size={18}/></span><span><strong>{item.title}</strong><p>{item.message}</p><small>{new Date(item.createdAtUtc).toLocaleString("en-ZA")}</small></span><ArrowRight size={17}/></button>)}</Card></div>;
}

export function UsersPage() {
  const {isDemo}=useAuth();
  const query=useQuery({queryKey:["users"],queryFn:()=>api<UserSummary[]>("/users",{},[])});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Loading workspace users..."/><Card>Loading users...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Manage users, roles, and account status."/><div className="form-error">{query.error.message}</div></div>;
  const users=query.data??[];
  return <RegisterPage title="User management" eyebrow="Identity and access" description="Manage users, roles, departments, account status, and least-privilege access." action="Invite user" metrics={[
    ["Active users",`${users.length}`,"Current workspace",Users,"blue"],["Administrators",`${users.filter(user=>user.roles.includes("Admin")).length}`,"Privileged accounts",KeyRound,"red"],["Roles in use",`${new Set(users.flatMap(user=>user.roles)).size}`,"Least-privilege assignments",ShieldCheck,"orange"],["Directory status","Healthy","Identity API available",LockKeyhole,"green"]
  ]}><table><thead><tr><th>User</th><th>Email</th><th>Role</th><th>Department</th><th>Status</th><th></th></tr></thead><tbody>{users.map((user)=><tr key={user.id}><td><span className="owner-cell"><i>{initials(user.fullName)}</i><strong>{user.fullName}</strong></span></td><td>{user.email}</td><td><Badge tone="info">{user.roles.join(", ")}</Badge></td><td>{user.departmentId?"Assigned":"Enterprise"}</td><td><Badge tone="success">Active</Badge></td><td><ComingSoonButton className="icon-button" aria-label={`Manage ${user.fullName}`}><MoreHorizontal size={17}/></ComingSoonButton></td></tr>)}</tbody></table></RegisterPage>;
}

export function SettingsPage() {
  const {user,isDemo}=useAuth();
  const queryClient=useQueryClient();
  const [tab,setTab]=useState("Organization");
  const organizationQuery=useQuery({queryKey:["organizations"],queryFn:()=>api<Organization[]>("/organizations",{},[])});
  const organization=organizationQuery.data?.[0];
  const [form,setForm]=useState({name:"",industry:"",country:"",employeeCount:0,registrationNumber:"",primaryContact:"",email:"",phone:"",address:""});
  const [saving,setSaving]=useState(false);
  const [message,setMessage]=useState("");
  useEffect(()=>{if(!organization)return;setForm({name:organization.name,industry:organization.industry,country:organization.country,employeeCount:organization.employeeCount,registrationNumber:organization.registrationNumber,primaryContact:organization.primaryContact,email:organization.email,phone:organization.phone,address:organization.address})},[organization]);
  const canSave=tab==="Organization"&&!isDemo&&Boolean(user?.roles.includes("Admin"))&&Boolean(organization);
  async function save(){if(!organization||!canSave)return;setSaving(true);setMessage("");try{await api(`/organizations/${organization.id}`,{method:"PUT",body:JSON.stringify(form)});await queryClient.invalidateQueries({queryKey:["organizations"]});setMessage("Organization settings saved.")}catch(error){setMessage(error instanceof Error?error.message:"Settings could not be saved.")}finally{setSaving(false)}}
  if(organizationQuery.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Loading workspace settings..."/><Card>Loading settings...</Card></div>;
  if(organizationQuery.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Organization profile, risk methodology, security, integrations, and notification preferences."/><div className="form-error">{organizationQuery.error.message}</div></div>;
  return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Organization profile, risk methodology, security, integrations, and notification preferences."/><div className="settings-layout"><Card className="settings-nav">{["Organization","Risk methodology","Security","Integrations","Notifications","Profile"].map(item=><button type="button" className={tab===item?"active":""} key={item} onClick={()=>{setTab(item);setMessage("")}}>{settingIcon(item)}{item}</button>)}</Card><Card className="settings-content"><div className="settings-head"><div><h2>{tab}</h2><p>Configure {tab.toLowerCase()} preferences for the RiskGuard workspace.</p></div>{canSave?<button type="button" className="button button-primary" disabled={saving} onClick={save}>{saving?"Saving...":"Save changes"}</button>:<ComingSoonButton className="button button-primary">Save changes</ComingSoonButton>}</div>{message?<div className={message.includes("saved")?"attention-banner attention-info":"form-error"}>{message}</div>:null}{tab==="Organization"?<><div className="organization-banner"><span className="org-avatar org-avatar-large">{initials(form.name||"RiskGuard")}</span><div><strong>{form.name||"RiskGuard workspace"}</strong><span>{form.industry||"Industry not set"} · {form.country||"Country not set"}</span></div><ComingSoonButton>Change logo</ComingSoonButton></div><div className="form-grid"><label>Company name<input value={form.name} disabled={!canSave} onChange={(event)=>setForm({...form,name:event.target.value})}/></label><label>Registration number<input value={form.registrationNumber} disabled={!canSave} onChange={(event)=>setForm({...form,registrationNumber:event.target.value})}/></label><label>Industry<input value={form.industry} disabled={!canSave} onChange={(event)=>setForm({...form,industry:event.target.value})}/></label><label>Country<input value={form.country} disabled={!canSave} onChange={(event)=>setForm({...form,country:event.target.value})}/></label><label>Employee count<input type="number" min="0" value={form.employeeCount} disabled={!canSave} onChange={(event)=>setForm({...form,employeeCount:Number(event.target.value)})}/></label><label>Primary contact<input value={form.primaryContact} disabled={!canSave} onChange={(event)=>setForm({...form,primaryContact:event.target.value})}/></label><label>Contact email<input type="email" value={form.email} disabled={!canSave} onChange={(event)=>setForm({...form,email:event.target.value})}/></label><label>Phone<input value={form.phone} disabled={!canSave} onChange={(event)=>setForm({...form,phone:event.target.value})}/></label></div><label className="field-label">Business address<textarea value={form.address} disabled={!canSave} onChange={(event)=>setForm({...form,address:event.target.value})}/></label></>:<SettingsPlaceholder tab={tab}/>}</Card></div></div>;
}

function SettingsPlaceholder({tab}:{tab:string}) {return <div className="settings-options">{[1,2,3].map((item)=><div key={item}><span><strong>{tab} option {item}</strong><small>This configuration workflow is coming soon.</small></span><button type="button" className="toggle" disabled title="Coming soon"><i/></button></div>)}</div>}

function RegisterPage({title,eyebrow,description,action,actionIcon,onAction,actionDisabled=false,metrics,children}:{title:string;eyebrow:string;description:string;action:string;actionIcon?:ReactNode;onAction?:()=>void|Promise<void>;actionDisabled?:boolean;metrics:[string,string,string,typeof Activity,string][];children:ReactNode}) {
  const actionButton=onAction
    ? <button type="button" className="button button-primary" disabled={actionDisabled} onClick={onAction}>{actionIcon??<Plus size={16}/>} {action}</button>
    : <ComingSoonButton className="button button-primary">{actionIcon??<Plus size={16}/>} {action}</ComingSoonButton>;
  return <div className="page-stack"><PageHeader eyebrow={eyebrow} title={title} description={description} actions={actionButton}/><div className="metric-grid metric-grid-compact">{metrics.map(([label,value,detail,Icon,tone])=><MetricCard key={label} label={label} value={value} detail={detail} icon={<Icon/>} tone={tone as "blue"|"red"|"orange"|"green"|"purple"}/>)}</div><Card className="table-card"><div className="table-toolbar"><div className="inline-search" title="Coming soon"><Search size={16}/><input disabled placeholder={`Search ${title.toLowerCase()} - Coming soon`}/></div><div className="toolbar-actions"><ComingSoonButton><Filter size={15}/> Filter</ComingSoonButton><ComingSoonButton className="icon-button"><MoreHorizontal size={18}/></ComingSoonButton></div></div><div className="table-wrap">{children}</div></Card></div>
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
