using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppInstaller.Models;
namespace AppInstaller.Providers;
internal static class MediaSources {
 public static async Task<ReleaseInfo?> ResolveAsync(ApplicationDefinition app,HttpClient http,CancellationToken token){
  if(app.Source=="vlc"){
   var status=await http.GetStringAsync("https://update.videolan.org/vlc/status-win-x64",token);
   var version=status.Split('\n')[0].Trim();
   if(!Regex.IsMatch(version,@"^\d+\.\d+\.\d+$"))throw new InvalidDataException("VideoLAN update feed has no stable Windows version");
   var file=$"vlc-{version}-win64.exe";
   // Obtain the authoritative hash from VideoLAN and use a fixed HTTPS mirror.
   // The main download host otherwise redirects to a different regional host on each PC.
   var url=$"https://download.videolan.org/pub/videolan/vlc/{version}/win64/{file}";
   var sums=await http.GetStringAsync(url+".sha256",token);
   var hash=Regex.Match(sums,@"^([a-f0-9]{64})\s+\*?"+Regex.Escape(file)+@"\s*$",RegexOptions.IgnoreCase);
   if(!hash.Success)throw new InvalidDataException("Official VLC SHA-256 is missing or refers to another file");
   return new(version,new($"https://videolan.mirror.liquidtelecom.com/vlc/{version}/win64/{file}"),file,null,hash.Groups[1].Value);
  }
  if(app.Source=="mpv-windows"){
   using var doc=JsonDocument.Parse(await GitHubReleaseClient.Shared.GetStringAsync(http,"https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest",token));
   var release=doc.RootElement;
   if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean())throw new InvalidDataException("No published mpv Windows build");
   var tag=release.GetProperty("tag_name").GetString();
   if(!DateTime.TryParseExact(tag,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new InvalidDataException("mpv Windows release has an invalid build date");
   // Select baseline x86_64, excluding ARM, dev libraries and x86_64-v3 CPU builds.
   var pattern=@"^mpv-x86_64-"+Regex.Escape(tag!)+@"-git-[a-f0-9]+\.7z$";
   var asset=release.GetProperty("assets").EnumerateArray().Single(a=>Regex.IsMatch(a.GetProperty("name").GetString()!,pattern,RegexOptions.IgnoreCase));
   var digest=asset.GetProperty("digest").GetString();
   if(digest is null||!Regex.IsMatch(digest,@"^sha256:[a-f0-9]{64}$",RegexOptions.IgnoreCase))throw new InvalidDataException("mpv build has no authoritative SHA-256");
   return new(date.ToString("yyyy.MM.dd",CultureInfo.InvariantCulture),new(asset.GetProperty("browser_download_url").GetString()!),asset.GetProperty("name").GetString()!,asset.GetProperty("size").GetInt64(),digest[7..]);
  }
  return null;
 }
}
