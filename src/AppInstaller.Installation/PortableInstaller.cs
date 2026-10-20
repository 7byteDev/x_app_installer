using System.IO.Compression;
using System.Runtime.InteropServices;
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class PortableInstaller {
 public async Task InstallAsync(ApplicationDefinition app,string package,CancellationToken token){
  if(app.PortableExecutable is null)throw new InvalidDataException("Portable executable is not declared");
  var parent=Path.Combine(ManagedApplications.ToolsDirectory,app.Id);Directory.CreateDirectory(parent);
  var staging=Path.Combine(parent,".staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
  try{
   if(app.InstallerType=="7z")await ExtractSevenZipAsync(package,staging,token);
   else{
   using var archive=ZipFile.OpenRead(package);long total=0;
   foreach(var entry in archive.Entries){
    token.ThrowIfCancellationRequested();total=checked(total+entry.Length);if(total>8L*1024*1024*1024)throw new InvalidDataException("Unpacked archive exceeds the size limit");
    var target=SafeDestination(staging,entry.FullName);
    if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Archive symbolic links are not supported");
    if(entry.FullName.EndsWith('/')||entry.FullName.EndsWith('\\')){Directory.CreateDirectory(target);continue;}
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);await using var input=entry.Open();await input.CopyToAsync(output,token);
   }
   }
   var candidates=new[]{staging}.Concat(Directory.GetDirectories(staging));
   var root=candidates.SingleOrDefault(d=>File.Exists(Path.Combine(d,app.PortableExecutable)))??throw new InvalidDataException("The archive does not contain the expected application layout");
   var version=await ManagedApplications.InspectRootAsync(app,root,token,true)??throw new InvalidDataException("Cannot verify extracted application version");
   if(app.Id=="mpv")version=MpvBuildVersion(package); // Verify the player above; track its Windows distribution build date.
   if(!Versions.Compatible(version,app.MinimumVersion,app.MaximumVersionExclusive))throw new InvalidDataException("Extracted version does not satisfy the dependency requirements");
   var destination=Path.Combine(parent,version+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.Move(root,destination);
   ManagedApplications.ConfigurePortableEnvironment(app,destination);
   var physicalExecutable=InstallerProcessLauncher.ResolvePackagePath(Path.Combine(destination,app.PortableExecutable));
   var physicalRoot=physicalExecutable[..^app.PortableExecutable.Length].TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   ManagedApplications.Record(new(app.Id,physicalRoot,version));
  }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
 }
 public static string MpvBuildVersion(string package){
  var match=System.Text.RegularExpressions.Regex.Match(Path.GetFileName(package),@"mpv-x86_64-(\d{8})-git-[a-f0-9]+\.7z$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  if(!match.Success||!DateTime.TryParseExact(match.Groups[1].Value,"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date))throw new InvalidDataException("Invalid mpv Windows build filename");
  return date.ToString("yyyy.MM.dd",System.Globalization.CultureInfo.InvariantCulture);
 }
 public static async Task ExtractSevenZipAsync(string package,string staging,CancellationToken token){
  using var archive=SharpCompress.Archives.SevenZip.SevenZipArchive.OpenArchive(package);
  using var reader=archive.ExtractAllEntries();long total=0;
  while(reader.MoveToNextEntry()){
   token.ThrowIfCancellationRequested();var entry=reader.Entry;
   if(entry.IsEncrypted||!string.IsNullOrEmpty(entry.LinkTarget))throw new InvalidDataException("Encrypted archives and archive links are not supported");
   var target=SafeDestination(staging,entry.Key??throw new InvalidDataException("Missing archive path"));
   if(entry.IsDirectory){Directory.CreateDirectory(target);continue;}
   if(entry.Size>8L*1024*1024*1024)throw new InvalidDataException("Unpacked archive exceeds the size limit");
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);
   using var input=reader.OpenEntryStream();await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
   var buffer=new byte[81920];int count;
   while((count=await input.ReadAsync(buffer,token))>0){total=checked(total+count);if(total>8L*1024*1024*1024)throw new InvalidDataException("Unpacked archive exceeds the size limit");await output.WriteAsync(buffer.AsMemory(0,count),token);}
  }
 }
 public static string SafeDestination(string root,string relative){
  if(relative.Contains(':')||Path.IsPathRooted(relative))throw new InvalidDataException("Unsafe archive path");
  var full=Path.GetFullPath(Path.Combine(root,relative));var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive entry escapes the extraction directory");return full;
 }
}
public static class UserEnvironment {
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr parameter,string text,uint flags,uint timeout,out IntPtr result);
 public static string MergePath(string? existing,string directory){
  static string Normalize(string path)=>Environment.ExpandEnvironmentVariables(path.Trim(' ','"')).TrimEnd('\\','/');
  var parts=(existing??"").Split(';',StringSplitOptions.RemoveEmptyEntries).ToList();if(!parts.Any(p=>Normalize(p).Equals(Normalize(directory),StringComparison.OrdinalIgnoreCase)))parts.Add(directory);return string.Join(';',parts);
 }
 public static void AddPath(string directory){Set("Path",MergePath(Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User),directory));var machine=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine);Environment.SetEnvironmentVariable("Path",machine+";"+Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User));}
 public static void Set(string variable,string value){Environment.SetEnvironmentVariable(variable,value,EnvironmentVariableTarget.User);Environment.SetEnvironmentVariable(variable,value);SendMessageTimeout(new IntPtr(0xFFFF),0x001A,IntPtr.Zero,"Environment",2,1000,out _);}
}
