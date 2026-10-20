using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class WindowsPrerequisites:IInstallationPreflight {
 [DllImport("kernel32.dll")]private static extern bool IsProcessorFeaturePresent(uint feature);
 [StructLayout(LayoutKind.Sequential)]private struct MemoryStatus {public uint Length;public uint Load;public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;}
 [DllImport("kernel32.dll")]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
 // Hyper-V owns the virtualization extensions on the host, so native feature flags can be false.
 public static bool IsHyperVHost(){
  if(!X86Base.IsSupported||(X86Base.CpuId(1,0).Ecx & unchecked((int)0x80000000))==0)return false;
  var vendor=X86Base.CpuId(0x40000000,0);
  if(vendor.Eax<0x40000003||vendor.Ebx!=0x7263694d||vendor.Ecx!=0x666f736f||vendor.Edx!=0x76482074)return false;
  return X86Base.CpuId(0x40000001,0).Eax==0x31237648&&(X86Base.CpuId(0x40000003,0).Ebx & 1)!=0; // Hv#1, CreatePartitions: host rather than guest.
 }
 public static bool HasNpcapCompatibility(){
  using var service=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npcap");
  return service is not null&&File.Exists(Path.Combine(Environment.SystemDirectory,"wpcap.dll"));
 }
 public async Task ValidateAsync(ApplicationDefinition app,CancellationToken token){
  if(app.Id=="sniffnet"&&!HasNpcapCompatibility())throw new InvalidOperationException("Sniffnet requires Npcap in WinPcap API-compatible mode. Install Npcap from https://npcap.com/#download and enable that option, then retry. The Npcap SDK is not required.");
  if(app.Id=="notepad"){
   var running=System.Diagnostics.Process.GetProcessesByName("notepad++");
   try{if(running.Length>0)throw new InvalidOperationException("Notepad++ is running. Save your open documents and close all Notepad++ windows before retrying. The installer will not close your editor automatically.");}
   finally{foreach(var process in running)process.Dispose();}
  }
  if(app.Id!="docker")return;
  using var product=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ProductOptions");
  if(product?.GetValue("ProductType") is string type&&type!="WinNT")throw new InvalidOperationException("Docker Desktop is not supported on Windows Server.");
  var memory=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};if(GlobalMemoryStatusEx(ref memory)&&memory.TotalPhysical<8UL*1024*1024*1024)throw new InvalidOperationException("Docker Desktop requires at least 8 GB of physical RAM.");
  var hyperVHost=IsHyperVHost();
  if(!hyperVHost&&!IsProcessorFeaturePresent(20))throw new InvalidOperationException("Docker Desktop requires a processor with Second Level Address Translation (SLAT).");
  using var server=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\LanmanServer");
  if(server?.GetValue("Start") is not int start||start!=2)throw new InvalidOperationException("Docker Desktop requires the Windows Server service (LanmanServer) enabled with Automatic startup. Configure it in Windows Services before retrying.");
  if(Environment.OSVersion.Version.Build is >=22000 and <22631)throw new InvalidOperationException("Docker Desktop requires Windows 11 23H2 or later.");
  if(Environment.OSVersion.Version.Build<19045)throw new InvalidOperationException("Docker Desktop requires a supported Windows build (Windows 10 22H2 or newer). Update Windows before retrying.");
  if(!hyperVHost&&!IsProcessorFeaturePresent(21))throw new InvalidOperationException("Docker Desktop requires hardware virtualization enabled in firmware. Enable it in BIOS/UEFI and restart before retrying.");
  var wsl=Path.Combine(Environment.SystemDirectory,"wsl.exe");if(!File.Exists(wsl))throw new InvalidOperationException("Docker Desktop requires WSL 2. Install WSL 2 and restart Windows before retrying.");
  try{var version=await ManagedApplications.CaptureAsync(wsl,["--version"],token);if(!Versions.AtLeast(version.Replace("\0",""),"2.1.5"))throw new IOException("WSL 2.1.5 or newer is required");await ManagedApplications.CaptureAsync(wsl,["--status"],token);}catch(IOException e){throw new InvalidOperationException("WSL 2.1.5 or newer is not ready. In an administrator terminal, run wsl --install --no-distribution (if missing), then wsl --update. Restart Windows and retry. Verify with wsl --version. See https://docs.docker.com/desktop/setup/install/windows-install/",e);}
 }
}
