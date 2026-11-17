using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fuse.Core.Models;
using Fuse.Core.Responses;
namespace Fuse.Core.Areas.RabbitMqIntegration;
public interface IRabbitMqInspector {
 Task<RabbitMqConnectionTestResult> ValidateAsync(Uri endpoint,string username,string? password,CancellationToken ct=default);
 Task<RabbitMqDiscoveryResponse> DiscoverAsync(Models.RabbitMqIntegration integration,IReadOnlyList<Models.Account> accounts,IReadOnlyList<BrokerQueue> queues,CancellationToken ct=default);
}
/// <summary>Fixed GET metadata endpoints only. Never retrieves messages, definitions or credential hashes.</summary>
public sealed class RabbitMqInspector(IHttpClientFactory factory) : IRabbitMqInspector {
 private async Task<JsonElement> Get(Uri origin,string user,string? password,string path,CancellationToken ct) {
  using var client=factory.CreateClient("rabbitmq-inspector");
  using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(origin,"api/"+path));
  request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(user+":"+password)));
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
  if(!response.IsSuccessStatusCode) throw new HttpRequestException("RabbitMQ metadata request failed.",null,response.StatusCode);
  await using var stream=await response.Content.ReadAsStreamAsync(ct);
  using var buffer=new MemoryStream(); var chunk=new byte[8192]; int count;
  while((count=await stream.ReadAsync(chunk,ct))>0) { if(buffer.Length+count>2_000_000) throw new InvalidDataException(); buffer.Write(chunk,0,count); }
  using var document=JsonDocument.Parse(buffer.ToArray());return document.RootElement.Clone();
 }
 private static string Status(Exception error)=>error switch { HttpRequestException { StatusCode:HttpStatusCode.Unauthorized }=>"authentication-failed",HttpRequestException { StatusCode:HttpStatusCode.Forbidden }=>"denied",HttpRequestException { StatusCode:HttpStatusCode.NotFound }=>"unavailable",OperationCanceledException=>"timeout",InvalidDataException=>"partial",_=>"unavailable" };
 private static string SectionStatus(Exception e) => Status(e)=="authentication-failed"?"denied":Status(e);
 private static string Str(JsonElement item,string field)=>item.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
 public async Task<RabbitMqConnectionTestResult> ValidateAsync(Uri endpoint,string username,string? password,CancellationToken ct=default) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(10));
  try {var result=await Get(endpoint,username,password,"whoami",deadline.Token);if(Str(result,"name")!=username)throw new InvalidDataException();return new(true,true,"connected",null);}
  catch(Exception e) when(!ct.IsCancellationRequested) {return new(false,false,Status(e),"Management API authentication or access failed. Check the endpoint, credentials and management permissions.");}
 }
 public async Task<RabbitMqDiscoveryResponse> DiscoverAsync(Models.RabbitMqIntegration item,IReadOnlyList<Models.Account> accounts,IReadOnlyList<BrokerQueue> queues,CancellationToken ct=default) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(25));
  var resources=new List<RabbitMqResource>();var users=new List<string>();var permissions=new List<RabbitMqPermission>();var warnings=new List<string>();
  var connection=await ValidateAsync(item.Endpoint,item.Username,item.Password,deadline.Token);
  var topology="unavailable";var usersStatus="unavailable";var permissionStatus="unavailable";
  var scope=Uri.EscapeDataString(item.VirtualHost);
  async Task<JsonElement> Fetch(string path)=>await Get(item.Endpoint,item.Username,item.Password,path,deadline.Token);
  if(connection.IsSuccessful) {
   try {
    // A specific vhost probe prevents a hidden or nonexistent vhost appearing as an empty inventory.
    var hosts=await Fetch("vhosts?columns=name");if(hosts.ValueKind!=JsonValueKind.Array || hosts.GetArrayLength()>1000)throw new InvalidDataException();
    if(!hosts.EnumerateArray().Any(h=>Str(h,"name")==item.VirtualHost))throw new HttpRequestException("Virtual host is not visible.",null,HttpStatusCode.Forbidden);
    foreach(var kind in new[]{"queues","exchanges","bindings"}) {
     var rows=await Fetch(kind+"/"+scope+"?disable_stats=true&columns=name,vhost,type,source,destination,destination_type,routing_key");
     if(rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>1000)throw new InvalidDataException();
     foreach(var row in rows.EnumerateArray()) {
      if(Str(row,"vhost")!=item.VirtualHost)throw new InvalidDataException();
      resources.Add(new(kind=="bindings"?Str(row,"source"):Str(row,"name"),kind,item.VirtualHost,kind=="bindings"?Str(row,"destination_type"):Str(row,"type"),kind=="bindings"?Str(row,"destination"):null,kind=="bindings"?Str(row,"routing_key"):null));
     }
    }
    topology="complete";
   }catch(Exception e) when(!ct.IsCancellationRequested) {topology=SectionStatus(e);warnings.Add("Topology discovery is incomplete; unobserved queues are unknown.");}
   try {var rows=await Fetch("users?columns=name");if(rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>1000)throw new InvalidDataException();foreach(var row in rows.EnumerateArray()){var name=Str(row,"name");if(name=="")throw new InvalidDataException();users.Add(name);}usersStatus="complete";}
   catch(Exception e) when(!ct.IsCancellationRequested) {usersStatus=SectionStatus(e);warnings.Add("User discovery is unavailable or incomplete. Viewing all users requires RabbitMQ administrator privileges.");}
   try {var rows=await Fetch("vhosts/"+scope+"/permissions?columns=user,vhost,configure,write,read");if(rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>1000)throw new InvalidDataException();foreach(var row in rows.EnumerateArray()){if(Str(row,"vhost")!=item.VirtualHost)throw new InvalidDataException();permissions.Add(new(Str(row,"user"),item.VirtualHost,Str(row,"configure"),Str(row,"write"),Str(row,"read")));}permissionStatus="complete";}
   catch(Exception e) when(!ct.IsCancellationRequested) {permissionStatus=SectionStatus(e);warnings.Add("Virtual host permission rules are unavailable or incomplete.");}
  } else warnings.Add(connection.ErrorMessage!);
  return new(item.Id,DateTime.UtcNow,item.VirtualHost,connection.Status,topology,usersStatus,permissionStatus,resources,
   queues.Select(q=>new RabbitMqQueueMatch(q.Name,resources.Any(r=>r.Kind=="queues"&&r.Name==q.Name)?"matched":topology=="complete"?"missing":"unknown")).ToArray(),users,permissions,
   accounts.Where(a=>a.TargetKind==TargetKind.MessageBroker&&a.TargetId==item.MessageBrokerId).Select(a=>new RabbitMqAccountMatch(a.Id,a.UserName,users.Contains(a.UserName??"",StringComparer.Ordinal)?"matched":usersStatus=="complete"&&!string.IsNullOrEmpty(a.UserName)?"missing":"unknown")).ToArray(),warnings);
 }
}
