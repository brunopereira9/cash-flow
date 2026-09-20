using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CashFlow.BuildingBlocks.Identity;
using Microsoft.Extensions.Configuration;

namespace Core.UnitTests;

public sealed class KeycloakStateClientTests
{
    [Fact]
    public async Task LoadsUserStateAndRealmRoles()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"admin-token\"}");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/users/user-1", StringComparison.Ordinal))
            {
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("admin-token", request.Headers.Authorization.Parameter);
                return JsonResponse("{\"enabled\":true}");
            }

            return JsonResponse("[{\"name\":\"admin\"},{\"name\":\"auditor\"}]");
        });
        var client = CreateClient(handler);
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-1")],
            "test"));
        var sut = CreateSut(client);

        var result = await sut.GetAsync(subject, CancellationToken.None);

        Assert.True(result.Enabled);
        Assert.Equal(["admin", "auditor"], result.Roles.OrderBy(role => role));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task PropagatesHttpFailureFromKeycloak()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSut(CreateClient(handler));
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-1")],
            "test"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.GetAsync(subject, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsPrincipalWithoutSubjectBeforeCallingKeycloak()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("Keycloak should not be called."));
        var sut = CreateSut(CreateClient(handler));
        var subject = new ClaimsPrincipal(new ClaimsIdentity([], "test"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetAsync(subject, CancellationToken.None));

        Assert.Equal("JWT has no subject.", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EscapesSubjectWhenBuildingUserRequestUrl()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
                return JsonResponse("{\"access_token\":\"admin-token\"}");

            if (request.RequestUri.AbsolutePath.Contains("/role-mappings/realm", StringComparison.Ordinal))
                return JsonResponse("[]");

            return JsonResponse("{\"enabled\":true}");
        });
        var sut = CreateSut(CreateClient(handler));
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user/1")],
            "test"));

        await sut.GetAsync(subject, CancellationToken.None);

        Assert.Contains("/users/user%2F1", handler.Requests[1].RequestUri!.AbsolutePath,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsMalformedUserStatePayload()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
                return JsonResponse("{\"access_token\":\"admin-token\"}");

            if (request.RequestUri.AbsolutePath.Contains("/role-mappings/realm", StringComparison.Ordinal))
                return JsonResponse("[]");

            return JsonResponse("{\"enabled\":\"yes\"}");
        });
        var sut = CreateSut(CreateClient(handler));
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-1")],
            "test"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetAsync(subject, CancellationToken.None));
    }

    private static KeycloakStateClient CreateSut(HttpClient client)
    {
        var clients = new StubHttpClientFactory(client);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://keycloak:8080/realms/cashflow",
                ["Keycloak:AdminClientId"] = "cashflow-api",
                ["Keycloak:AdminClientSecret"] = "secret"
            })
            .Build();

        return new KeycloakStateClient(clients, configuration);
    }

    private static HttpClient CreateClient(StubHttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://keycloak:8080")
        };
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
