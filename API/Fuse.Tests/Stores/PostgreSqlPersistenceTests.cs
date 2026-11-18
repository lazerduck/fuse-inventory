using Fuse.Core.Models;
using Fuse.Core.Configs;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Xunit;
namespace Fuse.Tests.Stores;
public class PostgreSqlPersistenceTests {
 [Fact]public async Task SavedConnectionSurvivesReloadAndInventoryImport(){
 var directory=Path.Combine(Path.GetTempPath(),"postgresql-store-"+Guid.NewGuid());
 try {
 using var store=new JsonFuseStore(new JsonFuseStoreOptions{DataDirectory=directory});
 var baseline=await store.GetAsync();Assert.Empty(baseline.PostgreSqlIntegrations);
 var environment=new EnvironmentInfo(Guid.NewGuid(),"Lab",null,[]);var broker=new DataStore(Guid.NewGuid(),"PostgreSQL",null,"PostgreSQL",environment.Id,null,null,[],DateTime.UtcNow,DateTime.UtcNow);
 var integration=new PostgreSqlIntegration(Guid.NewGuid(),"Inspect",broker.Id,new Uri("postgresql://localhost/lab"),"observer","private",DateTime.UtcNow,DateTime.UtcNow);
 await store.SaveAsync(baseline with{Environments=[environment],DataStores=[broker],PostgreSqlIntegrations=[integration]});
 Assert.Equal(integration,Assert.Single((await store.LoadAsync()).PostgreSqlIntegrations));
 var config=new ConfigService(store,new FakeAuditService(),Moq.Mock.Of<Fuse.Core.Areas.Logging.ILogService>(),new FakeCurrentUser());
 var exported=await config.ExportAsync(ConfigFormat.Json);Assert.DoesNotContain("private",exported);
 await config.ImportAsync(exported,ConfigFormat.Json);Assert.Equal(integration,Assert.Single((await store.LoadAsync()).PostgreSqlIntegrations));
 }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}
