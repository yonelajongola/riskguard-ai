import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  Bell,
  Bot,
  BriefcaseBusiness,
  Building2,
  ChevronDown,
  ClipboardCheck,
  FileChartColumn,
  Gauge,
  History,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  Search,
  Settings,
  ShieldCheck,
  Siren,
  Sun,
  Users,
  X,
} from "lucide-react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { notificationsDemo } from "../data/demo";
import { api } from "../lib/api";
import type { Notification } from "../types";

const navigation = [
  { label: "Overview", items: [
    { to: "/app", label: "Command center", icon: LayoutDashboard, end: true },
    { to: "/app/executive", label: "Executive view", icon: Gauge },
    { to: "/app/security", label: "Security posture", icon: ShieldCheck },
    { to: "/app/compliance", label: "Compliance", icon: ClipboardCheck },
    { to: "/app/operations", label: "Operations", icon: Activity },
  ] },
  { label: "Risk workspace", items: [
    { to: "/app/assessments", label: "Assessments", icon: ClipboardCheck },
    { to: "/app/risks", label: "Risk register", icon: Siren },
    { to: "/app/heatmap", label: "Risk heat map", icon: Gauge },
    { to: "/app/recommendations", label: "Recommendations", icon: Activity },
  ] },
  { label: "Assurance", items: [
    { to: "/app/incidents", label: "Incidents", icon: Siren },
    { to: "/app/vendors", label: "Vendors", icon: BriefcaseBusiness },
    { to: "/app/continuity", label: "Business continuity", icon: Building2 },
    { to: "/app/reports", label: "Reports", icon: FileChartColumn },
    { to: "/app/copilot", label: "AI Copilot", icon: Bot },
  ] },
  { label: "Administration", items: [
    { to: "/app/audit", label: "Audit logs", icon: History },
    { to: "/app/users", label: "User management", icon: Users },
    { to: "/app/settings", label: "Settings", icon: Settings },
  ] },
];

export function AppShell() {
  const { user, logout, isDemo } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState(false);
  const [dark, setDark] = useState(() => localStorage.getItem("riskguard.theme") !== "light");
  const queryClient = useQueryClient();
  const notificationQuery = useQuery({
    queryKey: ["notifications"],
    queryFn: () => api<Notification[]>("/notifications", {}, notificationsDemo),
  });
  const notificationItems = notificationQuery.data ?? (isDemo ? notificationsDemo : []);
  const unread = notificationItems.filter((item) => !item.isRead).length;

  useEffect(() => {
    document.documentElement.dataset.theme = dark ? "dark" : "light";
    localStorage.setItem("riskguard.theme", dark ? "dark" : "light");
  }, [dark]);

  useEffect(() => setOpen(false), [location.pathname]);

  return (
    <div className="app-frame">
      <aside className={`sidebar ${open ? "sidebar-open" : ""}`}>
        <div className="brand brand-app">
          <span className="brand-mark"><ShieldCheck size={22} /></span>
          <span><strong>RiskGuard</strong><small>AI</small></span>
          <button className="icon-button sidebar-close" onClick={() => setOpen(false)} aria-label="Close menu"><X size={20} /></button>
        </div>
        <div className="org-switcher">
          <span className="org-avatar">FB</span>
          <span><strong>FoodieBar</strong><small>Enterprise workspace</small></span>
          <ChevronDown size={15} />
        </div>
        <nav className="side-nav">
          {navigation.map((section) => {
            const visibleItems = section.items.filter((item) => canSee(item.to, user?.roles ?? []));
            if (visibleItems.length === 0) return null;
            return (
            <div className="nav-section" key={section.label}>
              <span className="nav-label">{section.label}</span>
              {visibleItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
                >
                  <item.icon size={18} strokeWidth={1.9} />
                  <span>{item.label}</span>
                  {item.to === "/app/incidents" ? <small className="nav-count">4</small> : null}
                </NavLink>
              ))}
            </div>
          );})}
        </nav>
        <div className="sidebar-foot">
          <div className="posture-mini">
            <div><span>Control maturity</span><strong>64%</strong></div>
            <div className="progress"><span className="progress-blue" style={{ width: "64%" }} /></div>
            <small>5 priority controls need evidence</small>
          </div>
          <button className="profile-chip" onClick={() => navigate("/app/profile")}>
            <span className="avatar">{initials(user?.fullName)}</span>
            <span><strong>{user?.fullName}</strong><small>{user?.roles[0] ?? "User"}</small></span>
            <ChevronDown size={15} />
          </button>
        </div>
      </aside>
      {open ? <button className="sidebar-scrim" onClick={() => setOpen(false)} aria-label="Close navigation" /> : null}
      <div className="app-main">
        <header className="topbar">
          <button className="icon-button menu-button" onClick={() => setOpen(true)} aria-label="Open menu"><Menu size={21} /></button>
          <div className="search-box">
            <Search size={17} />
            <input aria-label="Search" placeholder="Search risks, controls, vendors..." />
            <kbd>Ctrl K</kbd>
          </div>
          <div className="topbar-actions">
            {isDemo ? <span className="demo-pill">Demo data</span> : <span className="live-pill"><i /> Live</span>}
            <button className="icon-button" onClick={() => setDark((value) => !value)} aria-label="Toggle theme">
              {dark ? <Sun size={19} /> : <Moon size={19} />}
            </button>
            <div className="notification-wrap">
              <button className="icon-button" onClick={() => setNotifications((value) => !value)} aria-label="Notifications">
                <Bell size={19} />{unread > 0 ? <span className="notification-dot" /> : null}
              </button>
              {notifications ? (
                <div className="popover notifications-popover">
                  <div className="popover-head"><strong>Notifications</strong><button onClick={async () => { if (!isDemo) { await api("/notifications/read-all", { method: "POST" }); queryClient.invalidateQueries({ queryKey: ["notifications"] }); } }}>Mark all read</button></div>
                  {notificationItems.slice(0, 4).map((notification) => (
                    <button className="notification-item" key={notification.id} onClick={async () => { if (!isDemo && !notification.isRead) await api(`/notifications/${notification.id}/read`, { method: "POST" }); queryClient.invalidateQueries({ queryKey: ["notifications"] }); navigate(notification.link); }}>
                      <span className={`notice-dot notice-${notification.severity.toLowerCase()}`} />
                      <span><strong>{notification.title}</strong><small>{notification.message}</small></span>
                    </button>
                  ))}
                  <button className="popover-link" onClick={() => navigate("/app/notifications")}>View all notifications</button>
                </div>
              ) : null}
            </div>
            <button className="icon-button" onClick={async () => { await logout(); navigate("/login"); }} aria-label="Sign out"><LogOut size={18} /></button>
          </div>
        </header>
        <main className="page-content"><Outlet /></main>
      </div>
    </div>
  );
}

function canSee(path: string, roles: string[]) {
  const isEmployee = roles.includes("Employee");
  if (isEmployee && ["/app/executive", "/app/security", "/app/compliance", "/app/risks", "/app/heatmap", "/app/vendors", "/app/continuity", "/app/reports", "/app/audit", "/app/users", "/app/settings"].includes(path)) return false;
  if (path === "/app/users" || path === "/app/settings") return roles.includes("Admin");
  if (path === "/app/audit") return roles.some((role) => role === "Admin" || role === "Auditor");
  if (path === "/app/executive" || path === "/app/reports") {
    return roles.some((role) => ["Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst"].includes(role));
  }
  if (path === "/app/security") return roles.some((role) => ["Admin", "Risk Manager", "Security Analyst"].includes(role));
  return true;
}

function initials(name?: string) {
  return (name ?? "Risk Guard").split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();
}
