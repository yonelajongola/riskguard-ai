# Security Design

## Identity and Authentication

ASP.NET Core Identity provides password hashing, unique email enforcement, reset tokens, failed-attempt tracking, and account lockout.

The API issues:

- short-lived HMAC-SHA256 JWT access tokens;
- cryptographically random refresh tokens;
- only SHA-256 refresh-token hashes in the database.

`JWT_SECRET` is mandatory outside Development and must contain at least 32 bytes. Development creates an ephemeral in-memory signing secret when none is supplied; no signing key is stored in source control.

## Authorization

Roles:

- Admin
- Executive
- Risk Manager
- Auditor
- Compliance Officer
- Security Analyst
- Department Manager
- Employee

Named policies include `Administrators`, `RiskProfessionals`, `ReadSensitive`, and AI-specific policies. Controller queries also scope records by organization, department, and assignment.

## Secrets and Configuration

Real values must be supplied through environment variables, GitHub environments, or Azure Key Vault references.

Never commit:

- `.env`;
- Azure publish profiles;
- JWT signing material;
- SQL credentials or connection strings;
- Azure OpenAI keys;
- storage account keys;
- local databases;
- user-secrets files.

Placeholder files are deliberately nonfunctional until values are supplied.

## API Protections

- HTTPS redirection outside Development
- forwarded-header support for Azure reverse proxies
- explicit CORS origin, header, and method allowlists
- fixed-window per-IP rate limiting
- secure response headers
- FluentValidation and server-owned score mappings
- generic `application/problem+json` errors with trace identifiers
- EF Core parameterization
- production Swagger disabled by default
- public registration disabled by default outside Development

On an empty production database, startup creates only the fixed Identity role
definitions. The deployment runbook permits registration briefly to create the
first workspace administrator, then requires
`Authentication__AllowPublicRegistration=false` and an API restart. Demo users
and business data remain disabled through `SeedData__Enabled=false`. Shared
assessment reference data can be enabled independently with
`SeedData__ReferenceDataEnabled=true`.

## Data Protection

- Passwords are never stored in plaintext.
- Refresh tokens are not stored in reusable form.
- Sensitive prompt patterns are redacted before AI processing.
- Evidence notes, credentials, and tokens are excluded from AI context.
- Audit records capture important workflow and AI actions.
- Report endpoints require authenticated role policies.

## AI Safety

- Azure OpenAI credentials are configuration-only.
- AI endpoints require authentication.
- Prompt classification blocks restricted output through the general chat endpoint.
- Provider responses must match a structured contract.
- Unavailable or malformed provider output falls back to a labeled mock response.
- AI activity records user, timestamp, category, response type, mode, and related assessment.
- Recommendations remain advisory and require accountable-owner review.

## Frontend Security

- React renders API text without raw HTML injection.
- Session data is stored in `sessionStorage`, limiting persistence to the current tab session.
- Role guards improve navigation, while the API remains the authorization authority.
- Static Web Apps configuration applies SPA fallback and security headers.

## Production Controls

Recommended Azure controls:

- managed identity for service-to-service access;
- Key Vault references in App Service settings;
- Azure SQL auditing, Defender, backups, and private endpoints;
- Application Insights alerts for failures, latency, authentication abuse, and AI fallback rates;
- Azure Front Door/WAF for internet-facing regulated environments;
- restricted deployment environments and approval gates in GitHub.

## Remaining Hardening Opportunities

- secure HTTP-only refresh-token cookies;
- Microsoft Entra ID/OIDC SSO;
- global EF tenant query filters;
- malware scanning for uploaded evidence;
- database-level tenant controls;
- formal retention and legal-hold policies;
- automated dependency, SAST, DAST, and secret scanning.
