using Fuse.Core.Models;
using Fuse.Core.Configs;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Xunit;
namespace Fuse.Tests.Stores;
public class MongoDbPersistenceTests {
 [Fact]public async Task SavedConnectionSurvivesReloadAndInventoryImport(){
 var directory=Path.Combine(Path.GetTempPath(),"mongodb-store-"+Guid.NewGuid());
 try {
 using var store=new JsonFuseStore(new JsonFuseStoreOptions{DataDirectory=directory});
 var baseline=await store.GetAsync();Assert.Empty(baseline.MongoDbIntegrations);
 var environment=new EnvironmentInfo(Guid.NewGuid(),"Lab",null,[]);var broker=new DataStore(Guid.NewGuid(),"MongoDB",null,"MongoDB",environment.Id,null,null,[],DateTime.UtcNow,DateTime.UtcNow);
 var integration=new MongoDbIntegration(Guid.NewGuid(),"Inspect",broker.Id,new Uri("mongodb://localhost/lab"),"observer","private",DateTime.UtcNow,DateTime.UtcNow,"admin",true);
 await store.SaveAsync(baseline with{Environments=[environment],DataStores=[broker],MongoDbIntegrations=[integration]});
 Assert.Equal(integration,Assert.Single((await store.LoadAsync()).MongoDbIntegrations));
 var config=new ConfigService(store,new FakeAuditService(),Moq.Mock.Of<Fuse.Core.Areas.Logging.ILogService>(),new FakeCurrentUser());
 var exported=await config.ExportAsync(ConfigFormat.Json);Assert.DoesNotContain("private",exported);
 await config.ImportAsync(exported,ConfigFormat.Json);Assert.Equal(integration,Assert.Single((await store.LoadAsync()).MongoDbIntegrations));
 }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}
