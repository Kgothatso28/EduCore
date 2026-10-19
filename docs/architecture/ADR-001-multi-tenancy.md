# ADR-001: Shared-database tenant isolation foundation

- Status: Accepted for the initial platform foundation
- Date: 2026-10-15

## Context

EDUCORE must host multiple independent schools. School-owned records must not cross school boundaries even when a caller changes identifiers, supplies another school ID, or invokes background/report/file workflows. Microsoft SQL Server is authoritative. There is no documented contractual, regulatory, or scale requirement to isolate tenants in separate databases.

## Decision

Start with a **modular monolith**: an ASP.NET Core API, domain/application services, and an EF Core persistence layer. Use shared SQL Server tables with an immutable `SchoolId` on every school-owned row. `School` and platform-admin grants are platform-level records; tenant membership is explicit and binds one user to one school and a school-scoped role.

Resolve the active school from authenticated subject and active-school identity claims, then validate that exact `(UserId, SchoolId)` membership and school state against the authoritative database on each request. Never authorize from a client-supplied `SchoolId` alone. A future school-switch operation must validate membership before issuing a new trusted active-school token. Platform administrator grants are separate from school roles; school roles cannot confer platform privileges.

Enforce tenant scope at multiple application boundaries:

1. Deny by default; protected endpoints require authentication, a validated active membership, and a school-scoped permission.
2. Tenant-scoped EF Core query filters provide a default exclusion boundary, while application operations also use explicit tenant predicates and resource authorization.
3. A `SaveChanges` guard rejects school-owned entities without a matching current tenant. Database foreign keys and unique indexes include `SchoolId` where relevant to prevent cross-tenant references and collisions.
4. Background jobs and exports must carry an explicit tenant execution context and revalidate the school's state; no ambient tenant context is inferred from a worker's client/request state.
5. File object keys, cache keys, search partition keys, audit records, and generated exports include tenant identity and are only resolved through authorized server-side services.

Use SQL Server as the production EF Core provider. SQL Server Row-Level Security is **not enabled in the first increment**: it is defense in depth requiring correct connection-session context management across pooled connections, workers, admin operations, and migrations. Revisit only with provider-backed tests and operational procedures. Application authorization remains mandatory even if RLS is later adopted.

Use least-privilege database credentials, parameterized EF Core access, transactions for multi-record operations, and explicit EF Core migrations. Never expose database credentials or direct DB connectivity to browser/mobile clients.

## Initial tenant-owned data inventory

School, membership, and platform-admin grant are identity/control-plane foundations. All other school-owned data is tenant-bound, including:

- users, staff, students, guardians and school-scoped roles;
- classes, sections, subjects, enrollments, attendance, assessments, grades and academic sessions;
- fees, invoices, payments, receipts and financial reports;
- uploaded documents, learning materials, exports, notifications, caches, search projections, and tenant audit events.

Each future business entity must be classified as platform-owned or school-owned before its schema is added. School-owned entities require a non-null immutable `SchoolId`; there is no implicit global/single-school fallback.

## Tenant lifecycle

- Provision: create a school with a server-generated immutable ID, then separately create/assign its initial membership through a privileged, audited provisioning operation.
- Suspend: block tenant access and new work; workers must check active status before processing.
- Archive: make the tenant read-only and retain records according to a configured policy.
- Retention/deletion: require an authorized, audited lifecycle workflow with a retention/backup policy and explicit handling of related files, search data, caches, exports, and audit records. Hard deletion is not a routine administrator operation.

## Consequences and tradeoffs

- Shared tables simplify operations and transactions but make complete tenant scoping a correctness-critical invariant.
- Composite constraints and query filters reduce accidental leakage; neither alone replaces endpoint authorization or explicit resource checks.
- Shared resource pools need per-tenant quotas and abuse controls to limit noisy-neighbor impact.
- Initial implementation is intentionally a modular monolith, not microservices: the domain and team scale are not yet known, and a distributed design would add coordination without improving the first security boundary.

## Deferred decisions

- Identity provider, token issuance/signing and step-up controls for platform administration.
- SQL Server RLS and Azure hosting topology.
- Per-tenant storage/search infrastructure versus tenant-keyed shared services.
- Data residency, retention periods, availability SLO, RTO/RPO, and tenant quotas.

These require deployment/compliance requirements not present in the inspected workspace. Defaults must remain deny-by-default until decided.
