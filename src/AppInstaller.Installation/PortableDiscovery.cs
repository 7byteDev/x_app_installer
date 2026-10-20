using System.Diagnostics;
using System.IO.Enumeration;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.Installation;
/// <summary>Bounded discovery in conventional install roots; never a recursive whole-disk scan.</summary>
public static class PortableDiscovery {
 public static IReadOnlyList<string> Find(ApplicationDefinition app,CancellationToken token,IEnumerable<string>? searchRoots=null){
  if(app.DiscoveryPatterns.Length==0)return [];
  var roots=searchRoots?.ToList()??DefaultRoots();var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var watch=Stopwatch.StartNew();int examined=0;
  var pending=new Queue<(string Path,int Depth,bool Matched)>();foreach(var root in roots)pending.Enqueue((root,0,false));
  string[] containers=["tools","apps","portable","programs","dev","development","software","java","jdks"];
  while(pending.TryDequeue(out var item)){
   token.ThrowIfCancellationRequested();if(watch.Elapsed>TimeSpan.FromSeconds(2)||examined>=2500)break;
   if(!seen.Add(item.Path)||!Directory.Exists(item.Path))continue;
   var executable=app.DiscoveryExecutable??app.PortableExecutable;
   if(executable is null||File.Exists(Path.Combine(item.Path,executable)))found.Add(item.Path);
   if(item.Depth>=3)continue;
   try{
    foreach(var child in Directory.EnumerateDirectories(item.Path)){
     token.ThrowIfCancellationRequested();if(++examined>=2500||watch.Elapsed>TimeSpan.FromSeconds(2))break;
     if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;
     var name=Path.GetFileName(child);var matches=app.DiscoveryPatterns.Any(pattern=>FileSystemName.MatchesSimpleExpression(pattern,name,true));
     if(matches||item.Matched||containers.Contains(name,StringComparer.OrdinalIgnoreCase))pending.Enqueue((child,item.Depth+1,matches||item.Matched));
    }
   }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){}
  }
  return found.ToArray();
 }
 private static List<string> DefaultRoots(){
  var roots=new List<string>();
  foreach(var drive in DriveInfo.GetDrives()){try{if(drive.DriveType==DriveType.Fixed&&drive.IsReady)roots.Add(drive.RootDirectory.FullName);}catch(IOException){}}
  foreach(var folder in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.MyDocuments}){var path=Environment.GetFolderPath(folder);if(!string.IsNullOrEmpty(path))roots.Add(path);}
  var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  roots.AddRange([Path.Combine(home,"Downloads"),Path.Combine(home,"scoop","apps"),Path.Combine(local,"Programs"),Path.Combine(local,"Microsoft","WinGet","Packages")]);
  return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 public static IReadOnlyList<string> Index(IEnumerable<ApplicationDefinition> catalog,CancellationToken token)=>Find(new(){Id="discovery",Name="Discovery",DiscoveryPatterns=catalog.SelectMany(a=>a.DiscoveryPatterns).Distinct().ToArray()},token);
 public static string? InspectExecutable(string file){
  if(!File.Exists(file)||!Path.GetExtension(file).Equals(".exe",StringComparison.OrdinalIgnoreCase))return null;
  try{var info=FileVersionInfo.GetVersionInfo(file);var version=info.ProductVersion??info.FileVersion;return Versions.Parse(version) is not null?version:"Unknown";}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception){return null;}
 }
 public static string? InspectJdk(string root){
  var java=Path.Combine(root,"bin","java.exe");var javac=Path.Combine(root,"bin","javac.exe");var release=Path.Combine(root,"release");
  if(!File.Exists(java)||!File.Exists(javac)||!File.Exists(release))return null;
  try{
   using var input=File.OpenRead(java);using var reader=new BinaryReader(input);if(reader.ReadUInt16()!=0x5A4D)return null;input.Position=0x3C;var offset=reader.ReadUInt32();if(offset>input.Length-6)return null;input.Position=offset;if(reader.ReadUInt32()!=0x00004550||reader.ReadUInt16()!=0x8664)return null;
   var text=File.ReadAllText(release);var match=System.Text.RegularExpressions.Regex.Match(text,"(?m)^JAVA_VERSION=\"([^\"]+)\"");return match.Success?match.Groups[1].Value:null;
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException){return null;}
 }
}
