# Keep the execution-policy exception inside this PowerShell process; it does not change machine policy.
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = "Stop"

# Collect each school's demo password without echoing it to the console.
$northPassword = Read-Host "Set password for the Northfield demo administrator (12+ characters)" -AsSecureString
$southPassword = Read-Host "Set password for the Southridge demo administrator (12+ characters)" -AsSecureString
$teacherPassword = Read-Host "Set password for the Northfield demo teacher (12+ characters)" -AsSecureString
$studentPassword = Read-Host "Set password for the Northfield demo student (12+ characters)" -AsSecureString
# Convert secure strings only for the child process environment expected by .NET configuration.
$northBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($northPassword)
$southBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($southPassword)
$teacherBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($teacherPassword)
$studentBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($studentPassword)

try {
    # Demo seed mode is guarded by the API to run only in Development and a named demo database.
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DemoAuth__NorthPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($northBstr)
    $env:DemoAuth__SouthPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($southBstr)
    $env:DemoAuth__NorthTeacherPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($teacherBstr)
    $env:DemoAuth__NorthStudentPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($studentBstr)
    dotnet run --project "src\EduCore.Api\EduCore.Api.csproj" -- --seed-demo
    if ($LASTEXITCODE -ne 0) {
        throw "Demo data setup failed with exit code $LASTEXITCODE."
    }
}
finally {
    # Remove plaintext environment values and release unmanaged secure-string buffers even on failure.
    Remove-Item Env:\DemoAuth__NorthPassword, Env:\DemoAuth__SouthPassword, `
        Env:\DemoAuth__NorthTeacherPassword, Env:\DemoAuth__NorthStudentPassword `
        -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($northBstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($southBstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($teacherBstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($studentBstr)
}
