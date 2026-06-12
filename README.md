# RiskGuard AI

RiskGuard AI is a full-stack enterprise risk intelligence platform for managing assessments, risk registers, compliance gaps, incidents, third-party exposure, business continuity, reporting, and AI-assisted analysis.

It is designed as a portfolio-grade SaaS application: business workflows produce traceable scores and recommendations, authorization is enforced by role and tenant context, reports are generated from live records, and the AI Copilot remains usable locally without paid cloud services.

## Business Problem

Many small and mid-sized organizations manage risk through disconnected spreadsheets, email threads, and evidence folders. This makes it difficult to:

- maintain a consistent scoring method;
- identify the most urgent exposure;
- connect failed controls to treatment actions;
- show executives a current risk position;
- demonstrate accountability to auditors;
- monitor incidents, suppliers, and recovery readiness together.

## Solution

RiskGuard centralizes governance activities in one authenticated workspace. Weighted assessments update the risk register, failed controls create recommendations and compliance gaps, dashboards aggregate exposure, reports produce management-ready documents, and the AI Copilot explains current data using role-scoped context.

## Key Features

- JWT authentication, refresh tokens, account lockout, and role-based authorization
- Eight seeded roles with organization and department scoping
- Weighted risk assessments with draft, submission, review, and results workflows
- Risk dashboard, category trends, financial exposure, and heat map
- Recommendation ownership, priority, due dates, and completion tracking
- POPIA, GDPR, ISO 27001, NIST CSF, and CIS control mappings
- Incident, vendor, and business continuity registers
- PDF, XLSX, and CSV reporting
- Append-only audit trail and in-app notifications
- Azure OpenAI-ready Copilot with a context-aware safe mock fallback
- SQLite local development and SQL Server/Azure SQL support
- Docker Compose, GitHub Actions, Azure App Service, and Static Web Apps assets

## Technology

| Layer | Technology |
|---|---|
| Frontend | React 19, TypeScript, Vite, TanStack Query, React Router, Recharts, Lucide |
| Backend | .NET 10, ASP.NET Core Web API, FluentValidation, Serilog |
| Identity | ASP.NET Core Identity, JWT access tokens, hashed refresh tokens |
| Data | EF Core, SQLite, SQL Server/Azure SQL |
| Reports | QuestPDF, ClosedXML, CSV exports |
| AI | Azure OpenAI REST integration with structured mock fallback |
| Delivery | Docker, nginx, GitHub Actions, Azure App Service, Azure Static Web Apps |

## Repository Structure

```text
RiskGuardAI/
|-- backend/
|   |-- src/
|   |   |-- RiskGuard.API/
|   |   |-- RiskGuard.Application/
|   |   |-- RiskGuard.Domain/
|   |   |-- RiskGuard.Infrastructure/
|   |   `-- RiskGuard.Persistence/
|   |-- tests/
|   `-- RiskGuardAI.sln
|-- frontend/
|-- docs/
|   `-- images/
|-- .github/workflows/ci-cd.yml
|-- .env.example
|-- .gitignore
|-- docker-compose.yml
`-- README.md
```

## Architecture Overview

The backend follows Clean Architecture:

- **Domain** defines entities, enums, and business vocabulary.
- **Application** contains DTOs, validators, use cases, scoring, and service contracts.
- **Persistence** owns EF Core, Identity stores, migrations, and idempotent seed logic.
- **Infrastructure** implements tokens, reporting, file storage, email placeholders, and AI providers.
- **API** exposes secured controllers, policies, middleware, Swagger, and health checks.
- **Frontend** consumes the API through a single environment-driven client.

See [Architecture](docs/architecture.md) for dependency and request flows.

## Screenshots

Portfolio screenshots should be placed in [docs/images](docs/images/README.md).

| View | Placeholder |
|---|---|
| Landing page | `docs/images/landing-page.png` |
| Command center | `docs/images/dashboard-command-center.png` |
| Assessment workflow | `docs/images/assessment-workflow.png` |
| Risk heat map | `docs/images/risk-heat-map.png` |
| Compliance dashboard | `docs/images/compliance-dashboard.png` |
| Reports | `docs/images/reports-page.png` |
| AI Copilot | `docs/images/ai-copilot.png` |

Only representative demo data should appear in public screenshots.

## Demo Users

Development seeding creates these accounts:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@riskguard.local` | `Admin@12345` |
| Executive | `executive@riskguard.local` | `Executive@12345` |
| Risk Manager | `riskmanager@riskguard.local` | `Risk@12345` |
| Security Analyst | `security@riskguard.local` | `Security@12345` |
| Compliance Officer | `compliance@riskguard.local` | `Compliance@12345` |
| Auditor | `auditor@riskguard.local` | `Auditor@12345` |
| Department Manager | `manager@riskguard.local` | `Manager@12345` |
| Employee | `employee@riskguard.local` | `Employee@12345` |

These credentials are development data. `SeedData__Enabled` is disabled by default outside Development.

## Local Setup

### Prerequisites

- .NET SDK 10
- Node.js 24 or later
- npm
- Optional: Docker Desktop

### Backend

From the repository root:

```powershell
dotnet restore backend/RiskGuardAI.sln
dotnet build backend/RiskGuardAI.sln
dotnet ef database update `
  --project backend/src/RiskGuard.Persistence `
  --startup-project backend/src/RiskGuard.API
dotnet run --project backend/src/RiskGuard.API
```

The API runs at `http://localhost:5000`.

- Swagger: `http://localhost:5000/swagger`
- Detailed health: `http://localhost:5000/api/health`
- Framework health: `http://localhost:5000/health`

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` to the local backend unless `VITE_API_BASE_URL` is set.

### Verification

```powershell
dotnet test backend/RiskGuardAI.sln
cd frontend
npm run lint
npm run build
```

## Environment Variables

Copy `.env.example` for Docker or local shell configuration. Never commit the resulting `.env`.

| Variable | Required | Purpose |
|---|---:|---|
| `VITE_API_BASE_URL` | Frontend production | Backend origin, for example `https://api.example.com` |
| `SQL_CONNECTION_STRING` | SQL Server/Azure SQL | Database connection string |
| `SQL_SA_PASSWORD` | Docker only | Local SQL Server container password |
| `JWT_SECRET` | Non-development | At least 32 bytes of random signing material |
| `AZURE_OPENAI_ENDPOINT` | Optional | Azure OpenAI resource endpoint |
| `AZURE_OPENAI_KEY` | Optional | Azure OpenAI key, preferably a Key Vault reference |
| `AZURE_OPENAI_DEPLOYMENT` | Optional | Azure model deployment name |
| `AZURE_STORAGE_CONNECTION_STRING` | Optional | Future Blob Storage integration |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional | Azure Monitor/Application Insights configuration |

ASP.NET Core hierarchical environment variables are also supported, including `Database__Provider`, `Cors__AllowedOrigins__0`, and `SeedData__Enabled`.

Placeholder-only backend configuration is available at [appsettings.Example.json](backend/src/RiskGuard.API/appsettings.Example.json).

## Database Migrations

Create a migration:

```powershell
dotnet ef migrations add MigrationName `
  --project backend/src/RiskGuard.Persistence `
  --startup-project backend/src/RiskGuard.API
```

Apply migrations:

```powershell
dotnet ef database update `
  --project backend/src/RiskGuard.Persistence `
  --startup-project backend/src/RiskGuard.API
```

For production, generate and review an idempotent SQL script rather than applying schema changes automatically from the web process.

## Docker

```powershell
Copy-Item .env.example .env
# Set SQL_SA_PASSWORD and JWT_SECRET in .env.
docker compose up --build
```

The stack includes SQL Server, the API, and an nginx-hosted frontend.

## API Documentation

- [API reference](docs/api-documentation.md)
- Development Swagger: `http://localhost:5000/swagger`
- [Risk scoring methodology](docs/risk-scoring-methodology.md)

## Deployment

The reference Azure deployment uses:

- Azure Static Web Apps for the React frontend
- Azure App Service for the ASP.NET Core API
- Azure SQL Database
- Azure Key Vault references for secrets
- Application Insights/Azure Monitor
- Optional Azure OpenAI

See the [Deployment Guide](docs/deployment-guide.md).

Deployment link placeholders:

- Frontend: `https://YOUR-FRONTEND-NAME.azurestaticapps.net`
- API: `https://YOUR-API-NAME.azurewebsites.net`

## Security Summary

- Passwords are hashed by ASP.NET Core Identity.
- JWT signing material is never stored in source and is mandatory outside Development.
- Refresh tokens are random and persisted only as SHA-256 hashes.
- Controllers enforce authentication, role policies, and organization scoping.
- CORS uses an explicit allowlist and restricted headers/methods.
- Production Swagger and public registration are disabled by default.
- Global errors return generic problem details with trace identifiers.
- AI prompts are sanitized and interactions are audited.
- Known demo users are opt-in outside Development.

See [Security Design](docs/security-design.md).

## GitHub Actions

[ci-cd.yml](.github/workflows/ci-cd.yml) performs:

1. Backend restore, build, test, and publish
2. Frontend install, type-check, and production build
3. Docker image validation
4. Artifact upload
5. Manual Azure deployment using GitHub environments and secrets

No production secret is stored in the workflow.

## Future Enhancements

- Microsoft Entra ID/OIDC single sign-on
- Managed identity for Azure SQL, OpenAI, and Blob Storage
- Secure HTTP-only refresh-token cookies
- Global EF tenant query filters
- Malware scanning and retention policies for evidence
- Scheduled reports and notification delivery
- Background jobs for overdue treatments and reassessment
- Automated browser end-to-end tests

## Portfolio Description

RiskGuard AI demonstrates senior full-stack engineering across domain modeling, Clean Architecture, secure identity, authorization, workflow design, weighted analytics, professional reporting, responsive product design, AI integration, testing, containers, CI/CD, and Azure deployment readiness in one coherent business application.
