using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Providers;
internal static class OfficialSources {
 public static async Task<ReleaseInfo?> ResolveAsync(ApplicationDefinition app,HttpClient http,bool preview,CancellationToken token){
  var additional=await AdditionalSources.ResolveAsync(app,http,token);if(additional is not null)return additional;
  async Task<string> Text(string url)=>await http.GetStringAsync(url,token);
  async Task<JsonDocument> Json(string url)=>JsonDocument.Parse(await Text(url));
  string Match(string input,string pattern){var m=Regex.Match(input,pattern,RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value:throw new InvalidDataException("Official source format changed: "+app.Name);}
  if(app.Source=="adoptium"){
   using var available=await Json("https://api.adoptium.net/v3/info/available_releases");
   var feature=available.RootElement.GetProperty("most_recent_feature_release").GetInt32();
   using var assets=await Json($"https://api.adoptium.net/v3/assets/latest/{feature}/hotspot?architecture=x64&image_type=jdk&os=windows&release_type=ga");
   var item=assets.RootElement.EnumerateArray().First();var installer=item.GetProperty("binary").GetProperty("installer");
   return new(item.GetProperty("version").GetProperty("semver").GetString()!.Split('+')[0],new(installer.GetProperty("link").GetString()!),installer.GetProperty("name").GetString()!,installer.GetProperty("size").GetInt64(),installer.GetProperty("checksum").GetString());
  }
  if(app.Source=="python"){
   var page=await Text("https://www.python.org/downloads/windows/");
   var path=Match(page,"href=\"([^\"]+/release/python-[^\"]+/)\"[^>]*>Latest Python 3 Release");
   var release=await Text(new Uri(new Uri("https://www.python.org"),path).AbsoluteUri);
   var url=Match(release,"href=\"(https://www.python.org/ftp/python/[^\"]+/python-[0-9.]+-amd64.exe)\"");
   var version=Match(url,"python-([0-9.]+)-amd64.exe");
   var filename=Path.GetFileName(new Uri(url).LocalPath);
   var row=Regex.Match(release,"<tr[^>]*>(?:(?!</tr>).)*"+Regex.Escape(filename)+"(?:(?!</tr>).)*</tr>",RegexOptions.Singleline).Value;
   row=Regex.Replace(row,@"<[^>]+>","");
   var hash=Regex.Match(row,@"\b[a-f0-9]{64}\b",RegexOptions.IgnoreCase);
   return new(version,new(url),filename,null,hash.Success?hash.Value:null);
  }
  if(app.Source=="vscode"){
   using var doc=await Json("https://update.code.visualstudio.com/api/update/win32-x64/stable/latest");var d=doc.RootElement;var uri=new Uri(d.GetProperty("url").GetString()!);
   return new(d.GetProperty("productVersion").GetString()!,uri,Path.GetFileName(uri.LocalPath),null,d.GetProperty("sha256hash").GetString());
  }
  if(app.Source=="docker"){
   var doc=XDocument.Parse(await Text("https://desktop.docker.com/win/main/amd64/appcast.xml"));XNamespace sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle";
   var e=doc.Descendants("item").Select(i=>i.Element("enclosure")).First(e=>e?.Attribute("url")?.Value.EndsWith("Installer.exe",StringComparison.OrdinalIgnoreCase)==true)!;
   return new(e.Attribute(sparkle+"shortVersionString")!.Value,new(e.Attribute("url")!.Value),"DockerDesktopInstaller.exe",long.TryParse(e.Attribute("length")?.Value,out var size)?size:null,null);
  }
  if(app.Source=="redirect"){
   using var request=new HttpRequestMessage(HttpMethod.Get,app.FeedUrl);using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
   var uri=response.RequestMessage!.RequestUri!;
   if(uri.Scheme!="https"||!app.DownloadHosts.Contains(uri.IdnHost,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Untrusted official redirect");
   var filename=Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));var version=Match(filename,app.AssetPattern).Replace('-','+');
   return new(version,uri,filename,response.Content.Headers.ContentLength,null);
  }
  if(app.Source=="ffmpeg"){
   var version=(await Text("https://www.gyan.dev/ffmpeg/builds/release-version")).Trim();var checksum=(await Text("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256")).Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0];
   return new(version,new("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"),"ffmpeg-release-essentials.zip",null,checksum);
  }
  if(app.Source=="npm"){
   using var doc=await Json($"https://registry.npmjs.org/{Uri.EscapeDataString(app.PackageName??throw new InvalidDataException("npm package name is required"))}/latest");var d=doc.RootElement;var dist=d.GetProperty("dist");var uri=new Uri(dist.GetProperty("tarball").GetString()!);var integrity=dist.GetProperty("integrity").GetString()!;
   if(!integrity.StartsWith("sha512-",StringComparison.Ordinal))throw new InvalidDataException("npm release is missing SHA-512 integrity data");
   return new(d.GetProperty("version").GetString()!,uri,Path.GetFileName(uri.LocalPath),null,null,integrity[7..]);
  }
  if(app.Source=="hermes"){
   var page=await Text("https://hermes-agent.nousresearch.com/desktop");
   var url=Match(page,@"(https://hermes-assets\.nousresearch\.com/Hermes-Setup\.exe(?:\?build=[a-z0-9]+)?)");
   using var feed=JsonDocument.Parse(await GitHubReleaseClient.Shared.GetStringAsync(http,"https://api.github.com/repos/NousResearch/hermes-agent/releases/latest",token));
   return new(feed.RootElement.GetProperty("tag_name").GetString()!.TrimStart('v'),new(WebUtility.HtmlDecode(url)),"Hermes-Setup.exe",null,null);
  }
  return null;
 }
}