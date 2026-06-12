# RiskGuard AI Azure Deployment Runbook

This runbook deploys RiskGuard AI using:

- Azure Static Web Apps for the React frontend
- Azure App Service for the ASP.NET Core API
- Azure SQL Database for production data
- App Service Configuration, optionally backed by Azure Key Vault
- Application Insights and Azure Monitor
- GitHub Actions from `yonelajongola/riskguard-ai`

The application targets .NET 10 and Node.js 24. Use one Azure region for the
resource group, App Service, Azure SQL, Key Vault, and Application Insights
where those services are available.

## Deployment Values

Choose globally unique names where required. The examples below are used
throughout this guide.

| Value | Example |
| --- | --- |
| Resource group | `rg-riskguard-prod` |
| Region | `South Africa North` |
| SQL logical server | `riskguard-sql-UNIQUE` |
| SQL database | `RiskGuardAI` |
| App Service plan | `asp-riskguard-prod` |
| App Service | `riskguard-api-UNIQUE` |
| Static Web App | `riskguard-web-UNIQUE` |
| Key Vault | `kv-riskguard-UNIQUE` |
| Application Insights | `appi-riskguard-prod` |
| GitHub repository | `yonelajongola/riskguard-ai` |
| Production GitHub environment | `production` |

Resulting URLs:

```text
API:      https://riskguard-api-UNIQUE.azurewebsites.net
Frontend: https://YOUR-STATIC-WEB-APP.azurestaticapps.net
```

Do not include a trailing slash in either configured origin.

## Prerequisites

Before creating Azure resources, confirm the current `main` branch passes:

```powershell
dotnet restore backend/RiskGuardAI.sln
dotnet build backend/RiskGuardAI.sln --configuration Release
dotnet test backend/RiskGuardAI.sln --configuration Release

Set-Location frontend
npm ci
npm run lint
npm run build
Set-Location ..
```

Install the EF Core command-line tool if it is not already available:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef --version
```

## 1. Create Azure SQL Database

1. Sign in to the [Azure portal](https://portal.azure.com).
2. Search for **SQL databases** and select **Create**.
3. Select the Azure subscription.
4. Create or select resource group `rg-riskguard-prod`.
5. Enter database name `RiskGuardAI`.
6. Under **Server**, select **Create new**.
7. Enter a globally unique server name such as `riskguard-sql-UNIQUE`.
8. Select the same region as the API.
9. For the first deployment, select **SQL authentication** or
   **Microsoft Entra and SQL authentication**.
10. Create a strong server administrator login and password. Store them in a
    password manager, not in the repository.
11. For a portfolio environment, select **General Purpose**, **Serverless**,
    and a low minimum vCore setting. Select a production tier appropriate to
    the expected workload for a real deployment.
12. On **Networking**, select **Public endpoint**.
13. Set **Add current client IP address** to **Yes**. This permits the machine
    running the initial EF migration.
14. Keep minimum TLS at `1.2`.
15. Select **Review + create**, then **Create**.

After the database is created:

1. Open the SQL database.
2. Select **Connection strings**.
3. Copy the ADO.NET connection string.
4. Replace the user ID and password placeholders locally.

The result should resemble:

```text
Server=tcp:riskguard-sql-UNIQUE.database.windows.net,1433;Initial Catalog=RiskGuardAI;Persist Security Info=False;User ID=SQL_ADMIN;Password=SQL_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Do not commit this value.

## 2. Create the Backend App Service

1. In the Azure portal, search for **App Services** and select **Create Web
   App**.
2. Select `rg-riskguard-prod`.
3. Enter the globally unique name `riskguard-api-UNIQUE`.
4. For **Publish**, select **Code**.
5. For **Runtime stack**, select **.NET 10**.
6. Select **Linux** as the operating system.
7. Select the same region as Azure SQL.
8. Create App Service plan `asp-riskguard-prod`. A Basic B1 or higher plan is
   a reasonable portfolio starting point.
9. On the monitoring tab, enable Application Insights and create or select
   `appi-riskguard-prod`.
10. Leave GitHub continuous deployment disabled during creation because this
    repository already contains `.github/workflows/ci-cd.yml`.
11. Select **Review + create**, then **Create**.

Verify the runtime if necessary from Azure Cloud Shell:

```bash
az webapp list-runtimes --os linux | grep DOTNET
```

After creation:

1. Open the App Service.
2. Under **Settings > Configuration > General settings**, enable **HTTPS
   Only**.
3. Under **Settings > Identity**, turn the system-assigned identity **On**.
4. Under **Monitoring > Health check**, set the path to `/api/health`.
5. Select **Properties** and copy every address listed under **Outbound IP
   Addresses** and **Additional Outbound IP Addresses**.
6. Open the Azure SQL logical server, select **Networking**, and add firewall
   rules for the App Service outbound addresses.

For a quick portfolio deployment, Azure SQL's **Allow Azure services and
resources to access this server** option can be enabled instead. It is broader
than App Service-specific firewall rules. Prefer private endpoints and VNet
integration for a hardened production environment.

## 3. Add Backend Environment Variables

Open the App Service and go to **Settings > Environment variables > App
settings**. Add the following values:

| Name | Initial value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__Provider` | `SqlServer` |
| `SQL_CONNECTION_STRING` | Azure SQL connection string |
| `JWT_SECRET` | Random secret of at least 32 bytes |
| `Cors__AllowedOrigins__0` | Set after the Static Web App is created |
| `Authentication__AllowPublicRegistration` | `true` for first-admin bootstrap only |
| `SeedData__Enabled` | `false` |
| `Swagger__Enabled` | `false` |

Generate a JWT secret locally:

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

Store the generated value securely. Every running API instance and deployment
slot must use the same value. Changing it invalidates existing JWTs.

Select **Apply** and confirm the App Service restart.

### Key Vault-Ready Configuration

Direct App Service values are sufficient for a first deployment. To move
secrets into Key Vault:

1. Create Key Vault `kv-riskguard-UNIQUE`.
2. Add secrets named `RiskGuardSqlConnection` and `RiskGuardJwtSecret`.
3. Open the vault's **Access control (IAM)**.
4. Grant the App Service managed identity the **Key Vault Secrets User** role.
5. Replace the App Service values with:

```text
SQL_CONNECTION_STRING=@Microsoft.KeyVault(SecretUri=https://kv-riskguard-UNIQUE.vault.azure.net/secrets/RiskGuardSqlConnection/)
JWT_SECRET=@Microsoft.KeyVault(SecretUri=https://kv-riskguard-UNIQUE.vault.azure.net/secrets/RiskGuardJwtSecret/)
```

Restart the App Service. In the environment-variable list, verify that each Key
Vault reference reports a resolved status. If the vault has network
restrictions, allow the App Service through its supported network path.

## 4. Run EF Core Migrations Against Azure SQL

RiskGuard keeps provider-specific migrations:

- SQLite migrations: `backend/src/RiskGuard.Persistence/Migrations`
- SQL Server migrations:
  `backend/src/RiskGuard.Persistence.SqlServerMigrations/Migrations`

Do not apply the SQLite migration project to Azure SQL.

### Direct Database Update

From the repository root in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Database__Provider = "SqlServer"
$env:SQL_CONNECTION_STRING = "YOUR_AZURE_SQL_CONNECTION_STRING"
$env:JWT_SECRET = "YOUR_PRODUCTION_JWT_SECRET"
$env:DataProtection__KeysPath = Join-Path $env:TEMP "RiskGuard-DataProtectionKeys"

dotnet ef database update `
  --project backend/src/RiskGuard.Persistence.SqlServerMigrations `
  --startup-project backend/src/RiskGuard.API `
  --context RiskGuardDbContext
```

Expected result: the `InitialSqlServer` migration is applied and
`__EFMigrationsHistory` is created.

Clear the sensitive shell variables afterward:

```powershell
Remove-Item Env:SQL_CONNECTION_STRING
Remove-Item Env:JWT_SECRET
```

If the connection fails, return to the SQL logical server's **Networking**
page and update the current client IP firewall rule.

### Production-Preferred Script

Generate a reviewable idempotent SQL script:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Database__Provider = "SqlServer"
$env:SQL_CONNECTION_STRING = "YOUR_AZURE_SQL_CONNECTION_STRING"
$env:JWT_SECRET = "YOUR_PRODUCTION_JWT_SECRET"
$env:DataProtection__KeysPath = Join-Path $env:TEMP "RiskGuard-DataProtectionKeys"

dotnet ef migrations script --idempotent `
  --project backend/src/RiskGuard.Persistence.SqlServerMigrations `
  --startup-project backend/src/RiskGuard.API `
  --context RiskGuardDbContext `
  --output artifacts/riskguard-azure.sql
```

Review and apply the script using an approved SQL deployment process. The
GitHub Actions backend job also produces `riskguard-azure-sql` as a downloadable
workflow artifact.

## 5. Deploy the Backend from GitHub

The repository workflow uses a publish profile for App Service deployment.
OpenID Connect is preferred for a hardened production pipeline, but the
following steps match the checked-in workflow exactly.

### Obtain the Publish Profile

1. Open the App Service in Azure.
2. Download **Get publish profile** from the Overview page.
3. If download is blocked, temporarily enable **SCM Basic Auth Publishing
   Credentials** under App Service configuration, download the profile, and
   follow organizational policy for disabling or replacing publish-profile
   authentication later.
4. Treat the downloaded XML as a secret.

### Configure GitHub

In `https://github.com/yonelajongola/riskguard-ai`:

1. Go to **Settings > Environments**.
2. Create environment `production`.
3. Add environment secret `AZURE_WEBAPP_PUBLISH_PROFILE`.
4. Paste the complete publish-profile XML as its value.
5. Optionally add required reviewers to the environment.
6. Go to **Settings > Secrets and variables > Actions > Variables**.
7. Add repository variable:

```text
AZURE_WEBAPP_NAME=riskguard-api-UNIQUE
```

### Run the API Deployment

1. Open **Actions > RiskGuard AI CI/CD**.
2. Select **Run workflow**.
3. Select branch `main`.
4. Set **Component to deploy** to `api`.
5. Run the workflow.
6. Confirm the backend build, tests, SQL migration-script generation,
   container validation, and `Deploy API to Azure App Service` jobs pass.

The application performs a no-op migration check at startup after the manual
migration and creates only the required Identity roles. It does not load demo
organizations, users, assessments, or risks while `SeedData__Enabled=false`.

## 6. Create the Azure Static Web App

1. In the Azure portal, search for **Static Web Apps** and select **Create**.
2. Select `rg-riskguard-prod`.
3. Enter `riskguard-web-UNIQUE`.
4. Select a hosting plan. Free is suitable for a portfolio deployment;
   Standard provides additional production features.
5. For **Deployment source**, select **Other**. The repository already has a
   custom workflow, so this avoids Azure creating a second workflow file.
6. Select **Review + create**, then **Create**.
7. Open the Static Web App and copy its default hostname, for example:

```text
https://YOUR-STATIC-WEB-APP.azurestaticapps.net
```

8. Open **Manage deployment token** and copy the token.
9. In the GitHub `production` environment, add secret:

```text
AZURE_STATIC_WEB_APPS_API_TOKEN=<deployment-token>
```

## 7. Add `VITE_API_BASE_URL`

In GitHub, open **Settings > Secrets and variables > Actions > Variables** and
add:

```text
VITE_API_BASE_URL=https://riskguard-api-UNIQUE.azurewebsites.net
```

Use the API origin only. The frontend normalizes this value to include `/api`.
Vite variables are embedded at build time, so changing the variable requires a
new frontend build and deployment.

## 8. Configure CORS

Return to the backend App Service:

1. Open **Settings > Environment variables > App settings**.
2. Set:

```text
Cors__AllowedOrigins__0=https://YOUR-STATIC-WEB-APP.azurestaticapps.net
```

3. Do not include a trailing slash.
4. Add custom domains as additional indexed settings when needed:

```text
Cors__AllowedOrigins__1=https://riskguard.example.com
```

5. Select **Apply** and restart the API.

RiskGuard allows the `Authorization` and `Content-Type` headers, the `GET`,
`POST`, `PUT`, and `DELETE` methods, and credentials only for configured
origins. Do not use a wildcard production origin.

Deploy the frontend:

1. Open **Actions > RiskGuard AI CI/CD**.
2. Select **Run workflow** on `main`.
3. Set **Component to deploy** to `frontend`.
4. Run the workflow.
5. Confirm the frontend build and `Deploy frontend to Azure Static Web Apps`
   jobs pass.

`frontend/public/staticwebapp.config.json` is copied into the build output and
provides React SPA navigation fallback and security headers.

## 9. Test `/api/health`

Run:

```powershell
$ApiBase = "https://riskguard-api-UNIQUE.azurewebsites.net"
Invoke-RestMethod "$ApiBase/api/health"
```

Expected:

```json
{
  "apiStatus": "Healthy",
  "databaseStatus": "Healthy",
  "environment": "Production",
  "timestamp": "..."
}
```

HTTP `503` means the API is running but Azure SQL is unavailable.

## 10. Bootstrap and Test Login

A new production database deliberately contains no demo users. Create the first
workspace administrator while
`Authentication__AllowPublicRegistration=true`:

```powershell
$ApiBase = "https://riskguard-api-UNIQUE.azurewebsites.net"
$registration = @{
  firstName = "YOUR_FIRST_NAME"
  lastName = "YOUR_LAST_NAME"
  organizationName = "YOUR_ORGANIZATION"
  email = "YOUR_ADMIN_EMAIL"
  password = "YOUR_STRONG_ADMIN_PASSWORD"
} | ConvertTo-Json

$registered = Invoke-RestMethod `
  -Uri "$ApiBase/api/auth/register" `
  -Method Post `
  -ContentType "application/json" `
  -Body $registration

$registered.user
```

Immediately return to App Service configuration:

1. Set `Authentication__AllowPublicRegistration=false`.
2. Apply the change and restart the API.
3. Confirm a new registration request returns `404`.

Test login:

```powershell
$loginBody = @{
  email = "YOUR_ADMIN_EMAIL"
  password = "YOUR_STRONG_ADMIN_PASSWORD"
} | ConvertTo-Json

$session = Invoke-RestMethod `
  -Uri "$ApiBase/api/auth/login" `
  -Method Post `
  -ContentType "application/json" `
  -Body $loginBody

$session.accessToken
```

Then open the Static Web App `/login` route and sign in with the same account.

## 11. Test the Dashboard

Using the session from the login test:

```powershell
$headers = @{ Authorization = "Bearer $($session.accessToken)" }
Invoke-RestMethod "$ApiBase/api/risks/dashboard-summary" -Headers $headers
```

A new workspace can correctly return zero or empty risk metrics. Create and
submit an assessment through the frontend, then confirm the dashboard reflects
the calculated score, recommendations, and risk register entries.

## 12. Test Reports

Generate the executive PDF:

```powershell
Invoke-WebRequest `
  -Uri "$ApiBase/api/reports/executive/pdf" `
  -Headers $headers `
  -OutFile "RiskGuard-Executive-Report.pdf"
```

Expected:

- HTTP `200`
- `Content-Type: application/pdf`
- a non-empty PDF file
- a report action in `/api/audit-logs`

The Reports page should also generate PDF, XLSX, and CSV outputs appropriate to
the selected report.

## 13. Test AI Copilot Mock Mode

Do not add Azure OpenAI settings yet.

Check provider status:

```powershell
Invoke-RestMethod "$ApiBase/api/ai/status" -Headers $headers
```

Expected mode: `Safe mock`.

Request a risk summary:

```powershell
$summary = Invoke-RestMethod `
  -Uri "$ApiBase/api/ai/risk-summary" `
  -Method Post `
  -Headers $headers `
  -ContentType "application/json" `
  -Body "{}"

$summary | Format-List title, summary, riskPriority, isMock
```

Expected: `isMock` is `true` and the response contains the standard structured
AI fields. Open `/app/copilot` in the frontend and test **Biggest Risk** and
**Mitigation Plan**. Confirm the interactions appear in Audit Logs.

## 14. Add Azure OpenAI Later

1. Create or obtain access to an Azure OpenAI resource.
2. Deploy a supported chat model.
3. Record the resource endpoint, key, and deployment name.
4. Add these App Service settings:

```text
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_KEY=YOUR_KEY_OR_KEY_VAULT_REFERENCE
AZURE_OPENAI_DEPLOYMENT=YOUR_DEPLOYMENT_NAME
```

For Key Vault, store the key as `RiskGuardAzureOpenAiKey`, grant the App Service
managed identity access, and use:

```text
AZURE_OPENAI_KEY=@Microsoft.KeyVault(SecretUri=https://kv-riskguard-UNIQUE.vault.azure.net/secrets/RiskGuardAzureOpenAiKey/)
```

Restart the App Service and call `/api/ai/status`. Expected mode:
`Azure OpenAI`. Repeat the risk-summary test and confirm `isMock` is `false`.
Never send passwords, JWTs, connection strings, or keys in Copilot prompts.

## Monitoring

### Application Insights

1. Open the App Service.
2. Select **Application Insights**.
3. Turn it on and connect `appi-riskguard-prod`.
4. Confirm `APPLICATIONINSIGHTS_CONNECTION_STRING` appears in App Service
   configuration.
5. Use **Live Metrics**, **Failures**, **Performance**, and **Transaction
   search** after generating traffic.

### Azure Monitor Alerts

Create alerts for:

- App Service HTTP 5xx count
- response time
- `/api/health` availability
- App Service CPU and memory
- Azure SQL CPU, storage, and failed connections
- Application Insights failed requests and exceptions

Keep ASP.NET Core in `Production`; never enable the development exception page
to diagnose a public production service.

## Troubleshooting

### CORS Errors

Symptoms:

- browser reports that the request was blocked by CORS
- login works in an API client but fails in the Static Web App
- preflight `OPTIONS` request fails

Checks:

1. Confirm `Cors__AllowedOrigins__0` exactly matches the browser origin.
2. Remove any trailing slash.
3. Confirm the value uses `https`.
4. Restart App Service after changing settings.
5. Add a custom frontend domain as `Cors__AllowedOrigins__1`.
6. Confirm `VITE_API_BASE_URL` points to the API, not the Static Web App.

Test preflight:

```powershell
Invoke-WebRequest `
  -Uri "$ApiBase/api/health" `
  -Method Options `
  -Headers @{
    Origin = "https://YOUR-STATIC-WEB-APP.azurestaticapps.net"
    "Access-Control-Request-Method" = "GET"
    "Access-Control-Request-Headers" = "authorization,content-type"
  }
```

The response should include the configured origin in
`Access-Control-Allow-Origin`.

### Database Connection Errors

Symptoms:

- `/api/health` returns `503`
- startup logs show SQL connection or login failures
- migrations time out

Checks:

1. Confirm `Database__Provider=SqlServer`.
2. Confirm `SQL_CONNECTION_STRING` is present and the Key Vault reference
   resolves.
3. Confirm server name, database name, user ID, and password.
4. Require `Encrypt=True` and use `TrustServerCertificate=False`.
5. Add the current migration client IP to the SQL firewall.
6. Add all App Service outbound addresses or enable the broader Azure-services
   rule for the portfolio deployment.
7. Confirm the SQL database is not paused or exhausted.
8. Use the SQL Server migrations project, not `RiskGuard.Persistence`.

### JWT Errors

Symptoms:

- API fails during startup
- every protected endpoint returns `401`
- tokens stop working after configuration changes

Checks:

1. Confirm `JWT_SECRET` exists and is at least 32 bytes.
2. Ensure the value is not the literal placeholder `JWT_SECRET`.
3. Ensure all slots and instances use the same secret.
4. Restart the API after fixing configuration.
5. Sign in again after changing the secret; old tokens are intentionally
   invalid.
6. Confirm the frontend sends `Authorization: Bearer <token>`.

### Static Web App Routing Errors

Symptoms:

- `/login` works but refreshing `/app/reports` returns `404`
- deep links fail while client navigation works

Checks:

1. Confirm `staticwebapp.config.json` exists at the root of the deployed
   frontend artifact.
2. Confirm the frontend build includes
   `frontend/dist/staticwebapp.config.json`.
3. Confirm the workflow deploys the downloaded `riskguard-web` artifact with
   `skip_app_build: true`.
4. Redeploy the frontend after routing changes.
5. Do not configure the App Service API as a Static Web Apps managed API; it is
   an external backend reached through `VITE_API_BASE_URL`.

### API 500 Errors

1. Check `/api/health` first.
2. Open App Service **Log stream**.
3. Open Application Insights **Failures** and inspect the operation and
   exception.
4. Confirm Azure SQL migrations were applied.
5. Confirm Key Vault references are resolved.
6. Confirm `ASPNETCORE_ENVIRONMENT=Production`.
7. Review the deployment job and App Service deployment logs.

Azure CLI log commands:

```bash
az webapp log config \
  --resource-group rg-riskguard-prod \
  --name riskguard-api-UNIQUE \
  --application-logging filesystem \
  --level information

az webapp log tail \
  --resource-group rg-riskguard-prod \
  --name riskguard-api-UNIQUE
```

Do not place secrets or complete tokens in support screenshots or logs.

### Missing Environment Variables

Required production variables:

```text
ASPNETCORE_ENVIRONMENT
Database__Provider
SQL_CONNECTION_STRING
JWT_SECRET
Cors__AllowedOrigins__0
Authentication__AllowPublicRegistration
SeedData__Enabled
Swagger__Enabled
```

Optional integrations:

```text
AZURE_OPENAI_ENDPOINT
AZURE_OPENAI_KEY
AZURE_OPENAI_DEPLOYMENT
AZURE_STORAGE_CONNECTION_STRING
APPLICATIONINSIGHTS_CONNECTION_STRING
```

If App Service fails immediately:

1. Check the environment-variable names for spelling and double underscores.
2. Confirm `JWT_SECRET` and `SQL_CONNECTION_STRING` are not placeholders.
3. Confirm Key Vault references resolve.
4. Restart the App Service.
5. Check Log stream for the first startup exception.

## Release Checklist

- Azure SQL migration applied from the SQL Server migrations project
- App Service uses .NET 10 and Production environment
- JWT and SQL secrets stored outside source control
- demo seed disabled
- first administrator created and public registration disabled again
- API GitHub deployment successful
- Static Web Apps deployment successful
- exact frontend origin configured in CORS
- `/api/health` reports healthy
- login and protected dashboard work
- reports download successfully
- AI Copilot returns safe mock responses
- audit logs contain login, report, and AI actions
- Application Insights receives telemetry
- Azure Monitor alerts are configured

## Official Azure References

- [Create an Azure SQL database](https://learn.microsoft.com/azure/azure-sql/database/single-database-create-quickstart)
- [Deploy App Service with GitHub Actions](https://learn.microsoft.com/azure/app-service/deploy-github-actions)
- [Configure ASP.NET Core on App Service](https://learn.microsoft.com/azure/app-service/configure-language-dotnetcore)
- [Use Key Vault references in App Service](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references)
- [Configure App Service health checks](https://learn.microsoft.com/azure/app-service/monitor-instances-health-check)
- [Configure Static Web Apps builds](https://learn.microsoft.com/azure/static-web-apps/build-configuration)
- [Apply EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
