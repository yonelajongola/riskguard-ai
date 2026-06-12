# Architecture

## Overview

RiskGuard AI is a React single-page application backed by an ASP.NET Core API and a relational database. The backend follows Clean Architecture so domain and application rules remain independent of delivery frameworks and external providers.

```text
Browser
  |
  | HTTPS + JSON + JWT
  v
RiskGuard.API
  |-- RiskGuard.Application
  |-- RiskGuard.Infrastructure
  `-- RiskGuard.Persistence
          |
          v
     RiskGuard.Domain
```

## Backend Projects

### RiskGuard.Domain

Defines the core business model without ASP.NET Core, EF Core, or Azure dependencies.

Primary concepts:

- Organization and Department
- Assessment, Question, and Response
- Risk Item, Risk Score, and Recommendation
- Compliance Framework, Control, and Gap
- Incident and Comment
- Vendor and Vendor Assessment
- Business Continuity Plan and Critical System
- Report, Notification, Audit Log, Refresh Token, and AI Interaction

### RiskGuard.Application

Contains use-case contracts and deterministic business rules:

- API DTOs and FluentValidation validators
- assessment creation handler;
- answer normalization and weighted risk scoring;
- recommendation generation;
- incident workflow transitions;
- vendor risk calculation;
- compliance gap construction;
- service and repository interfaces.

### RiskGuard.Persistence

Owns EF Core and ASP.NET Core Identity:

- `RiskGuardDbContext`;
- entity configuration, indexes, and relationships;
- SQLite and SQL Server provider selection;
- migrations;
- repository implementations;
- idempotent reference and demo seed logic.

SQLite is used in Development. `Database__Provider=SqlServer` with
`SQL_CONNECTION_STRING` selects SQL Server or Azure SQL. Provider-specific EF
Core migrations are kept in separate assemblies:

- `RiskGuard.Persistence` for SQLite;
- `RiskGuard.Persistence.SqlServerMigrations` for SQL Server and Azure SQL.

This prevents SQLite column types and identity annotations from leaking into
Azure SQL deployment scripts.

### RiskGuard.Infrastructure

Implements external-service boundaries:

- signed JWT access tokens and refresh-token hashing;
- QuestPDF, ClosedXML, and CSV report generation;
- `AiRiskService` for Azure OpenAI;
- `MockAiRiskService` for safe local fallback;
- local file storage and logging email placeholders.

### RiskGuard.API

Provides:

- REST controllers and OpenAPI/Swagger;
- JWT authentication and role policies;
- organization and department authorization checks;
- rate limiting, strict CORS, forwarded headers, HTTPS, and security headers;
- RFC-style error responses;
- Serilog request logging;
- health endpoints;
- controlled migration and seed startup.

## Frontend

The React application is organized around:

- route-level pages and role guards;
- a shared authenticated API client;
- TanStack Query for server-state loading and invalidation;
- reusable dashboard components;
- responsive light and dark themes;
- structured loading, empty, and error states.

`VITE_API_BASE_URL` is the only public API location setting. Components do not contain deployment URLs.

## Assessment Submission Flow

1. A user loads assigned questions from the API.
2. Draft responses are validated against server-owned questions.
3. On submission, answer mappings produce normalized `0-100` values.
4. The weighted scoring service calculates assessment and category risk.
5. The API updates or creates the consolidated risk item.
6. Failed controls generate treatment recommendations and compliance gaps.
7. A historical score snapshot is saved.
8. Audit records capture the material action.
9. The frontend refreshes dashboard, results, recommendations, and registers.

## AI Copilot Flow

1. The authenticated request is classified by prompt category.
2. Role checks prevent restricted executive, compliance, cybersecurity, or mitigation output.
3. The API assembles tenant- and assignment-scoped risk context.
4. Secret-like prompt content is redacted.
5. Azure OpenAI returns structured JSON when configured.
6. Missing configuration or provider failure uses `MockAiRiskService`.
7. The response and an audit record are persisted.

## Deployment Topology

```text
Azure Static Web Apps
        |
        | HTTPS
        v
Azure App Service API
   |        |        |
   v        v        v
Azure SQL  Key Vault  Application Insights
                       |
                       v
                 Azure OpenAI (optional)
```

## Growth Considerations

- Add global organization query filters for defense in depth.
- Move refresh tokens to secure, HTTP-only cookies.
- Use managed identity for Azure SQL, Storage, and OpenAI.
- Add background processing for scheduled reports and reminders.
- Add distributed cache and queue-based report generation when scale requires it.
