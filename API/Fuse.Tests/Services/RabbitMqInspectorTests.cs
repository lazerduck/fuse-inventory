using System.Net;
using System.Text;
using Fuse.Core.Areas.RabbitMqIntegration;
using Fuse.Core.Models;
using Xunit;
namespace Fuse.Tests.Services;

public class RabbitMqInspectorTests
{
    private sealed class Transport(Func<string, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Paths { get; } = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Assert.Equal(HttpMethod.Get, r.Method); Paths.Add(r.RequestUri!.PathAndQuery); return Task.FromResult(respond(r.RequestUri.PathAndQuery)); }
    }
    static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScopeAndDeniedCapabilitiesDoNotProduceFalseMissing(bool denied)
    {
        using var handler = new Transport(path => path.Contains("whoami") ? Json("{\"name\":\"observer\"}") : path.Contains("/vhosts?") ? Json("[{\"name\":\"lab\"}]") : path.Contains("/users") || path.Contains("/permissions") ? new(HttpStatusCode.Forbidden) : path.Contains("/queues/") ? (denied ? new(HttpStatusCode.Forbidden) : Json("[{\"name\":\"orders\",\"vhost\":\"lab\"}]")) : Json("[]"));
        var inspector = new RabbitMqInspector(handler); var id = Guid.NewGuid(); var model = new RabbitMqIntegration(Guid.NewGuid(), "Lab", id, "lab", new Uri("http://broker:15672"), "observer", "private", DateTime.UtcNow, DateTime.UtcNow);
        var result = await inspector.DiscoverAsync(model, [], [new(Guid.NewGuid(), "orders", null), new(Guid.NewGuid(), "missing", null)]);
        Assert.Equal(denied ? "unknown" : "matched", result.Queues[0].Status); Assert.Equal(denied ? "unknown" : "missing", result.Queues[1].Status); Assert.Equal("denied", result.UsersStatus); Assert.Equal("denied", result.PermissionsStatus);
        Assert.All(handler.Paths, p => Assert.DoesNotContain("definitions", p));
    }
    [Fact]
    public async Task WrongVhostResponseIsNotAccepted()
    {
        using var handler = new Transport(path => path.Contains("whoami") ? Json("{\"name\":\"observer\"}") : path.Contains("/vhosts?") ? Json("[{\"name\":\"lab\"}]") : path.Contains("queues") ? Json("[{\"name\":\"orders\",\"vhost\":\"other\"}]") : Json("[]"));
        var result = await new RabbitMqInspector(handler).DiscoverAsync(new(Guid.NewGuid(), "Lab", Guid.NewGuid(), "lab", new Uri("http://broker"), "observer", "private", DateTime.UtcNow, DateTime.UtcNow), [], [new(Guid.NewGuid(), "orders", null)]);
        Assert.NotEqual("complete", result.TopologyStatus); Assert.Empty(result.Resources); Assert.Equal("unknown", result.Queues[0].Status);
    }
    [Fact] public async Task AuthenticationErrorsAreSanitized() { using var handler = new Transport(_ => new(HttpStatusCode.Unauthorized) { Content = new StringContent("private-password") }); var result = await new RabbitMqInspector(handler).ValidateAsync(new Uri("http://broker"), "observer", "private-password"); Assert.Equal("authentication-failed", result.Status); Assert.DoesNotContain("private-password", result.ErrorMessage!); }
}
