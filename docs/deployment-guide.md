# Deployment Guide

## Target Architecture

- **Frontend:** Azure Static Web Apps
- **Backend:** Azure App Service
- **Database:** Azure SQL Database
- **Secrets:** Azure Key Vault references through App Service settings
- **Monitoring:** Application Insights and Azure Monitor
- **AI:** Azure OpenAI, optional; mock mode remains functional without it

Deployment placeholders:

- Frontend: `https://YOUR-FRONTEND-NAME.azurestaticapps.net`
- API: `https://YOUR-API-NAME.azurewebsites.net`

## 1. Validate Locally

```powershell
dotnet restore backend/RiskGuardAI.sln
dotnet build backend/RiskGuardAI.sln --configuration Release
dotnet test backend/RiskGuardAI.sln --configuration Release

cd frontend
npm ci
npm run lint
npm run build
```

Confirm:

- `GET http://localhost:5000/api/health` is healthy;
- seeded login works in Development;
- dashboard, reports, and AI mock mode work;
- no local `.env`, database, log, `bin`, `obj`, or `node_modules` files are staged.

## 2. Create Azure SQL Database

1. Create an Azure SQL logical server and database.
2. Create a deployment identity or SQL account with migration permissions.
3. Restrict firewall access to approved deployment and App Service networks.
4. Enable auditing, Defender for SQL, backups, and required retention.
5. Store the connection string in Key Vault.

App Service setting:

```text
Database__Provider=SqlServer
SQL_CONNECTION_STRING=@Microsoft.KeyVault(SecretUri=https://YOUR-VAULT.vault.azure.net/secrets/RiskGuardSqlConnection/)
```

Prefer managed identity authentication for Azure SQL when the environment supports it.

## 3. Prepare Migrations

Install EF tooling:

```powershell
dotnet tool install --global dotnet-ef
```

Generate an idempotent SQL script:

```powershell
$env:Database__Provider="SqlServer"
$env:SQL_CONNECTION_STRING="Server=(local);Database=RiskGuardBuild;Trusted_Connection=True;TrustServerCertificate=True"

dotnet ef migrations script --idempotent `
  --project backend/src/RiskGuard.Persistence `
  --startup-project backend/src/RiskGuard.API `
  --output artifacts/RiskGuard-migration.sql
```

Review and apply the script through an approved deployment step. Do not run destructive migration commands from an unreviewed web startup.

## 4. Create Azure App Service

1. Create a Linux or Windows App Service capable of running .NET 10.
2. Enable a system-assigned managed identity.
3. Create or connect Application Insights.
4. Enable HTTPS-only.
5. Set the health check path to `/api/health`.
6. Disable FTP/basic publishing where organizational policy requires it.

Required settings:

```text
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=SqlServer
SQL_CONNECTION_STRING=@Microsoft.KeyVault(...)
JWT_SECRET=@Microsoft.KeyVault(...)
Cors__AllowedOrigins__0=https://YOUR-FRONTEND-NAME.azurestaticapps.net
Authentication__AllowPublicRegistration=false
SeedData__Enabled=false
Swagger__Enabled=false
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=...
```

Optional AI settings:

```text
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com
AZURE_OPENAI_KEY=@Microsoft.KeyVault(...)
AZURE_OPENAI_DEPLOYMENT=YOUR-DEPLOYMENT
```

Optional storage placeholder:

```text
AZURE_STORAGE_CONNECTION_STRING=@Microsoft.KeyVault(...)
```

The current local file provider should be replaced with Blob Storage before production evidence uploads are enabled.

## 5. Configure Key Vault

Store at minimum:

- SQL connection string;
- JWT signing secret;
- Azure OpenAI key, when used;
- storage connection string, when used.

Grant the App Service managed identity only `Key Vault Secrets User` access for the required vault or secrets. Use App Service Key Vault references rather than copying secret values into repository files.

## 6. Deploy the Backend

Publish locally:

```powershell
dotnet publish backend/src/RiskGuard.API/RiskGuard.API.csproj `
  --configuration Release `
  --output artifacts/api
```

Deploy through:

- the manual GitHub Actions production jobs;
- `az webapp deploy`;
- or an approved App Service deployment mechanism.

After deployment, verify:

```text
GET https://YOUR-API-NAME.azurewebsites.net/api/health
```

Expected: API and database status `Healthy`.

## 7. Create Azure Static Web App

1. Create an Azure Static Web App linked to the GitHub repository.
2. Set app location to `frontend`.
3. Set output location to `dist`.
4. Set the build command to `npm run build`.
5. Add repository variable:

```text
VITE_API_BASE_URL=https://YOUR-API-NAME.azurewebsites.net
```

`frontend/public/staticwebapp.config.json` provides SPA route fallback and security headers.

## 8. Configure CORS

Production API CORS must contain only approved frontend origins:

```text
Cors__AllowedOrigins__0=https://YOUR-FRONTEND-NAME.azurestaticapps.net
```

The API allows:

- `Authorization` and `Content-Type` headers;
- `GET`, `POST`, `PUT`, and `DELETE`;
- credentials only for explicitly allowed origins.

Do not use wildcard origins in Production.

## 9. Configure GitHub

Repository variables:

- `AZURE_WEBAPP_NAME`
- `VITE_API_BASE_URL`

Production environment secrets:

- `AZURE_WEBAPP_PUBLISH_PROFILE`
- `AZURE_STATIC_WEB_APPS_API_TOKEN`
- `AZURE_SQL_CONNECTION_STRING` for an independently approved migration job

Protect the `production` environment with reviewers. Prefer workload identity federation over long-lived deployment credentials when possible.

## 10. Monitoring

Configure Application Insights and Azure Monitor alerts for:

- API availability and `/api/health`;
- HTTP 5xx rate and response latency;
- failed logins and lockouts;
- SQL connectivity;
- report-generation failures;
- critical risk and incident activity;
- Azure OpenAI failures and mock fallback rate.

Serilog writes structured console logs that App Service can collect. Do not log JWTs, passwords, keys, connection strings, or complete AI context payloads.

## 11. Production Smoke Test

1. Open the Static Web App.
2. Sign in with an approved production account.
3. Confirm dashboard data loads.
4. Open assessments and a result.
5. Generate and download a report.
6. Open AI Copilot.
7. Confirm mock mode works without Azure OpenAI settings.
8. Add Azure OpenAI settings and confirm provider status changes.
9. Confirm audit records capture the AI and report actions.
10. Verify unauthorized roles receive `403` for restricted actions.

## 12. Rollback

- Retain the previous App Service deployment package or slot.
- Use deployment slots for production where available.
- Back up Azure SQL before schema changes.
- Make migrations backward compatible when possible.
- Roll back application code before applying a database rollback script.

## Local Docker

```powershell
Copy-Item .env.example .env
# Set SQL_SA_PASSWORD and JWT_SECRET.
docker compose up --build
```

Services:

- Frontend: `http://localhost:5173`
- API: `http://localhost:5000`
- SQL Server: `localhost:1433`

The Docker configuration explicitly enables demo seed data for local use only.

## Release Checklist

- Backend build and tests pass
- Frontend lint and build pass
- Migration reviewed and applied
- App Service settings resolve from Key Vault
- Production demo seed is disabled
- Production registration and Swagger are disabled
- CORS contains only approved Static Web App origins
- `/api/health` is monitored
- Report and Copilot smoke tests pass
- Backup and rollback procedures are confirmed
