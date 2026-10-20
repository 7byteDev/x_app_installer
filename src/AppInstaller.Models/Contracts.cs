
namespace AppInstaller.Models;
public record Dependency(string Id,string? MinimumVersion=null,string? MaximumVersionExclusive=null,string? Reason=null);
public record VerificationCommand(string FilePath,string[] Arguments,bool CompareVersion=false,string? RelativePath=null);
public record ApplicationDefinition {
 public required string Id {get;init;}
 public required string Name {get;init;}
 public string[] Categories {get;init;}=[];
 public string Description {get;init;}="";
 public string Glyph {get;init;}="◇";
 public string Color {get;init;}="#148BFF";
 public string Homepage {get;init;}="";
 public string DependencyStatus {get;init;}="Unknown";
 public string DependencyNotes {get;init;}="Requirements have not been verified.";
 public string[] RequirementSources {get;init;}=[];
 public Dependency[] OptionalDependencies {get;init;}=[];
 public string? MinimumVersion {get;init;}
 public string? MaximumVersionExclusive {get;init;}
 public string? FeedUrl {get;init;}
 public bool Interactive {get;init;}
 public string InstallationNotes {get;init;}="";
 public string? PackageName {get;init;}
 public string? PackageEntryPoint {get;init;}
 public string? PortableVersionFile {get;init;}
 public string? PortableVersionPattern {get;init;}
 public string[] PortableVersionArguments {get;init;}=[];
 public string? PathSubdirectory {get;init;}
 public string? PortableExecutable {get;init;}
 public string? EnvironmentVariable {get;init;}
 public string[] DiscoveryPatterns {get;init;}=[];
 public string? DiscoveryExecutable {get;init;}
 public string[] DetectionFiles {get;init;}=[];
 public string? DetectionVersionPattern {get;init;}
 public string Source {get;init;}="";
 public string? Repository {get;init;}
 public string AssetPattern {get;init;}="";
 public string InstallerType {get;init;}="exe";
 public string SilentArguments {get;init;}="";
 public string RegistryPattern {get;init;}="";
 public string[] Architectures {get;init;}=["x64"];
 public Dependency[] Dependencies {get;init;}=[];
 public VerificationCommand[] VerificationCommands {get;init;}=[];
 public string[] DownloadHosts {get;init;}=[];
 public string? Publisher {get;init;}
 public bool RequiresAdministrator {get;init;}=true;
 public bool AllowUnsignedWithHash {get;init;}
 public bool RequireSignature {get;init;}
 public bool Enabled {get;init;}
 public string? UnavailableReason {get;init;}
}
public record ReleaseInfo(string Version,Uri DownloadUri,string FileName,long? Size,string? Sha256,string? Sha512=null,Dependency[]? Dependencies=null){public bool IsStale {get;init;}public string? SourceNotice {get;init;}public DateTimeOffset? CheckedAt {get;init;}}
public record DownloadProgress(long Received,long? Total,double BytesPerSecond) {
 public double Percent=>Total>0?100.0*Received/Total.Value:0;
 public long? Remaining=>Total-Received;
}
public enum InstallationState {Queued,Checking,Downloading,Verifying,Installing,Confirming,Succeeded,Failed,Skipped,AlreadyInstalled,Cancelled}
public enum FailureAction {Retry,Skip,Cancel}
public record OperationProgress(string Id,InstallationState State,string Message,double Overall,DownloadProgress? Download=null);
public record InstallResult(string Id,InstallationState State,string Message,int? ExitCode=null);
public record InstallationFailure(ApplicationDefinition App,InstallationState Step,string Message,int? ExitCode,string Resolution);
public record LogEntry(DateTimeOffset Time,string Level,string Message) {public override string ToString()=>$"[{Time:HH:mm:ss}] {Level,-7} {Message}";}
public class InstallerSettings {
 public Dictionary<string,string> ApplicationLocations {get;set;}=new();
 public string DownloadDirectory {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XAppInstaller","Downloads");
 public string TemporaryDirectory {get;set;}=Path.Combine(Path.GetTempPath(),"XAppInstaller");
 public bool DeleteInstallers {get;set;}=true;
 public bool CheckOnStartup {get;set;}=true;
 public bool IncludePrerelease {get;set;}
 public bool VerifySignatures {get;set;}=true;
 public bool CreateRestorePoint {get;set;}
 public bool ShutdownAfterSuccess {get;set;}
 public string LogLevel {get;set;}="INFO";
 public bool SaveLogs {get;set;}=true;
}
