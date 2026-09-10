using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using EduCore.Infrastructure.Persistence;
using EduCore.Application.Abstractions;
using EduCore.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure Serilog (basic console logger). In production configure via configuration.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Services
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EduCore API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme"
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// DbContext
var conn = builder.Configuration.GetConnectionString("EduCoreDatabase") ?? builder.Configuration["ConnectionStrings:EduCoreDatabase"];
builder.Services.AddDbContext<EduCoreDbContext>(options => options.UseSqlServer(conn ?? "Server=(localdb)\\mssqllocaldb;Database=EduCoreDev;Trusted_Connection=True;"));

// Tenant context - simple HTTP implementation
// The HttpTenantContext type is declared in the EduCore.Api namespace below. When using
// top-level statements the file-level code is in the global namespace, so reference the
// implementation with its full namespace to ensure the type is found during compilation.
builder.Services.AddScoped<ITenantContext, EduCore.Api.HttpTenantContext>();

// Authentication - JWT foundation
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "ChangeThisSecretForDevOnly";
var key = Encoding.UTF8.GetBytes(jwtSecret);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

// Register demo repositories when DemoMode enabled
var demoMode = builder.Configuration.GetValue<bool>("DemoMode");
if (demoMode)
{
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IStudentRepository, EduCore.Infrastructure.Demo.DemoStudentRepository>();
    // Register demo parent repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IParentRepository, EduCore.Infrastructure.Demo.DemoParentRepository>();
    // Register demo staff repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IStaffRepository, EduCore.Infrastructure.Demo.DemoStaffRepository>();
    // Register demo class repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IClassRepository, EduCore.Infrastructure.Demo.DemoClassRepository>();
    // Register demo subject repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.ISubjectRepository, EduCore.Infrastructure.Demo.DemoSubjectRepository>();
    // Register demo attendance repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IAttendanceRepository, EduCore.Infrastructure.Demo.DemoAttendanceRepository>();
    // Register demo assessments repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IAssessmentRepository, EduCore.Infrastructure.Demo.DemoAssessmentRepository>();
    // Register demo examinations repository
    builder.Services.AddSingleton<EduCore.Application.Interfaces.IExaminationRepository, EduCore.Infrastructure.Demo.DemoExaminationRepository>();
    // Register demo results repository
            builder.Services.AddSingleton<EduCore.Application.Interfaces.IResultRepository, EduCore.Infrastructure.Demo.DemoResultRepository>();
            builder.Services.AddSingleton<EduCore.Application.Interfaces.IFinanceRepository, EduCore.Infrastructure.Demo.DemoFinanceRepository>();
            builder.Services.AddSingleton<EduCore.Application.Interfaces.ICommunicationRepository, EduCore.Infrastructure.Demo.DemoCommunicationRepository>();
            builder.Services.AddSingleton<EduCore.Application.Interfaces.IDocumentRepository, EduCore.Infrastructure.Demo.DemoDocumentRepository>();
}

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// If the app is started with dynamic ports for testing, Kestrel does not accept
// "localhost:0". Ensure that requests to dynamic ports bind to 127.0.0.1 or [::1]
// when developer requested dynamic binding. The dotnet run helper used earlier
// passed "https://localhost:0;http://localhost:0" which Kestrel rejects.
// Default Run will use configured URLs from environment or launchSettings.json.
app.Run();

// Minimal tenant context implementation in Api project
namespace EduCore.Api
{
    using System.Security.Claims;
    using Microsoft.AspNetCore.Http;

    internal class HttpTenantContext : ITenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? TenantId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null) return null;
                // Example: read tenant id from claim "tenant_id" if present
                return user.FindFirst("tenant_id")?.Value;
            }
        }
    }
}
