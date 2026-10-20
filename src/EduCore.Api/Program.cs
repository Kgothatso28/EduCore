using EduCore.Api.Data;
using EduCore.Api.Endpoints;
using EduCore.Api.Security;
using EduCore.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Keep the demo cookie identity and its database confined to Development.
var demoAuthEnabled = builder.Configuration.GetValue<bool>("DemoAuth:Enabled");
if (demoAuthEnabled && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Demo authentication is only permitted in Development.");
}

var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
var connectionString = builder.Configuration.GetConnectionString("EduCore");

if (!demoAuthEnabled &&
    (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException(
        "Authentication:Authority and Authentication:Audience must be configured.");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:EduCore must point to the authoritative SQL Server database.");
}

// Select one trusted identity mechanism at startup; do not silently fall back between them.
builder.Services.AddAuthentication(options =>
   {
       if (demoAuthEnabled)
       {
           options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
           options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
       }
       else
       {
           options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
       }
   });

if (demoAuthEnabled)
{
   builder.Services.AddAuthentication()
       .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
       {
           options.Cookie.Name = "__Host-EduCore.Demo";
           options.Cookie.HttpOnly = true;
           options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
           options.Cookie.SameSite = SameSiteMode.Strict;
           options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
           options.SlidingExpiration = false;
           options.Events.OnRedirectToLogin = context =>
           {
               context.Response.StatusCode = StatusCodes.Status401Unauthorized;
               return Task.CompletedTask;
           };
           options.Events.OnRedirectToAccessDenied = context =>
           {
               context.Response.StatusCode = StatusCodes.Status403Forbidden;
               return Task.CompletedTask;
           };
       });
   builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
   builder.Services.AddScoped<IPasswordHasher<EduCore.Api.Domain.DemoCredential>,
       PasswordHasher<EduCore.Api.Domain.DemoCredential>>();
   builder.Services.AddScoped<DemoAuthenticationService>();
   builder.Services.AddScoped<DemoSeeder>();
}
else
{
   builder.Services.AddAuthentication()
       .AddJwtBearer(options =>
       {
           options.Authority = authority;
           options.Audience = audience;
           options.RequireHttpsMetadata = true;
       });
}

builder.Services.AddAuthorizationBuilder()
   .AddPolicy("SchoolMember", policy =>
   {
       policy.RequireAuthenticatedUser();
       policy.AddRequirements(new ActiveSchoolMembershipRequirement());
    })
    .AddPolicy("SchoolAdministrator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(
            new ActiveSchoolMembershipRequirement(EduCore.Api.Domain.SchoolRole.SchoolAdministrator));
    })
    .AddPolicy("SchoolTeacher", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveSchoolMembershipRequirement(
            EduCore.Api.Domain.SchoolRole.Teacher));
    })
    .AddPolicy("SchoolStudent", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveSchoolMembershipRequirement(
            EduCore.Api.Domain.SchoolRole.Student));
    })
    .AddPolicy(PlatformPermissions.DashboardView, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.DashboardView))
    .AddPolicy(PlatformPermissions.SchoolsView, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.SchoolsView))
    .AddPolicy(PlatformPermissions.SchoolsManage, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.SchoolsManage))
    .AddPolicy(PlatformPermissions.UsersView, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.UsersView))
    .AddPolicy(PlatformPermissions.UsersManage, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.UsersManage))
    .AddPolicy(PlatformPermissions.RolesView, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.RolesView))
    .AddPolicy(PlatformPermissions.RolesManage, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.RolesManage))
    .AddPolicy(PlatformPermissions.AuditView, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.AuditView))
    .AddPolicy(PlatformPermissions.IdentityManage, policy =>
        RequirePlatformPermission(policy, PlatformPermissions.IdentityManage));
builder.Services.AddDbContext<EduCoreDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("demo-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("identity-action", context =>
    {
        var subjectId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var clientKey = $"{subjectId ?? "anonymous"}:{context.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many sign-in attempts. Try again later." },
            cancellationToken);
    };
});
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<SchoolContextResolver>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PlatformAdministrationService>();
builder.Services.AddScoped<PlatformAdminBootstrapper>();
builder.Services.AddHttpClient<KeycloakIdentityManagementService>();
builder.Services.AddScoped<ActiveSchoolMembershipHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler>(
    services => services.GetRequiredService<ActiveSchoolMembershipHandler>());
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    PlatformPermissionHandler>();

var app = builder.Build();

// Seeding is an explicit local-only operation and exits before serving requests.
if (args.Contains("--seed-platform-admin", StringComparer.OrdinalIgnoreCase))
{
    if (!demoAuthEnabled)
    {
        throw new InvalidOperationException(
            "Demo platform-admin seeding requires Development and DemoAuth:Enabled=true.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
        .SeedPlatformAdminAsync(CancellationToken.None);
    Console.WriteLine("The optional Development-only platform administrator is ready.");
    return;
}

if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
{
    if (!demoAuthEnabled)
    {
        throw new InvalidOperationException(
            "Demo seeding requires ASPNETCORE_ENVIRONMENT=Development and DemoAuth:Enabled=true.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
        .SeedAsync(CancellationToken.None);
    Console.WriteLine("EduCore LocalDB demo schools and sample students are ready.");
    return;
}

if (args.Contains("--provision-platform-admin", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EduCoreDbContext>();
    await dbContext.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PlatformAdminBootstrapper>()
        .ProvisionFirstAdministratorAsync(CancellationToken.None);
    Console.WriteLine("The configured platform administrator was provisioned.");
    return;
}

if (args.Contains("--provision-platform-identity-management", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EduCoreDbContext>();
    await dbContext.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PlatformAdminBootstrapper>()
        .ProvisionIdentityManagementCapabilityAsync(CancellationToken.None);
    Console.WriteLine("The existing platform security administrator can manage Keycloak identity actions.");
    return;
}

// Middleware order is security-sensitive: select the endpoint and establish identity before
// resolving its validated school, and authorize endpoints only after that tenant context exists.
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
if (demoAuthEnabled)
{
    // Enforce CSRF validation for every cookie-authenticated state-changing request before routes run.
    app.Use(async (context, next) =>
    {
        if (HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) ||
            HttpMethods.IsDelete(context.Request.Method))
        {
            try
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>()
                    .ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request token." });
                return;
            }
        }

        await next(context);
    });
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapDemoAuthenticationEndpoints();
}
// Resolve the school claim against current membership/status before tenant-aware endpoints execute.
app.UseMiddleware<SchoolTenantMiddleware>();
app.UseAuthorization();

// Liveness is process-only; readiness also verifies that the authoritative database is reachable.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (EduCoreDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready", database = "connected" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
// Student routes require administrator membership; data access remains tenant-filtered by the DbContext.
app.MapStudentEndpoints("SchoolAdministrator");
app.MapDashboardEndpoints();
app.MapPlatformEndpoints();

app.Run();

/// <summary>Adds one database-backed platform capability to a named authorization policy.</summary>
static void RequirePlatformPermission(
    Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy,
    string permission)
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new PlatformPermissionRequirement(permission));
}

/// <summary>Exposes the top-level entry point to integration-test hosting.</summary>
public partial class Program
{
}
