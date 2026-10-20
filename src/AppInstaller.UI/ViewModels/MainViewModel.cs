
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using AppInstaller.Core;
using AppInstaller.Models;
using AppInstaller.Providers;
using AppInstaller.Downloads;
using AppInstaller.Installation;
using AppInstaller.Security;
using AppInstaller.Logging;
using Microsoft.UI.Dispatching;
namespace AppInstaller.UI.ViewModels;
public sealed class MainViewModel:Observable,IDisposable {
 private readonly HttpClient http=new(new HttpClientHandler{AutomaticDecompression=System.Net.DecompressionMethods.All});private readonly HttpClient downloadHttp=TrustedUris.CreateClient();
 private readonly InstalledVersionDetector detector;private readonly AppInstaller.DependencyResolver.DependencyResolver resolver=new();
 private readonly LogService log=new();private readonly DispatcherQueue dispatcher;
 private readonly string settingsFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XAppInstaller","settings.json");
 private bool recomputing;private CancellationTokenSource? cancellation;
 public ObservableCollection<AppItem> Apps {get;}=[];
 public ObservableCollection<AppItem> FilteredApps {get;}=[];
 public ObservableCollection<CategoryItem> Categories {get;}=[];
 public ObservableCollection<ActivityLogItem> Logs {get;}=[];
 public InstallerSettings Settings {get;private set;}=new();
 public IReadOnlyList<InstallResult> LastResults {get;private set;}=[];
 public MainViewModel(DispatcherQueue queue) {
  dispatcher=queue;http.DefaultRequestHeaders.UserAgent.ParseAdd("XAppInstaller/1.0");http.Timeout=TimeSpan.FromSeconds(45);
  try{if(File.Exists(settingsFile))Settings=JsonSerializer.Deserialize<InstallerSettings>(File.ReadAllText(settingsFile))??new();}catch(Exception e){Logs.Add(new(new(DateTimeOffset.Now,"ERROR","Settings could not be loaded: "+e.Message)));}
  var definitions=Catalog.Load(Path.Combine(AppContext.BaseDirectory,"catalog","apps.json"));
  detector=new InstalledVersionDetector(a=>Settings.ApplicationLocations.TryGetValue(a.Id,out var path)?[path]:[],definitions);
  foreach(var app in definitions){var item=new AppItem(app);item.PropertyChanged+=SelectionChanged;Apps.Add(item);FilteredApps.Add(item);}
  string[] names=["All Apps","Development","AI & LLM","Reverse Engineering","System Tools","Network","Multimedia","Compression","Productivity"];
  string[] glyphs=["▦","◇","▧","⬡","⚙","◎","▷","▣","□"];
  for(int i=0;i<names.Length;i++)Categories.Add(new(names[i],glyphs[i],Apps.Count(a=>names[i]=="All Apps"||a.Definition.Categories.Contains(names[i]))));
  SelectedApp=Apps.First();log.Entry+=entry=>dispatcher.TryEnqueue(()=>{Logs.Add(new(entry));while(Logs.Count>1000)Logs.RemoveAt(0);});ConfigureLog();
 }
 private AppItem? selectedApp;public AppItem? SelectedApp {get=>selectedApp;set{Set(ref selectedApp,value);Changed(nameof(Description));Changed(nameof(Dependencies));}}
 public string Description=>SelectedApp?.Definition.Description??"";
 public string Dependencies {
  get{
   if(SelectedApp is null)return "Choose an application";var app=SelectedApp.Definition;
   if(app.DependencyStatus!="Verified")return "Requirements have not been verified. Check the official documentation before installing.";
   string Name(Dependency d)=>Apps.FirstOrDefault(a=>a.Id==d.Id)?.Name??d.Id;
   string Range(Dependency d)=>(d.MinimumVersion is null?"":$" ≥ {d.MinimumVersion}")+(d.MaximumVersionExclusive is null?"":$" and < {d.MaximumVersionExclusive}");
   var lines=app.Dependencies.Select(d=>$"Required: {Name(d)}{Range(d)}"+(string.IsNullOrEmpty(d.Reason)?"":$"\n{d.Reason}")).ToList();
   lines.AddRange(app.OptionalDependencies.Select(d=>$"Optional: {Name(d)}{Range(d)} — {d.Reason}"));
   if(lines.Count==0)lines.Add("No separate catalog applications required.");
   lines.Add(app.DependencyNotes);return string.Join("\n\n",lines);
  }
 }
 private bool busy;public bool Busy {get=>busy;set{Set(ref busy,value);Changed(nameof(Idle));}}
 public bool Idle=>!Busy;
 private string operation="Ready when you are";public string Operation {get=>operation;set=>Set(ref operation,value);}
 private string transfer="Select applications, then choose Install Selected.";public string Transfer {get=>transfer;set=>Set(ref transfer,value);}
 private double overall;public double Overall {get=>overall;set=>Set(ref overall,value);}
 private double current;public double Current {get=>current;set=>Set(ref current,value);}
 private bool indeterminate;public bool Indeterminate {get=>indeterminate;set=>Set(ref indeterminate,value);}
 public string AllLabel=>$"▶  Install All Available ({Apps.Count(a=>a.CanInstall)})";
 public string CatalogLabel=>$"{Apps.Count} applications · official sources";
 public string SelectionLabel=>$"Install Selected ({Apps.Count(a=>a.IsSelected)})";
 public void Filter(string category,string search="") {var previous=SelectedApp;FilteredApps.Clear();foreach(var a in Apps.Where(a=>(category=="All Apps"||a.Definition.Categories.Contains(category))&&(string.IsNullOrWhiteSpace(search)||a.Name.Contains(search,StringComparison.OrdinalIgnoreCase))))FilteredApps.Add(a);SelectedApp=previous is not null&&FilteredApps.Contains(previous)?previous:FilteredApps.FirstOrDefault();}
 private void SelectionChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(AppItem.CanInstall))Changed(nameof(AllLabel));if(e.PropertyName==nameof(AppItem.IsSelected)&&!recomputing)RecomputeSelection();}
 private void RecomputeSelection(){
  recomputing=true;
  try{
   var manual=Apps.Where(a=>a.IsSelected&&!a.Automatic).Select(a=>a.Id).ToArray();
   foreach(var a in Apps.Where(a=>a.Automatic)){a.IsSelected=false;a.Automatic=false;a.SelectionReason="";}
   var installed=Apps.ToDictionary(a=>a.Id,a=>a.Installed=="Not detected"?null:a.Installed);
   var order=resolver.Resolve(manual,Apps.Select(a=>a.Definition).ToArray(),installed);
   foreach(var def in order.Where(d=>!manual.Contains(d.Id))){var a=Apps.First(a=>a.Id==def.Id);a.Automatic=true;a.IsSelected=true;a.SelectionReason="Required by "+string.Join(", ",order.Where(p=>p.Dependencies.Any(d=>d.Id==a.Id)).Select(p=>p.Name));}
  }catch(Exception e){log.Write("ERROR",e.Message);}finally{recomputing=false;Changed(nameof(SelectionLabel));}
 }
 public void SelectAll(bool selected){recomputing=true;foreach(var a in Apps){a.Automatic=false;a.IsSelected=selected&&a.CanInstall;a.SelectionReason="";}recomputing=false;RecomputeSelection();}
 public async Task InitializeAsync(){if(Settings.CheckOnStartup)await RefreshAsync();else await DetectOnlyAsync();}
 private async Task DetectOnlyAsync(CancellationToken token=default){
  detector.BeginScan();
  foreach(var a in Apps){
   token.ThrowIfCancellationRequested();
   try{a.Installed=await detector.DetectAsync(a.Definition,token)??"Not detected";a.Location=detector.DetectedLocations.GetValueOrDefault(a.Id)??"";a.UpdateDetectedStatus();}
   catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
   catch(Exception e){log.Write("WARNING",a.Name+": "+e.Message);}
  }
 }
 public async Task RefreshAsync(){
  if(Busy)return;Busy=true;Operation="Scanning common installation folders and checking releases…";cancellation=new();var token=cancellation.Token;
  try{
   var scan=System.Diagnostics.Stopwatch.StartNew();await DetectOnlyAsync(token);log.Write("INFO",$"Local application scan finished in {scan.ElapsedMilliseconds} ms");Operation="Checking official releases…";
   foreach(var a in Apps){token.ThrowIfCancellationRequested();try{
   if(!a.Definition.Enabled)continue;a.Status="Checking…";a.Release=await new ApplicationProvider(a.Definition,http).ResolveAsync(Settings.IncludePrerelease,token);a.Latest=a.Release.Version;a.SourceStatus=a.Release.SourceNotice??$"Release checked {DateTimeOffset.Now:g}.";Changed(nameof(Dependencies));
   a.UpdateDetectedStatus();if(a.Release.IsStale){a.Latest+=" (cached)";a.Status=a.Installed=="Not detected"?"Not installed":"Latest unconfirmed · cached";log.Write("WARNING",$"{a.Name}: {a.SourceStatus}");}else log.Write("SUCCESS",$"{a.Name}: latest {a.Latest}; installed {a.Installed}");
  }catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch(Exception e){a.Release=null;a.Latest="Unavailable";a.SourceStatus=e.Message;a.Status=a.Installed=="Not detected"?"Not installed":e is GitHubRateLimitException?"GitHub rate limited":a.Installed=="Not detected"?"Latest unavailable":"Installed · latest unavailable";if(e is GitHubRateLimitException)a.Latest="Rate limited";log.Write(e is GitHubRateLimitException?"WARNING":"ERROR",$"{a.Name}: {e.Message}");}}
   var detected=Apps.ToDictionary(a=>a.Id,a=>a.Installed=="Not detected"?null:a.Installed);
   foreach(var a in Apps.Where(a=>a.Installed!="Not detected"&&a.Release is not null))if(resolver.Resolve([a.Id],Apps.Select(x=>x.Definition).ToArray(),detected).Any(d=>d.Id!=a.Id))a.Status="Needs dependencies";
   Operation="Catalog refreshed";RecomputeSelection();}
  catch(OperationCanceledException){Operation="Version check cancelled";}finally{Busy=false;cancellation.Dispose();cancellation=null;}
 }
 public async Task InstallAsync(bool all,Func<InstallationFailure,Task<FailureAction>> failure){
  if(Busy)return;if(all)SelectAll(true);var selected=Apps.Where(a=>a.IsSelected).Select(a=>a.Id).ToArray();if(selected.Length==0){Operation="Select at least one available application";return;}
  Busy=true;cancellation=new();LastResults=[];
  try{
   if(Settings.CreateRestorePoint)throw new NotSupportedException("Restore-point creation is unavailable. Disable that setting to continue.");
   await DetectOnlyAsync();
   var order=resolver.Resolve(selected,Apps.Select(a=>a.Definition).ToArray(),Apps.ToDictionary(a=>a.Id,a=>a.Installed=="Not detected"?null:a.Installed));
   if(order.Any(a=>!a.Enabled))throw new NotSupportedException("The queue includes a provider that is not implemented.");
   var queue=new InstallationQueue(a=>new ApplicationProvider(a,http),detector,new DownloadManager(downloadHttp),new IntegrityVerifier(),new WindowsInstaller(),log,new CommandVerifier(),new WindowsPrerequisites(),Apps.Select(a=>a.Definition).ToArray());
   var progress=new Progress<OperationProgress>(p=>{
    Operation=p.Message;Overall=p.Overall;var app=Apps.First(a=>a.Id==p.Id);app.Status=p.State.ToString();
    Indeterminate=p.State is InstallationState.Installing or InstallationState.Verifying or InstallationState.Checking or InstallationState.Confirming;
    if(p.Download is {} d){Current=d.Percent;Transfer=$"{d.Received/1048576.0:F1} MB / {(d.Total is {} t?$"{t/1048576.0:F1}":"?")} MB  ·  {d.BytesPerSecond/1048576:F1} MB/s  ·  {d.Percent:F0}%  ·  {(d.Remaining is {} r?$"{r/1048576.0:F1} MB remaining":"size unknown")}";}
    else Transfer=$"{app.Name} · {p.State}";
   });
   LastResults=await queue.RunAsync(order,Settings,progress,failure,cancellation.Token);
   Operation=$"Finished · {LastResults.Count(r=>r.State==InstallationState.Succeeded)} successful · {LastResults.Count(r=>r.State==InstallationState.Failed)} failed · {LastResults.Count(r=>r.State==InstallationState.Skipped)} skipped · {LastResults.Count(r=>r.State==InstallationState.AlreadyInstalled)} already installed · {LastResults.Count(r=>r.State==InstallationState.Cancelled)} cancelled";
   foreach(var result in LastResults)log.Write(result.State==InstallationState.Failed?"ERROR":"INFO",$"{result.Id}: {result.Message}");
   foreach(var a in Apps.Where(a=>a.Definition.Enabled)){a.Installed=await detector.DetectAsync(a.Definition,CancellationToken.None)??"Not detected";a.Location=detector.DetectedLocations.GetValueOrDefault(a.Id)??"";a.UpdateDetectedStatus();}
   if(Settings.ShutdownAfterSuccess&&LastResults.Count>0&&LastResults.All(r=>r.State is InstallationState.Succeeded or InstallationState.AlreadyInstalled)){Operation+=" · Shutdown scheduled in 60 seconds";System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe","/s /t 60"){CreateNoWindow=true,UseShellExecute=false});}
  }catch(Exception e){Operation=e.Message;log.Write("ERROR",e.Message);}finally{Busy=false;Indeterminate=false;cancellation.Dispose();cancellation=null;}
 }
 public void Cancel(){cancellation?.Cancel();Operation="Cancellation requested — an active installer will finish safely";}
 private void ConfigureLog(){log.MinimumLevel=Settings.LogLevel;log.FilePath=Settings.SaveLogs?Path.Combine(Path.GetDirectoryName(settingsFile)!,"Logs",DateTime.Now.ToString("yyyy-MM-dd")+".log"):null;}
 public void SaveSettings(){Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);File.WriteAllText(settingsFile,JsonSerializer.Serialize(Settings,new JsonSerializerOptions{WriteIndented=true}));ConfigureLog();}
 public void Dispose(){cancellation?.Cancel();http.Dispose();downloadHttp.Dispose();}
}
