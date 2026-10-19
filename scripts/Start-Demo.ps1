# Limit the policy override to this PowerShell process and stop when configuration or startup fails.
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = "Stop"

# Prompt only for credentials not already supplied by the caller's environment.
if (-not $env:DemoAuth__NorthPassword) {
    $env:DemoAuth__NorthPassword = Read-Host "Set password for the Northfield demo administrator (12+ characters)"
}
if (-not $env:DemoAuth__SouthPassword) {
    $env:DemoAuth__SouthPassword = Read-Host "Set password for the Southridge demo administrator (12+ characters)"
}

# Reject credentials that the seeder's password rules would refuse.
if (([string]::IsNullOrWhiteSpace($env:DemoAuth__NorthPassword)) -or ($env:DemoAuth__NorthPassword.Length -lt 12)) {
    throw "Northfield demo password must be at least 12 characters."
}
if (([string]::IsNullOrWhiteSpace($env:DemoAuth__SouthPassword)) -or ($env:DemoAuth__SouthPassword.Length -lt 12)) {
    throw "Southridge demo password must be at least 12 characters."
}

# Force the local, persistent demo database instead of inheriting a deployment connection string.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__EduCore = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=EduCore_Demo_Local;TrustServerCertificate=true"

# Bind the demo API to the documented local HTTPS and HTTP endpoints.
dotnet run --project "src\EduCore.Api\EduCore.Api.csproj" --no-launch-profile --urls "https://localhost:7260;http://localhost:5260"
