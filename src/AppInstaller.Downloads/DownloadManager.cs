
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Downloads;
public static class TrustedUris {
 public static void Validate(Uri uri,ApplicationDefinition app) {
  if(uri.Scheme!=Uri.UriSchemeHttps||!string.IsNullOrEmpty(uri.UserInfo)||!app.DownloadHosts.Contains(uri.IdnHost,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException($"Download host is not trusted: {uri.Host}");
 }
 public static HttpClient CreateClient() {var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(20)};http.DefaultRequestHeaders.UserAgent.ParseAdd("XAppInstaller/0.1");return http;}
}
public sealed class DownloadManager(HttpClient http):IDownloadManager {
 public async Task<string> DownloadAsync(ApplicationDefinition app,ReleaseInfo release,string directory,IProgress<DownloadProgress> progress,CancellationToken token) {
  if(Path.GetFileName(release.FileName)!=release.FileName||release.FileName.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Unsafe filename");
  Directory.CreateDirectory(directory);var destination=Path.Combine(directory,app.Id+"-"+release.FileName);var partial=destination+".part";
  for(int attempt=0;;attempt++) {
   try {
    var uri=release.DownloadUri;long offset=File.Exists(partial)?new FileInfo(partial).Length:0;HttpResponseMessage? response=null;
    for(int redirect=0;redirect<=6;redirect++) {
     TrustedUris.Validate(uri,app);using var request=new HttpRequestMessage(HttpMethod.Get,uri);if(offset>0)request.Headers.Range=new RangeHeaderValue(offset,null);
     response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
     if((int)response.StatusCode is >=300 and <400){var location=response.Headers.Location??throw new InvalidDataException("Missing redirect URL");uri=location.IsAbsoluteUri?location:new Uri(uri,location);response.Dispose();response=null;continue;}break;
    }
    using var reply=response??throw new HttpRequestException("Too many redirects");
    if(reply.StatusCode==HttpStatusCode.RequestedRangeNotSatisfiable){File.Delete(partial);throw new IOException("Resume rejected; restarting download");}
    reply.EnsureSuccessStatusCode();if(reply.StatusCode!=HttpStatusCode.PartialContent)offset=0;else if(reply.Content.Headers.ContentRange?.From!=offset)throw new IOException("Invalid Content-Range");
    var total=reply.Content.Headers.ContentLength+offset;using var input=await reply.Content.ReadAsStreamAsync(token);
    await using(var output=new FileStream(partial,offset>0?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.None,81920,true)) {
     byte[] buffer=new byte[81920];long received=offset;var watch=Stopwatch.StartNew();var last=TimeSpan.Zero;
     async Task<int> ReadAsync(){using var idle=CancellationTokenSource.CreateLinkedTokenSource(token);idle.CancelAfter(TimeSpan.FromSeconds(45));try{return await input.ReadAsync(buffer,idle.Token);}catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new IOException("The download stopped responding for 45 seconds. Retry the download.");}}
     int n;while((n=await ReadAsync())>0){await output.WriteAsync(buffer.AsMemory(0,n),token);received+=n;if(watch.Elapsed-last>TimeSpan.FromMilliseconds(100)){progress.Report(new(received,total,(received-offset)/Math.Max(.001,watch.Elapsed.TotalSeconds)));last=watch.Elapsed;}}
     if(total is not null&&received!=total)throw new IOException("Incomplete response");progress.Report(new(received,total,(received-offset)/Math.Max(.001,watch.Elapsed.TotalSeconds)));
    }
    File.Move(partial,destination,true);return destination;
   }catch(Exception e)when((e is HttpRequestException or IOException)&&attempt<2){await Task.Delay(TimeSpan.FromSeconds(attempt+1),token);}
  }
 }
}
