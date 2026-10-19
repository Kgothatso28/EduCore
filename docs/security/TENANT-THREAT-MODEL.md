# Tenant isolation threat model (initial)

## Scope and assets

The initial scope is the shared-database ASP.NET Core platform foundation. Protected assets include each school's identities, students/staff/guardians, academic and financial records, uploaded files, reports/exports, notifications, search results, and audit records. Platform-admin credentials and provisioning actions are separate high-impact assets.

## Trust boundaries and threat actors

- Anonymous callers and authenticated school users are untrusted at the API boundary.
- A school administrator is trusted only for that administrator's validated school membership and permissions.
- Users may belong to multiple schools; each school context is an independent authorization boundary.
- Background workers, scheduled reports, file storage, caches, and search are separate execution boundaries and must not inherit unrestricted authority.
- Platform administrators are privileged identities; their access must be explicit, least-privileged, and auditable.

## Threats and controls

| Threat | Required controls |
|---|---|
| IDOR by changing a student/record ID | Tenant-scoped lookup plus authorization of the loaded resource; return non-enumerating not-found/forbidden behavior consistently. |
| Forged client-supplied SchoolId | Resolve from authenticated identity and database-validated membership; ignore arbitrary body/query values for authority. |
| Cross-tenant database reference | Composite tenant-aware foreign keys and transactional application validation. |
| Tenant role escalating to platform admin | Separate platform-admin grant and policy; tenant role writes cannot mutate platform grants. |
| Query without tenant predicate | EF global query filters, explicit tenant-aware service methods, save guard, analyzer/code review, and negative tests. |
| Background task uses missing/stale tenant | Require explicit tenant envelope, revalidate active school/membership as applicable, isolate execution scope, and test failure behavior. |
| File/export/cache/search leakage | Tenant-keyed identifiers and storage paths; authorize access at retrieval; avoid shared keys; purge/reindex by tenant during lifecycle changes. |
| Noisy tenant or repeated enumeration | Rate limits, pagination, per-tenant quotas, alerting, and resource isolation as workload needs become measurable. |
| Privileged access abuse | Separate platform role, explicit elevation, audit actor/school/action/outcome, protected credentials, reviewable access evidence. |
| Tenant offboarding leaves residual data | Document retention and deletion workflow across DB, backups, objects, search, caches, exports, and workers. |

## Data-flow diagram

```text
Untrusted client
    | HTTPS + authenticated token (subject, validated active-school context)
    v
ASP.NET Core authentication
    | subject + active SchoolId
    v
Membership/school-state validation against SQL Server
    | validated TenantContext (or deny)
    v
Authorize endpoint + permission + resource tenant
    | explicit TenantId predicates + EF tenant filters
    v
EF Core transaction ---> SQL Server shared tenant-aware tables
    |                              |
    |                              +-- composite FKs / tenant-aware unique indexes
    +--> tenant-prefixed file/cache/search/export/notification operations

Background job ---> explicit tenant envelope ---> revalidate school state
                                      |            +--> tenant-scoped DB and external I/O
Platform admin ---> separate platform permission + explicit audited operation
```

## Required verification

Automated tests must exercise cross-tenant read/create/update/delete/export/admin attempts, ID substitution, fake school claims, valid multi-school switching, background work, file/export/notification/cache/search scopes, composite uniqueness/FKs, and behavior across migration/import/failure/restart scenarios. Tests must fail closed when tenant context is missing or membership is disabled.

This threat model is an initial design artifact, not a completed penetration test or certification.
