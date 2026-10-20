using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EduCore.Api.Services;

/// <summary>Performs narrowly scoped Keycloak account actions using a confidential service client.</summary>
public sealed class KeycloakIdentityManagementService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    private const int DefaultActionLifespanSeconds = 900;

    /// <summary>Whether all required settings are present and the Keycloak URL uses HTTPS.</summary>
    public bool IsConfigured
    {
        get
        {
            var baseUrl = configuration["IdentityManagement:Keycloak:BaseUrl"];
            return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttps &&
                   string.IsNullOrWhiteSpace(uri.UserInfo) &&
                   string.IsNullOrWhiteSpace(uri.Query) &&
                   string.IsNullOrWhiteSpace(uri.Fragment) &&
                   !string.IsNullOrWhiteSpace(configuration["IdentityManagement:Keycloak:Realm"]) &&
                   !string.IsNullOrWhiteSpace(configuration["IdentityManagement:Keycloak:ClientId"]) &&
                   !string.IsNullOrWhiteSpace(configuration["IdentityManagement:Keycloak:ClientSecret"]);
        }
    }

    /// <summary>Sends a one-time, expiring provider-managed password update email.</summary>
    public Task SendPasswordRecoveryAsync(
        string subjectId,
        CancellationToken cancellationToken) =>
        SendRequiredActionsEmailAsync(subjectId, ["UPDATE_PASSWORD"], cancellationToken);

    /// <summary>Sends a one-time provider-managed TOTP enrollment email.</summary>
    public Task RequireTotpEnrollmentAsync(
        string subjectId,
        CancellationToken cancellationToken) =>
        SendRequiredActionsEmailAsync(subjectId, ["CONFIGURE_TOTP"], cancellationToken);

    /// <summary>Revokes the target user's Keycloak sessions and associated refresh tokens.</summary>
    public async Task RevokeSessionsAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        ValidateSubject(subjectId);
        using var request = await CreateAdminRequestAsync(
            HttpMethod.Post, $"users/{Uri.EscapeDataString(subjectId)}/logout",
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    private async Task SendRequiredActionsEmailAsync(
        string subjectId,
        string[] actions,
        CancellationToken cancellationToken)
    {
        ValidateSubject(subjectId);
        var lifespan = GetActionLinkLifespan();
        using var request = await CreateAdminRequestAsync(
            HttpMethod.Put,
            $"users/{Uri.EscapeDataString(subjectId)}/execute-actions-email?lifespan={lifespan}",
            cancellationToken);
        request.Content = JsonContent.Create(actions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    private async Task<HttpRequestMessage> CreateAdminRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Keycloak identity management is not configured with an HTTPS URL and service client.");
        }

        var baseUrl = configuration["IdentityManagement:Keycloak:BaseUrl"]!.TrimEnd('/');
        var realm = Uri.EscapeDataString(
            configuration["IdentityManagement:Keycloak:Realm"]!);
        var tokenUrl = $"{baseUrl}/realms/{realm}/protocol/openid-connect/token";
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuration["IdentityManagement:Keycloak:ClientId"]!,
                ["client_secret"] = configuration["IdentityManagement:Keycloak:ClientSecret"]!
            })
        };
        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        EnsureSuccess(tokenResponse);
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new KeycloakIdentityManagementException(HttpStatusCode.BadGateway);
        }

        var request = new HttpRequestMessage(
            method, $"{baseUrl}/admin/realms/{realm}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", token.AccessToken);
        return request;
    }

    private int GetActionLinkLifespan()
    {
        var configured = configuration["IdentityManagement:Keycloak:ActionLinkLifespanSeconds"];
        if (configured is null)
        {
            return DefaultActionLifespanSeconds;
        }

        if (!int.TryParse(configured, out var seconds) || seconds is < 300 or > 3600)
        {
            throw new InvalidOperationException(
                "Keycloak action-link lifespan must be between 300 and 3600 seconds.");
        }

        return seconds;
    }

    private static void ValidateSubject(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 200)
        {
            throw new ArgumentException("A valid Keycloak subject ID is required.", nameof(subjectId));
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new KeycloakIdentityManagementException(response.StatusCode);
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);
}

/// <summary>Represents an unsuccessful Keycloak management API response without its body.</summary>
public sealed class KeycloakIdentityManagementException(HttpStatusCode statusCode)
    : Exception("The Keycloak identity-management request failed.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
