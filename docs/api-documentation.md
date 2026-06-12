# API Documentation

## Access

- Local base URL: `http://localhost:5000/api`
- Development Swagger: `http://localhost:5000/swagger`
- Detailed health: `GET /api/health`

Authenticated requests use:

```http
Authorization: Bearer <access-token>
Content-Type: application/json
```

Swagger is disabled by default outside Development.

## Authentication

| Method | Route | Access | Description |
|---|---|---|---|
| POST | `/auth/login` | Public | Issue JWT and refresh token |
| POST | `/auth/register` | Development/configured | Create a workspace administrator |
| POST | `/auth/refresh` | Public | Rotate refresh token |
| POST | `/auth/logout` | Authenticated | Revoke refresh token |
| POST | `/auth/forgot-password` | Public | Begin reset workflow |
| POST | `/auth/reset-password` | Public | Reset password and revoke refresh tokens |
| GET | `/auth/me` | Authenticated | Current user and workspace |

Login request:

```json
{
  "email": "admin@riskguard.local",
  "password": "Admin@12345"
}
```

## Organizations and Users

| Method | Route | Typical access |
|---|---|---|
| GET/POST | `/organizations` | Authenticated / Admin |
| GET/PUT/DELETE | `/organizations/{id}` | Authenticated / Admin |
| GET/POST | `/departments` | Authenticated / Admin or Risk Manager |
| PUT/DELETE | `/departments/{id}` | Admin |
| GET/POST | `/users` | Admin |
| GET | `/users/assignees` | Risk professionals |
| GET/PUT/DELETE | `/users/{id}` | Admin |

## Assessments

| Method | Route | Description |
|---|---|---|
| GET | `/assessments` | List visible assessments |
| POST | `/assessments` | Create an assessment |
| GET/PUT | `/assessments/{id}` | Detail or update |
| GET | `/assessments/{id}/questions` | Assessment question set |
| POST | `/assessments/{id}/draft` | Save draft responses |
| POST | `/assessments/{id}/submit` | Validate, score, and submit |
| POST | `/assessments/{id}/calculate` | Recalculate risk |
| GET | `/assessments/{id}/results` | Structured results |
| GET | `/assessments/{id}/report` | Assessment PDF |

Create request:

```json
{
  "organizationId": "00000000-0000-0000-0000-000000000000",
  "departmentId": null,
  "riskCategoryId": "00000000-0000-0000-0000-000000000000",
  "title": "Q3 Cybersecurity Assessment",
  "assignedToUserId": "user-id",
  "assignedToName": "Security Analyst",
  "dueDateUtc": "2026-07-01T00:00:00Z"
}
```

Submission request:

```json
{
  "responses": [
    {
      "questionId": "00000000-0000-0000-0000-000000000000",
      "answer": "No",
      "notes": "MFA rollout is planned."
    }
  ]
}
```

Clients cannot submit risk scores. The API calculates them from stored mappings and weights.

## Questions, Risks, and Recommendations

| Method | Route |
|---|---|
| GET/POST | `/questions` |
| GET | `/questions/category/{category}` |
| PUT/DELETE | `/questions/{id}` |
| GET | `/risk-categories` |
| GET | `/risks` |
| GET | `/risks/{id}` |
| GET | `/risks/dashboard-summary` |
| GET | `/risks/heatmap` |
| GET/POST | `/recommendations` |
| PUT | `/recommendations/{id}` |
| POST | `/recommendations/{id}/complete` |

## Governance Registers

### Compliance

| Method | Route |
|---|---|
| GET | `/compliance/frameworks` |
| GET | `/compliance/gaps` |
| GET | `/compliance/dashboard` |
| POST | `/compliance/gaps` |
| PUT | `/compliance/gaps/{id}` |

### Incidents

| Method | Route |
|---|---|
| GET/POST | `/incidents` |
| GET/PUT | `/incidents/{id}` |
| POST | `/incidents/{id}/comments` |
| POST | `/incidents/{id}/status` |

### Vendors and Continuity

| Method | Route |
|---|---|
| GET/POST | `/vendors` |
| GET/PUT/DELETE | `/vendors/{id}` |
| POST | `/vendors/{id}/calculate` |
| GET/POST | `/continuity` |
| PUT | `/continuity/{id}` |
| GET | `/continuity/dashboard` |

## AI Copilot

All AI routes require authentication. Sensitive output has additional role policies.

| Method | Route | Response type |
|---|---|---|
| GET | `/ai/status` | Provider/mock status |
| GET | `/ai/recent` | Current user's recent insights |
| POST | `/ai/risk-summary` | Risk explanation |
| POST | `/ai/recommendations` | Prioritized recommendations |
| POST | `/ai/copilot-chat` | Classified chat response |
| POST | `/ai/executive-summary` | Executive summary |
| POST | `/ai/mitigation-plan` | Mitigation plan |
| POST | `/ai/compliance-summary` | Compliance summary |

Chat request:

```json
{
  "prompt": "What is our cybersecurity exposure?",
  "responseType": "Technical analysis",
  "assessmentId": null
}
```

Structured responses include:

- title;
- summary;
- key findings;
- recommended actions;
- risk priority;
- business impact;
- next steps;
- response type;
- provider/mock indicator;
- aggregate risk context.

## Reports

| Method | Route | Format |
|---|---|---|
| GET | `/reports/executive/pdf` | PDF |
| GET | `/reports/risk/pdf/{assessmentId}` | PDF |
| GET | `/reports/compliance/pdf` | PDF |
| GET | `/reports/vendors/pdf` | PDF |
| GET | `/reports/incidents/pdf` | PDF |
| GET | `/reports/risks/excel` | XLSX |
| GET | `/reports/auditlogs/csv` | CSV |
| GET | `/reports/{register}/csv` | CSV |

## Audit and Notifications

| Method | Route |
|---|---|
| GET | `/audit-logs` |
| GET | `/notifications` |
| POST | `/notifications/{id}/read` |
| POST | `/notifications/read-all` |

## Health

`GET /api/health` is anonymous and returns:

```json
{
  "apiStatus": "Healthy",
  "databaseStatus": "Healthy",
  "environment": "Development",
  "timestamp": "2026-06-12T14:06:00Z"
}
```

It returns `503` when the database cannot be reached, without exposing connection details.

## Errors

Errors use `application/problem+json`:

```json
{
  "title": "Unexpected server error",
  "status": 500,
  "detail": "The request could not be completed. Use the trace identifier when contacting support.",
  "traceId": "..."
}
```

Common status codes: `400`, `401`, `403`, `404`, `409`, `429`, `500`, and `503`.
