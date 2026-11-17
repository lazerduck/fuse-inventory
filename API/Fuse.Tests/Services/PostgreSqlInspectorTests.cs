using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Npgsql;
using Xunit;
namespace Fuse.Tests.Services;
public class PostgreSqlInspectorTests
{
    private static Account Account(string username) => new(Guid.NewGuid(), Guid.NewGuid(), TargetKind.DataStore,
        AuthKind.UserPassword, new(SecretBindingKind.None,null,null),username,null,[],[],DateTime.UtcNow,DateTime.UtcNow);
    [Fact] public void MatchingIsExactAndIncompleteDiscoveryNeverClaimsMissing()
    {
        var accounts = new[] { Account("reader"),Account("Reader"),Account("group"),Account("blocked"),Account("missing") };
        PostgreSqlRoleResponse Role(string name,bool login=true,bool connect=true) => new(name,login,true,false,false,false,false,connect,[]);
        var roles = new[] { Role("reader"),Role("group",false),Role("blocked",connect:false) };
        Assert.Equal(new[]{"matched","missing","no-login","connect-denied","missing"},PostgreSqlInspector.Match(accounts,roles,"complete").Select(x=>x.Status));
        Assert.Equal(new[]{"matched","unknown","no-login","connect-denied","unknown"},PostgreSqlInspector.Match(accounts,roles,"truncated").Select(x=>x.Status));
        Assert.All(PostgreSqlInspector.Match(accounts,[],"denied"),x=>Assert.Equal("unknown",x.Status));
    }
    [Fact] public void ConnectionBuilderCannotInjectSettingsAndTlsVerifiesServer()
    {
        var endpoint=new Uri("postgresqls://example.com:5433/lab");
        var settings=new NpgsqlConnectionStringBuilder(PostgreSqlConnectionSettings.Build(endpoint,"a;Host=evil","x;Pooling=true"));
        Assert.Equal("example.com",settings.Host);Assert.Equal("a;Host=evil",settings.Username);
        Assert.Equal("x;Pooling=true",settings.Password);Assert.False(settings.Pooling);
        Assert.Equal(SslMode.VerifyFull,settings.SslMode);Assert.Contains("default_transaction_read_only=on",settings.Options);
    }
    [Fact] public async Task CancellationPropagates()
    {
        using var ct=new CancellationTokenSource();ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PostgreSqlInspector().ValidateAsync(new Uri("postgresql://localhost/lab"),"reader","secret",ct.Token));
    }
}
