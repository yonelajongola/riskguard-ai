import { useState, type ChangeEvent, type FormEvent } from "react";
import {
  ArrowRight,
  Bot,
  Building2,
  Check,
  ClipboardCheck,
  FileCheck2,
  Gauge,
  Menu,
  Network,
  ShieldCheck,
  Sparkles,
  X,
} from "lucide-react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { demoModeEnabled, registrationEnabled } from "../lib/config";

const features = [
  ["Risk assessments", "Structured, weighted assessments across eight enterprise risk categories.", ClipboardCheck],
  ["AI recommendations", "Prioritized actions, mitigation plans, and management-ready summaries.", Bot],
  ["Executive dashboards", "Live exposure, trend, financial impact, and ownership visibility.", Gauge],
  ["Compliance monitoring", "POPIA, GDPR, ISO 27001, NIST CSF, and CIS control readiness.", FileCheck2],
  ["Vendor intelligence", "Third-party dependency, contract, security, and compliance risk.", Network],
  ["Business continuity", "RTO, RPO, backup readiness, disaster recovery, and downtime exposure.", Building2],
];

export function LandingPage() {
  const { useDemo } = useAuth();
  const navigate = useNavigate();
  const [menu, setMenu] = useState(false);
  const enterDemo = () => { useDemo(); navigate("/app"); };

  return (
    <div className="landing">
      <nav className="landing-nav container">
        <Link to="/" className="brand">
          <span className="brand-mark"><ShieldCheck size={22} /></span>
          <span><strong>RiskGuard</strong><small>AI</small></span>
        </Link>
        <div className={`landing-links ${menu ? "open" : ""}`}>
          <a href="#platform">Platform</a><a href="#intelligence">Intelligence</a><a href="#compliance">Compliance</a><a href="#pricing">Pricing</a>
          <Link to="/login" className="button button-ghost">Sign in</Link>
          <Link to={registrationEnabled ? "/register" : "/login"} className="button button-primary">
            {registrationEnabled ? "Get started" : "Open workspace"} <ArrowRight size={16} />
          </Link>
        </div>
        <button className="icon-button landing-menu" onClick={() => setMenu((v) => !v)}>{menu ? <X /> : <Menu />}</button>
      </nav>

      <section className="hero container">
        <div className="hero-glow" />
        <div className="hero-copy">
          <div className="announcement"><Sparkles size={15} /> Enterprise risk, made decision-ready <ArrowRight size={14} /></div>
          <h1>AI-powered enterprise <span>risk intelligence</span></h1>
          <p>Identify, assess, monitor, and reduce business, cybersecurity, compliance, vendor, and operational risks from one intelligent platform.</p>
          <div className="hero-actions">
            <Link to={registrationEnabled ? "/register" : "/login"} className="button button-primary button-large">
              {registrationEnabled ? "Start assessing risk" : "Sign in to RiskGuard"} <ArrowRight size={18} />
            </Link>
            {demoModeEnabled ? <button onClick={enterDemo} className="button button-secondary button-large">Explore live demo</button> : null}
          </div>
          <div className="trust-row"><span><Check size={15} /> No cloud services required</span><span><Check size={15} /> POPIA-ready controls</span><span><Check size={15} /> Azure deployment ready</span></div>
        </div>
        <DashboardPreview />
      </section>

      <section className="logo-cloud container"><span>BUILT FOR CONTROL ENVIRONMENTS IN</span><div><b>Financial Services</b><b>Healthcare</b><b>Retail</b><b>Government</b><b>Technology</b></div></section>

      <section id="platform" className="section container">
        <div className="section-heading"><span className="eyebrow">One connected platform</span><h2>From scattered evidence to a clear risk position</h2><p>Replace spreadsheets, email chains, and disconnected reports with a governed system of record.</p></div>
        <div className="feature-grid">
          {features.map(([title, description, Icon]) => (
            <article className="feature-card" key={title as string}><span><Icon size={22} /></span><h3>{title as string}</h3><p>{description as string}</p><Link to={registrationEnabled ? "/register" : "/login"}>Explore capability <ArrowRight size={15} /></Link></article>
          ))}
        </div>
      </section>

      <section id="intelligence" className="section split-section">
        <div className="container split-grid">
          <div>
            <span className="eyebrow">Risk intelligence engine</span>
            <h2>Know what matters, who owns it, and what to do next</h2>
            <p>RiskGuard converts weighted control responses into exposure scores, compliance gaps, accountable actions, and board-level insight.</p>
            <ul className="check-list">
              <li><Check /> Consistent 0-100 weighted scoring</li>
              <li><Check /> Impact and likelihood heat mapping</li>
              <li><Check /> Control-to-framework traceability</li>
              <li><Check /> Historical trends and financial exposure</li>
            </ul>
          </div>
          <div className="intelligence-card">
            <div className="intelligence-top"><span>AI PRIORITY BRIEF</span><BadgeDot label="Updated now" /></div>
            <h3>Three actions will reduce current exposure by an estimated 24%</h3>
            {["Enforce MFA for privileged access", "Complete backup restoration testing", "Approve POPIA governance policy"].map((item, i) => (
              <div className="priority-row" key={item}><span>{i + 1}</span><div><strong>{item}</strong><small>{i === 0 ? "Critical · Due in 14 days" : "High · Due in 30 days"}</small></div><ArrowRight size={17} /></div>
            ))}
          </div>
        </div>
      </section>

      <section id="compliance" className="section container compliance-band">
        <div><span className="eyebrow">Compliance command center</span><h2>See readiness across every framework</h2><p>Map one control to many obligations and keep audit evidence attached to the work.</p></div>
        <div className="framework-row">{["POPIA", "GDPR", "ISO 27001", "NIST CSF", "CIS Controls"].map((name, index) => <div key={name}><strong>{[58,62,67,71,63][index]}%</strong><span>{name}</span></div>)}</div>
      </section>

      <section id="pricing" className="section container">
        <div className="section-heading"><span className="eyebrow">Simple starting points</span><h2>Risk governance that grows with you</h2></div>
        <div className="pricing-grid">
          {[
            ["Starter", "For focused SME risk programs", "R1,490", ["1 organization", "Core assessments", "PDF and Excel reports"]],
            ["Professional", "For growing assurance teams", "R4,990", ["Unlimited assessments", "All compliance frameworks", "AI Copilot and audit trail"]],
            ["Enterprise", "For regulated, multi-team environments", "Custom", ["Multi-tenant architecture", "Azure private deployment", "SSO and custom integrations"]],
          ].map(([name, detail, price, items], index) => (
            <article className={`price-card ${index === 1 ? "featured" : ""}`} key={name as string}>
              {index === 1 ? <span className="popular">MOST POPULAR</span> : null}<h3>{name as string}</h3><p>{detail as string}</p><strong>{price as string}{price !== "Custom" ? <small>/month</small> : null}</strong>
              <ul>{(items as string[]).map((item) => <li key={item}><Check size={16} />{item}</li>)}</ul><button className={`button ${index === 1 ? "button-primary" : "button-secondary"}`}>Choose {name as string}</button>
            </article>
          ))}
        </div>
      </section>

      <section className="cta-section"><div className="container"><span className="brand-mark"><ShieldCheck /></span><h2>Turn risk into confident decisions.</h2><p>{demoModeEnabled ? "See the complete FoodieBar risk workspace with realistic enterprise data." : "Open your governed enterprise risk workspace."}</p>{demoModeEnabled ? <button className="button button-primary button-large" onClick={enterDemo}>Open RiskGuard AI <ArrowRight size={18} /></button> : <Link className="button button-primary button-large" to="/login">Sign in to RiskGuard <ArrowRight size={18} /></Link>}</div></section>
      <footer className="landing-footer container"><Link to="/" className="brand"><span className="brand-mark"><ShieldCheck size={19} /></span><span><strong>RiskGuard</strong><small>AI</small></span></Link><span>Enterprise Risk Intelligence</span><small>© 2026 RiskGuard AI. Portfolio demonstration.</small></footer>
    </div>
  );
}

function DashboardPreview() {
  return (
    <div className="dashboard-preview">
      <div className="preview-bar"><i /><i /><i /><span>riskguard.ai / command-center</span></div>
      <div className="preview-shell">
        <aside><span className="preview-logo">R</span>{Array.from({ length: 7 }).map((_, i) => <i className={i === 0 ? "active" : ""} key={i} />)}</aside>
        <main>
          <div className="preview-head"><div><small>ENTERPRISE RISK</small><strong>Command center</strong></div><BadgeDot label="Live posture" /></div>
          <div className="preview-metrics">
            <div><small>OVERALL RISK</small><strong>67</strong><span>HIGH</span></div>
            <div><small>CRITICAL RISKS</small><strong>1</strong><span>Needs action</span></div>
            <div><small>COMPLIANCE</small><strong>64%</strong><span>+4% this quarter</span></div>
          </div>
          <div className="preview-grid">
            <div className="preview-chart"><small>RISK TREND</small><svg viewBox="0 0 300 100"><defs><linearGradient id="area" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stopColor="#2f8cff" stopOpacity=".4"/><stop offset="1" stopColor="#2f8cff" stopOpacity="0"/></linearGradient></defs><path d="M0,20 C40,28 55,16 90,36 S145,30 175,52 S225,43 255,61 S280,58 300,66 L300,100 L0,100Z" fill="url(#area)"/><path d="M0,20 C40,28 55,16 90,36 S145,30 175,52 S225,43 255,61 S280,58 300,66" fill="none" stroke="#2f8cff" strokeWidth="3"/></svg></div>
            <div className="preview-list"><small>TOP RISKS</small>{["MFA not enforced","Backup test overdue","POPIA policy gap"].map((x,i)=><div key={x}><i className={`severity s${i}`} /><span>{x}</span><b>{[82,71,61][i]}</b></div>)}</div>
          </div>
        </main>
      </div>
    </div>
  );
}

function BadgeDot({ label }: { label: string }) { return <span className="badge-dot"><i />{label}</span>; }

export function LoginPage() {
  const { login, useDemo, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState(demoModeEnabled ? "admin@riskguard.local" : "");
  const [password, setPassword] = useState(demoModeEnabled ? "Admin@12345" : "");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  if (isAuthenticated) return <Navigate to="/app" replace />;

  async function submit(event: FormEvent) {
    event.preventDefault(); setLoading(true); setError("");
    try { await login(email, password); navigate("/app"); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Sign in failed."); }
    finally { setLoading(false); }
  }

  return (
    <div className="auth-page">
      <div className="auth-visual">
        <Link to="/" className="brand"><span className="brand-mark"><ShieldCheck size={22} /></span><span><strong>RiskGuard</strong><small>AI</small></span></Link>
        <div><span className="eyebrow">Enterprise risk intelligence</span><h1>Clarity for every risk decision.</h1><p>One governed view of exposure, controls, incidents, vendors, and resilience.</p></div>
        <div className="auth-quote"><p>“RiskGuard turns assurance work into the management insight leadership actually needs.”</p><span>Portfolio demonstration · FoodieBar workspace</span></div>
      </div>
      <div className="auth-panel">
        <div className="auth-card">
          <span className="mobile-brand brand"><span className="brand-mark"><ShieldCheck /></span><span><strong>RiskGuard</strong><small>AI</small></span></span>
          <h2>Welcome back</h2><p>Sign in to your risk intelligence workspace.</p>
          <form onSubmit={submit}>
            <label>Work email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></label>
            <label>Password<div className="password-row"><input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required /><span>Reset through your administrator</span></div></label>
            {error ? <div className="form-error">{error}</div> : null}
            <button className="button button-primary button-full" disabled={loading}>{loading ? "Signing in..." : "Sign in securely"}<ArrowRight size={17} /></button>
          </form>
          {demoModeEnabled ? <><div className="divider"><span>or</span></div>
            <button className="button button-secondary button-full" onClick={() => { useDemo(); navigate("/app"); }}>Explore demo workspace</button>
            <div className="demo-credentials"><strong>Demo administrator</strong><code>admin@riskguard.local</code><code>Admin@12345</code></div></> : null}
          {registrationEnabled ? <p className="auth-foot">New to RiskGuard? <Link to="/register">Create an account</Link></p> : null}
        </div>
      </div>
    </div>
  );
}

export function RegisterPage() {
  const { register, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    organizationName: "",
    email: "",
    password: "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  if (isAuthenticated) return <Navigate to="/app" replace />;
  if (!registrationEnabled) return <Navigate to="/login" replace />;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setLoading(true);
    try {
      await register(form);
      navigate("/app");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Registration failed.");
    } finally {
      setLoading(false);
    }
  }

  const field = (name: keyof typeof form) => ({
    value: form[name],
    onChange: (event: ChangeEvent<HTMLInputElement>) =>
      setForm((current) => ({ ...current, [name]: event.target.value })),
  });

  return (
    <div className="auth-page auth-simple">
      <div className="auth-panel"><div className="auth-card">
        <Link to="/" className="brand auth-brand"><span className="brand-mark"><ShieldCheck /></span><span><strong>RiskGuard</strong><small>AI</small></span></Link>
        <span className="eyebrow">Create your workspace</span><h2>Start building risk visibility</h2><p>The first account becomes the administrator of an isolated workspace.</p>
        <form onSubmit={submit}>
          <div className="form-grid"><label>First name<input {...field("firstName")} placeholder="Lerato" required /></label><label>Last name<input {...field("lastName")} placeholder="Mokoena" required /></label></div>
          <label>Organization name<input {...field("organizationName")} placeholder="Acme Operations" required /></label>
          <label>Work email<input {...field("email")} type="email" placeholder="you@company.com" required /></label>
          <label>Password<input {...field("password")} type="password" placeholder="10+ characters, upper/lower, number, symbol" minLength={10} required /></label>
          {error ? <div className="form-error">{error}</div> : null}
          <button className="button button-primary button-full" disabled={loading}>{loading ? "Creating workspace..." : "Create workspace"} <ArrowRight size={17} /></button>
        </form>
        <p className="auth-foot">Already have an account? <Link to="/login">Sign in</Link></p>
      </div></div>
    </div>
  );
}

export function NotFoundPage() {
  return <div className="status-page"><span>404</span><h1>That route is outside the register.</h1><p>The page may have moved or you may not have access.</p><Link className="button button-primary" to="/app">Return to command center</Link></div>;
}

export function NotAuthorizedPage() {
  return <div className="status-page"><ShieldCheck size={52} /><h1>Access is restricted.</h1><p>Your current role does not have permission to view this area.</p><Link className="button button-primary" to="/app">Return to command center</Link></div>;
}
