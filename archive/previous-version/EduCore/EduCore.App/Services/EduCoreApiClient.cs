using System.Net.Http.Json;

namespace EduCore.App.Services;

public class EduCoreApiClient
{
    private readonly HttpClient _httpClient;

    public EduCoreApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resp = await _httpClient.GetFromJsonAsync<object>("/api/v1/system/health", cancellationToken);
            return resp != null;
        }
        catch
        {
            return false;
        }
    }
}
