using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Providers;
internal static class AdditionalSources {
 public static async Task<ReleaseInfo?> ResolveAsync(ApplicationDefinition app,HttpClient http,CancellationToken token){
  var media=await MediaSources.ResolveAsync(app,http,token);if(media is not null)return media;
  async Task<string> Text(string url)=>await http.GetStringAsync(url,token);
  string Match(string input,string pattern){var match=Regex.Match(input,pattern,RegexOptions.IgnoreCase|RegexOptions.Singleline);return match.Success?WebUtility.HtmlDecode(match.Groups[1].Value):throw new InvalidDataException("Official source format changed: "+app.Name);}
  ReleaseInfo Release(string version,string url,string? hash=null)=>new(version,new(url),Path.GetFileName(new Uri(url).LocalPath),null,hash);
  if(app.Source=="winscp"){
   return await WinScpSource.ResolveAsync(http,token);
  }
  if(app.Source=="putty"){
   var sums=await Text("https://the.earth.li/~sgtatham/putty/latest/sha256sums");
   var row=Regex.Match(sums,@"(?m)^([a-f0-9]{64})\s+\*?w64/(putty-64bit-([0-9.]+)-installer\.msi)\s*$",RegexOptions.IgnoreCase);
   if(!row.Success)throw new InvalidDataException("Official PuTTY checksum feed does not contain an x64 MSI");
   return Release(row.Groups[3].Value,"https://the.earth.li/~sgtatham/putty/latest/w64/"+row.Groups[2].Value,row.Groups[1].Value);
  }
  if(app.Source=="portscanner"){
   var page=await Text("https://www.advanced-port-scanner.com/");
   var url=Match(page,"href=\"(https://download.advanced-port-scanner.com/download/files/Advanced_Port_Scanner_[0-9.]+.exe)\"");
   return Release(Match(url,@"Advanced_Port_Scanner_([0-9.]+)\.exe"),url);
  }
  if(app.Source=="wireshark"){
   var xml=await Text("https://www.wireshark.org/update/0/Wireshark/0.0.0/Windows/x86-64/en-US/stable.xml");
   var document=XDocument.Parse(xml);XNamespace sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle";
   var enclosure=document.Descendants("enclosure").First(e=>e.Attribute("url")?.Value.EndsWith("-x64.exe",StringComparison.OrdinalIgnoreCase)==true);
   var version=enclosure.Attribute(sparkle+"shortVersionString")!.Value;
   var hashMatch=Regex.Match(enclosure.ToString(),@"SHA256:\s*([a-f0-9]{64})",RegexOptions.IgnoreCase);
   // Use the vendor's canonical HTTPS download host; the appcast may pick a regional mirror.
   var url="https://www.wireshark.org/download/win64/"+Path.GetFileName(new Uri(enclosure.Attribute("url")!.Value).LocalPath);
   return Release(version,url,hashMatch.Success?hashMatch.Groups[1].Value:null);
  }
  if(app.Source=="portmaster"){
   var page=await Text("https://safing.io/download/");
   var url=Match(page,"href=\"(https://updates.safing.io/latest/windows_amd64/packages/Portmaster_[0-9.]+_x64-setup.exe)\"");
   var filename=Path.GetFileName(new Uri(url).LocalPath);
   var sums=await Text("https://raw.githubusercontent.com/safing/checksums/master/sha256_installers.txt");
   var hash=Match(sums,@"(?m)^([a-f0-9]{64})\s+\./"+Regex.Escape(filename)+@"\s*$");
   return Release(Match(filename,@"Portmaster_([0-9.]+)_x64"),url,hash);
  }
  if(app.Source=="binary-metadata"){
   // This vendor supplies an unversioned installer URL. Inspect PE metadata without executing it.
   var url=new Uri(app.FeedUrl!);
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
   using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var client=new HttpClient(handler);client.DefaultRequestHeaders.UserAgent.ParseAdd("XAppInstaller/1.0");
   HttpResponseMessage? response=null;
   for(var redirect=0;redirect<7;redirect++){
    if(url.Scheme!="https"||!app.DownloadHosts.Contains(url.IdnHost,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Untrusted metadata download host");
    response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
    if((int)response.StatusCode is >=300 and <400){var location=response.Headers.Location??throw new InvalidDataException("Missing redirect URL");url=location.IsAbsoluteUri?location:new Uri(url,location);response.Dispose();response=null;continue;}break;
   }
   using var reply=response??throw new IOException("Too many metadata download redirects");reply.EnsureSuccessStatusCode();
   var file=Path.Combine(Path.GetTempPath(),"XAppInstaller-metadata-"+Guid.NewGuid().ToString("N")+".exe");
   try{
    await using(var input=await reply.Content.ReadAsStreamAsync(timeout.Token))await using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     var buffer=new byte[81920];long length=0;int count;
     while((count=await input.ReadAsync(buffer,timeout.Token))>0){length+=count;if(length>32*1024*1024)throw new InvalidDataException("Vendor metadata package exceeds size limit");await output.WriteAsync(buffer.AsMemory(0,count),timeout.Token);}
    }
    var info=FileVersionInfo.GetVersionInfo(file);var version=Versions.Parse(info.ProductVersion??info.FileVersion)?.ToString()??throw new InvalidDataException("Vendor installer has no version metadata");
    await using var stream=File.OpenRead(file);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
    return new(version,url,Path.GetFileName(url.LocalPath),stream.Length,hash);
   }finally{File.Delete(file);}
  }
  return null;
 }
}