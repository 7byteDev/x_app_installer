using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class InstallerLaunchException(string message,int nativeErrorCode,string resolution,Exception? inner=null):Exception(message,inner){
 public int NativeErrorCode=>nativeErrorCode;
 public string Resolution=>resolution;
}
public static class InstallerProcessLauncher {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
 private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint size,uint flags);
 public static string ResolvePackagePath(string package){
  if(!Path.IsPathFullyQualified(package))throw new ArgumentException("Installer path must be absolute.",nameof(package));
  // Packaged hosts can virtualize LocalAppData. Elevated installers do not share that view.
  using var file=File.OpenHandle(package,FileMode.Open,FileAccess.Read,FileShare.Read);
  var path=new StringBuilder(1024);
  var length=GetFinalPathNameByHandle(file,path,(uint)path.Capacity,0);
  if(length>=path.Capacity){path.EnsureCapacity(checked((int)length+1));length=GetFinalPathNameByHandle(file,path,(uint)path.Capacity,0);}
  if(length==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not resolve the installer file's physical path.");
  var resolved=path.ToString();
  if(resolved.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+resolved[8..];
  return resolved.StartsWith(@"\\?\",StringComparison.Ordinal)?resolved[4..]:resolved;
 }
 public static ProcessStartInfo CreateStartInfo(ApplicationDefinition app,string package){
  if(!Path.IsPathFullyQualified(package))throw new ArgumentException("Installer path must be absolute.",nameof(package));
  package=Path.GetFullPath(package);
  if(!File.Exists(package))throw new InstallerLaunchException("The verified installer is no longer available at: "+package,2,"Retry to download and verify a fresh package. If Windows quarantined it, review that result; do not disable protection.");
  package=ResolvePackagePath(package);
  // The shell/elevation service may run in a different directory context. Use a stable,
  // system-owned directory instead of the per-user download directory as its CWD.
  var info=new ProcessStartInfo{
   FileName=app.InstallerType=="msi"?Path.Combine(Environment.SystemDirectory,"msiexec.exe"):package,
   WorkingDirectory=Environment.SystemDirectory,
   UseShellExecute=true,
   // EXE bootstrappers (including Python Burn) manage their own elevation. The default
   // open action also honors requireAdministrator manifests. MSI needs explicit elevation.
   Verb=app.InstallerType=="msi"&&app.RequiresAdministrator?"runas":string.Empty
  };
  info.Arguments=app.InstallerType=="msi"?$"/i \"{package}\" {app.SilentArguments} /L*v \"{MsiLogPath(package)}\"":app.SilentArguments;
  return info;
 }
 public static string MsiLogPath(string package)=>Path.Combine(Path.GetTempPath(),Path.GetFileName(package)+".install.log");
 public static Process Start(ApplicationDefinition app,string package){
  var info=CreateStartInfo(app,package);
  try{return Process.Start(info)??throw new IOException("Windows did not return an installer process handle.");}
  catch(Win32Exception e)when(e.NativeErrorCode==740){
   // A legacy EXE may request elevation without advertising it in its manifest.
   info.Verb="runas";return StartWithDiagnostics(info,package);
  }
  catch(Win32Exception e)when(e.NativeErrorCode is 2 or 3&&File.Exists(package)){
   // Some shell configurations reject an explicit CWD on handoff. Retry only this
   // launch failure, with the default directory; no installer has started yet.
   info.WorkingDirectory=string.Empty;return StartWithDiagnostics(info,package);
  }
  catch(Win32Exception e){throw Failure(e,package);}
 }
 private static Process StartWithDiagnostics(ProcessStartInfo info,string package){
  try{return Process.Start(info)??throw new IOException("Windows did not return an installer process handle.");}
  catch(Win32Exception e){throw Failure(e,package);}
 }
 private static InstallerLaunchException Failure(Win32Exception error,string package){
  var resolution=error.NativeErrorCode switch{
   2 or 3=>"Retry to download and verify the package again. If the file still exists, check Windows application-control events and that the installation drive is accessible.",
   5=>"Windows denied access. Check file permissions and application-control policy; do not disable Windows protection.",
   1223=>"The Windows administrator prompt was cancelled. Choose Retry and approve it if you intend to install this application.",
   740=>"Windows requires administrator permission. Retry and approve the UAC prompt.",
   193 or 216=>"The installer format or architecture is not supported. Download the correct package for this Windows architecture.",
   577 or 1260=>"Windows security or administrator policy blocked this installer. Review the Windows security/application-control result or contact your administrator; do not bypass it.",
   _=>"Check the Windows error code and application-control events, then retry or skip this application."
  };
  return new($"Windows could not start the installer (Windows error {error.NativeErrorCode}: {error.Message}).\nPackage: {package}",error.NativeErrorCode,resolution,error);
 }
}
