using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Security;
public sealed class IntegrityVerifier:IIntegrityVerifier {
 public async Task VerifyAsync(ApplicationDefinition app,ReleaseInfo release,string path,bool signatures,CancellationToken token) {
  if(release.Sha256 is {} expected){await using var stream=File.OpenRead(path);var actual=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 mismatch. Package will not be executed.");}
  if(release.Sha512 is {} expected512){await using var stream=File.OpenRead(path);var actual=Convert.ToBase64String(await SHA512.HashDataAsync(stream,token));if(!actual.Equals(expected512,StringComparison.Ordinal))throw new InvalidDataException("SHA-512 mismatch. Package will not be executed.");}
  if(app.InstallerType is "zip" or "7z" or "npm"){
   if(release.Sha256 is null&&release.Sha512 is null)throw new InvalidDataException("This package has no authoritative integrity checksum.");
   return; // ZIP and npm tarballs do not support Authenticode; require cryptographic release hashes.
  }
  if(signatures||app.RequireSignature||release.Sha256 is null){
   await Task.Run(()=>NativeTrust.Verify(path,app.AllowUnsignedWithHash&&release.Sha256 is not null),token);
   if(app.Publisher is {} publisher){
#pragma warning disable SYSLIB0057
    using var cert=new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
    if(!cert.Subject.Contains(publisher,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unexpected publisher: "+cert.Subject);
   }
  }
 }
}
internal static class NativeTrust {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct FileInfo {public uint Size;[MarshalAs(UnmanagedType.LPWStr)]public string Path;public IntPtr File;public IntPtr Subject;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct TrustData {public uint Size;public IntPtr PolicyCallback;public IntPtr Sip;public uint Ui;public uint Revocation;public uint UnionChoice;public IntPtr File;public uint StateAction;public IntPtr State;public IntPtr Url;public uint Flags;public uint Context;public IntPtr SignatureSettings;}
 [DllImport("wintrust.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern int WinVerifyTrust(IntPtr hwnd,ref Guid action,ref TrustData data);
 public static void Verify(string path,bool allowUnsigned){
  var info=new FileInfo{Size=(uint)Marshal.SizeOf<FileInfo>(),Path=path};var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
  var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
  var data=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Revocation=1,UnionChoice=1,File=pointer,StateAction=1,Flags=0x80};
  try{Marshal.StructureToPtr(info,pointer,false);var result=WinVerifyTrust(new IntPtr(-1),ref action,ref data);if(result!=0&&!(allowUnsigned&&result==unchecked((int)0x800B0100)))throw new InvalidDataException($"Windows Authenticode trust verification failed (0x{result:X8}).");}
  finally{data.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref data);Marshal.DestroyStructure<FileInfo>(pointer);Marshal.FreeHGlobal(pointer);}
 }
}
