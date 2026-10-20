using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Win32;
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class CommandVerifier:IPostInstallationVerifier {
 public async Task VerifyAsync(ApplicationDefinition app,string expectedVersion,CancellationToken token){
  if(app.InstallerType=="npm"){
   var node=ManagedApplications.FindExecutable("node.exe")??throw new IOException("Node.js cannot be found after installation");
   foreach(var prefix in new[]{ManagedApplications.NpmPrefix,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"npm")}){
    var entry=Path.Combine(prefix,"node_modules",app.PackageName!,app.PackageEntryPoint!);if(!File.Exists(entry))continue;
    var output=await ManagedApplications.CaptureAsync(node,[entry,"--version"],token);
    if(!Versions.AtLeast(output,expectedVersion))throw new IOException("Installed npm CLI does not report the expected version");return;
   }
   throw new IOException("Installed npm CLI entry point is missing");
  }
  foreach(var command in app.VerificationCommands){
   var candidates=ExpandCandidates(command.FilePath).ToList();
   if(command.RelativePath is {} relative)candidates.InsertRange(0,RegistryLocations(app).Select(root=>Path.Combine(root,relative)).Where(File.Exists));
   string? lastError=null;var verified=false;
   foreach(var file in candidates.Distinct(StringComparer.OrdinalIgnoreCase)){
    try{
     var output=await ManagedApplications.CaptureAsync(file,command.Arguments.Select(arg=>Environment.ExpandEnvironmentVariables(arg.Replace("{ExecutableDirectory}",Path.GetDirectoryName(file)!))),token);
     if(command.CompareVersion&&!Versions.AtLeast(output,expectedVersion)){lastError="Command version does not match release: "+output[..Math.Min(output.Length,500)];continue;}
     verified=true;break;
    }catch(IOException e){lastError=e.Message;}
   }
   if(!verified)throw new IOException(lastError??"Installed command not found: "+command.FilePath);
  }
 }
 private static IEnumerable<string> RegistryLocations(ApplicationDefinition app){
  if(string.IsNullOrEmpty(app.RegistryPattern))return [];
  var locations=new List<string>();
  foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
   using var root=RegistryKey.OpenBaseKey(hive,view);using var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");if(uninstall is null)continue;
   foreach(var name in uninstall.GetSubKeyNames()){using var key=uninstall.OpenSubKey(name);if(key?.GetValue("DisplayName") is string display&&Regex.IsMatch(display,app.RegistryPattern,RegexOptions.IgnoreCase)&&key.GetValue("InstallLocation") is string location&&Directory.Exists(location))locations.Add(location);}
  }
  return locations;
 }
 public static IEnumerable<string> ExpandCandidates(string pattern){
  pattern=Environment.ExpandEnvironmentVariables(pattern);if(!Path.IsPathFullyQualified(pattern))throw new InvalidDataException("Command path must be absolute");
  var root=Path.GetPathRoot(pattern)!;var parts=pattern[root.Length..].Split(['\\','/'],StringSplitOptions.RemoveEmptyEntries);IEnumerable<string> current=[root];
  for(int index=0;index<parts.Length;index++){
   var part=parts[index];var final=index==parts.Length-1;var next=new List<string>();
   foreach(var directory in current){if(!Directory.Exists(directory))continue;try{next.AddRange(final?Directory.EnumerateFiles(directory,part):Directory.EnumerateDirectories(directory,part));}catch(UnauthorizedAccessException){}}
   current=next;
  }
  return current;
 }
}
