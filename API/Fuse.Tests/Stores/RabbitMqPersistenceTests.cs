using Fuse.Core.Models;
using Fuse.Core.Configs;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Xunit;
namespace Fuse.Tests.Stores;
public class RabbitMqPersistenceTests {
 [Fact]public async Task SavedConnectionSurvivesReloadAndInventoryImport(){
 var directory=Path.Combine(Path.GetTempPath(),"rabbit-store-"+Guid.NewGuid());
 try {
 using var store=new JsonFuseStore(new JsonFuseStoreOptions{DataDirectory=directory});
 var baseline=await store.GetAsync();Assert.Empty(baseline.RabbitMqIntegrations);
 var environment=new EnvironmentInfo(Guid.NewGuid(),"Lab",null,[]);var broker=new MessageBroker(Guid.NewGuid(),"Rabbit",null,"RabbitMQ",environment.Id,null,[],DateTime.UtcNow,DateTime.UtcNow);
 var integration=new RabbitMqIntegration(Guid.NewGuid(),"Inspect",broker.Id,"a/b",new Uri("http://rabbit"),"observer","private",DateTime.UtcNow,DateTime.UtcNow);
 await store.SaveAsync(baseline with{Environments=[environment],MessageBrokers=[broker],RabbitMqIntegrations=[integration]});
 Assert.Equal(integration,Assert.Single((await store.LoadAsync()).RabbitMqIntegrations));
 var config=new ConfigService(store,new FakeAuditService(),Moq.Mock.Of<Fuse.Core.Areas.Logging.ILogService>(),new FakeCurrentUser());
 var exported=await config.ExportAsync(ConfigFormat.Json);Assert.DoesNotContain("private",exported);
 await config.ImportAsync(exported,ConfigFormat.Json);Assert.Equal(integration,Assert.Single((await store.LoadAsync()).RabbitMqIntegrations));
 }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}
