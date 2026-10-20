using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppInstaller.Models;
namespace AppInstaller.Providers;
public sealed class GitHubRateLimitException(DateTimeOffset retryAt):HttpRequestException($"GitHub's release API is temporarily rate limited. Try Refresh after {retryAt.ToLocalTime():HH:mm:ss zzz}. Installed applications are still detected.",null,HttpStatusCode.TooManyRequests){public DateTimeOffset RetryAt=>retryAt;}
public sealed class GitHubReleaseClient {
 public static GitHubReleaseClient Shared {get;}=new();
 private DateTimeOffset retryAt;
 public async Task<string> GetStringAsync(HttpClient http,string url,CancellationToken token){
  token.ThrowIfCancellationRequested();
  if(DateTimeOffset.UtcNow<retryAt)throw new GitHubRateLimitException(retryAt);
  using var response=await http.GetAsync(url,token);
  var body=await response.Content.ReadAsStringAsync(token);
  bool exhausted=response.Headers.TryGetValues("X-RateLimit-Remaining",out var remaining)&&remaining.FirstOrDefault()=="0";
  if((response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)&&(exhausted||response.StatusCode==HttpStatusCode.TooManyRequests||body.Contains("rate limit",StringComparison.OrdinalIgnoreCase))){
   var until=DateTimeOffset.UtcNow.AddMinutes(1);
   if(response.Headers.TryGetValues("X-RateLimit-Reset",out var resets)&&long.TryParse(resets.FirstOrDefault(),out var seconds)&&seconds>0&&seconds<253402300799)until=DateTimeOffset.FromUnixTimeSeconds(seconds);
   if(response.Headers.RetryAfter?.Delta is {} delay)until=DateTimeOffset.UtcNow+delay;
   else if(response.Headers.RetryAfter?.Date is {} date)until=date;
   retryAt=until>DateTimeOffset.UtcNow?until:DateTimeOffset.UtcNow.AddMinutes(1);
   throw new GitHubRateLimitException(retryAt);
  }
  response.EnsureSuccessStatusCode();return body;
 }
}
public sealed class ReleaseMetadataCache(string? directory=null,TimeProvider? clock=null){
 public static ReleaseMetadataCache Shared {get;}=new();
 private readonly string root=directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XAppInstaller","ReleaseCache");
 private readonly TimeProvider time=clock??TimeProvider.System;
 private sealed record Entry(DateTimeOffset CheckedAt,ReleaseInfo Release);
 public async Task<ReleaseInfo> ResolveAsync(ApplicationDefinition app,bool preview,Func<Task<ReleaseInfo>> fetch,CancellationToken token){
  token.ThrowIfCancellationRequested();
  if(app.Source is not ("github" or "hermes" or "binary-metadata" or "mpv-windows"))return await fetch();
  var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(app)+preview)));
  var path=Path.Combine(root,key+".json");Entry? cached=null;
  try{cached=JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(path,token));}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}
  var age=cached is null?TimeSpan.MaxValue:time.GetUtcNow()-cached.CheckedAt;
  if(cached is not null&&age>=TimeSpan.Zero&&age<TimeSpan.FromMinutes(15))return cached.Release with{CheckedAt=cached.CheckedAt,SourceNotice=$"Release checked {cached.CheckedAt.ToLocalTime():g}; using the 15-minute metadata cache."};
  try{
   var release=(await fetch()) with{CheckedAt=time.GetUtcNow(),IsStale=false,SourceNotice=null};
   try{Directory.CreateDirectory(root);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(new Entry(time.GetUtcNow(),release)),token);File.Move(temporary,path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
   return release;
  }catch(HttpRequestException e)when(cached is not null&&age>=TimeSpan.Zero&&age<TimeSpan.FromDays(7)){
   return cached.Release with{IsStale=true,CheckedAt=cached.CheckedAt,SourceNotice=$"Showing cached release metadata from {cached.CheckedAt.ToLocalTime():g}. Latest version could not be confirmed. {e.Message}"};
  }
 }
}