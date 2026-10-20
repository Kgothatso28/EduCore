# EDUCORE 1.2

EDUCORE is being built as a multi-school school-management platform. This is the first tenant-security foundation increment, not a complete school system or a production-ready deployment.

## What exists in this increment

- ASP.NET Core 10 API with JWT bearer validation configured from an external trusted issuer.
- SQL Server and EF Core 10 as the authoritative application persistence provider.
- Shared-database schema for schools, school memberships, separate platform-administrator grants, and tenant-owned students.
- Server-side active-school membership/status validation; the tenant ID is never taken from request bodies or query parameters.
- Default EF Core tenant query filtering, tenant-write guards, scoped student identifiers, and school-administrator policy enforcement.
- Role-protected principal, teacher, and student dashboards. Teachers can see only assigned class rosters, and students can see only the student profile linked to their authenticated identity.
- Principal analytics include SQL-aggregated monthly student-record creation counts (filterable by calendar year) and student distribution by class. Teacher analytics include student counts across only assigned classes. Charts use accessible native HTML/SVG; no chart package is required.
- The static HTML/CSS/JavaScript interface uses a shared responsive shell, role-aware school navigation, accessible mobile drawers, and consistent sign-in, dashboard, table, and empty-state styling without additional frontend dependencies.
- A separate database-backed platform-permission layer for school registry, existing identity/membership metadata, delegated platform capabilities, administrative audits, and EduCore-managed demo login history.
- SQL Server migration and negative/positive tenant-isolation tests against uniquely named SQL Server LocalDB databases.

Enrollment charts group currently retained `Students.CreatedUtc` records; they cannot recover deleted records or historical month-end enrollment. Attendance, assessment/grade data, fees/finance, timetables, announcements, course materials, and parent dashboards are not implemented, so their analytics are explicitly unavailable instead of using invented data. The platform directory is derived from known memberships and platform grants; it is not a complete identity-provider directory. Account creation/deactivation and provider-wide login history remain identity-provider responsibilities. Keycloak password-recovery email, TOTP-enrollment email, and session/refresh-token revocation endpoints are implemented but unavailable until the integration below is configured and tested. Offline-validated access JWTs may remain valid until expiry after Keycloak logout. The Development-only demo password change signs out its current cookie but cannot revoke copied cookies. Platform pages and APIs do not query student records. Do not issue arbitrary `active_school_id` claims: the trusted identity provider must only issue/switch that claim after validating membership. Authorized platform administrators can provision a school membership for an exact issuer subject; this does not create or verify the external account.

## Architecture and security documents

- [Workspace/source inventory](docs/architecture/INVENTORY.md)
- [Reference application review](docs/REFERENCE-APPLICATIONS.md)
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

The seed script prompts for four separate passwords (minimum 12 characters), hashes them with ASP.NET Core's password hasher, and creates Northfield Academy and Southridge School in the explicitly named `EduCore_Demo_Local` database. Seed is idempotent and does not reset an existing database or change an existing account password. Demo credentials are not stored in source. Usernames are `north-admin`, `south-admin`, `north-teacher`, and `north-student`; keep the passwords you entered.

Open `https://localhost:7260/` (accept/trust the local ASP.NET development certificate using your normal .NET developer workflow). The school administrators have school-wide student record management; the Northfield teacher has one assigned Grade 8 Blue Mathematics class; the Northfield student account is linked to Jordan Morgan's N-1001 profile. The teacher can view that class roster, and the student can view their linked profile and class subjects. Each account's password is the one you entered during seeding. The cookie login is Development-only, uses a 30-minute non-sliding session, HttpOnly/Secure/SameSite cookies and anti-forgery tokens, and is not the production identity provider.

The demo data stays in LocalDB across application restarts. It does not affect non-demo databases. To discard only the local demo DB, first stop the app, then run this exact targeted command:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -Q "IF DB_ID(N'EduCore_Demo_Local') IS NOT NULL BEGIN ALTER DATABASE [EduCore_Demo_Local] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [EduCore_Demo_Local]; END"
```

This explicitly deletes only `EduCore_Demo_Local`; do not change the name to a shared or production database. Re-run the seed script to create fresh demo data.

### Platform administration

The platform UI is at `https://localhost:7260/platform.html`. Platform capabilities are independent database grants checked on each API request. School memberships are separate, and no platform permission implies access to academic records or student profiles. The overview uses school/membership aggregates only. The membership directory shows only known identity identifiers, local-demo account state when available, school roles, and explicitly delegated platform capabilities.

For a separate local Super Admin account, run this in `EDUCORE 1.2` and choose a unique username/password when prompted:

```powershell
.\scripts\Seed-PlatformAdmin.ps1
```

This creates the local credential with ASP.NET Core password hashing, applies additive migrations to the `EduCore_Demo_*` database, and grants the platform capabilities without adding any school membership. It never resets an existing password. The local login flow is Development-only; it is not production identity management.

For a production identity already provisioned by the trusted OIDC provider, explicitly bootstrap the first platform administrator once using its exact immutable subject identifier:

```powershell
$env:PlatformAdmin__BootstrapSubjectId = "exact-subject-from-the-trusted-issuer"
dotnet run --project src\EduCore.Api\EduCore.Api.csproj -- --provision-platform-admin
```

Configure the trusted issuer, audience, and SQL connection first. The command applies pending migrations and refuses to run if a platform role manager is already provisioned. Protect execution and configuration as privileged deployment operations. The API can delegate only allowlisted lower-risk platform capabilities; `platform.roles.manage` and `platform.identity.manage` are bootstrap-only, self-grants are rejected, and all grants/revocations are audited.

On an existing deployment that already has a `platform.roles.manage` grant, explicitly upgrade the selected existing security administrator once. This does not restore or alter any other grant:

```powershell
$env:PlatformAdmin__BootstrapSubjectId = "exact-existing-security-admin-subject"
dotnet run --project src\EduCore.Api\EduCore.Api.csproj -- --provision-platform-identity-management
```

Available platform APIs are rooted at `/api/v1/platform`: `/dashboard`, `/schools`, `/users`, `/memberships/{id}`, `/permissions`, `/login-history`, and `/audit-events`. `POST /schools/{schoolId}/memberships` assigns a school-local role; `PUT /memberships/{id}` changes or deactivates it. School suspension preserves data and memberships while backend tenant validation blocks normal school operations. Audit/login history is searchable, date-filterable, paginated, UTC timestamped, and append-only through the application. Database operator credentials must also restrict direct table mutation and retention; application checks cannot prevent a database owner from tampering.

Authentication history records successful and failed Development cookie logins and observable demo logout events. It omits IP and user-agent data, passwords, hashes, reset tokens, and session secrets. JWT login events occur at the external identity provider and are not represented as EduCore logins.

### Keycloak identity-management integration

The following protected operations use Keycloak's Admin REST API:

- `POST /api/v1/platform/users/{subjectId}/password-recovery` sends Keycloak's `UPDATE_PASSWORD` action email.
- `POST /api/v1/platform/users/{subjectId}/mfa-enrollment` sends its `CONFIGURE_TOTP` action email.
- `POST /api/v1/platform/users/{subjectId}/sessions/revoke` logs out the Keycloak user and revokes their Keycloak session/refresh tokens.

All require the non-delegable `platform.identity.manage` capability, installed only by the explicit first-admin bootstrap; requests are rate-limited to five per 15 minutes per actor and client IP. Keycloak action links default to a 15-minute lifespan; configure a value from 300 to 3600 seconds. EduCore never receives or stores passwords or action tokens. An operator's self-recovery uses Keycloak's hosted login **Forgot password** flow, not an unauthenticated EduCore reset endpoint.

Configure these values through a secret store or environment variables (never commit the client secret):

```powershell
$env:IdentityManagement__Keycloak__BaseUrl = "https://keycloak.example.com"
$env:IdentityManagement__Keycloak__Realm = "educore"
$env:IdentityManagement__Keycloak__ClientId = "educore-api-admin"
$env:IdentityManagement__Keycloak__ClientSecret = "from-secret-store"
$env:IdentityManagement__Keycloak__ActionLinkLifespanSeconds = "900"
```

The base URL is the Keycloak server origin, without `/realms/...`; it must use HTTPS. Configure a confidential service-account client with client-credentials enabled and the minimum realm-management user-management permission needed for these endpoints (do not grant `realm-admin`). Restrict that service account to the EduCore realm. Ensure the trusted issuer is the same realm, the `sub` claim is the Keycloak internal user ID used by Admin REST, and SQL has the provisioned `platform.identity.manage` grant. For email recovery, configure Keycloak SMTP, verified/usable user email addresses, the realm **Forgot password** flow, and a strong realm password policy. For MFA, configure the realm OTP policy and authentication flow to require OTP after enrollment; the API email only initiates TOTP setup. Test both flows end-to-end in a non-production realm before enabling.

Keycloak logout revokes server-side sessions and refresh tokens. Since the API validates JWTs locally, an already-issued access token can remain accepted until its expiry; short access-token lifetimes or online token revocation/introspection are required if immediate access-token invalidation is a requirement. Keycloak integration tests use a local fake HTTP handler only; no Keycloak instance, SMTP delivery, MFA enrollment, or end-to-end recovery has been verified.

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

Migrations are in `src/EduCore.Api/Data/Migrations/`. Apply them explicitly after configuring the deployment environment:

```powershell
dotnet ef database update --project src\EduCore.Api\EduCore.Api.csproj --startup-project src\EduCore.Api\EduCore.Api.csproj
```

The student endpoints are under `/api/v1/students`; they require an active `SchoolAdministrator` membership. A record ID from another tenant returns not found through the tenant-scoped data service. Principal analytics are returned by `/api/v1/dashboard/principal/analytics?year=2026`; the year is validated server-side and all counts/groupings derive from the authenticated school context.

## Tenant rules for future modules

Every new domain record must be classified as platform-owned or school-owned. School-owned records inherit `TenantOwnedEntity`, include `SchoolId` in tenant-scoped constraints/indexes, remain under the base query filter, and are only changed through operations that use the validated `TenantContext`. Cross-tenant relationships require tenant-aware composite constraints. Never use `IgnoreQueryFilters` in application operations. Jobs, files, exports, notifications, caches, and search require explicit tenant-bound access services and tests before those modules ship.

SQL Server Row-Level Security is not enabled in this increment; it is not a substitute for API authorization and will only be considered with connection-pool, worker, migration, and privileged-operation tests.

## Production and assurance status

This project has not undergone a full security review, penetration test, live Keycloak integration, operational readiness review, or compliance audit. SOC 2 alignment and any certification are not claimed. Review the security test report for uncovered acceptance scenarios before treating any deployment as safe.
