using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fuse.Core.Interfaces;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fuse.Tests.Mcp;

[Collection("ApiAuthCollection")]
[Trait("Category", "Integration")]
public sealed class McpEndpointTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Endpoint_IsHiddenWhenDisabled_AndRequiresAuthenticationWhenEnabled()
    {
        var store = fixture.Services.GetRequiredService<IFuseStore>();
        using var client = fixture.CreateUnauthenticatedHttpClient();

        await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { McpServerEnabled = false } });
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/mcp", null)).StatusCode);

        try
        {
            await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { McpServerEnabled = true } });
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/mcp", null)).StatusCode);
        }
        finally
        {
            await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { McpServerEnabled = false } });
        }
    }

    [Fact]
    public void AppSettings_DefaultsMcpServerToDisabled()
    {
        Assert.False(new Fuse.Core.Models.AppSettings().McpServerEnabled);
    }

    [Fact]
    public async Task AuthenticatedClient_CanInitializeAndDiscoverTools()
    {
        using var client = fixture.CreateUnauthenticatedHttpClient();
        var state = await client.GetFromJsonAsync<JsonElement>("/api/security/state");
        if (state.GetProperty("requiresSetup").GetBoolean())
        {
            var setup = await client.PostAsJsonAsync("/api/security/accounts", new
            {
                userName = "initialAdmin", password = "InitialPassword123!", roleIds = Array.Empty<Guid>(), isAdmin = true
            });
            setup.EnsureSuccessStatusCode();
        }

        var login = await client.PostAsJsonAsync("/api/security/login", new
        {
            userName = "initialAdmin", password = "InitialPassword123!"
        });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var store = fixture.Services.GetRequiredService<IFuseStore>();
        try
        {
            await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { McpServerEnabled = true } });
            var initialize = await SendMcpAsync(client, 1, "initialize", new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "fuse-tests", version = "1.0" }
            });
            Assert.Contains("serverInfo", initialize, StringComparison.Ordinal);

            var tools = await SendMcpAsync(client, 2, "tools/list", new { });
            Assert.Contains("inventory_review_completeness", tools, StringComparison.Ordinal);
            Assert.Contains("inventory_update_application_documentation", tools, StringComparison.Ordinal);
            Assert.Contains("clearFields", tools, StringComparison.Ordinal);
            Assert.DoesNotContain("\"name\":\"inventory_secret", tools, StringComparison.OrdinalIgnoreCase);

            var create = await client.PostAsJsonAsync("/api/application", new
            {
                name = $"MCP write test {Guid.NewGuid():N}",
                version = (string?)null,
                description = (string?)null,
                owner = (string?)null,
                notes = (string?)null,
                framework = (string?)null,
                repositoryUri = (string?)null,
                icon = (string?)null,
                tagIds = Array.Empty<Guid>()
            });
            create.EnsureSuccessStatusCode();
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            var applicationId = created.GetProperty("id").GetGuid();
            var updatedAt = created.GetProperty("updatedAt").GetDateTime();

            var update = await SendMcpAsync(client, 3, "tools/call", new
            {
                name = "inventory_update_application_documentation",
                arguments = new
                {
                    applicationId,
                    expectedUpdatedAt = updatedAt,
                    changes = new { description = "Updated through MCP" }
                }
            });
            Assert.DoesNotContain("\"isError\":true", update, StringComparison.Ordinal);

            var read = await client.GetFromJsonAsync<JsonElement>($"/api/application/{applicationId}");
            Assert.Equal("Updated through MCP", read.GetProperty("description").GetString());

            var stale = await SendMcpAsync(client, 4, "tools/call", new
            {
                name = "inventory_update_application_documentation",
                arguments = new
                {
                    applicationId,
                    expectedUpdatedAt = updatedAt,
                    changes = new { owner = "Should not be applied" }
                }
            });
            Assert.Contains("record changed after it was read", stale, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { McpServerEnabled = false } });
        }
    }

    private static async Task<string> SendMcpAsync(HttpClient client, int id, string method, object parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id, method, @params = parameters })
        };
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"MCP returned {(int)response.StatusCode}: {body}");
        return body;
    }
}
