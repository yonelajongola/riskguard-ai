import { StrictMode } from "react";
import type { ReactNode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import { AppShell } from "./components/AppShell";
import { LandingPage, LoginPage, NotAuthorizedPage, NotFoundPage, RegisterPage } from "./pages/PublicPages";
import { DashboardPage, ExecutiveDashboard, SecurityDashboard, OperationsDashboard } from "./pages/DashboardPages";
import {
  AuditPage,
  CompliancePage,
  ContinuityPage,
  HeatMapPage,
  IncidentsPage,
  NotificationsPage,
  RecommendationsPage,
  ReportsPage,
  RisksPage,
  SettingsPage,
  UsersPage,
  VendorsPage,
} from "./pages/ModulePages";
import { AssessmentsPage, AssessmentWorkspacePage } from "./pages/AssessmentPages";
import { CopilotPage } from "./pages/CopilotPage";
import "./styles.css";

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false } },
});

function Protected() {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <AppShell /> : <Navigate to="/login" replace />;
}

function RoleRoute({ roles, children }: { roles: string[]; children: ReactNode }) {
  const { user } = useAuth();
  return user?.roles.some((role) => roles.includes(role))
    ? children
    : <Navigate to="/not-authorized" replace />;
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/not-authorized" element={<NotAuthorizedPage />} />
      <Route path="/app" element={<Protected />}>
        <Route index element={<DashboardPage />} />
        <Route path="executive" element={<RoleRoute roles={["Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst"]}><ExecutiveDashboard /></RoleRoute>} />
        <Route path="security" element={<RoleRoute roles={["Admin", "Risk Manager", "Security Analyst"]}><SecurityDashboard /></RoleRoute>} />
        <Route path="compliance" element={<CompliancePage />} />
        <Route path="operations" element={<OperationsDashboard />} />
        <Route path="department" element={<DashboardPage />} />
        <Route path="assessments" element={<AssessmentsPage />} />
        <Route path="assessments/new" element={<RoleRoute roles={["Admin", "Risk Manager", "Compliance Officer", "Security Analyst"]}><AssessmentWorkspacePage /></RoleRoute>} />
        <Route path="assessments/:id" element={<AssessmentWorkspacePage />} />
        <Route path="risks" element={<RisksPage />} />
        <Route path="heatmap" element={<HeatMapPage />} />
        <Route path="recommendations" element={<RecommendationsPage />} />
        <Route path="compliance/frameworks" element={<CompliancePage />} />
        <Route path="compliance/gaps" element={<CompliancePage />} />
        <Route path="incidents" element={<IncidentsPage />} />
        <Route path="incidents/:id" element={<IncidentsPage />} />
        <Route path="vendors" element={<VendorsPage />} />
        <Route path="vendors/:id" element={<VendorsPage />} />
        <Route path="continuity" element={<ContinuityPage />} />
        <Route path="reports" element={<RoleRoute roles={["Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst"]}><ReportsPage /></RoleRoute>} />
        <Route path="copilot" element={<CopilotPage />} />
        <Route path="audit" element={<RoleRoute roles={["Admin", "Auditor"]}><AuditPage /></RoleRoute>} />
        <Route path="notifications" element={<NotificationsPage />} />
        <Route path="users" element={<RoleRoute roles={["Admin"]}><UsersPage /></RoleRoute>} />
        <Route path="settings" element={<RoleRoute roles={["Admin"]}><SettingsPage /></RoleRoute>} />
        <Route path="profile" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider><App /></AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
