# Limit the policy override to this PowerShell process and stop when configuration or startup fails.
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = "Stop"

# Force the local, persistent demo database instead of inheriting a deployment connection string.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__EduCore = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=EduCore_Demo_Local;TrustServerCertificate=true"

# Bind the demo API to the documented local HTTPS and HTTP endpoints.
dotnet run --project "src\EduCore.Api\EduCore.Api.csproj" --no-launch-profile --urls "https://localhost:7260;http://localhost:5260"
