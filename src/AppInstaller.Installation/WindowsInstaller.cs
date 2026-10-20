using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class InstalledVersionDetector(Func<ApplicationDefinition,IEnumerable<string>>? configuredLocations=null,IReadOnlyList<ApplicationDefinition>? discoveryCatalog=null):IInstalledVersionDetector {
 private readonly System.Collections.Concurrent.ConcurrentDictionary<string,IReadOnlyList<string>> discovery=new();
 public System.Collections.Concurrent.ConcurrentDictionary<string,string> DetectedLocations {get;}=new();
 private Task<IReadOnlyList<string>>? sharedDiscovery;
 public void BeginScan(){discovery.Clear();DetectedLocations.Clear();sharedDiscovery=null;}
 private async Task<IReadOnlyList<string>> DiscoverAsync(ApplicationDefinition app,CancellationToken token){
  if(discovery.TryGetValue(app.Id,out var cached))return cached;
  IReadOnlyList<string> paths;
  if(discoveryCatalog is null)paths=await Task.Run(()=>PortableDiscovery.Find(app,token),token);
  else{sharedDiscovery??=Task.Run(()=>PortableDiscovery.Index(discoveryCatalog,token),token);var index=await sharedDiscovery;var executable=app.DiscoveryExecutable??app.PortableExecutable;paths=executable is null?[]:index.Where(root=>File.Exists(Path.Combine(root,executable))).ToArray();}
  discovery[app.Id]=paths;return paths;
 }
 public async Task<string?> DetectAsync(ApplicationDefinition app,CancellationToken token){
  token.ThrowIfCancellationRequested();string? portableJavaVersion=null;
  if(app.Id=="java"){
   var roots=new List<string>(configuredLocations?.Invoke(app)??[]);
   foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})if(Environment.GetEnvironmentVariable("JAVA_HOME",target) is {} home)roots.Add(home);
   if(ManagedApplications.FindExecutable("javac.exe") is {} javac)roots.Add(Path.GetDirectoryName(Path.GetDirectoryName(javac))!);
   roots.AddRange(await DiscoverAsync(app,token));
   var installed=roots.Distinct(StringComparer.OrdinalIgnoreCase).Select(root=>(Root:root,Version:PortableDiscovery.InspectJdk(root))).Where(item=>Versions.Compatible(item.Version,app.MinimumVersion,app.MaximumVersionExclusive)).OrderByDescending(item=>Versions.Parse(item.Version)).FirstOrDefault();
   if(installed.Version is not null){DetectedLocations[app.Id]=installed.Root;portableJavaVersion=installed.Version;}
  }
  if(app.InstallerType is "zip" or "7z"){
   var roots=new List<string>(configuredLocations?.Invoke(app)??[]);var receipt=ManagedApplications.Read(app.Id);if(receipt is not null)roots.Add(receipt.Root);
   if(app.EnvironmentVariable is {} variable)foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})if(Environment.GetEnvironmentVariable(variable,target) is {} path&&!roots.Contains(path))roots.Add(path);
   if(app.Id=="ffmpeg"&&ManagedApplications.FindExecutable("ffmpeg.exe") is {} ffmpeg){var version=PortableDiscovery.InspectExecutable(ffmpeg);if(Versions.Parse(version) is not null&&Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive)){DetectedLocations[app.Id]=Path.GetDirectoryName(ffmpeg)!;return version;}}
   roots.AddRange(await DiscoverAsync(app,token));
   foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)){var version=await ManagedApplications.InspectRootAsync(app,root,token,receipt is not null&&root.Equals(receipt.Root,StringComparison.OrdinalIgnoreCase));if(Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive)){DetectedLocations[app.Id]=root;return app.Id=="mpv"&&receipt is not null&&root.Equals(receipt.Root,StringComparison.OrdinalIgnoreCase)?receipt.Version:version;}}
   return null;
  }
  if(app.InstallerType=="npm"){
   foreach(var prefix in new[]{ManagedApplications.NpmPrefix,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"npm")}){
    var file=Path.Combine(prefix,"node_modules",app.PackageName??throw new InvalidDataException("Missing npm package name"),"package.json");if(!File.Exists(file))continue;
    using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));var version=doc.RootElement.GetProperty("version").GetString();if(Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive)&&File.Exists(Path.Combine(prefix,"node_modules",app.PackageName!,app.PackageEntryPoint??throw new InvalidDataException("Missing npm entry point"))))return version;
   }
   return null;
  }
  if(app.Id=="hermes"){
   var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"hermes","hermes-agent");var desktop=Path.Combine(root,"apps","desktop","release","win-unpacked","Hermes.exe");
   if(File.Exists(desktop)&&ManagedApplications.FindExecutable("git.exe") is {} git){try{var output=await ManagedApplications.CaptureAsync(git,["-C",root,"describe","--tags","--abbrev=0"],token);var match=Regex.Match(output,@"\d+(?:\.\d+)+");if(match.Success)return match.Value;}catch(IOException){}}
  }
  var conventional=await Task.Run(()=>{
   if(string.IsNullOrEmpty(app.RegistryPattern))return null;var versions=new List<string>();
   foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
    if(app.Id=="java"&&view==RegistryView.Registry32)continue;
    using var root=RegistryKey.OpenBaseKey(hive,view);using var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");if(uninstall is null)continue;
    foreach(var name in uninstall.GetSubKeyNames()){
     token.ThrowIfCancellationRequested();using var key=uninstall.OpenSubKey(name);
     if(key?.GetValue("DisplayName") is string display&&Regex.IsMatch(display,app.RegistryPattern,RegexOptions.IgnoreCase)&&key.GetValue("DisplayVersion") is string version&&Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive))versions.Add(app.Id=="python"?Regex.Match(display,@"\d+(?:\.\d+)+").Value:version);
    }
   }
   return versions.OrderByDescending(v=>Versions.Parse(v)).FirstOrDefault();
  },token);
  string? best=conventional;
  if(portableJavaVersion is not null&&(Versions.Parse(best) is null||Versions.Parse(portableJavaVersion)>Versions.Parse(best)))best=portableJavaVersion;
  else if(app.Id=="java"&&conventional is not null)DetectedLocations.TryRemove(app.Id,out _);
  if(app.DiscoveryExecutable is {} executable){
   foreach(var root in await DiscoverAsync(app,token)){
    var version=PortableDiscovery.InspectExecutable(Path.Combine(root,executable));
    if(Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive)&&Versions.Parse(version) is not null&&(Versions.Parse(best) is null||Versions.Parse(version)>Versions.Parse(best))){best=version;DetectedLocations[app.Id]=root;}
   }
  }
  return best;
 }
}
public sealed class InstallerException(string message,int exitCode):Exception(message){public int ExitCode=>exitCode;}
public sealed class WindowsInstaller:IInstaller {
 public async Task<int> InstallAsync(ApplicationDefinition app,string path,CancellationToken token){
  token.ThrowIfCancellationRequested();
  if(app.InstallerType is "zip" or "7z"){await new PortableInstaller().InstallAsync(app,path,token);return 0;}
  if(app.InstallerType=="npm"){
   var node=ManagedApplications.FindExecutable("node.exe")??throw new IOException("Node.js is required before installing REA");
   var npm=Path.Combine(Path.GetDirectoryName(node)!,"node_modules","npm","bin","npm-cli.js");if(!File.Exists(npm))throw new IOException("The Node.js installation has no npm CLI");
   var start=new ProcessStartInfo(node){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(var argument in new[]{npm,"install","--global","--ignore-scripts","--no-audit","--no-fund","--prefix",ManagedApplications.NpmPrefix,"--registry=https://registry.npmjs.org",path})start.ArgumentList.Add(argument);
   using var p=Process.Start(start)??throw new IOException("npm did not start");var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync(CancellationToken.None);await output;
   if(p.ExitCode!=0)throw new InstallerException("npm installation failed: "+await error,p.ExitCode);UserEnvironment.AddPath(ManagedApplications.NpmPrefix);return 0;
  }
  if(app.InstallerType is not ("msi" or "exe"))throw new NotSupportedException("Unsupported installer format");
  using var process=InstallerProcessLauncher.Start(app,path);
  await process.WaitForExitAsync(CancellationToken.None);
  if(process.ExitCode is not (0 or 3010))throw new InstallerException($"Installer returned {process.ExitCode}."+(app.InstallerType=="msi"?$" Diagnostic log: {InstallerProcessLauncher.MsiLogPath(path)}":""),process.ExitCode);return process.ExitCode;
 }
}
