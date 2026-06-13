import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
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
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
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
  Department,
  HeatMapItem,
  Incident,
  Notification,
  Organization,
  Recommendation,
  Risk,
  UserSummary,
  Vendor,
} from "../types";
import { Badge, Card, formatDate, formatMoney, MetricCard, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { Modal, ModalActions } from "../components/Modal";
import { useAuth } from "../context/AuthContext";

export function RisksPage() {
  const {isDemo}=useAuth();
  const {id}=useParams();
  const query = useQuery({queryKey:["risks"],queryFn:()=>api<Risk[]>("/risks",{},risksDemo)});
  const detail=useQuery({queryKey:["risk",id],enabled:Boolean(id),queryFn:()=>api<Risk>(`/risks/${id}`,{},risksDemo.find((item)=>item.id===id)??risksDemo[0])});
  const [search,setSearch]=useState("");
  const [category,setCategory]=useState("All");
  const [level,setLevel]=useState("All");
  const [status,setStatus]=useState("All");
  const [showFilters,setShowFilters]=useState(false);
  const [exporting,setExporting]=useState(false);
  const [message,setMessage]=useState("");
  if(id){
    if(detail.isLoading&&!isDemo)return <Card>Loading risk...</Card>;
    if(detail.isError&&!isDemo)return <div className="form-error">{detail.error.message}</div>;
    const risk=detail.data;
    if(!risk)return null;
    return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title={risk.title} description={risk.description||"Risk detail and current treatment context."} actions={<Link className="button button-secondary" to="/app/risks">Back to register</Link>}/><div className="metric-grid metric-grid-compact"><MetricCard label="Risk score" value={`${risk.score}`} detail={risk.riskLevel} icon={<Siren/>} tone="red"/><MetricCard label="Impact" value={`${risk.impact}/4`} detail="Business impact" icon={<Activity/>} tone="orange"/><MetricCard label="Likelihood" value={`${risk.likelihood}/4`} detail="Current likelihood" icon={<CircleAlert/>} tone="purple"/><MetricCard label="Exposure" value={formatMoney(risk.financialExposure)} detail={risk.status} icon={<FileChartColumn/>} tone="blue"/></div><Card><div className="card-head"><div><span className="card-kicker">{risk.category}</span><h2>Treatment ownership</h2></div><RiskBadge level={risk.riskLevel}/></div><p>{risk.description}</p><div className="recommendation-meta"><span><UserCog size={14}/>{risk.owner}</span><span><Building2 size={14}/>{risk.department?.name??"Enterprise"}</span></div></Card></div>;
  }
  const all=query.data??(isDemo?risksDemo:[]);
  const risks=all.filter((risk)=>risk.title.toLowerCase().includes(search.toLowerCase())&&(category==="All"||risk.category===category)&&(level==="All"||risk.riskLevel===level)&&(status==="All"||risk.status===status));
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Loading current risks..."/><Card>Loading risk register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership and treatment status."/><div className="form-error">{query.error.message}</div></div>;
  const critical=risks.filter(risk=>risk.riskLevel==="Critical").length;
  const high=risks.filter(risk=>risk.riskLevel==="High").length;
  const exposure=risks.reduce((total,risk)=>total+risk.financialExposure,0);
  async function exportExcel(){setExporting(true);setMessage("");try{await downloadReport("/reports/risks/excel","RiskGuard-Risk-Register.xlsx")}catch(error){setMessage(error instanceof Error?error.message:"Risk register export failed.")}finally{setExporting(false)}}
  return <div className="page-stack"><PageHeader eyebrow="Enterprise register" title="Risk register" description="Prioritized exposure with ownership, treatment status, and estimated financial impact." actions={!isDemo?<><button type="button" className="button button-secondary" disabled={exporting} onClick={exportExcel}><Download size={16}/> {exporting?"Exporting...":"Export Excel"}</button><Link className="button button-primary" to="/app/assessments/new"><Plus size={16}/> Add risk assessment</Link></>:undefined}/>
    {message?<div className="form-error">{message}</div>:null}
    <div className="metric-grid metric-grid-compact"><MetricCard label="Total risks" value={`${risks.length}`} detail="Current workspace" icon={<Siren/>} tone="blue"/><MetricCard label="Critical" value={`${critical}`} detail="Outside tolerance" icon={<CircleAlert/>} tone="red"/><MetricCard label="High" value={`${high}`} detail="Treatment required" icon={<Activity/>} tone="orange"/><MetricCard label="Gross exposure" value={formatMoney(exposure)} detail="Estimated financial impact" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="table-toolbar"><div className="inline-search"><Search size={16}/><input value={search} onChange={(e)=>setSearch(e.target.value)} placeholder="Search risk register..."/></div><div className="toolbar-actions"><button type="button" className="button button-secondary" onClick={()=>setShowFilters((value)=>!value)}><ListFilter size={15}/> {showFilters?"Hide filters":"Categories and filters"}</button></div></div>
      {showFilters?<div className="filter-panel"><label>Category<select value={category} onChange={(event)=>setCategory(event.target.value)}><option>All</option>{[...new Set(all.map((risk)=>risk.category))].map((item)=><option key={item}>{item}</option>)}</select></label><label>Risk level<select value={level} onChange={(event)=>setLevel(event.target.value)}><option>All</option>{["Low","Medium","High","Critical"].map((item)=><option key={item}>{item}</option>)}</select></label><label>Status<select value={status} onChange={(event)=>setStatus(event.target.value)}><option>All</option>{[...new Set(all.map((risk)=>risk.status))].map((item)=><option key={item}>{item}</option>)}</select></label><button type="button" className="button button-secondary" onClick={()=>{setCategory("All");setLevel("All");setStatus("All")}}>Reset</button></div>:null}
      <div className="table-wrap"><table><thead><tr><th>Risk statement</th><th>Category</th><th>Department</th><th>Owner</th><th>Score</th><th>Status</th><th>Exposure</th></tr></thead><tbody>{risks.map(risk=><tr key={risk.id}><td><Link className="cell-link" to={`/app/risks/${risk.id}`}><span className={`severity-bar severity-${risk.riskLevel.toLowerCase()}`}/><span><strong>{risk.title}</strong><small>ID · {risk.id.toUpperCase()}</small></span></Link></td><td>{risk.category}</td><td>{risk.department?.name}</td><td><span className="owner-cell"><i>{initials(risk.owner)}</i>{risk.owner}</span></td><td><span className="score-cell"><strong>{risk.score}</strong><RiskBadge level={risk.riskLevel}/></span></td><td><StatusBadge status={risk.status}/></td><td>{formatMoney(risk.financialExposure)}</td></tr>)}</tbody></table></div>
    </Card></div>;
}

export function HeatMapPage() {
  const {isDemo}=useAuth();
  const [department,setDepartment]=useState("All");
  const [selected,setSelected]=useState<HeatMapItem[]>([]);
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
  const all=query.data??[];
  const heatItems=all.filter((item)=>department==="All"||item.department===department);
  const selectedItems=selected.length?selected:heatItems.filter((risk)=>risk.impact>=3&&risk.likelihood>=3);
  return <div className="page-stack"><PageHeader eyebrow="Impact × likelihood" title="Enterprise risk heat map" description="See concentration, severity, and accountable departments across the current risk profile." actions={<label className="button button-secondary">Department<select aria-label="Filter heat map by department" value={department} onChange={(event)=>{setDepartment(event.target.value);setSelected([])}}><option>All</option>{[...new Set(all.map((item)=>item.department))].map((item)=><option key={item}>{item}</option>)}</select></label>}/>
    <div className="heatmap-layout"><Card className="heatmap-card"><div className="heatmap-y-label">Business impact</div><div className="heatmap-grid">
      {impact.map((impactLabel,row)=><div className="heat-row" key={impactLabel}><span className="axis-label">{impactLabel}</span>{likelihood.map((likelihoodLabel,col)=>{const severity=matrixSeverity(row,col);const items=heatItems.filter(risk=>risk.impact===4-row&&risk.likelihood===col+1);return <button type="button" className={`heat-cell heat-${severity}`} key={likelihoodLabel} onClick={()=>setSelected(items)} title={`Show ${items.length} risks in ${impactLabel} impact, ${likelihoodLabel} likelihood`}><span>{items.length}</span>{items.map((risk)=><i key={risk.id}>{risk.level.slice(0,1)}</i>)}</button>})}</div>)}
      <div className="heat-x-row"><span/>{likelihood.map(item=><span key={item}>{item}</span>)}</div><div className="heat-x-label">Likelihood</div>
    </div></Card><Card className="heatmap-side"><div className="card-head"><div><span className="card-kicker">Selected zone</span><h2>Risk concentration</h2></div><Badge tone={selectedItems.some((item)=>item.level==="Critical")?"critical":"info"}>{selectedItems.length} risks</Badge></div>{selectedItems.map(risk=><Link className="heat-risk" to={`/app/risks/${risk.id}`} key={risk.id}><div><RiskBadge level={risk.level}/><strong>{risk.title}</strong><span>{risk.department}</span></div><ArrowRight size={16}/></Link>)}{selectedItems.length===0?<p className="muted">Select a populated heat-map cell to inspect its risks.</p>:null}<div className="heat-legend"><strong>Severity legend</strong>{["Low","Medium","High","Critical"].map(x=><span key={x}><i className={`heat-${x.toLowerCase()}`}/>{x}</span>)}</div></Card></div>
  </div>;
}

export function RecommendationsPage() {
  const {isDemo,user}=useAuth();
  const queryClient=useQueryClient();
  const query=useQuery({queryKey:["recommendations"],queryFn:()=>api<Recommendation[]>("/recommendations",{},recommendationsDemo)});
  const risks=useQuery({queryKey:["risks"],queryFn:()=>api<Risk[]>("/risks",{},risksDemo)});
  const [open,setOpen]=useState(false);
  const [editing,setEditing]=useState<Recommendation>();
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState("");
  const [form,setForm]=useState({riskItemId:"",title:"",description:"",priority:"High",suggestedOwner:"",dueDateUtc:new Date(Date.now()+30*86400000).toISOString().slice(0,10),businessImpact:"",complianceMapping:""});
  const items=query.data??(isDemo?recommendationsDemo:[]);
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Loading treatment actions..."/><Card>Loading recommendations...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Prioritized mitigation actions and ownership."/><div className="form-error">{query.error.message}</div></div>;
  const canManage=!isDemo&&user?.roles.some((role)=>["Admin","Risk Manager","Compliance Officer","Security Analyst"].includes(role));
  function startCreate(){setEditing(undefined);setError("");setForm({riskItemId:risks.data?.[0]?.id??"",title:"",description:"",priority:"High",suggestedOwner:user?.fullName??"",dueDateUtc:new Date(Date.now()+30*86400000).toISOString().slice(0,10),businessImpact:"",complianceMapping:""});setOpen(true)}
  function startEdit(item:Recommendation){setEditing(item);setError("");setForm({riskItemId:"",title:item.title,description:item.description,priority:item.priority,suggestedOwner:item.suggestedOwner,dueDateUtc:item.dueDateUtc.slice(0,10),businessImpact:"",complianceMapping:item.complianceMapping});setOpen(true)}
  async function save(event:FormEvent){event.preventDefault();setSaving(true);setError("");try{if(editing){await api(`/recommendations/${editing.id}`,{method:"PUT",body:JSON.stringify({status:editing.status,owner:form.suggestedOwner,dueDateUtc:new Date(`${form.dueDateUtc}T12:00:00Z`).toISOString()})})}else{const risk=risks.data?.find((item)=>item.id===form.riskItemId);if(!risk)throw new Error("Select a related risk.");await api("/recommendations",{method:"POST",body:JSON.stringify({riskItemId:risk.id,assessmentId:null,title:form.title,description:form.description,category:risk.category,severity:form.priority,priority:form.priority,suggestedOwner:form.suggestedOwner,dueDateUtc:new Date(`${form.dueDateUtc}T12:00:00Z`).toISOString(),businessImpact:form.businessImpact,complianceMapping:form.complianceMapping,status:"Open"})})}await queryClient.invalidateQueries({queryKey:["recommendations"]});setOpen(false)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Recommendation could not be saved.")}finally{setSaving(false)}}
  async function complete(item:Recommendation){setError("");try{await api(`/recommendations/${item.id}/complete`,{method:"POST"});await queryClient.invalidateQueries({queryKey:["recommendations"]})}catch(requestError){setError(requestError instanceof Error?requestError.message:"Recommendation could not be completed.")}}
  return <div className="page-stack"><PageHeader eyebrow="Risk treatment" title="Recommendations" description="Prioritized mitigation actions with ownership, deadlines, business impact, and control mappings." actions={canManage?<button type="button" className="button button-primary" onClick={startCreate}><Plus size={16}/> Add recommendation</button>:undefined}/>
    {error?<div className="form-error">{error}</div>:null}
    <div className="recommendation-grid">{["Open","InProgress","Completed"].map((status)=><div className="kanban-column" key={status}><div className="kanban-head"><span>{status==="InProgress"?"In progress":status}</span><b>{items.filter(x=>x.status===status).length}</b></div>{items.filter(x=>x.status===status).map(item=><Card className="recommendation-card" key={item.id}><div><Badge tone={item.priority.toLowerCase() as "critical"|"high"}>{item.priority}</Badge>{canManage?<div className="action-menu"><button type="button" className="icon-button" aria-label={`Edit ${item.title}`} onClick={()=>startEdit(item)}><MoreHorizontal size={17}/></button></div>:null}</div><h3>{item.title}</h3><p>{item.description}</p><div className="recommendation-meta"><span><CalendarClock size={14}/>{formatDate(item.dueDateUtc)}</span><span><UserCog size={14}/>{item.suggestedOwner}</span></div><div className="mapping-tags">{item.complianceMapping.split(";").filter(Boolean).map(x=><small key={x}>{x}</small>)}</div>{canManage&&item.status!=="Completed"?<button type="button" className="button button-secondary" onClick={()=>complete(item)}>Mark complete</button>:null}</Card>)}{items.filter(x=>x.status===status).length===0?<Card className="completed-summary"><CheckCircle2 size={30}/><strong>No {status==="InProgress"?"in-progress":status.toLowerCase()} actions</strong><span>The API returned no recommendations in this state.</span></Card>:null}</div>)}</div>
    <Modal open={open} title={editing?"Update recommendation":"Add recommendation"} description="Assign a practical treatment action to an existing workspace risk." onClose={()=>setOpen(false)}><form className="modal-form" onSubmit={save}>{!editing?<label className="field-label">Related risk<select value={form.riskItemId} onChange={(event)=>setForm({...form,riskItemId:event.target.value})} required>{risks.data?.map((risk)=><option value={risk.id} key={risk.id}>{risk.title}</option>)}</select></label>:null}<div className="form-grid"><label>Title<input value={form.title} disabled={Boolean(editing)} onChange={(event)=>setForm({...form,title:event.target.value})} required/></label><label>Owner<input value={form.suggestedOwner} onChange={(event)=>setForm({...form,suggestedOwner:event.target.value})} required/></label><label>Priority<select value={form.priority} disabled={Boolean(editing)} onChange={(event)=>setForm({...form,priority:event.target.value})}>{["Medium","High","Critical"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Due date<input type="date" value={form.dueDateUtc} onChange={(event)=>setForm({...form,dueDateUtc:event.target.value})} required/></label></div>{!editing?<><label className="field-label">Description<textarea value={form.description} onChange={(event)=>setForm({...form,description:event.target.value})} required/></label><label className="field-label">Business impact<textarea value={form.businessImpact} onChange={(event)=>setForm({...form,businessImpact:event.target.value})}/></label><label className="field-label">Compliance mapping<input value={form.complianceMapping} onChange={(event)=>setForm({...form,complianceMapping:event.target.value})} placeholder="ISO 27001; NIST CSF"/></label></>:null}{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel={editing?"Update recommendation":"Create recommendation"} onCancel={()=>setOpen(false)}/></form></Modal>
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
  return <div className="page-stack"><PageHeader eyebrow="Regulatory assurance" title="Compliance command center" description="Readiness, control performance, evidence status, and remediation across five frameworks." actions={!isDemo?<button type="button" className="button button-primary" disabled={reporting} onClick={downloadCompliance}><FileCheck2 size={16}/> {reporting?"Generating...":"Download compliance report"}</button>:undefined}/>
    {reportError?<div className="form-error">{reportError}</div>:null}
    <div className="metric-grid"><MetricCard label="Overall readiness" value={`${data.readiness}%`} detail="Across 5 frameworks" icon={<ShieldCheck/>} tone="blue" trend={4}/><MetricCard label="Controls passed" value={`${data.passed}`} detail="Evidence accepted" icon={<CheckCircle2/>} tone="green"/><MetricCard label="Failed controls" value={`${data.failed}`} detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Evidence missing" value={`${data.missing}`} detail="Audit attention" icon={<Archive/>} tone="orange"/></div>
    <div className="dashboard-grid"><Card className="span-2"><div className="card-head"><div><span className="card-kicker">Framework readiness</span><h2>Assurance coverage</h2></div></div><div className="framework-readiness">{data.frameworks.map((item,index)=><div key={item.category}><span className="framework-icon">{item.category.slice(0,2)}</span><span><strong>{item.category}</strong><small>{[8,4,12,9,10][index]} controls assessed</small></span><ProgressBar value={item.score} tone="blue"/><b>{item.score}%</b></div>)}</div></Card><Card className="compliance-donut"><div className="card-head"><div><span className="card-kicker">Control results</span><h2>Current status</h2></div></div><div className="donut-chart"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={[{name:"Passed",value:data.passed},{name:"Failed",value:data.failed},{name:"Missing",value:data.missing}]} dataKey="value" innerRadius={55} outerRadius={80} paddingAngle={4}><Cell fill="#22c55e"/><Cell fill="#ef4444"/><Cell fill="#eab308"/></Pie></PieChart></ResponsiveContainer><div><strong>{data.passed+data.failed+data.missing}</strong><span>Controls</span></div></div></Card></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Remediation register</span><h2>Compliance gaps</h2></div><Link to="/app/compliance/gaps">View all <ArrowRight size={15}/></Link></div><div className="table-wrap"><table><thead><tr><th>Framework / control</th><th>Gap</th><th>Severity</th><th>Owner</th><th>Due date</th><th>Status</th></tr></thead><tbody>{(gaps.data??gapsDemo).map(gap=><tr key={gap.id}><td><strong>{gap.control?.framework?.name}</strong><small className="block-muted">{gap.control?.code} · {gap.control?.title}</small></td><td className="wide-cell">{gap.description}</td><td><Badge tone={gap.severity.toLowerCase() as "high"|"medium"}>{gap.severity}</Badge></td><td>{gap.owner}</td><td>{formatDate(gap.dueDateUtc)}</td><td><StatusBadge status={gap.status}/></td></tr>)}</tbody></table></div></Card>
  </div>;
}

export function IncidentsPage() {
  const {isDemo,user}=useAuth();
  const {id}=useParams();
  const queryClient=useQueryClient();
  const detail=useQuery({queryKey:["incident",id],enabled:Boolean(id),queryFn:()=>api<Incident>(`/incidents/${id}`,{},incidentsDemo.find((item)=>item.id===id)??incidentsDemo[0])});
  const query=useQuery({queryKey:["incidents"],queryFn:()=>api<Incident[]>("/incidents",{},incidentsDemo)});
  const departments=useQuery({queryKey:["departments"],queryFn:()=>api<Department[]>("/departments",{},[])});
  const risks=useQuery({queryKey:["risks"],queryFn:()=>api<Risk[]>("/risks",{},risksDemo)});
  const [search,setSearch]=useState("");
  const [status,setStatus]=useState("All");
  const [open,setOpen]=useState(false);
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState("");
  const [form,setForm]=useState({title:"",description:"",category:"Cybersecurity",severity:"High",owner:user?.fullName??"",departmentId:"",riskItemId:"",dueDateUtc:new Date(Date.now()+7*86400000).toISOString().slice(0,10),evidenceNotes:""});
  if(id){
    if(detail.isLoading)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident details" description="Loading incident..."/><Card>Loading incident...</Card></div>;
    if(detail.isError)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident details" description="The requested incident could not be loaded."/><div className="form-error">{detail.error.message}</div><Link className="button button-secondary" to="/app/incidents">Back to incidents</Link></div>;
    const incident=detail.data;
    if(!incident)return null;
    return <div className="page-stack"><PageHeader eyebrow="Case and event response" title={incident.title} description={incident.description||"Incident details and current workflow status."} actions={<Link className="button button-secondary" to="/app/incidents">Back to incidents</Link>}/><div className="metric-grid metric-grid-compact"><MetricCard label="Severity" value={incident.severity} detail="Current classification" icon={<Siren/>} tone="red"/><MetricCard label="Status" value={splitWords(incident.status)} detail="Workflow state" icon={<Activity/>} tone="blue"/><MetricCard label="Owner" value={incident.owner} detail={incident.department?.name||"Enterprise"} icon={<UserCog/>} tone="purple"/><MetricCard label="Detected" value={formatDate(incident.detectedAtUtc)} detail="Event timestamp" icon={<Clock3/>} tone="orange"/></div><Card className="form-card"><div className="card-head"><div><span className="card-kicker">Evidence and response</span><h2>Incident record</h2></div><Badge tone={incident.severity.toLowerCase() as "high"|"medium"}>{incident.severity}</Badge></div><p className="muted">{incident.evidenceNotes||"No evidence notes have been recorded."}</p></Card></div>;
  }
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Loading incidents..."/><Card>Loading incident register...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Case and event response" title="Incident management" description="Track investigation, mitigation, and closure."/><div className="form-error">{query.error.message}</div></div>;
  const all=query.data??incidentsDemo;
  const items=all.filter((item)=>(status==="All"||item.status===status)&&`${item.title} ${item.owner} ${item.category}`.toLowerCase().includes(search.toLowerCase()));
  const canManage=!isDemo&&user?.roles.some((role)=>["Admin","Risk Manager","Security Analyst","Compliance Officer","Department Manager"].includes(role));
  async function save(event:FormEvent){event.preventDefault();if(!form.departmentId&&!form.riskItemId){setError("Select a department or related risk.");return}setSaving(true);setError("");try{await api("/incidents",{method:"POST",body:JSON.stringify({...form,departmentId:form.departmentId||null,riskItemId:form.riskItemId||null,dueDateUtc:form.dueDateUtc?new Date(`${form.dueDateUtc}T12:00:00Z`).toISOString():null})});await queryClient.invalidateQueries({queryKey:["incidents"]});setOpen(false)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Incident could not be created.")}finally{setSaving(false)}}
  return <><RegisterPage title="Incident management" eyebrow="Case and event response" description="Track detection, investigation, mitigation, evidence, and closure against related risks." action={canManage?"Create incident":undefined} onAction={()=>setOpen(true)} search={search} onSearch={setSearch} filter={status} onFilter={setStatus} filterOptions={["All","Detected","Assigned","Investigating","Mitigated","Resolved","Closed"]} metrics={[
    ["Open incidents",`${all.filter((item)=>!["Resolved","Closed"].includes(item.status)).length}`,"Current open cases",Siren,"red"],["Investigating",`${all.filter((item)=>item.status==="Investigating").length}`,"Active response",Search,"orange"],["High severity",`${all.filter((item)=>["High","Critical"].includes(item.severity)).length}`,"Priority cases",Clock3,"blue"],["Closed",`${all.filter((item)=>item.status==="Closed").length}`,"Evidence retained",CheckCircle2,"green"]
  ]}><table><thead><tr><th>Incident</th><th>Category</th><th>Department</th><th>Owner</th><th>Detected</th><th>Severity</th><th>Status</th></tr></thead><tbody>{items.map(item=><tr key={item.id}><td><Link className="cell-link" to={`/app/incidents/${item.id}`}><span className="type-icon type-danger"><Siren size={17}/></span><span><strong>{item.title}</strong><small>INC-{item.id.toUpperCase()}</small></span></Link></td><td>{item.category}</td><td>{item.department?.name}</td><td>{item.owner}</td><td>{formatDate(item.detectedAtUtc)}</td><td><Badge tone={item.severity.toLowerCase() as "high"|"medium"}>{item.severity}</Badge></td><td><StatusBadge status={item.status}/></td></tr>)}</tbody></table></RegisterPage><Modal open={open} title="Create incident" description="Record the event, ownership, evidence, and related risk." onClose={()=>setOpen(false)}><form className="modal-form" onSubmit={save}><div className="form-grid"><label>Title<input value={form.title} onChange={(event)=>setForm({...form,title:event.target.value})} required/></label><label>Owner<input value={form.owner} onChange={(event)=>setForm({...form,owner:event.target.value})} required/></label><label>Category<select value={form.category} onChange={(event)=>setForm({...form,category:event.target.value})}>{["Cybersecurity","Operational","Compliance","Financial","Vendor","Privacy","BusinessContinuity"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Severity<select value={form.severity} onChange={(event)=>setForm({...form,severity:event.target.value})}>{["Low","Medium","High","Critical"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Department<select value={form.departmentId} onChange={(event)=>setForm({...form,departmentId:event.target.value})}><option value="">None</option>{departments.data?.map((item)=><option value={item.id} key={item.id}>{item.name}</option>)}</select></label><label>Related risk<select value={form.riskItemId} onChange={(event)=>setForm({...form,riskItemId:event.target.value})}><option value="">None</option>{risks.data?.map((item)=><option value={item.id} key={item.id}>{item.title}</option>)}</select></label><label>Due date<input type="date" value={form.dueDateUtc} onChange={(event)=>setForm({...form,dueDateUtc:event.target.value})}/></label></div><label className="field-label">Description<textarea value={form.description} onChange={(event)=>setForm({...form,description:event.target.value})} required/></label><label className="field-label">Evidence notes<textarea value={form.evidenceNotes} onChange={(event)=>setForm({...form,evidenceNotes:event.target.value})}/></label>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel="Create incident" onCancel={()=>setOpen(false)}/></form></Modal></>;
}

export function VendorsPage() {
  const {isDemo,user}=useAuth();
  const {id}=useParams();
  const queryClient=useQueryClient();
  const detail=useQuery({queryKey:["vendor",id],enabled:Boolean(id),queryFn:()=>api<Vendor>(`/vendors/${id}`,{},vendorsDemo.find((item)=>item.id===id)??vendorsDemo[0])});
  const query=useQuery({queryKey:["vendors"],queryFn:()=>api<Vendor[]>("/vendors",{},vendorsDemo)});
  const [open,setOpen]=useState(false);
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState("");
  const [search,setSearch]=useState("");
  const [filter,setFilter]=useState("All");
  const [form,setForm]=useState({name:"",serviceProvided:"",criticality:"High",contractStartDateUtc:new Date().toISOString().slice(0,10),contractExpiryDateUtc:new Date(Date.now()+365*86400000).toISOString().slice(0,10),complianceStatus:"NotAssessed",securityRating:50,dependencyLevel:"High",owner:user?.fullName??"",notes:""});
  if(id){
    if(detail.isLoading)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor details" description="Loading vendor..."/><Card>Loading vendor...</Card></div>;
    if(detail.isError)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor details" description="The requested vendor could not be loaded."/><div className="form-error">{detail.error.message}</div><Link className="button button-secondary" to="/app/vendors">Back to vendors</Link></div>;
    const vendor=detail.data;
    if(!vendor)return null;
    return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title={vendor.name} description={vendor.serviceProvided} actions={<Link className="button button-secondary" to="/app/vendors">Back to vendors</Link>}/><div className="metric-grid metric-grid-compact"><MetricCard label="Risk score" value={`${vendor.riskScore}`} detail={vendor.riskLevel} icon={<CircleAlert/>} tone="red"/><MetricCard label="Security rating" value={`${vendor.securityRating}%`} detail="Current rating" icon={<ShieldCheck/>} tone="blue"/><MetricCard label="Criticality" value={vendor.criticality} detail="Business dependency" icon={<Building2/>} tone="purple"/><MetricCard label="Contract expiry" value={formatDate(vendor.contractExpiryDateUtc)} detail={vendor.complianceStatus} icon={<CalendarClock/>} tone="orange"/></div><Card className="form-card"><div className="card-head"><div><span className="card-kicker">Vendor record</span><h2>Assurance notes</h2></div><RiskBadge level={vendor.riskLevel}/></div><p className="muted">{vendor.notes||"No vendor notes have been recorded."}</p></Card></div>;
  }
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Loading vendor exposure..."/><Card>Loading vendors...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers and dependencies."/><div className="form-error">{query.error.message}</div></div>;
  const all=query.data??(isDemo?vendorsDemo:[]);
  const data=all.filter((vendor)=>(filter==="All"||vendor.riskLevel===filter)&&`${vendor.name} ${vendor.serviceProvided} ${vendor.owner}`.toLowerCase().includes(search.toLowerCase()));
  const canManage=!isDemo&&user?.roles.some((role)=>["Admin","Risk Manager","Compliance Officer","Security Analyst"].includes(role));
  async function save(event:FormEvent){event.preventDefault();if(!user?.organizationId)return;setSaving(true);setError("");try{await api("/vendors",{method:"POST",body:JSON.stringify({...form,organizationId:user.organizationId,contractStartDateUtc:new Date(`${form.contractStartDateUtc}T12:00:00Z`).toISOString(),contractExpiryDateUtc:new Date(`${form.contractExpiryDateUtc}T12:00:00Z`).toISOString()})});await queryClient.invalidateQueries({queryKey:["vendors"]});setOpen(false)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Vendor could not be created.")}finally{setSaving(false)}}
  return <div className="page-stack"><PageHeader eyebrow="Third-party assurance" title="Vendor risk" description="Monitor critical suppliers, contract exposure, security posture, compliance, and business dependency." actions={canManage?<button type="button" className="button button-primary" onClick={()=>setOpen(true)}><Plus size={16}/> Add vendor</button>:undefined}/>
    {error?<div className="form-error">{error}</div>:null}
    <div className="metric-grid"><MetricCard label="Total vendors" value={`${all.length}`} detail="Active vendor records" icon={<Network/>} tone="blue"/><MetricCard label="Critical vendors" value={`${all.filter((vendor)=>vendor.criticality==="Critical").length}`} detail="High business dependency" icon={<Building2/>} tone="purple"/><MetricCard label="High-risk vendors" value={`${all.filter((vendor)=>["High","Critical"].includes(vendor.riskLevel)).length}`} detail="Treatment required" icon={<CircleAlert/>} tone="red"/><MetricCard label="Expiring soon" value={`${all.filter((vendor)=>new Date(vendor.contractExpiryDateUtc)<=new Date(Date.now()+45*86400000)).length}`} detail="Within 45 days" icon={<CalendarClock/>} tone="orange"/></div>
    <Card><div className="table-toolbar"><div className="inline-search"><Search size={16}/><input value={search} onChange={(event)=>setSearch(event.target.value)} placeholder="Search vendors..."/></div><label className="button button-secondary"><Filter size={15}/><select value={filter} onChange={(event)=>setFilter(event.target.value)}><option>All</option>{["Low","Medium","High","Critical"].map((value)=><option key={value}>{value}</option>)}</select></label></div></Card>
    <div className="vendor-grid">{data.map(vendor=><Card className="vendor-card" key={vendor.id}><div className="vendor-head"><span className="vendor-logo">{vendor.name.split(" ").map(x=>x[0]).join("").slice(0,2)}</span><div><h3>{vendor.name}</h3><p>{vendor.serviceProvided}</p></div><Link className="icon-button" aria-label={`View ${vendor.name}`} to={`/app/vendors/${vendor.id}`}><ArrowRight size={17}/></Link></div><div className="vendor-score"><div><span>Risk score</span><strong>{vendor.riskScore}</strong></div><RiskBadge level={vendor.riskLevel}/></div><ProgressBar value={vendor.riskScore}/><div className="vendor-meta"><span><strong>{vendor.criticality}</strong>Criticality</span><span><strong>{vendor.securityRating}%</strong>Security rating</span><span><strong>{formatDate(vendor.contractExpiryDateUtc)}</strong>Contract expiry</span></div><div className="vendor-foot"><span className="owner-cell"><i>{initials(vendor.owner)}</i>{vendor.owner}</span><Badge tone={vendor.complianceStatus==="Compliant"?"success":"medium"}>{splitWords(vendor.complianceStatus)}</Badge></div></Card>)}</div>
    <Modal open={open} title="Add vendor" description="Create a governed third-party record for assurance and risk scoring." onClose={()=>setOpen(false)}><form className="modal-form" onSubmit={save}><div className="form-grid"><label>Vendor name<input value={form.name} onChange={(event)=>setForm({...form,name:event.target.value})} required/></label><label>Service provided<input value={form.serviceProvided} onChange={(event)=>setForm({...form,serviceProvided:event.target.value})} required/></label><label>Owner<input value={form.owner} onChange={(event)=>setForm({...form,owner:event.target.value})} required/></label><label>Security rating<input type="number" min="0" max="100" value={form.securityRating} onChange={(event)=>setForm({...form,securityRating:Number(event.target.value)})}/></label><label>Criticality<select value={form.criticality} onChange={(event)=>setForm({...form,criticality:event.target.value})}>{["Low","Medium","High","Critical"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Dependency<select value={form.dependencyLevel} onChange={(event)=>setForm({...form,dependencyLevel:event.target.value})}>{["Low","Medium","High","Critical"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Compliance<select value={form.complianceStatus} onChange={(event)=>setForm({...form,complianceStatus:event.target.value})}>{["NotAssessed","NonCompliant","PartiallyCompliant","Compliant"].map((value)=><option key={value}>{splitWords(value)}</option>)}</select></label><label>Contract starts<input type="date" value={form.contractStartDateUtc} onChange={(event)=>setForm({...form,contractStartDateUtc:event.target.value})}/></label><label>Contract expires<input type="date" value={form.contractExpiryDateUtc} onChange={(event)=>setForm({...form,contractExpiryDateUtc:event.target.value})}/></label></div><label className="field-label">Notes<textarea value={form.notes} onChange={(event)=>setForm({...form,notes:event.target.value})}/></label>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel="Add vendor" onCancel={()=>setOpen(false)}/></form></Modal>
  </div>;
}

export function ContinuityPage() {
  const {isDemo,user}=useAuth();
  const queryClient=useQueryClient();
  const query=useQuery({queryKey:["continuity"],queryFn:()=>api<ContinuityPlan[]>("/continuity",{},continuityDemo)});
  const [mode,setMode]=useState<"plan"|"system"|"test"|null>(null);
  const [selectedSystem,setSelectedSystem]=useState("");
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState("");
  const [planForm,setPlanForm]=useState({name:"Enterprise continuity plan",owner:user?.fullName??"",continuityScore:0,status:"Active"});
  const [systemForm,setSystemForm]=useState({name:"",systemOwner:user?.fullName??"",recoveryTimeObjectiveHours:4,recoveryPointObjectiveHours:4,backupFrequency:"Daily",lastBackupTestDateUtc:"",lastDisasterRecoveryTestDateUtc:"",downtimeImpact:"",continuityScore:50,status:"Needs attention"});
  const [testForm,setTestForm]=useState({testedAtUtc:new Date().toISOString().slice(0,10),continuityScore:70,status:"Ready",notes:""});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Loading recovery readiness..."/><Card>Loading continuity plan...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing."/><div className="form-error">{query.error.message}</div></div>;
  const plan=(query.data??(isDemo?continuityDemo:[]))[0];
  const canManage=!isDemo&&user?.roles.some((role)=>["Admin","Risk Manager","Compliance Officer","Security Analyst"].includes(role));
  async function savePlan(event:FormEvent){event.preventDefault();if(!user?.organizationId)return;setSaving(true);setError("");try{await api("/continuity",{method:"POST",body:JSON.stringify({...planForm,organizationId:user.organizationId})});await queryClient.invalidateQueries({queryKey:["continuity"]});setMode(null)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Continuity plan could not be created.")}finally{setSaving(false)}}
  async function saveSystem(event:FormEvent){event.preventDefault();if(!plan)return;setSaving(true);setError("");try{await api(`/continuity/${plan.id}/systems`,{method:"POST",body:JSON.stringify({...systemForm,lastBackupTestDateUtc:systemForm.lastBackupTestDateUtc?new Date(`${systemForm.lastBackupTestDateUtc}T12:00:00Z`).toISOString():null,lastDisasterRecoveryTestDateUtc:systemForm.lastDisasterRecoveryTestDateUtc?new Date(`${systemForm.lastDisasterRecoveryTestDateUtc}T12:00:00Z`).toISOString():null})});await queryClient.invalidateQueries({queryKey:["continuity"]});setMode(null)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Critical system could not be added.")}finally{setSaving(false)}}
  async function saveTest(event:FormEvent){event.preventDefault();if(!plan||!selectedSystem)return;setSaving(true);setError("");try{await api(`/continuity/${plan.id}/systems/${selectedSystem}/recovery-test`,{method:"POST",body:JSON.stringify({...testForm,testedAtUtc:new Date(`${testForm.testedAtUtc}T12:00:00Z`).toISOString()})});await queryClient.invalidateQueries({queryKey:["continuity"]});setMode(null)}catch(requestError){setError(requestError instanceof Error?requestError.message:"Recovery test could not be recorded.")}finally{setSaving(false)}}
  if(!plan)return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, and testing." actions={canManage?<button type="button" className="button button-primary" onClick={()=>setMode("plan")}><Plus size={16}/> Create continuity plan</button>:undefined}/><Card>No continuity plan has been configured.</Card><Modal open={mode==="plan"} title="Create continuity plan" onClose={()=>setMode(null)}><form className="modal-form" onSubmit={savePlan}><div className="form-grid"><label>Plan name<input value={planForm.name} onChange={(event)=>setPlanForm({...planForm,name:event.target.value})} required/></label><label>Owner<input value={planForm.owner} onChange={(event)=>setPlanForm({...planForm,owner:event.target.value})} required/></label><label>Initial readiness<input type="number" min="0" max="100" value={planForm.continuityScore} onChange={(event)=>setPlanForm({...planForm,continuityScore:Number(event.target.value)})}/></label><label>Status<select value={planForm.status} onChange={(event)=>setPlanForm({...planForm,status:event.target.value})}>{["Active","Inactive","Archived"].map((value)=><option key={value}>{value}</option>)}</select></label></div>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel="Create plan" onCancel={()=>setMode(null)}/></form></Modal></div>;
  const systems=plan.criticalSystems??[];
  const overdue=systems.filter((system)=>!system.lastDisasterRecoveryTestDateUtc||new Date(system.lastDisasterRecoveryTestDateUtc)<new Date(Date.now()-180*86400000)).length;
  const average=systems.length?Math.round(systems.reduce((total,system)=>total+system.continuityScore,0)/systems.length):0;
  return <div className="page-stack"><PageHeader eyebrow="Operational resilience" title="Business continuity" description="Recovery objectives, backup assurance, disaster recovery testing, and downtime exposure." actions={canManage?<button type="button" className="button button-primary" disabled={systems.length===0} onClick={()=>{setSelectedSystem(systems[0]?.id??"");setMode("test")}}><RefreshCw size={16}/> Record recovery test</button>:undefined}/>
    {error?<div className="form-error">{error}</div>:null}
    <div className="metric-grid"><MetricCard label="Continuity score" value={`${plan.continuityScore}%`} detail="Plan readiness" icon={<Activity/>} tone="orange"/><MetricCard label="System readiness" value={`${average}%`} detail={`${systems.length} critical systems`} icon={<Archive/>} tone="blue"/><MetricCard label="Tests overdue" value={`${overdue}`} detail="Older than 180 days" icon={<ServerCog/>} tone="red"/><MetricCard label="Downtime exposure" value={formatMoney(systems.reduce((total,system)=>total+system.recoveryTimeObjectiveHours*15000,0))} detail="Estimated RTO exposure" icon={<FileChartColumn/>} tone="purple"/></div>
    <Card className="table-card"><div className="card-head"><div><span className="card-kicker">Recovery inventory</span><h2>Critical systems</h2></div>{canManage?<button type="button" className="button button-secondary" onClick={()=>setMode("system")}><Plus size={15}/> Add system</button>:null}</div><div className="table-wrap"><table><thead><tr><th>Critical system</th><th>Owner</th><th>RTO</th><th>RPO</th><th>Backup frequency</th><th>Last DR test</th><th>Readiness</th><th>Status</th></tr></thead><tbody>{systems.map(system=><tr key={system.id}><td>{canManage?<button type="button" className="cell-link bare-button" onClick={()=>{setSelectedSystem(system.id);setTestForm({...testForm,continuityScore:system.continuityScore,status:system.status});setMode("test")}}><span className="type-icon"><ServerCog size={17}/></span><span><strong>{system.name}</strong><small>Record or review a recovery test</small></span></button>:<span className="cell-link"><span className="type-icon"><ServerCog size={17}/></span><span><strong>{system.name}</strong><small>Recovery system record</small></span></span>}</td><td>{system.systemOwner}</td><td>{system.recoveryTimeObjectiveHours}h</td><td>{system.recoveryPointObjectiveHours}h</td><td>{system.backupFrequency}</td><td>{formatDate(system.lastDisasterRecoveryTestDateUtc)}</td><td><div className="readiness-cell"><ProgressBar value={system.continuityScore} tone="blue"/><strong>{system.continuityScore}%</strong></div></td><td><StatusBadge status={system.status}/></td></tr>)}</tbody></table></div></Card>
    <Modal open={mode==="system"} title="Add critical system" description="Define recovery objectives, backup assurance, and current readiness." onClose={()=>setMode(null)}><form className="modal-form" onSubmit={saveSystem}><div className="form-grid"><label>System name<input value={systemForm.name} onChange={(event)=>setSystemForm({...systemForm,name:event.target.value})} required/></label><label>System owner<input value={systemForm.systemOwner} onChange={(event)=>setSystemForm({...systemForm,systemOwner:event.target.value})} required/></label><label>RTO hours<input type="number" min="0" value={systemForm.recoveryTimeObjectiveHours} onChange={(event)=>setSystemForm({...systemForm,recoveryTimeObjectiveHours:Number(event.target.value)})}/></label><label>RPO hours<input type="number" min="0" value={systemForm.recoveryPointObjectiveHours} onChange={(event)=>setSystemForm({...systemForm,recoveryPointObjectiveHours:Number(event.target.value)})}/></label><label>Backup frequency<input value={systemForm.backupFrequency} onChange={(event)=>setSystemForm({...systemForm,backupFrequency:event.target.value})} required/></label><label>Readiness score<input type="number" min="0" max="100" value={systemForm.continuityScore} onChange={(event)=>setSystemForm({...systemForm,continuityScore:Number(event.target.value)})}/></label><label>Last backup test<input type="date" value={systemForm.lastBackupTestDateUtc} onChange={(event)=>setSystemForm({...systemForm,lastBackupTestDateUtc:event.target.value})}/></label><label>Last DR test<input type="date" value={systemForm.lastDisasterRecoveryTestDateUtc} onChange={(event)=>setSystemForm({...systemForm,lastDisasterRecoveryTestDateUtc:event.target.value})}/></label><label>Status<input value={systemForm.status} onChange={(event)=>setSystemForm({...systemForm,status:event.target.value})} required/></label></div><label className="field-label">Downtime impact<textarea value={systemForm.downtimeImpact} onChange={(event)=>setSystemForm({...systemForm,downtimeImpact:event.target.value})}/></label>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel="Add system" onCancel={()=>setMode(null)}/></form></Modal>
    <Modal open={mode==="test"} title="Record recovery test" description="Update the latest tested recovery date, readiness score, and outcome." onClose={()=>setMode(null)}><form className="modal-form" onSubmit={saveTest}><div className="form-grid"><label>Critical system<select value={selectedSystem} onChange={(event)=>setSelectedSystem(event.target.value)} required>{systems.map((system)=><option value={system.id} key={system.id}>{system.name}</option>)}</select></label><label>Test date<input type="date" max={new Date().toISOString().slice(0,10)} value={testForm.testedAtUtc} onChange={(event)=>setTestForm({...testForm,testedAtUtc:event.target.value})}/></label><label>Readiness score<input type="number" min="0" max="100" value={testForm.continuityScore} onChange={(event)=>setTestForm({...testForm,continuityScore:Number(event.target.value)})}/></label><label>Outcome<select value={testForm.status} onChange={(event)=>setTestForm({...testForm,status:event.target.value})}>{["Ready","Needs attention","At risk"].map((value)=><option key={value}>{value}</option>)}</select></label></div><label className="field-label">Test notes<textarea value={testForm.notes} onChange={(event)=>setTestForm({...testForm,notes:event.target.value})} required/></label>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel="Record test" onCancel={()=>setMode(null)}/></form></Modal>
  </div>;
}

export function ReportsPage() {
  const {isDemo,user}=useAuth();
  const [message,setMessage]=useState("");
  const [activePath,setActivePath]=useState("");
  const [scheduleOpen,setScheduleOpen]=useState(false);
  const [schedule,setSchedule]=useState(()=>{try{return JSON.parse(localStorage.getItem("riskguard.reportSchedule")??"null")??{frequency:"Monthly",report:"Executive risk report",day:"1",enabled:false}}catch{return{frequency:"Monthly",report:"Executive risk report",day:"1",enabled:false}}});
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
    <div className="report-grid">{reports.map(({title,detail,type,Icon,path,file,roles})=>{const allowed=!isDemo&&(!roles||roles.some((role)=>user?.roles.includes(role)));return <Card className="report-card" key={title}><span className="report-icon"><Icon size={23}/></span><Badge tone="info">{type}</Badge><h3>{title}</h3><p>{detail}</p><div><button type="button" className="button button-secondary" disabled={Boolean(activePath)||!allowed} title={isDemo?"Sign in with a live account to generate files.":allowed?`Generate ${title}`:"Your role cannot export the audit log."} onClick={()=>get(path,file)}><Download size={16}/> {isDemo?"Live account required":!allowed?"Restricted":activePath===path?"Preparing...":type==="PDF"?"Download PDF":`Export ${type}`}</button></div></Card>})}</div>
    <Card className="schedule-card"><div><span className="type-icon"><CalendarClock/></span><div><h3>Report schedule reminder</h3><p>{schedule.enabled?`${schedule.report} is due ${schedule.frequency.toLowerCase()} on day ${schedule.day}. Open RiskGuard to generate it.`:"Configure a recurring in-app reminder for report generation."}</p></div></div><Badge tone={schedule.enabled?"success":"neutral"}>{schedule.enabled?"Enabled":"Disabled"}</Badge><button type="button" className="button button-secondary" onClick={()=>setScheduleOpen(true)}>Manage schedule</button></Card>
    <Modal open={scheduleOpen} title="Manage report reminder" description="Save a recurring browser preference. Reports remain generated on demand so protected data is never emailed automatically." onClose={()=>setScheduleOpen(false)}><form className="modal-form" onSubmit={(event)=>{event.preventDefault();localStorage.setItem("riskguard.reportSchedule",JSON.stringify(schedule));setScheduleOpen(false);setMessage("Report schedule preference saved.")}}><div className="form-grid"><label>Report<select value={schedule.report} onChange={(event)=>setSchedule({...schedule,report:event.target.value})}>{reports.map((report)=><option key={report.title}>{report.title}</option>)}</select></label><label>Frequency<select value={schedule.frequency} onChange={(event)=>setSchedule({...schedule,frequency:event.target.value})}>{["Weekly","Monthly","Quarterly"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Day<input type="number" min="1" max={schedule.frequency==="Weekly"?7:28} value={schedule.day} onChange={(event)=>setSchedule({...schedule,day:event.target.value})}/></label><label className="toggle-label">Enabled<input type="checkbox" checked={schedule.enabled} onChange={(event)=>setSchedule({...schedule,enabled:event.target.checked})}/></label></div><ModalActions busy={false} submitLabel="Save schedule" onCancel={()=>setScheduleOpen(false)}/></form></Modal>
  </div>;
}

export function AuditPage() {
  const {isDemo}=useAuth();
  const [exporting,setExporting]=useState(false);
  const [exportError,setExportError]=useState("");
  const [search,setSearch]=useState("");
  const [filter,setFilter]=useState("All");
  const query=useQuery({queryKey:["audit"],queryFn:()=>api<AuditLog[]>("/audit-logs",{},auditDemo)});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Loading audit evidence..."/><Card>Loading audit log...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Immutable activity history" title="Audit trail" description="Read-only evidence of important system and user actions."/><div className="form-error">{query.error.message}</div></div>;
  async function exportAudit(){setExporting(true);setExportError("");try{await downloadReport("/reports/auditlogs/csv","RiskGuard-Audit-Activity.csv")}catch(error){setExportError(error instanceof Error?error.message:"Audit export failed.")}finally{setExporting(false)}}
  const all=query.data??(isDemo?auditDemo:[]);
  const items=all.filter((log)=>(filter==="All"||log.entityType===filter)&&`${log.userEmail} ${log.action} ${log.entityType} ${log.description}`.toLowerCase().includes(search.toLowerCase()));
  return <><RegisterPage title="Audit trail" eyebrow="Immutable activity history" description="Read-only evidence of authentication, scoring, workflow, reporting, and administrative actions." action={isDemo?undefined:exporting?"Exporting...":"Export CSV"} actionIcon={<Download size={16}/>} onAction={isDemo?undefined:exportAudit} actionDisabled={exporting} metrics={[
    ["Events",`${all.length}`,"Loaded audit events",Activity,"blue"],["User actions",`${all.filter((log)=>Boolean(log.userEmail)).length}`,"Attributed events",Users,"purple"],["Entity types",`${new Set(all.map((log)=>log.entityType)).size}`,"Audited modules",Settings,"orange"],["Failed logins",`${all.filter((log)=>log.action==="Failed login").length}`,"Authentication failures",KeyRound,"red"]
  ]} search={search} onSearch={setSearch} filter={filter} onFilter={setFilter} filterOptions={["All",...[...new Set(all.map((log)=>log.entityType))]]}><table><thead><tr><th>Timestamp</th><th>User</th><th>Action</th><th>Entity</th><th>Description</th><th>IP address</th></tr></thead><tbody>{items.map(log=><tr key={log.id}><td>{new Date(log.createdAtUtc).toLocaleString("en-ZA")}</td><td><span className="owner-cell"><i>{initials(log.userEmail)}</i>{log.userEmail}</span></td><td><strong>{log.action}</strong></td><td><Badge tone="neutral">{log.entityType}</Badge></td><td className="wide-cell">{log.description}</td><td><code>{log.ipAddress}</code></td></tr>)}</tbody></table></RegisterPage>{exportError?<div className="form-error">{exportError}</div>:null}</>;
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
  return <div className="page-stack"><PageHeader eyebrow="Your work queue" title="Notifications" description="Critical alerts, assignments, deadlines, report events, and overdue actions." actions={!isDemo?<button type="button" className="button button-secondary" disabled={!hasUnread||updating==="all"} onClick={readAll}>{updating==="all"?"Updating...":"Mark all as read"}</button>:undefined}/>{actionError?<div className="form-error">{actionError}</div>:null}<Card className="notification-page-list">{items.map(item=><button type="button" onClick={()=>openNotification(item)} disabled={updating===item.id} className={`notification-page-item ${item.isRead?"read":""}`} key={item.id}><span className={`notice-icon notice-${item.severity.toLowerCase()}`}><CircleAlert size={18}/></span><span><strong>{item.title}</strong><p>{item.message}</p><small>{new Date(item.createdAtUtc).toLocaleString("en-ZA")}</small></span><ArrowRight size={17}/></button>)}</Card></div>;
}

export function UsersPage() {
  const {isDemo,user:currentUser}=useAuth();
  const queryClient=useQueryClient();
  const query=useQuery({queryKey:["users"],queryFn:()=>api<UserSummary[]>("/users",{},[])});
  const departments=useQuery({queryKey:["departments"],queryFn:()=>api<Department[]>("/departments",{},[])});
  const [search,setSearch]=useState("");
  const [filter,setFilter]=useState("All");
  const [open,setOpen]=useState(false);
  const [editing,setEditing]=useState<UserSummary>();
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState("");
  const [form,setForm]=useState({firstName:"",lastName:"",email:"",password:"",role:"Employee",departmentId:"",isActive:true});
  if(query.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Loading workspace users..."/><Card>Loading users...</Card></div>;
  if(query.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Identity and access" title="User management" description="Manage users, roles, and account status."/><div className="form-error">{query.error.message}</div></div>;
  const users=query.data??[];
  const visible=users.filter((item)=>(filter==="All"||item.roles.includes(filter))&&`${item.fullName} ${item.email} ${item.roles.join(" ")}`.toLowerCase().includes(search.toLowerCase()));
  function startCreate(){setEditing(undefined);setError("");setForm({firstName:"",lastName:"",email:"",password:"",role:"Employee",departmentId:"",isActive:true});setOpen(true)}
  function startEdit(item:UserSummary){const [firstName,...rest]=item.fullName.split(" ");setEditing(item);setError("");setForm({firstName,lastName:rest.join(" "),email:item.email,password:"",role:item.roles[0]??"Employee",departmentId:item.departmentId??"",isActive:item.isActive!==false});setOpen(true)}
  async function save(event:FormEvent){event.preventDefault();setSaving(true);setError("");try{if(editing){await api(`/users/${editing.id}`,{method:"PUT",body:JSON.stringify({firstName:form.firstName,lastName:form.lastName,role:form.role,isActive:form.isActive,departmentId:form.departmentId||null})})}else{await api("/users",{method:"POST",body:JSON.stringify({firstName:form.firstName,lastName:form.lastName,email:form.email,password:form.password,role:form.role,organizationId:currentUser?.organizationId??null,departmentId:form.departmentId||null})})}await queryClient.invalidateQueries({queryKey:["users"]});setOpen(false)}catch(requestError){setError(requestError instanceof Error?requestError.message:"User could not be saved.")}finally{setSaving(false)}}
  return <><RegisterPage title="User management" eyebrow="Identity and access" description="Manage users, roles, departments, account status, and least-privilege access." action={isDemo?undefined:"Invite user"} onAction={isDemo?undefined:startCreate} search={search} onSearch={setSearch} filter={filter} onFilter={setFilter} filterOptions={["All","Admin","Executive","Risk Manager","Auditor","Compliance Officer","Security Analyst","Department Manager","Employee"]} metrics={[
    ["Active users",`${users.filter((item)=>item.isActive!==false).length}`,"Current workspace",Users,"blue"],["Administrators",`${users.filter(user=>user.roles.includes("Admin")).length}`,"Privileged accounts",KeyRound,"red"],["Roles in use",`${new Set(users.flatMap(user=>user.roles)).size}`,"Least-privilege assignments",ShieldCheck,"orange"],["Directory status","Healthy","Identity API available",LockKeyhole,"green"]
  ]}><table><thead><tr><th>User</th><th>Email</th><th>Role</th><th>Department</th><th>Status</th><th></th></tr></thead><tbody>{visible.map((user)=><tr key={user.id}><td><span className="owner-cell"><i>{initials(user.fullName)}</i><strong>{user.fullName}</strong></span></td><td>{user.email}</td><td><Badge tone="info">{user.roles.join(", ")}</Badge></td><td>{departments.data?.find((item)=>item.id===user.departmentId)?.name??"Enterprise"}</td><td><Badge tone={user.isActive===false?"neutral":"success"}>{user.isActive===false?"Inactive":"Active"}</Badge></td><td><button type="button" className="icon-button" aria-label={`Manage ${user.fullName}`} onClick={()=>startEdit(user)}><MoreHorizontal size={17}/></button></td></tr>)}</tbody></table></RegisterPage><Modal open={open} title={editing?"Manage user":"Invite user"} description={editing?"Update role, department, or account status.":"Create a workspace account with a temporary password."} onClose={()=>setOpen(false)}><form className="modal-form" onSubmit={save}><div className="form-grid"><label>First name<input value={form.firstName} onChange={(event)=>setForm({...form,firstName:event.target.value})} required/></label><label>Last name<input value={form.lastName} onChange={(event)=>setForm({...form,lastName:event.target.value})} required/></label><label>Email<input type="email" value={form.email} disabled={Boolean(editing)} onChange={(event)=>setForm({...form,email:event.target.value})} required/></label>{!editing?<label>Temporary password<input type="password" minLength={10} value={form.password} onChange={(event)=>setForm({...form,password:event.target.value})} required/></label>:null}<label>Role<select value={form.role} onChange={(event)=>setForm({...form,role:event.target.value})}>{["Admin","Executive","Risk Manager","Auditor","Compliance Officer","Security Analyst","Department Manager","Employee"].map((value)=><option key={value}>{value}</option>)}</select></label><label>Department<select value={form.departmentId} onChange={(event)=>setForm({...form,departmentId:event.target.value})}><option value="">Enterprise</option>{departments.data?.map((item)=><option value={item.id} key={item.id}>{item.name}</option>)}</select></label>{editing?<label className="toggle-label">Account active<input type="checkbox" checked={form.isActive} disabled={editing.id===currentUser?.id} onChange={(event)=>setForm({...form,isActive:event.target.checked})}/></label>:null}</div>{error?<div className="form-error">{error}</div>:null}<ModalActions busy={saving} submitLabel={editing?"Save user":"Create user"} onCancel={()=>setOpen(false)}/></form></Modal></>;
}

export function SettingsPage() {
  const {user,isDemo,updateUser}=useAuth();
  const location=useLocation();
  const queryClient=useQueryClient();
  const [tab,setTab]=useState(location.pathname.endsWith("/profile")?"Profile":"Organization");
  const organizationQuery=useQuery({queryKey:["organizations"],queryFn:()=>api<Organization[]>("/organizations",{},[])});
  const organization=organizationQuery.data?.[0];
  const [form,setForm]=useState({name:"",industry:"",country:"",employeeCount:0,registrationNumber:"",primaryContact:"",email:"",phone:"",address:""});
  const [preferences,setPreferences]=useState(()=>loadPreferences());
  const [profile,setProfile]=useState(()=>{const [firstName,...last]=user?.fullName.split(" ")??[""];return{firstName,lastName:last.join(" "),phoneNumber:""}});
  const [password,setPassword]=useState({currentPassword:"",newPassword:"",confirmPassword:""});
  const [logo,setLogo]=useState(()=>localStorage.getItem("riskguard.organizationLogo")??"");
  const [saving,setSaving]=useState(false);
  const [message,setMessage]=useState("");
  useEffect(()=>{if(!organization)return;setForm({name:organization.name,industry:organization.industry,country:organization.country,employeeCount:organization.employeeCount,registrationNumber:organization.registrationNumber,primaryContact:organization.primaryContact,email:organization.email,phone:organization.phone,address:organization.address})},[organization]);
  const admin=Boolean(user?.roles.includes("Admin"));
  const canSave=!isDemo&&(tab==="Profile"||tab==="Security"||admin);
  async function save(){
    if(!canSave)return;
    setSaving(true);setMessage("");
    try{
      if(tab==="Organization"){
        if(!organization)throw new Error("Organization is unavailable.");
        await api(`/organizations/${organization.id}`,{method:"PUT",body:JSON.stringify(form)});
        await queryClient.invalidateQueries({queryKey:["organizations"]});
      }else if(tab==="Profile"){
        const updated=await api<UserSummary>("/auth/profile",{method:"PUT",body:JSON.stringify(profile)});
        updateUser(updated);
      }else if(tab==="Security"){
        if(password.newPassword!==password.confirmPassword)throw new Error("New password confirmation does not match.");
        await api("/auth/change-password",{method:"POST",body:JSON.stringify({currentPassword:password.currentPassword,newPassword:password.newPassword})});
        setPassword({currentPassword:"",newPassword:"",confirmPassword:""});
      }else{
        localStorage.setItem("riskguard.workspacePreferences",JSON.stringify(preferences));
      }
      setMessage(`${tab} settings saved.`);
    }catch(error){setMessage(error instanceof Error?error.message:"Settings could not be saved.")}
    finally{setSaving(false)}
  }
  function changeLogo(event:React.ChangeEvent<HTMLInputElement>){const file=event.target.files?.[0];if(!file)return;if(file.size>1024*1024){setMessage("Logo must be smaller than 1 MB.");return}const reader=new FileReader();reader.onload=()=>{const value=String(reader.result);setLogo(value);localStorage.setItem("riskguard.organizationLogo",value);setMessage("Organization logo saved in this browser.")};reader.readAsDataURL(file)}
  const notificationOptions: Array<[string, "criticalAlerts"|"assessmentDeadlines"|"incidentAssignments"|"reportReminders"]>=[
    ["Critical risk alerts","criticalAlerts"],
    ["Assessment deadlines","assessmentDeadlines"],
    ["Incident assignments","incidentAssignments"],
    ["Report reminders","reportReminders"],
  ];
  if(organizationQuery.isLoading&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Loading workspace settings..."/><Card>Loading settings...</Card></div>;
  if(organizationQuery.isError&&!isDemo)return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Organization profile, risk methodology, security, integrations, and notification preferences."/><div className="form-error">{organizationQuery.error.message}</div></div>;
  return <div className="page-stack"><PageHeader eyebrow="Workspace administration" title="Settings" description="Organization profile, risk methodology, security, integrations, and notification preferences."/><div className="settings-layout"><Card className="settings-nav">{["Organization","Risk methodology","Security","Integrations","Notifications","Profile"].filter((item)=>admin||["Security","Notifications","Profile"].includes(item)).map(item=><button type="button" className={tab===item?"active":""} key={item} onClick={()=>{setTab(item);setMessage("")}}>{settingIcon(item)}{item}</button>)}</Card><Card className="settings-content"><div className="settings-head"><div><h2>{tab}</h2><p>Configure {tab.toLowerCase()} preferences for the RiskGuard workspace.</p></div><button type="button" className="button button-primary" disabled={!canSave||saving} onClick={save}>{saving?"Saving...":"Save changes"}</button></div>{message?<div className={message.includes("saved")?"attention-banner attention-info":"form-error"}>{message}</div>:null}{tab==="Organization"?<><div className="organization-banner">{logo?<img className="org-logo-preview" src={logo} alt="Organization logo"/>:<span className="org-avatar org-avatar-large">{initials(form.name||"RiskGuard")}</span>}<div><strong>{form.name||"RiskGuard workspace"}</strong><span>{form.industry||"Industry not set"} · {form.country||"Country not set"}</span></div><label className="button button-secondary">Change logo<input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={changeLogo}/></label></div><div className="form-grid"><label>Company name<input value={form.name} disabled={!canSave} onChange={(event)=>setForm({...form,name:event.target.value})}/></label><label>Registration number<input value={form.registrationNumber} disabled={!canSave} onChange={(event)=>setForm({...form,registrationNumber:event.target.value})}/></label><label>Industry<input value={form.industry} disabled={!canSave} onChange={(event)=>setForm({...form,industry:event.target.value})}/></label><label>Country<input value={form.country} disabled={!canSave} onChange={(event)=>setForm({...form,country:event.target.value})}/></label><label>Employee count<input type="number" min="0" value={form.employeeCount} disabled={!canSave} onChange={(event)=>setForm({...form,employeeCount:Number(event.target.value)})}/></label><label>Primary contact<input value={form.primaryContact} disabled={!canSave} onChange={(event)=>setForm({...form,primaryContact:event.target.value})}/></label><label>Contact email<input type="email" value={form.email} disabled={!canSave} onChange={(event)=>setForm({...form,email:event.target.value})}/></label><label>Phone<input value={form.phone} disabled={!canSave} onChange={(event)=>setForm({...form,phone:event.target.value})}/></label></div><label className="field-label">Business address<textarea value={form.address} disabled={!canSave} onChange={(event)=>setForm({...form,address:event.target.value})}/></label></>:tab==="Risk methodology"?<div className="form-grid"><label>Low threshold<input type="number" min="0" max="100" value={preferences.lowThreshold} onChange={(event)=>setPreferences({...preferences,lowThreshold:Number(event.target.value)})}/></label><label>Medium threshold<input type="number" min="0" max="100" value={preferences.mediumThreshold} onChange={(event)=>setPreferences({...preferences,mediumThreshold:Number(event.target.value)})}/></label><label>High threshold<input type="number" min="0" max="100" value={preferences.highThreshold} onChange={(event)=>setPreferences({...preferences,highThreshold:Number(event.target.value)})}/></label><label>Review cadence<select value={preferences.reviewCadence} onChange={(event)=>setPreferences({...preferences,reviewCadence:event.target.value})}>{["Monthly","Quarterly","Biannually","Annually"].map((value)=><option key={value}>{value}</option>)}</select></label></div>:tab==="Security"?<div className="form-grid"><label>Current password<input type="password" value={password.currentPassword} onChange={(event)=>setPassword({...password,currentPassword:event.target.value})} required/></label><label>New password<input type="password" minLength={10} value={password.newPassword} onChange={(event)=>setPassword({...password,newPassword:event.target.value})} required/></label><label>Confirm new password<input type="password" minLength={10} value={password.confirmPassword} onChange={(event)=>setPassword({...password,confirmPassword:event.target.value})} required/></label></div>:tab==="Integrations"?<div className="form-grid"><label>SIEM endpoint<input type="url" value={preferences.siemUrl} onChange={(event)=>setPreferences({...preferences,siemUrl:event.target.value})} placeholder="https://siem.example.com"/></label><label>Ticketing endpoint<input type="url" value={preferences.ticketingUrl} onChange={(event)=>setPreferences({...preferences,ticketingUrl:event.target.value})} placeholder="https://tickets.example.com"/></label><label>Webhook endpoint<input type="url" value={preferences.webhookUrl} onChange={(event)=>setPreferences({...preferences,webhookUrl:event.target.value})} placeholder="https://hooks.example.com/riskguard"/></label></div>:tab==="Notifications"?<div className="settings-options">{notificationOptions.map(([label,key])=><div key={key}><span><strong>{label}</strong><small>Show this notification category in your RiskGuard work queue.</small></span><button type="button" className={`toggle ${preferences[key]?"active":""}`} aria-pressed={preferences[key]} onClick={()=>setPreferences({...preferences,[key]:!preferences[key]})}><i/></button></div>)}</div>:<div className="form-grid"><label>First name<input value={profile.firstName} onChange={(event)=>setProfile({...profile,firstName:event.target.value})} required/></label><label>Last name<input value={profile.lastName} onChange={(event)=>setProfile({...profile,lastName:event.target.value})} required/></label><label>Email<input value={user?.email??""} disabled/></label><label>Phone<input value={profile.phoneNumber} onChange={(event)=>setProfile({...profile,phoneNumber:event.target.value})}/></label></div>}</Card></div></div>;
}

interface WorkspacePreferences {
  lowThreshold: number;
  mediumThreshold: number;
  highThreshold: number;
  reviewCadence: string;
  siemUrl: string;
  ticketingUrl: string;
  webhookUrl: string;
  criticalAlerts: boolean;
  assessmentDeadlines: boolean;
  incidentAssignments: boolean;
  reportReminders: boolean;
}

function loadPreferences():WorkspacePreferences {
  const defaults={lowThreshold:25,mediumThreshold:50,highThreshold:75,reviewCadence:"Quarterly",siemUrl:"",ticketingUrl:"",webhookUrl:"",criticalAlerts:true,assessmentDeadlines:true,incidentAssignments:true,reportReminders:true};
  try{return{...defaults,...JSON.parse(localStorage.getItem("riskguard.workspacePreferences")??"{}")} as WorkspacePreferences}catch{return defaults}
}

function RegisterPage({title,eyebrow,description,action,actionIcon,onAction,actionDisabled=false,metrics,children,search,onSearch,filter,filterOptions,onFilter}:{title:string;eyebrow:string;description:string;action?:string;actionIcon?:ReactNode;onAction?:()=>void|Promise<void>;actionDisabled?:boolean;metrics:[string,string,string,typeof Activity,string][];children:ReactNode;search?:string;onSearch?:(value:string)=>void;filter?:string;filterOptions?:string[];onFilter?:(value:string)=>void}) {
  const actionButton=action&&onAction
    ? <button type="button" className="button button-primary" disabled={actionDisabled} onClick={onAction}>{actionIcon??<Plus size={16}/>} {action}</button>
    : undefined;
  const hasToolbar=Boolean(onSearch||onFilter);
  return <div className="page-stack"><PageHeader eyebrow={eyebrow} title={title} description={description} actions={actionButton}/><div className="metric-grid metric-grid-compact">{metrics.map(([label,value,detail,Icon,tone])=><MetricCard key={label} label={label} value={value} detail={detail} icon={<Icon/>} tone={tone as "blue"|"red"|"orange"|"green"|"purple"}/>)}</div><Card className="table-card">{hasToolbar?<div className="table-toolbar">{onSearch?<div className="inline-search"><Search size={16}/><input value={search??""} onChange={(event)=>onSearch(event.target.value)} placeholder={`Search ${title.toLowerCase()}...`}/></div>:<span/>}{onFilter?<label className="button button-secondary"><Filter size={15}/><select aria-label={`Filter ${title}`} value={filter} onChange={(event)=>onFilter(event.target.value)}>{filterOptions?.map((option)=><option key={option}>{option}</option>)}</select></label>:null}</div>:null}<div className="table-wrap">{children}</div></Card></div>
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
