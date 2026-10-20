# Keep the execution-policy exception inside this PowerShell process; it does not change machine policy.
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = "Stop"

$userName = Read-Host "Set a username for the separate local platform administrator"
$password = Read-Host "Set its password (12+ characters)" -AsSecureString
$passwordBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)

try {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DemoAuth__PlatformAdmin = $userName
    $env:DemoAuth__PlatformAdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordBstr)
    $project = Join-Path $PSScriptRoot "..\src\EduCore.Api\EduCore.Api.csproj"
    dotnet run --project $project --no-launch-profile -- --seed-platform-admin
    if ($LASTEXITCODE -ne 0) {
        throw "Platform administrator setup failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:\DemoAuth__PlatformAdmin, Env:\DemoAuth__PlatformAdminPassword `
        -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordBstr)
}
