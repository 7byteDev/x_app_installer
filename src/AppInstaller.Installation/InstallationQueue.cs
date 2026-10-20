
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Installation;
public sealed class InstallationQueue(Func<ApplicationDefinition,IApplicationProvider> providers,IInstalledVersionDetector detector,IDownloadManager downloads,IIntegrityVerifier verifier,IInstaller installer,ILogService log,IPostInstallationVerifier? postVerifier=null,IInstallationPreflight? preflight=null,IReadOnlyList<ApplicationDefinition>? catalog=null):IInstallationQueue {
 public async Task<IReadOnlyList<InstallResult>> RunAsync(IReadOnlyList<ApplicationDefinition> order,InstallerSettings settings,IProgress<OperationProgress> progress,Func<InstallationFailure,Task<FailureAction>> failure,CancellationToken token) {
  var results=new List<InstallResult>();var blocked=new HashSet<string>();
  for(int index=0;index<order.Count;index++) {
   var app=order[index];var state=InstallationState.Checking;
   void Report(InstallationState next,string message,DownloadProgress? download=null){state=next;progress.Report(new(app.Id,next,message,100.0*index/order.Count,download));}
   if(token.IsCancellationRequested){results.Add(new(app.Id,InstallationState.Cancelled,"Cancelled before start"));blocked.Add(app.Id);continue;}
   if(app.Dependencies.Any(d=>blocked.Contains(d.Id))){results.Add(new(app.Id,InstallationState.Skipped,"A dependency failed or was skipped"));blocked.Add(app.Id);continue;}
   while(true) {
    string? file=null;
    try {
     Report(InstallationState.Checking,$"Checking {app.Name}");log.Write("INFO",$"Checking {app.Name} release and installed version");
     var release=await providers(app).ResolveAsync(settings.IncludePrerelease,token);var installed=await detector.DetectAsync(app,token);
     if(!Versions.Compatible(release.Version,app.MinimumVersion,app.MaximumVersionExclusive))throw new InvalidOperationException("The official release does not satisfy the required version range.");
     foreach(var dep in release.Dependencies??app.Dependencies){
      var definition=(catalog??order).FirstOrDefault(d=>d.Id==dep.Id)??throw new InvalidOperationException($"Missing dependency definition: {dep.Id}");
      var constrained=definition with{MinimumVersion=dep.MinimumVersion,MaximumVersionExclusive=dep.MaximumVersionExclusive};
      var detected=await detector.DetectAsync(constrained,token);
      if(!Versions.Compatible(detected,dep.MinimumVersion,dep.MaximumVersionExclusive))throw new InvalidOperationException($"{app.Name} requires compatible {definition.Name}. Refresh the catalog and include its missing dependencies.");
     }
     if(release.IsStale)throw new InvalidOperationException("Latest release could not be confirmed. "+release.SourceNotice+" Refresh after the source recovers before installing.");
     if(Versions.AtLeast(installed,release.Version)){if((app.InstallerType is "zip" or "7z")&&ManagedApplications.Read(app.Id) is {} managed)ManagedApplications.ConfigurePortableEnvironment(app,managed.Root);if(postVerifier is not null)await postVerifier.VerifyAsync(app,release.Version,token);results.Add(new(app.Id,InstallationState.AlreadyInstalled,$"Version {installed} is current"));break;}
     if(preflight is not null)await preflight.ValidateAsync(app,token);
     Report(InstallationState.Downloading,$"Downloading {app.Name}");var directProgress=new InlineProgress<DownloadProgress>(p=>Report(InstallationState.Downloading,$"Downloading {app.Name}",p));
     file=await downloads.DownloadAsync(app,release,settings.DownloadDirectory,directProgress,token);
     file=InstallerProcessLauncher.ResolvePackagePath(file);
     using var packageLock=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
     Report(InstallationState.Verifying,$"Verifying {app.Name}");await verifier.VerifyAsync(app,release,file,settings.VerifySignatures,token);log.Write("SUCCESS",$"{app.Name}: integrity verified");
     Report(InstallationState.Installing,app.Interactive?$"Installing {app.Name} â€” complete the vendor setup window":$"Installing {app.Name} â€” administrator permission may be requested");var exit=await installer.InstallAsync(app,file,token);
     Report(InstallationState.Confirming,$"Verifying installed {app.Name}");installed=await detector.DetectAsync(app,CancellationToken.None);
     if(!Versions.AtLeast(installed,release.Version))throw new InvalidOperationException($"Post-install verification failed. Expected {release.Version}, detected {installed??"nothing"}. Check Windows installed apps.");
     if(postVerifier is not null)await postVerifier.VerifyAsync(app,release.Version,CancellationToken.None);
     results.Add(new(app.Id,InstallationState.Succeeded,exit==3010?"Installed; restart required":"Installed and verified",exit));log.Write("SUCCESS",$"{app.Name} {installed}: installed and verified");
     packageLock.Dispose();
     if(settings.DeleteInstallers){try{File.Delete(file);}catch(IOException e){log.Write("WARNING","Installed successfully; could not remove package: "+e.Message);}}break;
    }catch(OperationCanceledException)when(token.IsCancellationRequested){results.Add(new(app.Id,InstallationState.Cancelled,"Cancelled"));blocked.Add(app.Id);break;}
    catch(Exception e){
     if(state==InstallationState.Verifying&&file is not null){try{File.Delete(file);}catch(IOException){}}
     var code=(e as InstallerException)?.ExitCode;log.Write("ERROR",$"{app.Name} / {state}: {e.Message}");
     var resolution=e is InstallerLaunchException launch?launch.Resolution:state==InstallationState.Checking?"The prerequisite check stopped before download or installation. Resolve the requirement above, then choose Retry, or Skip to continue with other applications.":code switch{1619=>"Windows Installer could not open the package. Retry to obtain a fresh package and inspect the diagnostic log above for its resolved path.",1622=>"Windows Installer could not create its diagnostic log. Check that the temporary directory is accessible, then retry.",1603=>"Windows Installer failed. Close the application, check for a pending restart and inspect the vendor installation log before retrying.",1618=>"Another Windows installation is running. Let it finish, then choose Retry.",1638=>"Another version of this application is already installed. Review the installed version and the vendor upgrade instructions.",_=>"Check connectivity, disk space and Windows permissions. Retry or skip this application."};
     var action=await failure(new(app,state,e.Message,code,resolution));if(action==FailureAction.Retry)continue;
     results.Add(new(app.Id,action==FailureAction.Skip?InstallationState.Skipped:InstallationState.Failed,e.Message,code));blocked.Add(app.Id);
     if(action==FailureAction.Cancel){foreach(var rest in order.Skip(index+1))results.Add(new(rest.Id,InstallationState.Cancelled,"Queue cancelled"));return results;}break;
    }
   }
   var result=results.Last();progress.Report(new(app.Id,result.State,result.Message,100.0*(index+1)/order.Count));
  }
  return results;
 }
 private sealed class InlineProgress<T>(Action<T> action):IProgress<T>{public void Report(T value)=>action(value);}
}
