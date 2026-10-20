
using AppInstaller.Models;
using AppInstaller.Core;
using AppInstaller.DependencyResolver;
using AppInstaller.Providers;
using AppInstaller.Downloads;
using AppInstaller.Security;
using AppInstaller.Installation;
using AppInstaller.Logging;
using System.Security.Cryptography;
if(args.Contains("--launch-probe"))return;
var root=args.FirstOrDefault(a=>!a.StartsWith("--"))??Environment.CurrentDirectory;
var catalog=Catalog.Load(Path.Combine(root,"catalog","apps.json"));
if(args.Contains("--docker-preflight")){
 Console.WriteLine($"Hyper-V host partition: {WindowsPrerequisites.IsHyperVHost()}");
 try{await new WindowsPrerequisites().ValidateAsync(catalog.Single(a=>a.Id=="docker"),CancellationToken.None);Console.WriteLine("Docker prerequisites passed.");}
 catch(InvalidOperationException e){Console.WriteLine("Docker prerequisite: "+e.Message);}
 return;
}
if(args.Contains("--detection-diagnostics")){
 var detector=new InstalledVersionDetector(discoveryCatalog:catalog);
 foreach(var id in new[]{"python","ffmpeg"}){var app=catalog.Single(a=>a.Id==id);Console.WriteLine($"{id}: version={await detector.DetectAsync(app,CancellationToken.None)}, root={detector.DetectedLocations.GetValueOrDefault(id)}");}
 var older=catalog.Single(a=>a.Id=="python") with{RegistryPattern=@"^Python 3\.14\.7 \(64-bit\)$"};
 Console.WriteLine("Python with only older registry match: "+await detector.DetectAsync(older,CancellationToken.None));
 return;
}
int passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
var resolver=new DependencyResolver();
ApplicationDefinition Def(string id,params Dependency[] deps)=>new(){Id=id,Name=id,Dependencies=deps,Enabled=true};
var graph=new[]{Def("a",new Dependency("b","2.0")),Def("b",new Dependency("c")),Def("c")};
var empty=new Dictionary<string,string?>();
Check(resolver.Resolve(["a"],graph,empty).Select(a=>a.Id).SequenceEqual(["c","b","a"]),"Transitive dependency order");
Check(resolver.Resolve(["a","b"],graph,empty).Count==3,"Shared dependencies deduplicated");
Check(resolver.Resolve(["a"],graph,new Dictionary<string,string?>{{"b","2.1"},{"c","1.0"}}).Count==1,"Compatible installed dependency omitted");
Check(resolver.Resolve(["a"],graph,new Dictionary<string,string?>{{"b","1.0"}}).Count==3,"Incompatible dependency upgraded");
try{resolver.Resolve(["a"],[Def("a",new Dependency("b")),Def("b",new Dependency("a"))],empty);throw new Exception("Cycle accepted");}catch(InvalidOperationException){Check(true,"Circular dependency rejected");}
try{resolver.Resolve(["a"],[Def("a",new Dependency("missing"))],empty);throw new Exception("Missing dependency accepted");}catch(InvalidOperationException){Check(true,"Unknown dependency rejected");}
Check(catalog.Count==30&&catalog.Count(a=>a.Enabled)==30,"30 catalog entries, all enabled");
Check(Versions.AtLeast("7-Zip 26.04 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-10-05","26.04"),"7-Zip product-name digit does not mask its version");
Check(!Versions.AtLeast("7-Zip 26.00 (x64)","26.04"),"Older 7-Zip version is rejected");
Check(Versions.AtLeast("openjdk 27 2026-09-15","27.0.0"),"Java feature-only version output is recognized");
Check(Versions.AtLeast("2.50.1.windows.1","2.49.0"),"Git version normalization");
Check(catalog.All(a=>a.DependencyStatus=="Verified"&&a.RequirementSources.Length>0),"All 30 dependency definitions have audit sources");
var reaOrder=resolver.Resolve(["rea"],catalog,empty);
var reaIds=reaOrder.Select(a=>a.Id).ToArray();
Check(reaIds.Contains("node")&&reaIds.Contains("java")&&reaIds.Contains("ghidra")&&reaIds.Last()=="rea","REA includes Node, Java and Ghidra");
Check(Array.IndexOf(reaIds,"java")<Array.IndexOf(reaIds,"ghidra")&&Array.IndexOf(reaIds,"ghidra")<Array.IndexOf(reaIds,"rea"),"Java installs before Ghidra before REA");
Check(reaOrder.Single(a=>a.Id=="ghidra").MaximumVersionExclusive=="12.2","REA constrains Ghidra to its compatible release series");
Check(resolver.Resolve(["a"],[Def("a",new Dependency("b","1.0","2.0")),Def("b")],new Dictionary<string,string?>{{"b","2.5"}}).Any(a=>a.Id=="b"),"Too-new installed dependency is not accepted");
Check(resolver.Resolve(["a"],graph,new Dictionary<string,string?>{{"b","2.1"}}).Any(a=>a.Id=="c"),"Transitive prerequisites are checked even when their parent is installed");
try{resolver.Resolve(["a","d"],[Def("a",new Dependency("b","3.0")),Def("d",new Dependency("b",null,"2.0")),Def("b")],empty);throw new Exception("Conflicting requirements accepted");}catch(InvalidOperationException){Check(true,"Conflicting dependency version ranges rejected");}
Check(!Versions.AtLeast("1.1.8+9","1.1.8+10"),"Bionic build revision compared");
Check(catalog.Single(a=>a.Id=="bionic").Dependencies.Length==0,"Bionic does not force an unrelated LM Studio installation");
Check(catalog.Single(a=>a.Id=="hermes").Dependencies.Length==0&&catalog.Single(a=>a.Id=="hermes").DependencyNotes.Contains("manages"),"Hermes managed runtime is distinguished from system prerequisites");
Check(UserEnvironment.MergePath(@"C:\Tools;C:\ffmpeg\bin",@"c:\FFMPEG\bin\").Split(';').Length==2,"PATH additions deduplicate case and trailing separators");
try{PortableInstaller.SafeDestination(Path.GetTempPath(),"../escape.exe");throw new Exception("Zip slip accepted");}catch(InvalidDataException){Check(true,"ZIP path traversal rejected");}
try{PortableInstaller.SafeDestination(Path.GetTempPath(),"bin/evil.exe:stream");throw new Exception("NTFS stream accepted");}catch(InvalidDataException){Check(true,"ZIP alternate data streams rejected");}

foreach(var uri in new[]{"http://nodejs.org/a","https://evil.example/a","https://nodejs.org.evil.example/a"}){
 try{TrustedUris.Validate(new(uri),catalog.First(a=>a.Id=="node"));throw new Exception("Untrusted URL accepted");}catch(InvalidDataException){Check(true,"Untrusted URL rejected: "+uri);}
}

if(args.FirstOrDefault(a=>a.StartsWith("--ghidra-root=")) is {} ghidraRoot){
 var detector=new InstalledVersionDetector(a=>a.Id=="ghidra"?[ghidraRoot[14..]]:[]);
 var found=await detector.DetectAsync(catalog.Single(a=>a.Id=="ghidra"),CancellationToken.None);
 Check(found is not null,"Configured external portable Ghidra is detected: "+found);
}

if(args.Contains("--discovery")){
 var detector=new InstalledVersionDetector();var timer=System.Diagnostics.Stopwatch.StartNew();
 foreach(var id in new[]{"java","ghidra"}){
  var installed=await detector.DetectAsync(catalog.Single(a=>a.Id==id),CancellationToken.None);
  Check(installed is not null,$"Automatic discovery: {id} {installed} at {detector.DetectedLocations.GetValueOrDefault(id)}");
 }
 Console.WriteLine($"Discovery elapsed: {timer.ElapsedMilliseconds} ms");
 Check(timer.Elapsed<TimeSpan.FromSeconds(6),"JDK and Ghidra common-folder scan completes within the time budget");
}
var temp=Path.Combine(Path.GetTempPath(),"XAppInstaller-check-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try{
 var file=Path.Combine(temp,"sample.bin");await File.WriteAllTextAsync(file,"integrity fixture");
 
 
 
 var probeDirectory=Path.Combine(temp,"launch probe ü space");Directory.CreateDirectory(probeDirectory);
 foreach(var source in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(source,Path.Combine(probeDirectory,Path.GetFileName(source)));
 var probe=Path.Combine(probeDirectory,"AppInstaller.Checks.exe");
 var probeApp=Def("launch-probe") with{InstallerType="exe",RequiresAdministrator=false,SilentArguments="--launch-probe"};
 var start=InstallerProcessLauncher.CreateStartInfo(probeApp,probe);
 Check(start.WorkingDirectory==Environment.SystemDirectory&&string.IsNullOrEmpty(start.Verb),"EXE launch uses a stable working directory and vendor-managed elevation");
 using(var packageLock=new FileStream(probe,FileMode.Open,FileAccess.Read,FileShare.Read)){
  var exit=await new WindowsInstaller().InstallAsync(probeApp,probe,CancellationToken.None);
  Check(exit==0,"Real harmless EXE launches from a Unicode/spaced path while the package is locked");
 }
 var redirectedFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XAppInstaller","Checks");Directory.CreateDirectory(redirectedFolder);
 var redirectedFile=Path.Combine(redirectedFolder,Guid.NewGuid().ToString("N")+".msi");
 try{
  File.WriteAllText(redirectedFile,"path fixture");
  var physical=InstallerProcessLauncher.ResolvePackagePath(redirectedFile);
  Check(Path.IsPathFullyQualified(physical)&&File.ReadAllText(physical)=="path fixture","Virtualized LocalAppData resolves to the physical package");
  var redirectedStart=InstallerProcessLauncher.CreateStartInfo(probeApp with{InstallerType="msi"},redirectedFile);
  Check(redirectedStart.Arguments.Contains("\""+physical+"\"")&&redirectedStart.Arguments.Contains("/L*v"),"MSI handoff uses the physical path and diagnostic logging");
  var exeStart=InstallerProcessLauncher.CreateStartInfo(probeApp,redirectedFile);
  Check(exeStart.FileName==physical,"EXE handoff uses the physical path");
 }finally{File.Delete(redirectedFile);}
 var msi=InstallerProcessLauncher.CreateStartInfo(probeApp with{InstallerType="msi",RequiresAdministrator=true},probe);
 Check(msi.Verb=="runas"&&msi.Arguments.StartsWith("/i \""),"MSI retains explicit UAC elevation and quoted package path");
 try{InstallerProcessLauncher.CreateStartInfo(probeApp,Path.Combine(temp,"missing.exe"));throw new Exception("Missing installer accepted");}catch(InstallerLaunchException e){Check(e.NativeErrorCode==2&&e.Resolution.Contains("Retry"),"Missing package produces an actionable launch error");}
 var portableId="test-"+Guid.NewGuid().ToString("N");
 var portableApp=Def(portableId) with{InstallerType="zip",RequiresAdministrator=false,PortableExecutable="run.bin",PortableVersionFile="version.txt",PortableVersionPattern=@"version=(\d+\.\d+)"};
 var archiveFile=Path.Combine(temp,"fixture.zip");
 using(var archive=System.IO.Compression.ZipFile.Open(archiveFile,System.IO.Compression.ZipArchiveMode.Create)){
  using(var writer=new StreamWriter(archive.CreateEntry("app/run.bin").Open()))writer.Write("Non-executable fixture");
  using(var writer=new StreamWriter(archive.CreateEntry("app/version.txt").Open()))writer.Write("version=2.1");
 }
 try{
  await new WindowsInstaller().InstallAsync(portableApp,archiveFile,CancellationToken.None);
  var portableVersion=await new InstalledVersionDetector().DetectAsync(portableApp,CancellationToken.None);
  Check(portableVersion=="2.1","Portable ZIP extraction, managed receipt and installed-version detection");
 }finally{
  var owned=Path.Combine(ManagedApplications.ToolsDirectory,portableId);
  if(!Path.GetFullPath(owned).StartsWith(Path.GetFullPath(ManagedApplications.ToolsDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture cleanup path");
  if(Directory.Exists(owned))Directory.Delete(owned,true);File.Delete(ManagedApplications.ReceiptPath(portableId));
 }
 var sha512=Convert.ToBase64String(SHA512.HashData(await File.ReadAllBytesAsync(file)));
 var npmFixture=Def("npmhash") with{InstallerType="npm"};
 var npmRelease=new ReleaseInfo("1.0",new("https://example.com/fixture.tgz"),"fixture.tgz",null,null,sha512);
 await new IntegrityVerifier().VerifyAsync(npmFixture,npmRelease,file,true,CancellationToken.None);Check(true,"npm SHA-512 integrity accepted without Authenticode");
 try{await new IntegrityVerifier().VerifyAsync(npmFixture,npmRelease with{Sha512=Convert.ToBase64String(new byte[64])},file,true,CancellationToken.None);throw new Exception("Bad npm integrity accepted");}catch(InvalidDataException){Check(true,"npm SHA-512 mismatch rejected");}
var app=Def("hash");var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file)));var release=new ReleaseInfo("1.0",new("https://example.com/sample.bin"),"sample.bin",null,hash);
 var verifier=new IntegrityVerifier();await verifier.VerifyAsync(app,release,file,false,CancellationToken.None);Check(true,"Valid SHA-256 accepted");
 try{await verifier.VerifyAsync(app,release with{Sha256=new string('0',64)},file,false,CancellationToken.None);throw new Exception("Invalid hash accepted");}catch(InvalidDataException){Check(true,"Hash mismatch rejected");}
 var installed=new FakeDetector();var installer=new FakeInstaller(installed);var queue=new InstallationQueue(a=>new FakeProvider(a),installed,new FakeDownload(file),new FakeVerifier(),installer,new LogService());
 var results=await queue.RunAsync([Def("a"),Def("b",new Dependency("a"))],new(){DeleteInstallers=false},new Progress<OperationProgress>(),_=>Task.FromResult(FailureAction.Skip),CancellationToken.None);
 Check(results.All(r=>r.State==InstallationState.Succeeded)&&installer.Order.SequenceEqual(["a","b"]),"Sequential queue and post-install verification");
 installer.Fail=true;installed.Versions.Clear();results=await queue.RunAsync([Def("a"),Def("b",new Dependency("a"))],new(){DeleteInstallers=false},new Progress<OperationProgress>(),_=>Task.FromResult(FailureAction.Skip),CancellationToken.None);
 Check(results[0].State==InstallationState.Skipped&&results[1].State==InstallationState.Skipped,"Skipped failed dependency is reported as skipped and blocks dependent");
 using var cancel=new CancellationTokenSource();cancel.Cancel();results=await queue.RunAsync([Def("a")],new(){DeleteInstallers=false},new Progress<OperationProgress>(),_=>Task.FromResult(FailureAction.Skip),cancel.Token);Check(results[0].State==InstallationState.Cancelled,"Cancelled queue never starts installer");
 
 Check(Versions.AtLeast("1.2","1.2.0"),"Missing version components normalize to zero");
 Check(!Versions.AtLeast("2.56.0.1","2.56.0.windows.2"),"Git Windows patch revision is compared");
 var payload=System.Text.Encoding.UTF8.GetBytes("download resume fixture");
 var fixtureApp=Def("fixture") with{DownloadHosts=["fixture.test"]};
 var fixtureRelease=new ReleaseInfo("1.0",new("https://fixture.test/package.bin"),"package.bin",payload.Length,Convert.ToHexString(SHA256.HashData(payload)));
 await File.WriteAllBytesAsync(Path.Combine(temp,"fixture-package.bin.part"),payload[..5]);
 var ranged=false;
 using(var client=new HttpClient(new FixtureHandler(request=>{
  ranged=request.Headers.Range?.Ranges.Single().From==5;
  var response=new HttpResponseMessage(System.Net.HttpStatusCode.PartialContent){Content=new ByteArrayContent(payload[5..])};
  response.Content.Headers.ContentRange=new System.Net.Http.Headers.ContentRangeHeaderValue(5,payload.Length-1,payload.Length);return response;
 }))){
  var path=await new DownloadManager(client).DownloadAsync(fixtureApp,fixtureRelease,temp,new Progress<DownloadProgress>(),CancellationToken.None);
  Check(ranged&&(await File.ReadAllBytesAsync(path)).SequenceEqual(payload),"Partial download resumes with validated Content-Range");
 }
 using(var client=new HttpClient(new FixtureHandler(_=>new HttpResponseMessage(System.Net.HttpStatusCode.Redirect){Headers={Location=new Uri("https://untrusted.test/evil.exe")}}))){
  try{await new DownloadManager(client).DownloadAsync(fixtureApp,fixtureRelease,temp,new Progress<DownloadProgress>(),CancellationToken.None);throw new Exception("Unsafe redirect accepted");}catch(InvalidDataException){Check(true,"Untrusted redirect is rejected before following");}
 }
 installed.Versions.Clear();installer.Fail=false;
 var executionsBefore=installer.Order.Count;
 queue=new InstallationQueue(a=>new FakeProvider(a),installed,new FakeDownload(file),new RejectVerifier(),installer,new LogService());
 results=await queue.RunAsync([Def("reject")],new(){DeleteInstallers=false},new Progress<OperationProgress>(),_=>Task.FromResult(FailureAction.Skip),CancellationToken.None);
 Check(results.Single().State==InstallationState.Skipped&&installer.Order.Count==executionsBefore,"Integrity failure prevents installer execution");
 var legacyStore=Path.Combine(temp,"legacy-package-store");var legacyRoot=Path.Combine(legacyStore,"Tools","legacy-ffmpeg","9.0.2");Directory.CreateDirectory(legacyRoot);Directory.CreateDirectory(Path.Combine(legacyStore,"Installed"));
 await File.WriteAllTextAsync(Path.Combine(legacyStore,"Installed","legacy-ffmpeg.json"),System.Text.Json.JsonSerializer.Serialize(new ManagedInstallation("legacy-ffmpeg",Path.Combine(ManagedApplications.ToolsDirectory,"legacy-ffmpeg","9.0.2"),"9.0.2")));
 var migrated=ManagedApplications.Read("legacy-ffmpeg",[legacyStore]);Check(migrated?.Root==legacyRoot&&migrated.Version=="9.0.2","Legacy package receipt resolves to its physical installation outside the package context");
 await File.WriteAllTextAsync(Path.Combine(legacyStore,"Installed","escape.json"),System.Text.Json.JsonSerializer.Serialize(new ManagedInstallation("escape",temp,"1.0")));
 Check(ManagedApplications.Read("escape",[legacyStore]) is null,"Legacy receipt cannot reference an unrelated folder");
 Check(catalog.Single(a=>a.Id=="sniffnet").DependencyNotes.Contains("Npcap")&&!catalog.Single(a=>a.Id=="sniffnet").Dependencies.Any(d=>d.Id=="wireshark"),"Sniffnet requires Npcap without falsely requiring Wireshark");
 Check(catalog.Single(a=>a.Id=="wireshark").Interactive,"Wireshark uses interactive setup to include Npcap");
 Check(catalog.Single(a=>a.Id=="dbeaver").Dependencies.Length==0,"DBeaver bundled Java does not force a system JDK");
 var winScpRequests=0;var ticketRequests=0;
 using(var client=new HttpClient(new FixtureHandler(request=>{
  winScpRequests++;
  var body=request.RequestUri!.AbsolutePath=="/eng/download.php"
   ?(winScpRequests==1?"<html>Temporary verification page</html>":"<a href = 'https://winscp.net/download/WinSCP-6.7-Setup.exe/download'>Setup</a>")
   :$"<a href='/download/files/ticket{++ticketRequests}/WinSCP-6.7-Setup.exe'>Download</a><p>SHA-<span>256</span>: {new string('a',64)}</p>";
  return new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(body)};
 }))){
  var provider=new ApplicationProvider(catalog.Single(a=>a.Id=="winscp"),client);
  var resolved=await provider.ResolveAsync(false,CancellationToken.None);
  Check(resolved.Version=="6.7"&&resolved.Sha256==new string('a',64)&&winScpRequests==3,"WinSCP recovers from an incomplete page and parses absolute/single-quoted links and checksum markup");
  var nextTicket=await provider.ResolveAsync(false,CancellationToken.None);
  Check(nextTicket.DownloadUri!=resolved.DownloadUri&&ticketRequests==2,"WinSCP installation resolution always requests a fresh download ticket");
 }
 Check(PortableInstaller.MpvBuildVersion("mpv-mpv-x86_64-20261020-git-abcdef1234.7z")=="2026.10.20","mpv tracks the Windows distribution build date rather than its unrelated engine version");
 try{PortableInstaller.MpvBuildVersion("mpv-x86_64-20260230-git-abcdef.7z");throw new Exception("Invalid date accepted");}catch(InvalidDataException){Check(true,"Invalid mpv build dates are rejected");}
 Check(catalog.Single(a=>a.Id=="mpv").Dependencies.Length==0&&catalog.Single(a=>a.Id=="mpv").InstallerType=="7z","mpv portable extraction does not force installed 7-Zip or FFmpeg");
 var cacheClock=new FixtureClock(DateTimeOffset.UtcNow);
 var releaseCache=new ReleaseMetadataCache(Path.Combine(temp,"release-cache"),cacheClock);
 var cacheApp=Def("cache") with{Source="github"};var fetches=0;
 Task<ReleaseInfo> FetchRelease(){fetches++;return Task.FromResult(new ReleaseInfo("2.0",new("https://github.com/example/releases/installer.exe"),"installer.exe",42,new string('a',64),null,[new("java","21.0")]));}
 var firstCached=await releaseCache.ResolveAsync(cacheApp,false,FetchRelease,CancellationToken.None);
 var secondCached=await new ReleaseMetadataCache(Path.Combine(temp,"release-cache"),cacheClock).ResolveAsync(cacheApp,false,FetchRelease,CancellationToken.None);
 Check(fetches==1&&secondCached.Sha256==firstCached.Sha256&&secondCached.Dependencies?.Single().MinimumVersion=="21.0","Release cache persists integrity and dependencies and avoids repeated requests");
 cacheClock.Advance(TimeSpan.FromMinutes(16));
 var stale=await releaseCache.ResolveAsync(cacheApp,false,()=>throw new GitHubRateLimitException(DateTimeOffset.UtcNow.AddMinutes(5)),CancellationToken.None);
 Check(stale.IsStale&&stale.SourceNotice!.Contains("Latest version could not be confirmed"),"Rate limit falls back to explicitly stale cached metadata");
 await releaseCache.ResolveAsync(cacheApp with{MinimumVersion="3.0"},false,FetchRelease,CancellationToken.None);
 Check(fetches==2,"Different dependency constraints do not share a release cache entry");
 using(var alreadyCancelled=new CancellationTokenSource()){
  alreadyCancelled.Cancel();try{await releaseCache.ResolveAsync(cacheApp,false,FetchRelease,alreadyCancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Cached release resolution respects cancellation");}
 }
 var github=new GitHubReleaseClient();var githubRequests=0;var expectedRetry=DateTimeOffset.UtcNow.AddMinutes(5);
 using(var client=new HttpClient(new FixtureHandler(_=>{
  githubRequests++;var response=new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden){Content=new StringContent("API rate limit exceeded")};
  response.Headers.Add("X-RateLimit-Remaining","0");response.Headers.Add("X-RateLimit-Reset",expectedRetry.ToUnixTimeSeconds().ToString());return response;
 }))){
  for(var attempt=0;attempt<2;attempt++)try{await github.GetStringAsync(client,"https://api.github.com/repos/example/app/releases",CancellationToken.None);throw new Exception("Rate limit ignored");}catch(GitHubRateLimitException e){Check(Math.Abs((e.RetryAt-expectedRetry).TotalSeconds)<2,"GitHub reset header is preserved");}
  Check(githubRequests==1,"GitHub backoff prevents repeated requests while rate limited");
 }
 using(var client=new HttpClient(new FixtureHandler(_=>new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden){Content=new StringContent("Access denied")}))){
  try{await new GitHubReleaseClient().GetStringAsync(client,"https://api.github.com/repos/example/app/releases",CancellationToken.None);throw new Exception("403 ignored");}
  catch(HttpRequestException e){Check(e is not GitHubRateLimitException&&e.StatusCode==System.Net.HttpStatusCode.Forbidden,"Unrelated HTTP 403 remains an access error");}
 }
 installed.Versions.Clear();var countBeforeStale=installer.Order.Count;
 queue=new InstallationQueue(a=>new StaleProvider(a),installed,new FakeDownload(file),new FakeVerifier(),installer,new LogService());
 results=await queue.RunAsync([Def("stale")],new(){DeleteInstallers=false},new Progress<OperationProgress>(),_=>Task.FromResult(FailureAction.Skip),CancellationToken.None);
 Check(installer.Order.Count==countBeforeStale&&results.Single().Message.Contains("Latest release could not be confirmed"),"Stale release metadata cannot start a new installation");
 if(args.Contains("--network")){
  using var http=new HttpClient(new HttpClientHandler{AutomaticDecompression=System.Net.DecompressionMethods.All});http.DefaultRequestHeaders.UserAgent.ParseAdd("XAppInstaller-Checks/1.0");http.Timeout=TimeSpan.FromSeconds(30);
  using var downloadHttp=TrustedUris.CreateClient();
  foreach(var definition in catalog.Where(a=>a.Enabled&&(args.FirstOrDefault(arg=>arg.StartsWith("--only=")) is not {} selectedOnly||selectedOnly[7..].Split(',').Contains(a.Id)))){
   var live=await new ApplicationProvider(definition,http).ResolveAsync(false,CancellationToken.None);Check(!string.IsNullOrEmpty(live.Version)&&live.DownloadUri.Scheme=="https",definition.Name+" live release: "+live.Version);Console.WriteLine(live.DownloadUri);if(definition.Id=="ghidra")Check(live.Dependencies?.Single().MinimumVersion=="21.0","Ghidra JDK requirement read from its exact release properties");if(definition.Id=="python")Check(live.Sha256?.Length==64,"Python official SHA-256 parsed across HTML checksum spans");
   if(args.Contains("--download")&&(args.FirstOrDefault(a=>a.StartsWith("--only=")) is not {} only||only[7..].Split(',').Contains(definition.Id))){
    var path=await new DownloadManager(downloadHttp).DownloadAsync(definition,live,temp,new Progress<DownloadProgress>(),CancellationToken.None);
    await verifier.VerifyAsync(definition,live,path,true,CancellationToken.None);Check(true,definition.Name+" real download and integrity verification");
    if(definition.Id=="mpv"){
     var extract=Path.Combine(temp,"mpv-extracted");Directory.CreateDirectory(extract);
     await PortableInstaller.ExtractSevenZipAsync(path,extract,CancellationToken.None);
     Check(File.Exists(Path.Combine(extract,"mpv.exe")),"Real mpv 7z archive extracts with the expected executable");
     var version=await ManagedApplications.InspectRootAsync(definition,extract,CancellationToken.None,true);
     Check(Versions.Parse(version) is not null,"Extracted mpv --version runs successfully without installing or changing PATH: "+version);
     Check(PortableInstaller.MpvBuildVersion(path)==live.Version,"mpv installed receipt build date matches the release metadata");
    }
   }
  }
 }
}finally{Directory.Delete(temp,true);}
Console.WriteLine($"{passed} checks passed. No real installers executed.");
sealed class FakeDetector:IInstalledVersionDetector {public Dictionary<string,string> Versions=new();public Task<string?> DetectAsync(ApplicationDefinition a,CancellationToken t)=>Task.FromResult(Versions.GetValueOrDefault(a.Id));}
sealed class FakeProvider(ApplicationDefinition app):IApplicationProvider {public ApplicationDefinition Definition=>app;public Task<ReleaseInfo> ResolveAsync(bool p,CancellationToken t)=>Task.FromResult(new ReleaseInfo("1.0",new("https://example.com/test"),"test",null,null));}
sealed class FakeDownload(string file):IDownloadManager {public Task<string> DownloadAsync(ApplicationDefinition a,ReleaseInfo r,string d,IProgress<DownloadProgress> p,CancellationToken t)=>Task.FromResult(file);}
sealed class FakeVerifier:IIntegrityVerifier {public Task VerifyAsync(ApplicationDefinition a,ReleaseInfo r,string p,bool s,CancellationToken t)=>Task.CompletedTask;}
sealed class FakeInstaller(FakeDetector detector):IInstaller {public List<string> Order=[];public bool Fail;public Task<int> InstallAsync(ApplicationDefinition a,string p,CancellationToken t){if(Fail)throw new IOException("Test failure");Order.Add(a.Id);detector.Versions[a.Id]="1.0";return Task.FromResult(0);}}


sealed class FixtureHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(response(request));}
sealed class RejectVerifier:IIntegrityVerifier {public Task VerifyAsync(ApplicationDefinition a,ReleaseInfo r,string p,bool s,CancellationToken t)=>throw new InvalidDataException("Fixture rejected");}

sealed class FixtureClock(DateTimeOffset now):TimeProvider {private DateTimeOffset current=now;public override DateTimeOffset GetUtcNow()=>current;public void Advance(TimeSpan interval)=>current+=interval;}
sealed class StaleProvider(ApplicationDefinition app):IApplicationProvider {public ApplicationDefinition Definition=>app;public Task<ReleaseInfo> ResolveAsync(bool p,CancellationToken t)=>Task.FromResult(new ReleaseInfo("1.0",new("https://example.com/test"),"test",null,null){IsStale=true,SourceNotice="Fixture is stale"});}