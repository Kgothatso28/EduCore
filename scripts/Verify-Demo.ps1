# Apply script policy only to this process and make failed HTTP assertions terminate verification.
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = "Stop"
# Load the framework HTTP types used for cookie-preserving API calls.
Add-Type -AssemblyName System.Net.Http

$baseUrl = "https://localhost:7260"

# Send one API request, preserve HTTP error details, and decode successful JSON responses.
function Invoke-JsonRequest {
    param(
        [Parameter(Mandatory = $true)] [System.Net.Http.HttpClient] $Client,
        [Parameter(Mandatory = $true)] [string] $Method,
        [Parameter(Mandatory = $true)] [string] $Uri,
        [Parameter()] [hashtable] $Headers = @{},
        [Parameter()] [string] $Body = $null
    )

    # Build the request explicitly so JSON, CSRF, and other headers can be supplied consistently.
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$Method, $Uri)
    foreach ($entry in $Headers.GetEnumerator()) {
        if ($entry.Key -eq 'Content-Type') {
            continue
        }
        $request.Headers.Add($entry.Key, [string]$entry.Value)
    }

    if ($null -ne $Body) {
        # StringContent sets the JSON media type and encodes the request body as UTF-8.
        $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
    }

    $response = $Client.SendAsync($request).GetAwaiter().GetResult()
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ($response.StatusCode -ge [System.Net.HttpStatusCode]::BadRequest) {
        throw "HTTP status $($response.StatusCode) from $Uri. Response: $content"
    }

    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    return $content | ConvertFrom-Json
}

# Keep antiforgery and authentication cookies between calls, as a browser would.
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [System.Net.CookieContainer]::new()
# Development certificates are local-only; never use this callback for a deployed endpoint.
$handler.ServerCertificateCustomValidationCallback = { param($sender, $cert, $chain, $errors) return $true }
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [System.Uri]::new($baseUrl)

try {
    # Wait briefly for the API and its database before starting functional checks.
    $ready = $false
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        try {
            $probe = $client.GetAsync('/health/ready').GetAwaiter().GetResult()
            if ($probe.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
                $ready = $true
                break
            }
        }
        catch {
            # A connection failure during startup is transient; the bounded loop below reports a hard failure.
        }

        Start-Sleep -Seconds 1
    }

    if (-not $ready) {
        throw "The API did not become ready before the timeout expired."
    }

    # Obtain an antiforgery token before login because cookie-authenticated writes are protected.
    $csrfToken = (Invoke-JsonRequest -Client $client -Method 'GET' -Uri '/api/v1/auth/csrf').token
    $loginBody = (@{ userName = 'north-admin'; password = $env:DemoAuth__NorthPassword } | ConvertTo-Json -Compress)
    Invoke-JsonRequest -Client $client -Method 'POST' -Uri '/api/v1/auth/login' -Headers @{ 'X-CSRF-TOKEN' = $csrfToken } -Body $loginBody

    # Login rotates authentication state; retrieve the current token before selecting the school.
    $csrfToken = (Invoke-JsonRequest -Client $client -Method 'GET' -Uri '/api/v1/auth/csrf').token
    $schools = Invoke-JsonRequest -Client $client -Method 'GET' -Uri '/api/v1/auth/schools' -Headers @{ 'X-CSRF-TOKEN' = $csrfToken }
    if ($null -eq $schools -or $schools.Count -lt 1) {
        throw "No demo schools were returned for the signed-in administrator."
    }

    $selectionBody = (@{ schoolId = $schools[0].id } | ConvertTo-Json -Compress)
    Invoke-JsonRequest -Client $client -Method 'POST' -Uri '/api/v1/auth/school' -Headers @{ 'X-CSRF-TOKEN' = $csrfToken } -Body $selectionBody

    # Confirm the selected school is validated before exercising tenant-owned endpoints.
    $sessionData = Invoke-JsonRequest -Client $client -Method 'GET' -Uri '/api/v1/session' -Headers @{ 'X-CSRF-TOKEN' = $csrfToken }
    if ($null -eq $sessionData -or [string]::IsNullOrWhiteSpace([string]$sessionData.schoolId)) {
        throw "Session did not establish an active school context."
    }

    $students = Invoke-JsonRequest -Client $client -Method 'GET' -Uri '/api/v1/students?page=1&pageSize=10' -Headers @{ 'X-CSRF-TOKEN' = $csrfToken }
    if ($null -eq $students -or $students.totalCount -lt 1) {
        throw "The student list was empty for the selected school."
    }

    Write-Host "Demo verification passed: login, school selection, session context, and student API all responded successfully."
}
finally {
    # Release the HTTP handler and its session cookies regardless of verification outcome.
    $client.Dispose()
}
