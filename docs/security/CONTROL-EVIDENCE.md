# Security control and evidence plan

This is a development control plan aligned to common SOC 2 Trust Services Criteria themes. It is not a SOC 2 report, audit, certification, or representation that controls are operating effectively. A qualified auditor must determine applicability and evaluate evidence over the required period.

| Control area | EDUCORE implementation/evidence target | Current status |
|---|---|---|
| Identity and access (CC6) | Trusted OIDC issuer configuration; membership-validated tenant selection; school-scoped roles; platform grants separated from school roles; reviewable privileged access and revocation records. Retain role/membership change evidence and access reviews. | JWT validation, membership/status checks, platform grants, school role changes, bootstrap-only identity-management permission, and Keycloak management API integration exist. Keycloak, SMTP, realm policy, live recovery/MFA/session-revocation tests, access reviews, and operational evidence retention remain deployment requirements. |
| Tenant confidentiality (CC6) | SchoolId on every school-owned row; tenant-aware constraints; tenant-scoped file/cache/search/export keys; cross-tenant negative tests; controlled SQL access and encryption. Retain migration and test outputs. | Initial School, Membership and Student schema/query/write guards exist. Non-student domains and external storage/search boundaries remain unimplemented. |
| Privileged and audit activity (CC6/CC7) | Append-only audit event with SchoolId (when applicable), actor subject, action, target type/id, outcome, timestamp, correlation ID, and minimal non-sensitive metadata. Restrict access and retain reviewed evidence. | Platform school, membership, permission, password-change, and Keycloak action events are recorded without credentials or tokens. Database-owner tamper protection, correlation IDs, retention, external-provider authentication events, and operational review are not implemented. |
| Change management (CC8) | Reviewed source changes, migration review, build and tenant-isolation tests on every change, controlled release, approval and rollback evidence. | Initial migration and repeatable build/tests exist. CI pipeline, release approval, rollback exercise, and retained build attestations are not implemented. |
| Vulnerability management (CC7) | Dependency advisory scan, remediation tracking, supported runtime policy, security testing, incident triage and documented response evidence. | Current NuGet dependency audit reports no known vulnerable packages. Continuous scanning, application penetration testing, and incident workflow are not implemented. |
| Availability and recovery (A1) | Define SLO/RTO/RPO; protected backups; restore tests; monitoring/alerting; tenant-aware queue retries and disaster recovery evidence. | No deployment topology, backup/restore runbook, availability objectives, or recovery test exists. |
| Processing integrity (PI1) | Validate school-scoped commands; transactional writes; idempotent jobs; reconciliation for financial flows; evidence of failed/duplicate processing tests. | Student validation and SQL constraints exist. Financial processing, jobs, and reconciliation are not implemented. |
| Privacy and retention (P-series, if applicable) | Data inventory, purpose/access control, retention schedules, export/deletion request handling, archival and backup-expiry evidence. | Inventory is initial only. Retention periods, deletion workflows, privacy notices, and request handling are undecided/unimplemented. |

## Evidence handling requirements

- Keep build, test, dependency-scan, migration, access-review, incident, backup, restore, and release evidence in an approved organization-controlled system with access control and retention—not in source control if it contains personal or production data.
- Record the exact commit/build, environment, tool versions, date, operator, result, and remediation reference for each evidence artifact.
- Use synthetic tenant data in CI; never copy real student, guardian, staff, payment, or credential data into test logs.
- Review evidence for tenant confidentiality before sharing it with auditors or vendors.
- Maintain a control owner and review cadence before production use; this repository does not establish operating effectiveness.

## Release gates

Do not enable additional school-owned modules until their API, database relationships, background work, files/exports, cache/search behavior, audit events, and tenant-negative tests are reviewed together. Do not claim SOC 2 compliance or production readiness based only on this checklist or the current foundation tests.
