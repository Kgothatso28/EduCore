# EDUCORE 1.2

EDUCORE is being built as a multi-school school-management platform. This is the first tenant-security foundation increment, not a complete school system or a production-ready deployment.

## What exists in this increment

- ASP.NET Core 10 API with JWT bearer validation configured from an external trusted issuer.
- SQL Server and EF Core 10 as the authoritative application persistence provider.
- Shared-database schema for schools, school memberships, separate platform-administrator grants, and tenant-owned students.
- Server-side active-school membership/status validation; the tenant ID is never taken from request bodies or query parameters.
- Default EF Core tenant query filtering, tenant-write guards, scoped student identifiers, and school-administrator policy enforcement.
- SQL Server migration and negative/positive tenant-isolation tests against uniquely named SQL Server LocalDB databases.

No school-provisioning endpoint, token issuer, complete user directory, or non-student business modules are implemented yet. Do not issue arbitrary `active_school_id` claims: the configured trusted identity provider must only issue/switch that claim after validating membership. There is currently no application workflow to provision the first school or membership.

## Architecture and security documents

- [Workspace/source inventory](docs/architecture/INVENTORY.md)
- [Multi-tenancy architecture decision](docs/architecture/ADR-001-multi-tenancy.md)
- [Tenant-isolation threat model and data-flow](docs/security/TENANT-THREAT-MODEL.md)
- [Security test report](docs/security/SECURITY_TEST_REPORT.md)
- [Security control/evidence plan](docs/security/CONTROL-EVIDENCE.md)

## Build and test (PowerShell)

Requires the .NET 10 SDK and SQL Server LocalDB for the current provider-backed tests.

```powershell
dotnet tool restore
dotnet build EduCore.sln
dotnet test tests\EduCore.Api.Tests\EduCore.Api.Tests.csproj
```

Tests create a uniquely named database on `(localdb)\MSSQLLocalDB` and delete it after each test. They do not connect to a production SQL Server.

## Configure and run

Set secrets/configuration through a secret store or environment variables. Do not commit database credentials or signing keys.

### Local two-school demonstration

Requires .NET 10 SDK and SQL Server LocalDB. From `EDUCORE 1.2`:

```powershell
.\scripts\Seed-Demo.ps1
.\scripts\Start-Demo.ps1
```

The seed script prompts for two separate passwords (minimum 12 characters), hashes them with ASP.NET Core's password hasher, and creates Northfield Academy and Southridge School in the explicitly named `EduCore_Demo_Local` database. Seed is idempotent and does not reset an existing database or change an existing account password. Demo credentials are not stored in source. Usernames default to `north-admin` and `south-admin`; keep the passwords you entered.

Open `https://localhost:7260/` (accept/trust the local ASP.NET development certificate using your normal .NET developer workflow). Use the matching administrator username/password. Each administrator is a member of only their own demo school; if only one school is available, it is activated automatically after login. The page supports student search, pagination, create, edit, and delete using the SQL-backed API. The cookie login is Development-only, uses a 30-minute non-sliding session, HttpOnly/Secure/SameSite cookies and anti-forgery tokens, and is not the production identity provider.

The demo data stays in LocalDB across application restarts. It does not affect non-demo databases. To discard only the local demo DB, first stop the app, then run this exact targeted command:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -Q "IF DB_ID(N'EduCore_Demo_Local') IS NOT NULL BEGIN ALTER DATABASE [EduCore_Demo_Local] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [EduCore_Demo_Local]; END"
```

This explicitly deletes only `EduCore_Demo_Local`; do not change the name to a shared or production database. Re-run the seed script to create fresh demo data.

### Trusted JWT deployment configuration

For non-demo environments, configure a trusted OIDC issuer and API audience, then apply migrations explicitly:

```powershell
$env:Authentication__Authority = "https://your-trusted-issuer/"
$env:Authentication__Audience = "your-api-audience"
$env:ConnectionStrings__EduCore = "Server=your-sql-server;Database=EduCore;Integrated Security=true;Encrypt=true;TrustServerCertificate=false"
dotnet tool restore
dotnet run --project src\EduCore.Api\EduCore.Api.csproj
```

The API fails at startup if the trusted authority, audience, or SQL Server connection string is missing. The identity provider must emit exactly one subject claim and, for school APIs, one signed `active_school_id` claim. Membership, role, and school status are still checked in SQL Server on each request.

The first migration is in `src/EduCore.Api/Data/Migrations/`. Apply it explicitly after configuring the deployment environment:

```powershell
dotnet ef database update --project src\EduCore.Api\EduCore.Api.csproj --startup-project src\EduCore.Api\EduCore.Api.csproj
```

The student endpoints are under `/api/v1/students`; they require an active `SchoolAdministrator` membership. A record ID from another tenant returns not found through the tenant-scoped data service.

## Tenant rules for future modules

Every new domain record must be classified as platform-owned or school-owned. School-owned records inherit `TenantOwnedEntity`, include `SchoolId` in tenant-scoped constraints/indexes, remain under the base query filter, and are only changed through operations that use the validated `TenantContext`. Cross-tenant relationships require tenant-aware composite constraints. Never use `IgnoreQueryFilters` in application operations. Jobs, files, exports, notifications, caches, and search require explicit tenant-bound access services and tests before those modules ship.

SQL Server Row-Level Security is not enabled in this increment; it is not a substitute for API authorization and will only be considered with connection-pool, worker, migration, and privileged-operation tests.

## Production and assurance status

This project has not undergone a full security review, penetration test, external identity-provider integration, operational readiness review, or compliance audit. SOC 2 alignment and any certification are not claimed. Review the security test report for uncovered acceptance scenarios before treating any deployment as safe.
