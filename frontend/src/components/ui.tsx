import type { ButtonHTMLAttributes, ReactNode } from "react";
import { ArrowDownRight, ArrowUpRight, Minus } from "lucide-react";
import type { RiskLevel } from "../types";

export function Card({
  children,
  className = "",
  interactive = false,
}: {
  children: ReactNode;
  className?: string;
  interactive?: boolean;
}) {
  return <section className={`card ${interactive ? "card-interactive" : ""} ${className}`}>{children}</section>;
}

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "low" | "medium" | "high" | "critical" | "info" | "neutral" | "success";
}) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

export function RiskBadge({ level }: { level: RiskLevel | string }) {
  return <Badge tone={level.toLowerCase() as "low" | "medium" | "high" | "critical"}>{level}</Badge>;
}

export function MetricCard({
  label,
  value,
  detail,
  icon,
  tone = "blue",
  trend,
}: {
  label: string;
  value: string;
  detail: string;
  icon: ReactNode;
  tone?: "blue" | "red" | "orange" | "green" | "purple";
  trend?: number;
}) {
  return (
    <Card className={`metric metric-${tone}`}>
      <div className="metric-top">
        <span className="metric-label">{label}</span>
        <span className="metric-icon">{icon}</span>
      </div>
      <strong className="metric-value">{value}</strong>
      <div className="metric-foot">
        {trend !== undefined ? (
          <span className={`trend ${trend < 0 ? "trend-good" : trend > 0 ? "trend-bad" : ""}`}>
            {trend < 0 ? <ArrowDownRight size={14} /> : trend > 0 ? <ArrowUpRight size={14} /> : <Minus size={14} />}
            {Math.abs(trend)}%
          </span>
        ) : null}
        <span>{detail}</span>
      </div>
    </Card>
  );
}

export function ComingSoonButton({
  children,
  className = "button button-secondary",
  title = "Coming soon",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      type="button"
      className={className}
      disabled
      title={title}
      aria-label={`${typeof children === "string" ? children : "Action"} - Coming soon`}
    >
      {children} <span className="coming-soon-label">Coming soon</span>
    </button>
  );
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow?: string;
  title: string;
  description: string;
  actions?: ReactNode;
}) {
  return (
    <header className="page-header">
      <div>
        {eyebrow ? <div className="eyebrow">{eyebrow}</div> : null}
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
  );
}

export function ProgressBar({
  value,
  tone,
}: {
  value: number;
  tone?: "low" | "medium" | "high" | "critical" | "blue";
}) {
  const resolved =
    tone ??
    (value <= 25 ? "low" : value <= 50 ? "medium" : value <= 75 ? "high" : "critical");
  return (
    <div className="progress" aria-label={`${value}%`}>
      <span className={`progress-${resolved}`} style={{ width: `${Math.min(100, Math.max(0, value))}%` }} />
    </div>
  );
}

export function EmptyState({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="empty-state">
      <div className="empty-mark">RG</div>
      <strong>{title}</strong>
      <span>{detail}</span>
    </div>
  );
}

export function formatDate(value?: string) {
  if (!value) return "Not set";
  return new Intl.DateTimeFormat("en-ZA", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(value));
}

export function formatMoney(value: number) {
  return new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
    maximumFractionDigits: 0,
  }).format(value);
}
