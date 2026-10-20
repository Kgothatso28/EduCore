using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EduCore.Api.Services;
using Microsoft.Extensions.Configuration;

namespace EduCore.Api.Tests;

public sealed class KeycloakIdentityManagementTests
{
    [Fact]
    public async Task Password_recovery_uses_short_lived_provider_action_without_returning_token()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(JsonResponse("""{"access_token":"test-access-token"}"""));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler);
        var service = new KeycloakIdentityManagementService(client, Configuration());

        await service.SendPasswordRecoveryAsync("user-subject-id", default);

        var tokenRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Equal(
            "https://keycloak.example/realms/educore/protocol/openid-connect/token",
            tokenRequest.Uri);
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);
        Assert.Contains("client_secret=test-client-secret", tokenRequest.Body);

        var actionRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Put, actionRequest.Method);
        Assert.Equal(
            "https://keycloak.example/admin/realms/educore/users/user-subject-id/execute-actions-email?lifespan=900",
            actionRequest.Uri);
        Assert.Equal("""["UPDATE_PASSWORD"]""", actionRequest.Body);
        Assert.Equal(
            new AuthenticationHeaderValue("Bearer", "test-access-token"),
            actionRequest.Authorization);
    }

    [Fact]
    public async Task Mfa_enrollment_and_session_revocation_use_separate_keycloak_operations()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(JsonResponse("""{"access_token":"mfa-token"}"""));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Responses.Enqueue(JsonResponse("""{"access_token":"logout-token"}"""));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler);
        var service = new KeycloakIdentityManagementService(client, Configuration());

        await service.RequireTotpEnrollmentAsync("keycloak-user", default);
        await service.RevokeSessionsAsync("keycloak-user", default);

        Assert.Equal("""["CONFIGURE_TOTP"]""", handler.Requests[1].Body);
        Assert.Equal(
            "https://keycloak.example/admin/realms/educore/users/keycloak-user/logout",
            handler.Requests[3].Uri);
        Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
    }

    [Fact]
    public async Task Provider_errors_do_not_expose_the_response_body()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(JsonResponse("""{"access_token":"test-access-token"}"""));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("sensitive provider diagnostics")
        });
        using var client = new HttpClient(handler);
        var service = new KeycloakIdentityManagementService(client, Configuration());

        var error = await Assert.ThrowsAsync<KeycloakIdentityManagementException>(
            () => service.SendPasswordRecoveryAsync("keycloak-user", default));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.DoesNotContain("sensitive provider diagnostics", error.Message);
    }

    [Fact]
    public async Task Management_actions_require_complete_https_configuration()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new KeycloakIdentityManagementService(
            client,
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["IdentityManagement:Keycloak:BaseUrl"] = "http://keycloak.example",
                    ["IdentityManagement:Keycloak:Realm"] = "educore",
                    ["IdentityManagement:Keycloak:ClientId"] = "client",
                    ["IdentityManagement:Keycloak:ClientSecret"] = "secret"
                }).Build());

        Assert.False(service.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RevokeSessionsAsync("user-id", default));
        Assert.Empty(handler.Requests);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityManagement:Keycloak:BaseUrl"] = "https://keycloak.example",
            ["IdentityManagement:Keycloak:Realm"] = "educore",
            ["IdentityManagement:Keycloak:ClientId"] = "educore-api",
            ["IdentityManagement:Keycloak:ClientSecret"] = "test-client-secret"
        }).Build();

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            return Responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Uri,
        AuthenticationHeaderValue? Authorization,
        string Body);
}
