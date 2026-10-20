using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Installation;
public record ManagedInstallation(string Id,string Root,string Version);
public static class ManagedApplications {
 public static string BaseDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XAppInstaller");
 public static string ToolsDirectory=>Path.Combine(BaseDirectory,"Tools");
 public static string NpmPrefix=>Path.Combine(ToolsDirectory,"npm");
 public static string ReceiptPath(string id)=>Path.Combine(BaseDirectory,"Installed",id+".json");
 public static void Record(ManagedInstallation installation){var path=ReceiptPath(installation.Id);Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(installation));File.Move(temp,path,true);}
 private static IEnumerable<string> InstallationStores(){
  yield return BaseDirectory;
  var packages=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","Local","Packages");
  string[] directories;try{directories=Directory.GetDirectories(packages);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){yield break;}
  foreach(var package in directories){var store=Path.Combine(package,"LocalCache","Local","XAppInstaller");if(Directory.Exists(Path.Combine(store,"Installed")))yield return store;}
 }
 public static ManagedInstallation? Read(string id,IEnumerable<string>? stores=null){
  var candidates=new List<ManagedInstallation>();
  foreach(var store in stores??InstallationStores())try{
   var item=JsonSerializer.Deserialize<ManagedInstallation>(File.ReadAllText(Path.Combine(store,"Installed",id+".json")));
   if(item is null||item.Id!=id)continue;
   var storedTools=Path.GetFullPath(Path.Combine(store,"Tools"))+Path.DirectorySeparatorChar;
   var oldTools=Path.GetFullPath(ToolsDirectory)+Path.DirectorySeparatorChar;
   var root=Path.GetFullPath(item.Root);
   if(root.StartsWith(oldTools,StringComparison.OrdinalIgnoreCase)&&!store.Equals(BaseDirectory,StringComparison.OrdinalIgnoreCase))root=Path.Combine(storedTools,root[oldTools.Length..]);
   if(!root.StartsWith(storedTools,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(root))continue;
   candidates.Add(item with{Root=root});
  }catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException or ArgumentException){}
  return candidates.OrderByDescending(item=>Versions.Parse(item.Version)).FirstOrDefault();
 }

 public static void ConfigurePortableEnvironment(ApplicationDefinition app,string root){
  var executable=InstallerProcessLauncher.ResolvePackagePath(Path.Combine(root,app.PortableExecutable!));
  var physicalRoot=executable[..^app.PortableExecutable!.Length].TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  if(app.PathSubdirectory is {} subdirectory){
   var oldPath=Path.Combine(root,subdirectory);var newPath=Path.Combine(physicalRoot,subdirectory);
   var existing=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User);
   var migrated=string.Join(';',(existing??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Where(p=>!p.Trim(' ','"').TrimEnd('\\','/').Equals(oldPath.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)));
   UserEnvironment.Set("Path",UserEnvironment.MergePath(migrated,newPath));
   Environment.SetEnvironmentVariable("Path",Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)+";"+Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User));
  }
  if(app.EnvironmentVariable is {} variable)UserEnvironment.Set(variable,physicalRoot);
 }
 public static string? FindExecutable(string name){
  var paths=(Environment.GetEnvironmentVariable("PATH")??"")+";"+(Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.Machine)??"")+";"+(Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.User)??"");
  var locations=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"nodejs"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","cmd")}.Concat(paths.Split(';',StringSplitOptions.RemoveEmptyEntries));
  foreach(var directory in locations){try{var file=Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim(' ','"')),name);if(File.Exists(file))return file;}catch(ArgumentException){}}
  return null;
 }
 public static async Task<string> CaptureAsync(string file,IEnumerable<string> args,CancellationToken token){
  var start=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)start.ArgumentList.Add(arg);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
  using var process=Process.Start(start)??throw new IOException("Cannot start version check");var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
  try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}token.ThrowIfCancellationRequested();throw new IOException("Version check timed out");}
  var output=await stdout+await stderr;if(process.ExitCode!=0)throw new IOException("Version check failed: "+output);return output;
 }
 public static async Task<string?> InspectRootAsync(ApplicationDefinition app,string root,CancellationToken token,bool allowExecution=false){
  if(app.PortableExecutable is null||!File.Exists(Path.Combine(root,app.PortableExecutable)))return null;
  string? output=null;
  if(app.PortableVersionFile is {} versionFile){var path=Path.Combine(root,versionFile);if(!File.Exists(path))return null;output=await File.ReadAllTextAsync(path,token);}
  else if(app.PortableVersionArguments.Length>0){if(!allowExecution)return PortableDiscovery.InspectExecutable(Path.Combine(root,app.PortableExecutable));output=await CaptureAsync(Path.Combine(root,app.PortableExecutable),app.PortableVersionArguments,token);}
  if(output is not null&&app.PortableVersionPattern is {} pattern){var match=Regex.Match(output,pattern);return match.Success?match.Groups[1].Value:null;}
  return null;
 }
}
