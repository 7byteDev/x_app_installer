
using AppInstaller.Models;
namespace AppInstaller.Core;
public interface IApplicationProvider {ApplicationDefinition Definition {get;} Task<ReleaseInfo> ResolveAsync(bool includePrerelease,CancellationToken token);}
public interface IInstalledVersionDetector {Task<string?> DetectAsync(ApplicationDefinition app,CancellationToken token);}
public interface IDependencyResolver {IReadOnlyList<ApplicationDefinition> Resolve(IEnumerable<string> selected,IReadOnlyList<ApplicationDefinition> catalog,IReadOnlyDictionary<string,string?> installed);}
public interface IDownloadManager {Task<string> DownloadAsync(ApplicationDefinition app,ReleaseInfo release,string directory,IProgress<DownloadProgress> progress,CancellationToken token);}
public interface IIntegrityVerifier {Task VerifyAsync(ApplicationDefinition app,ReleaseInfo release,string path,bool signatures,CancellationToken token);}
public interface IInstaller {Task<int> InstallAsync(ApplicationDefinition app,string path,CancellationToken token);}
public interface ILogService {event Action<LogEntry>? Entry;void Write(string level,string message);}
public interface IInstallationQueue {Task<IReadOnlyList<InstallResult>> RunAsync(IReadOnlyList<ApplicationDefinition> order,InstallerSettings settings,IProgress<OperationProgress> progress,Func<InstallationFailure,Task<FailureAction>> failure,CancellationToken token);}
public static class Versions {
 public static Version? Parse(string? value) {if(value is null)return null;var normalized=System.Text.RegularExpressions.Regex.Replace(value.Replace(".windows.",".",StringComparison.OrdinalIgnoreCase),@"^(\d+\.\d+\.\d+)[+-](\d+)$","$1.$2");var m=System.Text.RegularExpressions.Regex.Match(normalized,@"\d+(?:\.\d+){1,3}");if(!m.Success)m=System.Text.RegularExpressions.Regex.Match(normalized,@"\d+");return m.Success&&Version.TryParse(m.Value.Contains('.')?m.Value:m.Value+".0",out var v)?new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision)):null;}
 public static bool Compatible(string? value,string? minimum=null,string? maximumExclusive=null)=>AtLeast(value,minimum)&&(maximumExclusive is null||Parse(value) is {} v&&Parse(maximumExclusive) is {} max&&v<max);
 public static bool AtLeast(string? installed,string? minimum)=>installed is not null&&(minimum is null||Parse(installed) is {} i&&Parse(minimum) is {} m&&i>=m);
}

public interface IPostInstallationVerifier { Task VerifyAsync(ApplicationDefinition app,string expectedVersion,CancellationToken token); }

public interface IInstallationPreflight {Task ValidateAsync(ApplicationDefinition app,CancellationToken token);}
