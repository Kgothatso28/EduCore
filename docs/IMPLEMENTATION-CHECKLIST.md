# Implementation status

Status is based on code and verification in the current target, not the existence of a design document.

## Working and verified foundation

- [x] Target project and source-project inventory recorded; source folders preserved.
- [x] .NET 10 API and EF Core 10 SQL Server provider.
- [x] Shared-database tenant foundation: School, membership, separate platform-admin grant, immutable SchoolId.
- [x] Authentication configuration retained for trusted JWT issuer outside the explicitly Development-only demo.
- [x] Development-only cookie login with ASP.NET password hashing, bounded session lifetime, CSRF protection, and membership-validated school selection.
- [x] Tenant-scoped read/write behavior for students and school-admin-only student mutations.
- [x] Student create/read/update/delete, search, filtering by text and pagination through the UI and API.
- [x] Explicit local demo seeding with two schools and distinct admin identities. Passwords are prompted at seed time, hashed in SQL Server, and not embedded in project files.
- [x] Initial schema and incremental EF migrations.
- [x] Test fixtures use unique SQL Server LocalDB databases; no database reset outside the test-created database.

## Partial / not yet meeting full module requirements

- [ ] Authentication: the local demo has login/logout and validated school switching; production identity-provider integration, recovery, invitations, profile updates, account deactivation workflows, and production token issuance/renewal are not implemented.
- [ ] School lifecycle: two-school demo seed only. No platform-authorized provisioning/suspension/reactivation/archive/retention/delete endpoints or audit workflow.
- [ ] Student: current profile contains only local ID and names. No class assignment, guardian links, status/admission history, archive, or complete demographic profile.
- [ ] UI: connected responsive login and student-management page exists. No role-specific full dashboards or remaining module views.
- [ ] Demo: sample schools and students are seeded through SQL Server and credentials are supplied at run time. Restart persistence and HTTP workflows are being checked; no shared seeded password is prescribed.

## Not implemented

- Guardians/parent portal, staff/teachers, classes/sections/subjects/academic sessions/enrollments.
- Attendance, assessment configuration, grade books, report cards and correction/publishing audit.
- Fees, billing, invoices, receipts, payments, waivers or external payment integration.
- Document uploads/downloads, external notification channels, reports/CSV/PDF exports, background jobs, cache/search.
- Domain audit event persistence and platform-admin oversight interface.
- SQL Server Row-Level Security, deployment infrastructure, CI pipeline, operational monitoring, backup/restore and disaster-recovery tests.

## Next implementation order

1. Build class and guardian entities with tenant-aware relationships and student assignment screens.
2. Add staff/teacher identities and teacher assignment authorization.
3. Add attendance register and history (unique school/student/date) with correction audit.
4. Add academic sessions, subjects, assessments and configurable grading; enforce assigned-teacher permissions.
5. Add financial records as an isolated module with immutable posted payments/reversal transactions.
6. Complete platform provisioning/lifecycle and append-only audit operations.
7. Secure documents/notifications/reports/exports and add their tenant-negative tests.
8. Add complete API/UI end-to-end test infrastructure and review production deployment controls.

The full master prompt acceptance criteria are not yet complete. Production readiness and SOC 2 compliance are not claimed.
