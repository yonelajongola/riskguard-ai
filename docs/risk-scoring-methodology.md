# Risk Scoring Methodology

## Scoring Direction

Every answer is normalized to a risk score from `0` to `100`.

- `0` means effective control or minimal exposure.
- `100` means absent control or severe exposure.
- Higher values always mean greater risk.

Default yes/no mapping:

| Answer | Risk score |
|---|---:|
| Yes | 0 |
| Partially | 50 |
| No | 100 |
| Not applicable | 0 after scope approval |

Other question types must define an equivalent server-owned mapping.

## Weighted Formula

```text
Risk Score = sum(Answer Score * Question Weight) / sum(Question Weight)
```

Example:

| Question | Answer score | Weight | Weighted value |
|---|---:|---:|---:|
| Privileged MFA | 100 | 2 | 200 |
| Password policy | 0 | 1 | 0 |

`200 / 3 = 66.67`, which is **High** risk.

The API clamps answer scores to `0-100`. An assessment with no scored answers returns `0`.

## Risk Levels

| Score | Level | UI color |
|---:|---|---|
| 0-25 | Low | Green |
| 26-50 | Medium | Yellow |
| 51-75 | High | Orange |
| 76-100 | Critical | Red |

## Derived Measures

- **Overall risk:** weighted assessment score.
- **Category risk:** weighted result within a category.
- **Department risk:** aggregate of visible department risks.
- **Compliance readiness:** inverse control exposure adjusted for open mapped gaps.
- **Cybersecurity posture:** inverse cybersecurity exposure.
- **Continuity readiness:** recovery and continuity control confidence.
- **Vendor risk:** weighted contract, security, compliance, dependency, reliability, and data-access exposure.
- **Trend:** change between current and historical score snapshots.

## Heat Map

The heat map uses `Impact * Likelihood` as a visual prioritization aid.

Impact:

1. Low
2. Medium
3. High
4. Critical

Likelihood:

1. Rare
2. Possible
3. Likely
4. Almost certain

The weighted assessment score remains the canonical analytical result.

## Recommendations

Failed answers generate recommendations:

- `51-75`: High priority, normally due within 30 days.
- `76-100`: Critical priority, normally due within 14 days.

Recommendations include an accountable owner, due date, business impact, compliance mapping, and lifecycle status. Generated actions require human approval; the system does not automatically accept risk.

## Governance

Changes to weights, mappings, thresholds, or recommendation rules should be:

1. reviewed by risk and compliance owners;
2. versioned with the application;
3. tested against boundary values;
4. documented in release notes;
5. applied consistently across historical comparisons.
