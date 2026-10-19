using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EduCore.Web.Services;

public class AuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private string? _token;
    private string? _role;
    private string? _tenantId;

    public AuthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Role => _role;
    public string? TenantId => _tenantId;

    public async Task<bool> LoginAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("EduCoreApi");
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (!resp.IsSuccessStatusCode) return false;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("accessToken", out var at)) return false;
        _token = at.GetString();

        // set role and tenant from token payload
        try
        {
            var parts = _token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
                var obj = JsonSerializer.Deserialize<JsonElement>(bytes);
                if (obj.TryGetProperty("role", out var r)) _role = r.GetString();
                if (obj.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out var r2)) _role ??= r2.GetString();
                if (obj.TryGetProperty("tenant_id", out var t)) _tenantId = t.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return true;
    }

    public void Logout()
    {
        _token = null;
        _role = null;
        _tenantId = null;
    }

    public HttpClient CreateApiClient()
    {
        var client = _httpClientFactory.CreateClient("EduCoreApi");
        if (!string.IsNullOrEmpty(_token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return client;
    }
}
