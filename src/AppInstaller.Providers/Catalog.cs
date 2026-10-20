
using System.Text.Json;
using System.Text.RegularExpressions;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Providers;
public static class Catalog {
 public static IReadOnlyList<ApplicationDefinition> Load(string path) {
  var apps=JsonSerializer.Deserialize<List<ApplicationDefinition>>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Empty catalog");
  if(apps.Select(a=>a.Id).Distinct().Count()!=apps.Count)throw new InvalidDataException("Duplicate provider IDs");
  foreach(var app in apps){
   if(!Regex.IsMatch(app.Id,@"^[a-z0-9-]+$"))throw new InvalidDataException("Unsafe application ID");
   if(app.Enabled&&(app.DependencyStatus!="Verified"||app.RequirementSources.Length==0||app.DownloadHosts.Length==0))throw new InvalidDataException($"{app.Name}: enabled providers require audited requirements, sources and trusted hosts");
   if(app.Enabled&&app.InstallerType is "exe"&&app.SilentArguments.Length==0&&!app.Interactive)throw new InvalidDataException($"{app.Name}: silent arguments or interactive installation must be declared");
   foreach(var relative in new[]{app.PortableExecutable,app.PortableVersionFile,app.PathSubdirectory,app.PackageEntryPoint}.Where(x=>x is not null))if(Path.IsPathRooted(relative!)||relative!.Split(['\\','/']).Contains("..")||relative.Contains(':'))throw new InvalidDataException("Unsafe relative provider path");
   foreach(var dependency in app.Dependencies.Concat(app.OptionalDependencies))if(!apps.Any(a=>a.Id==dependency.Id))throw new InvalidDataException("Unknown catalog dependency: "+dependency.Id);
  }
  return apps;
 }
}
public sealed class ApplicationProvider(ApplicationDefinition definition,HttpClient http):IApplicationProvider {
 public ApplicationDefinition Definition=>definition;
 public Task<ReleaseInfo> ResolveAsync(bool includePrerelease,CancellationToken token)=>ReleaseMetadataCache.Shared.ResolveAsync(definition,includePrerelease,()=>ResolveOfficialAsync(includePrerelease,token),token);
 private async Task<ReleaseInfo> ResolveOfficialAsync(bool includePrerelease,CancellationToken token) {
  if(!definition.Enabled)throw new NotSupportedException(definition.UnavailableReason??"Provider not enabled");
  if(!definition.Architectures.Contains(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()))throw new NotSupportedException("Unsupported Windows architecture.");
  var official=await OfficialSources.ResolveAsync(definition,http,includePrerelease,token);if(official is not null)return official;
  if(definition.Source=="node") {
   using var doc=JsonDocument.Parse(await http.GetStringAsync("https://nodejs.org/dist/index.json",token));
   var row=doc.RootElement.EnumerateArray().First(r=>r.GetProperty("files").EnumerateArray().Any(f=>f.GetString()=="win-x64-msi")&&(includePrerelease||!Regex.IsMatch(r.GetProperty("version").GetString()!,"alpha|beta|rc|nightly",RegexOptions.IgnoreCase)));
   var version=row.GetProperty("version").GetString()!;var file=$"node-{version}-x64.msi";var baseUrl=$"https://nodejs.org/dist/{version}/";
   var checksums=await http.GetStringAsync(baseUrl+"SHASUMS256.txt",token);
   var hash=checksums.Split('\n').Select(s=>s.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).First(s=>s.Length==2&&s[1]==file)[0];
   return new(version.TrimStart('v'),new(baseUrl+file),file,null,hash);
  }
  if(definition.Source=="github") {
   using var doc=JsonDocument.Parse(await GitHubReleaseClient.Shared.GetStringAsync(http,$"https://api.github.com/repos/{definition.Repository}/releases?per_page=30",token));
   foreach(var release in doc.RootElement.EnumerateArray()) {
    if(release.GetProperty("draft").GetBoolean()||!includePrerelease&&release.GetProperty("prerelease").GetBoolean())continue;
    foreach(var asset in release.GetProperty("assets").EnumerateArray()) {
     var name=asset.GetProperty("name").GetString()!;
     var releaseVersion=Versions.Parse(release.GetProperty("tag_name").GetString());
     if(!Versions.Compatible(releaseVersion?.ToString(),definition.MinimumVersion,definition.MaximumVersionExclusive))continue;
     if(!Regex.IsMatch(name,definition.AssetPattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))continue;
     string? hash=null;
     if(asset.TryGetProperty("digest",out var d)&&d.GetString() is {} digest&&digest.StartsWith("sha256:"))hash=digest[7..];
     if(hash is null&&release.TryGetProperty("body",out var b)) {
      var line=(b.GetString()??"").Split('\n').FirstOrDefault(l=>l.Contains(name,StringComparison.Ordinal));
      if(line is not null){var match=Regex.Match(line,@"\b[0-9a-fA-F]{64}\b");if(match.Success)hash=match.Value;}
     }
     var tag=release.GetProperty("tag_name").GetString()!;
     Dependency[]? dependencies=null;
     if(definition.Id=="ghidra"){
      var properties=await http.GetStringAsync($"https://raw.githubusercontent.com/NationalSecurityAgency/ghidra/{Uri.EscapeDataString(tag)}/Ghidra/application.properties",token);
      var min=Regex.Match(properties,@"(?m)^application\.java\.min=(\d+)").Groups[1].Value;
      var max=Regex.Match(properties,@"(?m)^application\.java\.max=(\d+)").Groups[1].Value;
      if(string.IsNullOrEmpty(min))throw new InvalidDataException("Ghidra release does not declare a Java requirement");
      dependencies=[new("java",min+".0",string.IsNullOrEmpty(max)?null:(int.Parse(max)+1)+".0","Required by this Ghidra release")];
     }
     return new(Regex.Match(tag.Replace(".windows.","."),@"\d+(?:\.\d+){1,3}").Value,new(asset.GetProperty("browser_download_url").GetString()!),name,asset.GetProperty("size").GetInt64(),hash,null,dependencies);
    }
   }
   throw new InvalidDataException("No stable Windows x64 installer found in official releases.");
  }
  throw new NotSupportedException("Unknown version source");
 }
}
