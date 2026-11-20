using System.Text.Json;
using Fuse.Core.Areas.MongoDbIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Helpers;
using Fuse.Tests.TestInfrastructure;
using MongoDB.Bson;
using Moq;
using Xunit;
namespace Fuse.Tests.Services;

public class MongoDbIntegrationTests
{
    private static DataStore Store => new(Guid.NewGuid(), "Mongo", null, "MongoDB", Guid.NewGuid(), null, new Uri("mongodb://localhost/lab_app"), [], DateTime.UtcNow, DateTime.UtcNow);
    private static Account Account(Guid target, string name, string? source) => new(Guid.NewGuid(), target, TargetKind.DataStore,
        AuthKind.UserPassword, new(SecretBindingKind.None,null,null),name,source is null ? null : new() { ["authSource"] = source },[],[],DateTime.UtcNow,DateTime.UtcNow);

    [Fact] public async Task SavedCredentialsArePrivateAndConfigurationDoesNotConnect()
    {
        var ds = Store; var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { DataStores = [ds] });
        var inspector = new Mock<IMongoDbInspector>(MockBehavior.Strict);
        var audit = new FakeAuditService();
        var service = new MongoDbIntegrationService(store,inspector.Object,audit,new FakeCurrentUser());
        var result = await service.CreateAsync(new("Mongo",ds.Id,ds.ConnectionUri!,"observer","private-password","admin",true));
        Assert.True(result.IsSuccess); var item = result.Value!;
        Assert.True(item.UseTls); Assert.Equal("admin",item.AuthenticationDatabase); Assert.True(item.HasPassword);
        Assert.DoesNotContain("private-password",JsonSerializer.Serialize(await service.GetAllAsync()));
        Assert.True((await service.UpdateAsync(new(item.Id,item.Name,ds.Id,item.Endpoint,item.Username,null,"lab_app",false))).IsSuccess);
        Assert.Equal("private-password",Assert.Single(store.Current!.MongoDbIntegrations).Password);
        Assert.Equal("lab_app",Assert.Single(store.Current.MongoDbIntegrations).AuthenticationDatabase);
        Assert.True((await service.UpdateAsync(new(item.Id,item.Name,ds.Id,item.Endpoint,item.Username,"","lab_app"))).IsSuccess);
        Assert.False((await service.GetByIdAsync(item.Id))!.HasPassword);
        Assert.DoesNotContain("private-password",JsonSerializer.Serialize(audit.Logs));
        Assert.Equal(ErrorType.Conflict,(await service.CreateAsync(new("Other",ds.Id,item.Endpoint,"observer",null,"admin"))).ErrorType);
        Assert.True((await service.DeleteAsync(new(item.Id))).IsSuccess);
        inspector.VerifyNoOtherCalls();
    }
    [Fact] public async Task SavedProbeUsesAuthDatabaseAndDiscoveryScopesAccounts()
    {
        var ds = Store; var store = new InMemoryFuseStore(); var account = Account(ds.Id,"sam","lab_app");
        await store.UpdateAsync(s => s with { DataStores = [ds], Accounts = [account, account with { Id=Guid.NewGuid(),TargetId=Guid.NewGuid() }] });
        var inspector = new Mock<IMongoDbInspector>(MockBehavior.Strict);
        inspector.Setup(x=>x.ValidateAsync(It.Is<ValidateMongoDbConnection>(c=>c.AuthenticationDatabase=="admin" && c.Password=="secret" && c.UseTls),It.IsAny<CancellationToken>())).ReturnsAsync(new MongoDbConnectionTestResult(true,true,"authenticated",null));
        inspector.Setup(x=>x.DiscoverAsync(It.IsAny<MongoDbIntegration>(),It.Is<IReadOnlyList<Account>>(a=>a.Count==1 && a[0]==account),It.IsAny<CancellationToken>())).ReturnsAsync(new MongoDbDiscoveryResponse(Guid.Empty,DateTime.UtcNow,"lab_app","authenticated","complete",[],"complete",[],[],[],[],[]));
        var service = new MongoDbIntegrationService(store,inspector.Object,new FakeAuditService(),new FakeCurrentUser());
        var item = (await service.CreateAsync(new("Mongo",ds.Id,ds.ConnectionUri!,"observer","secret","admin",true))).Value!;
        Assert.True((await service.ValidateSavedAsync(item.Id)).Value!.IsSuccessful);
        Assert.True((await service.DiscoverAsync(item.Id)).IsSuccess);inspector.VerifyAll();
    }
    [Theory]
    [InlineData("mongodb://user:secret@localhost/lab_app", "admin")]
    [InlineData("mongodb://localhost/lab_app?tlsAllowInvalidCertificates=true", "admin")]
    [InlineData("mongodb://localhost/", "admin")]
    [InlineData("mongodb://localhost/a%2Fb", "admin")]
    [InlineData("mongodb://localhost/lab_app", "")]
    [InlineData("mongodb://localhost/lab_app", "$external")]
    public void InvalidConnectionsCannotInjectDriverSettings(string endpoint,string source) =>
        Assert.False(MongoDbConnectionSettings.Validate(new Uri(endpoint),"observer","secret",source).IsSuccess);

    [Fact] public void UserIdentityIncludesTheExactAuthenticationDatabase()
    {
        var target=Guid.NewGuid();
        var accounts=new[]{Account(target,"sam","app"),Account(target,"sam","reporting"),Account(target,"Sam","app"),Account(target,"sam",null)};
        var users=new[]{new MongoDbUserResponse("sam","app",[],[],[],"complete",[])};
        Assert.Equal(new[]{"matched","missing","missing","no-auth-database"},MongoDbInspector.Match(accounts,users,[new("app","complete"),new("reporting","complete")]).Select(x=>x.Status));
        Assert.Equal("unknown",MongoDbInspector.Match(accounts,users,[new("app","complete"),new("reporting","denied")])[1].Status);
    }

    private sealed class Session(Func<string,BsonDocument,BsonDocument> execute) : IMongoDbSession, IMongoDbSessionFactory
    {
        public bool Disposed {get;private set;}
        public List<BsonDocument> Commands {get;} = [];
        public IMongoDbSession Open(ValidateMongoDbConnection command) => this;
        public Task<BsonDocument> RunAsync(string database,BsonDocument command,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Commands.Add(command); return Task.FromResult(execute(database,command)); }
        public void Dispose() => Disposed=true;
    }
    private static BsonDocument Auth => BsonDocument.Parse("{authInfo:{authenticatedUsers:[{user:'observer',db:'admin'}]}}");
    private static BsonDocument User(string source, string name="sam") => new()
    {
        {"user",name},{"db",source},{"roles",new BsonArray {new BsonDocument{{"role","reader"},{"db",source}}}},
        {"inheritedRoles",new BsonArray {new BsonDocument{{"role","base_reader"},{"db",source}}}},
        {"inheritedPrivileges",MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>("[{resource:{db:'lab_app',collection:'orders'},actions:['find']},{resource:{db:'reporting',collection:'private'},actions:['insert']}]" )},
        {"credentials",new BsonDocument("secret","never-project")},{"customData",new BsonDocument("private","never-project")}
    };
    [Fact] public async Task FixedMetadataQueriesProjectOnlySafeFieldsAndSelectedDatabasePrivileges()
    {
        var item=new MongoDbIntegration(Guid.NewGuid(),"Mongo",Store.Id,new Uri("mongodb://localhost/lab_app"),"observer","private",DateTime.UtcNow,DateTime.UtcNow,"admin",false);
        using var session=new Session((db,c)=>c.GetElement(0).Name switch {
            "connectionStatus"=>Auth,
            "listCollections"=>BsonDocument.Parse("{cursor:{id:0,firstBatch:[{name:'orders'}]}}"),
            "rolesInfo"=>new("roles",new BsonArray()),
            "usersInfo"=>new("users",new BsonArray {User(db)}),
            _=>throw new Exception("Unexpected command")
        });
        var d=await new MongoDbInspector(session).DiscoverAsync(item,[Account(item.DataStoreId,"sam","lab_app"),Account(item.DataStoreId,"sam","reporting")]);
        Assert.Equal(2,d.Users.Count);Assert.All(d.Accounts,a=>Assert.Equal("matched",a.Status));
        Assert.All(d.Users,u=>{Assert.Single(u.MatchedAccountIds);Assert.Equal("base_reader",Assert.Single(u.InheritedRoles).Name);Assert.Equal("orders",Assert.Single(u.Privileges).Collection);});
        Assert.DoesNotContain("never-project",JsonSerializer.Serialize(d));Assert.True(session.Disposed);
        Assert.All(session.Commands.Where(c=>c.Contains("usersInfo")),c=>{Assert.False(c["showCredentials"].AsBoolean);Assert.False(c["showCustomData"].AsBoolean);});
        Assert.All(session.Commands,c=>Assert.Equal(5000,c["maxTimeMS"].AsInt32));
    }
    [Fact] public async Task TruncationAndFailedAuthenticationDoNotClaimMissingUsers()
    {
        var item=new MongoDbIntegration(Guid.NewGuid(),"Mongo",Store.Id,new Uri("mongodb://localhost/lab_app"),"observer","private",DateTime.UtcNow,DateTime.UtcNow,"admin",false);
        using var session=new Session((db,c)=>c.GetElement(0).Name switch {
            "connectionStatus"=>Auth,
            "listCollections"=>BsonDocument.Parse("{cursor:{id:0,firstBatch:[]}}"),
            "rolesInfo"=>new("roles",new BsonArray()),
            "usersInfo"=>new("users",new BsonArray(Enumerable.Range(0,201).Select(i=>User(db,"user"+i)))),
            _=>throw new Exception("Unexpected command")
        });
        var accounts=new[]{Account(item.DataStoreId,"unobserved","lab_app")};
        var d=await new MongoDbInspector(session).DiscoverAsync(item,accounts);
        Assert.Equal("truncated",Assert.Single(d.UserScopes).Status);Assert.Equal("unknown",Assert.Single(d.Accounts).Status);Assert.Equal(200,d.Users.Count);
        using var anonymous=new Session((_,_)=>BsonDocument.Parse("{authInfo:{authenticatedUsers:[]}}"));
        d=await new MongoDbInspector(anonymous).DiscoverAsync(item,accounts);
        Assert.Equal("authentication-failed",d.ConnectionStatus);Assert.Equal("unknown",Assert.Single(d.Accounts).Status);Assert.Single(anonymous.Commands);
    }
    [Fact] public async Task CancellationDisposesTheSession()
    {
        using var session=new Session((_,_)=>Auth);using var ct=new CancellationTokenSource();ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new MongoDbInspector(session).ValidateAsync(new(new Uri("mongodb://localhost/lab_app"),"observer","secret","admin"),ct.Token));
        Assert.True(session.Disposed);
    }

    [Theory]
    [InlineData("{a:1,b:-1}", "Matched")]
    [InlineData("{b:-1,a:1}", "Drift")]
    public async Task IndexReviewPreservesKeyOrderAndProjectsOnlyExpectedMetadata(string keys, string expected)
    {
        var ds = Store;
        var node = new MongoDbIntegration(Guid.NewGuid(), "Mongo", ds.Id, ds.ConnectionUri!, "observer", "private",
            DateTime.UtcNow, DateTime.UtcNow, "admin", false);
        var snapshot = new InMemoryFuseStore().Current! with { MongoDbIntegrations = [node], DataStores = [ds] };
        using var session = new Session((db, command) =>
        {
            Assert.Equal("lab_app", db);
            Assert.Equal("orders", command["listIndexes"].AsString);
            return BsonDocument.Parse("{cursor:{id:0,firstBatch:[{name:'lookup',key:{a:1,b:-1},unique:true,expireAfterSeconds:12,private:'secret'}]}}");
        });
        var provider = new MongoDbReviewProvider(session);
        var input = new Fuse.Core.Areas.IntegrationReviews.ReviewInput("mongodb", node.Id, "collection-index", null, new()
        {
            ["collection"]="orders", ["index"]="lookup", ["keys"]=BsonDocument.Parse(keys).ToJson(), ["unique"]="yes", ["ttl"]="12"
        });
        var result = await provider.InspectAsync(snapshot, input, default);
        Assert.Equal(expected, result.Status.ToString());
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(result));
        Assert.Single(session.Commands);
        input.Fields["collection"]="system.users";
        Assert.NotNull(provider.Validate(snapshot, input));
    }
}
