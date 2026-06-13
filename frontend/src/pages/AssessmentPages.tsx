import { useEffect, useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  Check,
  CheckCircle2,
  CircleAlert,
  ClipboardCheck,
  Clock3,
  Download,
  Filter,
  MoreHorizontal,
  Plus,
} from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, downloadReport } from "../lib/api";
import { assessmentsDemo } from "../data/demo";
import type {
  Assessment,
  AssessmentQuestion,
  AssessmentResult,
  Department,
  RiskCategory,
  UserSummary,
  RiskCalculationResult,
} from "../types";
import { Badge, Card, ComingSoonButton, formatDate, MetricCard, PageHeader, ProgressBar, RiskBadge } from "../components/ui";
import { useAuth } from "../context/AuthContext";

const demoQuestions: AssessmentQuestion[] = [
  "Is multi-factor authentication enabled for all privileged users?",
  "Are passwords protected by strong complexity requirements?",
  "Are admin accounts reviewed regularly?",
  "Is endpoint protection installed on all business devices?",
  "Are systems patched within an approved timeframe?",
  "Are failed login attempts monitored?",
  "Is sensitive data encrypted at rest and in transit?",
  "Are backups protected from ransomware?",
].map((text, index) => ({
  id: `demo-question-${index}`,
  text,
  weight: index === 0 || index === 2 || index === 6 ? 2 : 1,
  answerType: "YesNo",
  scoreMappingJson: "{\"Yes\":0,\"Partially\":50,\"No\":100,\"Not applicable\":0}",
  recommendationText: "Implement, test, and retain evidence for the required control.",
  complianceMappings: "ISO 27001; NIST CSF; CIS Controls",
}));

export function AssessmentsPage() {
  const { user, isDemo } = useAuth();
  const query = useQuery({
    queryKey: ["assessments"],
    queryFn: () => api<Assessment[]>("/assessments", {}, assessmentsDemo),
  });
  const [filter, setFilter] = useState("All");
  const all = query.data ?? (isDemo ? assessmentsDemo : []);
  const assessments = all.filter((item) => filter === "All" || item.status === filter);
  const active = all.filter((item) => !["Reviewed", "Approved", "Archived"].includes(item.status));
  const submitted = all.filter((item) => item.status === "Submitted").length;
  const overdue = active.filter((item) => new Date(item.dueDateUtc) < new Date()).length;
  const completion = all.length === 0
    ? 0
    : Math.round(all.filter((item) => ["Submitted", "Reviewed", "Approved"].includes(item.status)).length * 100 / all.length);
  const canCreate = user?.roles.some((role) =>
    ["Admin", "Risk Manager", "Compliance Officer", "Security Analyst"].includes(role));

  if (query.isLoading && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Structured assurance" title="Risk assessments" description="Loading assessment data..."/><Card>Loading assessments...</Card></div>;
  }
  if (query.isError && !isDemo) {
    return <div className="page-stack"><PageHeader eyebrow="Structured assurance" title="Risk assessments" description="Assign, complete, review, and approve weighted assessments across the enterprise."/><div className="form-error">{query.error.message}</div></div>;
  }

  return (
    <div className="page-stack">
      <PageHeader
        eyebrow="Structured assurance"
        title="Risk assessments"
        description="Assign, complete, review, and approve weighted assessments across the enterprise."
        actions={canCreate ? <Link className="button button-primary" to="/app/assessments/new"><Plus size={16}/> New assessment</Link> : undefined}
      />
      <div className="metric-grid metric-grid-compact">
        <MetricCard label="Active assessments" value={`${active.length}`} detail="Assigned or in progress" icon={<ClipboardCheck/>} tone="blue"/>
        <MetricCard label="Awaiting review" value={`${submitted}`} detail="Submitted assessments" icon={<Clock3/>} tone="orange"/>
        <MetricCard label="Completion rate" value={`${completion}%`} detail="Current register" icon={<CheckCircle2/>} tone="green"/>
        <MetricCard label="Overdue" value={`${overdue}`} detail={overdue ? "Needs owner attention" : "All actions on track"} icon={<CircleAlert/>} tone="purple"/>
      </div>
      <Card className="table-card">
        <div className="table-toolbar">
          <div className="tabs">
            {["All","Draft","Assigned","InProgress","Submitted","Reviewed"].map((item) =>
              <button className={filter === item ? "active" : ""} onClick={() => setFilter(item)} key={item}>
                {item === "InProgress" ? "In progress" : item}
              </button>)}
          </div>
          <div className="toolbar-actions"><ComingSoonButton><Filter size={15}/> Advanced filters</ComingSoonButton><ComingSoonButton className="icon-button" aria-label="More assessment options"><MoreHorizontal size={18}/></ComingSoonButton></div>
        </div>
        <div className="table-wrap"><table><thead><tr><th>Assessment</th><th>Department</th><th>Assignee</th><th>Due date</th><th>Status</th><th>Risk score</th><th></th></tr></thead><tbody>
          {assessments.map((assessment) => <tr key={assessment.id}>
            <td><Link className="cell-link" to={`/app/assessments/${assessment.id}`}><span className="type-icon"><ClipboardCheck size={17}/></span><span><strong>{assessment.title}</strong><small>{assessment.riskCategory?.name}</small></span></Link></td>
            <td>{assessment.department?.name ?? "Enterprise"}</td>
            <td><span className="owner-cell"><i>{initials(assessment.assignedToName)}</i>{assessment.assignedToName}</span></td>
            <td>{formatDate(assessment.dueDateUtc)}</td>
            <td><StatusBadge status={assessment.status}/></td>
            <td>{assessment.score ? <span className="score-cell"><strong>{assessment.score}</strong><RiskBadge level={assessment.riskLevel}/></span> : <span className="muted">Pending</span>}</td>
            <td><Link className="icon-button" aria-label={`Open ${assessment.title}`} to={`/app/assessments/${assessment.id}`}><ArrowRight size={16}/></Link></td>
          </tr>)}
        </tbody></table></div>
      </Card>
    </div>
  );
}

export function AssessmentWorkspacePage() {
  const { isDemo, user } = useAuth();
  const { id } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [step, setStep] = useState(0);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [savingDraft, setSavingDraft] = useState(false);
  const [calculating, setCalculating] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const isNew = !id;
  const assessmentQuery = useQuery({
    queryKey: ["assessment", id],
    enabled: Boolean(id),
    queryFn: () => api<Assessment>(`/assessments/${id}`, {}, assessmentsDemo.find((item) => item.id === id) ?? assessmentsDemo[0]),
  });
  const assessment = assessmentQuery.data;
  const questionsQuery = useQuery({
    queryKey: ["assessment-questions", id],
    enabled: Boolean(id && assessment),
    queryFn: () => api<AssessmentQuestion[]>(`/assessments/${id}/questions`, {}, demoQuestions),
  });
  const questions = questionsQuery.data ?? (isDemo ? demoQuestions : []);
  const question = questions[Math.min(step, Math.max(questions.length - 1, 0))];
  const finalized = assessment ? ["Submitted", "Reviewed", "Approved", "Archived"].includes(assessment.status) : false;
  const resultQuery = useQuery({
    queryKey: ["assessment-result", id],
    enabled: Boolean(id && finalized && !isDemo),
    queryFn: () => api<AssessmentResult>(`/assessments/${id}/results`),
  });
  const progress = questions.length ? Math.round(Object.keys(answers).length * 100 / questions.length) : 0;

  useEffect(() => {
    if (!assessment?.responses) return;
    setAnswers(Object.fromEntries(assessment.responses.map((response) => [response.questionId, response.answer])));
    setNotes(Object.fromEntries(assessment.responses.map((response) => [response.questionId, response.notes])));
  }, [assessment]);

  async function submitAssessment() {
    if (!id || questions.length === 0) return;
    if (questions.some((item) => !answers[item.id])) {
      setMessage("Answer every question before submitting the assessment.");
      return;
    }
    setSubmitting(true);
    setMessage("");
    try {
      const result = await api<AssessmentResult>(`/assessments/${id}/submit`, {
        method: "POST",
        body: JSON.stringify({
          responses: questions.map((item) => ({
            questionId: item.id,
            answer: answers[item.id],
            notes: notes[item.id] ?? "",
          })),
        }),
      });
      queryClient.setQueryData(["assessment-result", id], result);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["assessment", id] }),
        queryClient.invalidateQueries({ queryKey: ["assessments"] }),
        queryClient.invalidateQueries({ queryKey: ["risks"] }),
        queryClient.invalidateQueries({ queryKey: ["recommendations"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
        queryClient.invalidateQueries({ queryKey: ["audit"] }),
      ]);
      setMessage("Assessment submitted and risk results updated.");
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Assessment submission failed.");
    } finally {
      setSubmitting(false);
    }
  }

  async function saveDraft(advance = false) {
    if (!id || finalized) return;
    setSavingDraft(true);
    setMessage("");
    try {
      await api(`/assessments/${id}/draft`, {
        method: "PUT",
        body: JSON.stringify({
          responses: questions
            .filter((item) => answers[item.id])
            .map((item) => ({
              questionId: item.id,
              answer: answers[item.id],
              notes: notes[item.id] ?? "",
            })),
        }),
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["assessment", id] }),
        queryClient.invalidateQueries({ queryKey: ["assessments"] }),
      ]);
      setMessage("Draft saved.");
      if (advance && step < questions.length - 1) setStep(step + 1);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Draft save failed.");
    } finally {
      setSavingDraft(false);
    }
  }

  async function downloadAssessmentReport() {
    if (!id) return;
    setMessage("");
    setDownloading(true);
    try {
      await downloadReport(`/reports/risk/pdf/${id}`, `RiskGuard-${assessment?.title ?? "assessment"}.pdf`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Assessment report generation failed.");
    } finally {
      setDownloading(false);
    }
  }

  async function calculateRisk() {
    if (!id) return;
    setCalculating(true);
    setMessage("");
    try {
      const calculation = await api<RiskCalculationResult>(`/assessments/${id}/calculate`, { method: "POST" });
      const result = await api<AssessmentResult>(`/assessments/${id}/results`);
      queryClient.setQueryData(["assessment-result", id], result);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["assessment", id] }),
        queryClient.invalidateQueries({ queryKey: ["assessments"] }),
        queryClient.invalidateQueries({ queryKey: ["risks"] }),
        queryClient.invalidateQueries({ queryKey: ["recommendations"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
      ]);
      setMessage(`Risk score recalculated: ${Math.round(calculation.score)} (${calculation.level}).`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Risk calculation failed.");
    } finally {
      setCalculating(false);
    }
  }

  const canCalculate = user?.roles.some((role) =>
    ["Admin", "Risk Manager", "Compliance Officer", "Security Analyst"].includes(role));

  return (
    <div className="page-stack assessment-workspace">
      <button className="back-link" onClick={() => navigate("/app/assessments")}><ArrowLeft size={16}/> Back to assessments</button>
      <PageHeader
        eyebrow={isNew ? "Create assessment" : "Assessment workspace"}
        title={assessment?.title ?? "New risk assessment"}
        description={isNew ? "Define scope, ownership, category, and review dates." : finalized ? "This assessment is read-only because it has entered review." : "Complete every control and submit the assessment for review."}
        actions={!isNew && finalized
          ? <><Link className="button button-secondary" to="/app/recommendations">View recommendations</Link>
              {canCalculate ? <button type="button" className="button button-secondary" disabled={calculating} onClick={calculateRisk}>{calculating ? "Calculating..." : "Calculate risk"}</button> : null}
              <button type="button" className="button button-primary" disabled={downloading} onClick={downloadAssessmentReport}><Download size={16}/> {downloading ? "Generating..." : "Download report"}</button></>
          : !isNew
            ? <><button type="button" className="button button-secondary" disabled={savingDraft || submitting} onClick={() => saveDraft(false)}>{savingDraft ? "Saving..." : "Save draft"}</button><button type="button" className="button button-primary" disabled={submitting || savingDraft || questions.length === 0} onClick={submitAssessment}>{submitting ? "Submitting..." : "Submit assessment"}</button></>
            : undefined}
      />
      {message ? <div className="form-error">{message}</div> : null}
      {isNew ? <CreateAssessmentForm/> : assessmentQuery.isLoading ? <Card>Loading assessment...</Card> : assessmentQuery.isError ? <div className="form-error">{assessmentQuery.error.message}</div> : questionsQuery.isLoading ? <Card>Loading risk questions...</Card> : questionsQuery.isError ? <div className="form-error">{questionsQuery.error.message}</div> : (
        <>
        {finalized && !isDemo ? <AssessmentResults result={resultQuery.data} loading={resultQuery.isLoading} error={resultQuery.error}/>: null}
        <div className="assessment-layout">
          <Card className="assessment-nav">
            <div className="assessment-progress"><div><span>Completion</span><strong>{progress}%</strong></div><ProgressBar value={progress} tone="blue"/><small>{Object.keys(answers).length} of {questions.length} answered</small></div>
            <div className="question-nav">{questions.map((item,index) =>
              <button type="button" key={item.id} className={`${step === index ? "active" : ""} ${answers[item.id] ? "complete" : ""}`} onClick={() => setStep(index)}>
                <span>{answers[item.id] ? <Check size={14}/> : index + 1}</span><div><strong>Question {index + 1}</strong><small>{item.complianceMappings.split(";")[0]}</small></div>
              </button>)}</div>
          </Card>
          <Card className="question-card">
            <div className="question-meta"><Badge tone="info">{assessment?.riskCategory?.name ?? "Risk control"}</Badge><span>Weight: {question?.weight ?? 1}x</span><span>Question {step + 1} of {questions.length}</span></div>
            <h2>{question?.text}</h2>
            <p>Select the answer that best reflects the control's current operating effectiveness.</p>
            <div className="answer-grid">
              {[["Yes","Control is implemented and evidenced","0"],["Partially","Control exists but is incomplete","50"],["No","Control is not implemented","100"],["Not applicable","Outside the approved scope","N/A"]].map(([answer,detail,score]) =>
                <button type="button" key={answer} disabled={finalized} className={answers[question?.id] === answer ? "selected" : ""} onClick={() => question && setAnswers({...answers,[question.id]:answer})}>
                  <span className="radio-dot"/><span><strong>{answer}</strong><small>{detail}</small></span><b>{score}</b>
                </button>)}
            </div>
            <label className="field-label">Assessment notes<textarea value={notes[question?.id] ?? ""} disabled={finalized} onChange={(event) => question && setNotes({...notes,[question.id]:event.target.value})} placeholder="Explain the current control, gaps, exceptions, or compensating measures..."/></label>
            <div className="evidence-drop"><CheckCircle2 size={24}/><strong>Evidence references</strong><span>Record evidence details in the notes. File storage is not configured in this local build.</span></div>
            <div className="question-actions"><button type="button" className="button button-secondary" disabled={step === 0} onClick={() => setStep(step - 1)}>Previous</button><button type="button" className="button button-primary" disabled={finalized || savingDraft || !answers[question?.id] || step === questions.length - 1} onClick={() => saveDraft(true)}>{savingDraft ? "Saving..." : "Save & continue"} <ArrowRight size={16}/></button></div>
          </Card>
          <Card className="assessment-context">
            <span className="card-kicker">Control context</span><h3>Why this matters</h3><p>{question?.recommendationText || "A documented and evidenced control reduces exposure and supports assurance."}</p>
            <div className="context-block"><strong>Compliance mapping</strong>{(question?.complianceMappings || "Internal control").split(";").map((mapping) => <span key={mapping}>{mapping.trim()}</span>)}</div>
            <div className="context-block"><strong>Scoring integrity</strong><span>The API derives the score from the approved answer mapping.</span></div>
          </Card>
        </div>
        </>
      )}
    </div>
  );
}

function AssessmentResults({
  result,
  loading,
  error,
}: {
  result?: AssessmentResult;
  loading: boolean;
  error: Error | null;
}) {
  if (loading) return <Card>Loading assessment results...</Card>;
  if (error) return <div className="form-error">{error.message}</div>;
  if (!result) return null;

  return (
    <div className="page-stack">
      <div className="metric-grid metric-grid-compact">
        <MetricCard label="Overall risk score" value={`${Math.round(result.overallRiskScore)}`} detail={result.riskLevel} icon={<CircleAlert/>} tone="orange"/>
        <MetricCard label="Questions scored" value={`${result.answers.length}`} detail={result.category} icon={<ClipboardCheck/>} tone="blue"/>
        <MetricCard label="Recommendations" value={`${result.recommendations.length}`} detail="Generated treatment actions" icon={<CheckCircle2/>} tone="green"/>
        <MetricCard label="Compliance gaps" value={`${result.complianceGaps.length}`} detail={result.department} icon={<CircleAlert/>} tone="purple"/>
      </div>
      <Card>
        <div className="card-head"><div><span className="card-kicker">Calculated result</span><h2>{result.riskTitle ?? result.title}</h2></div><Link to="/app/recommendations">View recommendations <ArrowRight size={15}/></Link></div>
        <RiskBadge level={result.riskLevel}/>
        {result.recommendations.length === 0 ? <p className="muted">No high-priority recommendations were generated.</p> : (
          <div className="action-list">
            {result.recommendations.map((item, index) => (
              <div className="action-item" key={item.id}>
                <span className="action-rank">{index + 1}</span>
                <span><strong>{item.title}</strong><small>{item.suggestedOwner} - due {formatDate(item.dueDateUtc)}</small></span>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

function CreateAssessmentForm() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const demoCategories: RiskCategory[] = [{ id: "demo-category", name: "Cybersecurity", type: "Cybersecurity", description: "" }];
  const demoDepartments: Department[] = [{ id: "demo-department", organizationId: "demo-organization", name: "IT" }];
  const demoUsers: UserSummary[] = [{ id: "demo-user", email: "security@riskguard.local", fullName: "Naledi Khumalo", roles: ["Security Analyst"], organizationId: "demo-organization" }];
  const categories = useQuery({ queryKey: ["risk-categories"], queryFn: () => api<RiskCategory[]>("/risk-categories", {}, demoCategories) });
  const departments = useQuery({ queryKey: ["departments"], queryFn: () => api<Department[]>("/departments", {}, demoDepartments) });
  const assignees = useQuery({ queryKey: ["assignees"], queryFn: () => api<UserSummary[]>("/users/assignees", {}, demoUsers) });
  const referenceError = categories.error ?? departments.error ?? assignees.error;
  const [form, setForm] = useState({
    title: "Quarterly Enterprise Risk Assessment",
    riskCategoryId: "",
    departmentId: "",
    assignedToUserId: "",
    dueDateUtc: new Date(Date.now() + 14 * 86_400_000).toISOString().slice(0, 10),
  });
  const [message, setMessage] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setForm((current) => ({
      ...current,
      riskCategoryId: current.riskCategoryId || categories.data?.[0]?.id || "",
      departmentId: current.departmentId || departments.data?.[0]?.id || "",
      assignedToUserId: current.assignedToUserId || assignees.data?.[0]?.id || "",
    }));
  }, [categories.data, departments.data, assignees.data]);

  async function create(event: FormEvent) {
    event.preventDefault();
    const assignee = assignees.data?.find((item) => item.id === form.assignedToUserId);
    if (!user?.organizationId || !assignee) {
      setMessage("Workspace or assignee data is not available.");
      return;
    }
    setSaving(true);
    setMessage("");
    try {
      const created = await api<Assessment>("/assessments", {
        method: "POST",
        body: JSON.stringify({
          organizationId: user.organizationId,
          departmentId: form.departmentId || null,
          riskCategoryId: form.riskCategoryId,
          title: form.title,
          assignedToUserId: assignee.id,
          assignedToName: assignee.fullName,
          dueDateUtc: new Date(`${form.dueDateUtc}T12:00:00Z`).toISOString(),
        }),
      });
      await queryClient.invalidateQueries({ queryKey: ["assessments"] });
      navigate(`/app/assessments/${created.id}`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Assessment creation failed.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card className="form-card"><form onSubmit={create}>
      <div className="form-section"><span className="step-number">1</span><div><h2>Assessment scope</h2><p>Choose the business area and risk domain to assess.</p><div className="form-grid">
        <label>Assessment title<input value={form.title} onChange={(event) => setForm({...form,title:event.target.value})} required/></label>
        <label>Risk category<select value={form.riskCategoryId} onChange={(event) => setForm({...form,riskCategoryId:event.target.value})} required>{categories.data?.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
        <label>Department<select value={form.departmentId} onChange={(event) => setForm({...form,departmentId:event.target.value})} required>{departments.data?.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
        <label>Assessment method<input value="Standard weighted control assessment" disabled/></label>
      </div></div></div>
      <div className="form-section"><span className="step-number">2</span><div><h2>Ownership and schedule</h2><p>Assign accountability and a clear review timeline.</p><div className="form-grid">
        <label>Assignee<select value={form.assignedToUserId} onChange={(event) => setForm({...form,assignedToUserId:event.target.value})} required>{assignees.data?.map((item) => <option value={item.id} key={item.id}>{item.fullName} - {item.roles[0]}</option>)}</select></label>
        <label>Due date<input type="date" value={form.dueDateUtc} onChange={(event) => setForm({...form,dueDateUtc:event.target.value})} required/></label>
      </div></div></div>
      {referenceError ? <div className="form-error">{referenceError.message}</div> : null}
      {message ? <div className="form-error">{message}</div> : null}
      {!categories.isLoading && categories.data?.length === 0 ? <div className="form-error">No risk categories are configured. An administrator must load the assessment reference data before an assessment can be created.</div> : null}
      <div className="form-footer"><Link to="/app/assessments" className="button button-secondary">Cancel</Link><button type="submit" className="button button-primary" disabled={saving || categories.isLoading || departments.isLoading || assignees.isLoading || Boolean(referenceError) || !form.riskCategoryId || !form.departmentId || !form.assignedToUserId}>{saving ? "Creating..." : "Create and assign"} <ArrowRight size={16}/></button></div>
    </form></Card>
  );
}

function StatusBadge({status}:{status:string}) {
  const clean = status.replace(/([a-z])([A-Z])/g, "$1 $2");
  const lower = status.toLowerCase();
  const tone = lower.includes("complete") || lower === "active" || lower === "ready"
    ? "success"
    : lower.includes("progress") || lower.includes("review") || lower.includes("submit")
      ? "info"
      : lower.includes("overdue")
        ? "high"
        : "neutral";
  return <Badge tone={tone}>{clean}</Badge>;
}

function initials(value:string) {
  return value.split(/[\s@.]+/).filter(Boolean).map((item) => item[0]).join("").slice(0,2).toUpperCase();
}
