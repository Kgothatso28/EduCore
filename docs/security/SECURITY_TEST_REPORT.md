# Tenant security test report

- Scope: EDUCORE first tenant-foundation increment
- Test provider: SQL Server 2025 LocalDB through EF Core SQL Server provider
- Result: 11 automated tests passed
- Production SQL Server, identity provider, and network deployment were not tested.

## Coverage in this increment

| Check | Result |
|---|---|
| School A cannot list/read/update/delete School B's student | Covered: query filter and resource lookup tests |
| Foreign record ID does not bypass the tenant filter | Covered: find, update, and delete return no resource for the other school |
| Create authority comes from validated server tenant context, not a request SchoolId property | Covered at service/DTO boundary |
| School membership and active school status are checked | Covered, including inactive membership and suspended school |
| Legitimate multi-school identity resolves each explicitly claimed active school separately | Covered; a school without membership is rejected |
| School teacher cannot authorize an administrator-only student operation | Covered |
| School roles cannot represent platform-administrator privilege | Covered at role model boundary; platform-grant operation is not implemented |
| Tenant-scoped uniqueness | Covered against SQL Server: same local student ID rejected within one school and accepted in another |
| Missing tenant context fails closed | Covered for reads and writes |
| Tenant ID mutation after insert | Covered by write guard |
| SQL Server migration/schema application | Applied during test setup using a fresh per-test database |

## Not covered yet (release blockers for the corresponding features)

- Staff, class, attendance, grade, fee, invoice, payment, document, and report models/API operations do not exist yet; their cross-tenant CRUD/export tests remain unimplemented.
- No background queue, scheduled report, import pipeline, or tenant-aware worker exists; no job execution test is claimed.
- No file storage, export, notification, cache, or search service exists yet; no leakage test is claimed for those surfaces.
- No tenant provisioning/suspension/archive/retention/deletion workflows, administrative audit pipeline, or platform-admin API exists.
- No SQL Server Row-Level Security, production SQL Server, connection-pool context reset, deployment migration rollback, or application restart/restore test is included.
- The request pipeline has not been exercised against an external JWT issuer; unit tests validate membership resolution and authorization handlers using test principals.
- No adversarial penetration testing, data-flow runtime tracing, SOC 2 audit, or certification was performed.

The tests prove only the existing student foundation and related authorization/schema invariants. All uncovered data categories and processing surfaces must have equivalent tenant-isolation tests before implementation is considered complete.

## Latest verification

- `dotnet test tests\EduCore.Api.Tests\EduCore.Api.Tests.csproj`: passed, 11/11.
- The test fixture applies the generated initial migration to a unique SQL Server LocalDB database per test and deletes the database during cleanup. A post-test LocalDB query confirmed no `EduCoreTenantTests_*` database remained.
- `dotnet build EduCore.sln --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet list src\EduCore.Api\EduCore.Api.csproj package --vulnerable --include-transitive`: no vulnerable packages reported by the configured NuGet advisory source.
- `dotnet list tests\EduCore.Api.Tests\EduCore.Api.Tests.csproj package --vulnerable --include-transitive`: no vulnerable packages reported by the configured NuGet advisory source.

These results apply only to the source and dependency state at this increment; rerun after changes and before deployment.
