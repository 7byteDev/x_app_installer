using System.Net;
using System.Text.RegularExpressions;
using AppInstaller.Models;
namespace AppInstaller.Providers;
internal static class WinScpSource {
 private static readonly Uri Origin=new("https://winscp.net");
 public static async Task<ReleaseInfo> ResolveAsync(HttpClient http,CancellationToken token){
  string stage="release page";
  for(var attempt=0;attempt<3;attempt++){
   try{
    // WinSCP's file links contain short-lived download tickets. Always fetch a new
    // ticket for installation, and bypass intermediary caches on a recovery attempt.
    async Task<string> Page(Uri uri){
     using var request=new HttpRequestMessage(HttpMethod.Get,uri);
     request.Headers.CacheControl=new(){NoCache=true};
     using var reply=await http.SendAsync(request,token);reply.EnsureSuccessStatusCode();
     return await reply.Content.ReadAsStringAsync(token);
    }
    stage="release page";
    var suffix=attempt==0?"":"?xappinstaller="+Guid.NewGuid().ToString("N");
    var page=await Page(new(Origin,"/eng/download.php"+suffix));
    var detailsUri=Links(page).FirstOrDefault(u=>u.Host==Origin.Host&&Regex.IsMatch(u.AbsolutePath,@"^/download/WinSCP-[0-9.]+-Setup\.exe/download$",RegexOptions.IgnoreCase))
     ??throw new InvalidDataException("Installer link missing");
    var version=Regex.Match(detailsUri.AbsolutePath,@"WinSCP-([0-9.]+)-Setup",RegexOptions.IgnoreCase).Groups[1].Value;
    stage="download details";
    var details=await Page(attempt==0?detailsUri:new Uri(detailsUri+suffix));
    var filename=$"WinSCP-{version}-Setup.exe";
    var download=Links(details).FirstOrDefault(u=>u.Host==Origin.Host&&u.AbsolutePath.StartsWith("/download/files/",StringComparison.Ordinal)&&u.AbsolutePath.EndsWith("/"+filename,StringComparison.OrdinalIgnoreCase))
     ??throw new InvalidDataException("Fresh download link missing");
    var plain=WebUtility.HtmlDecode(Regex.Replace(details,"<[^>]+>"," "));
    var hash=Regex.Match(plain,@"SHA[\s-]*256\s*:\s*([a-f0-9]{64})",RegexOptions.IgnoreCase);
    if(!hash.Success)throw new InvalidDataException("Official SHA-256 missing");
    return new(version,download,filename,null,hash.Groups[1].Value);
   }catch(InvalidDataException)when(attempt<2){await Task.Delay(250,token);}
   catch(InvalidDataException e){throw new InvalidDataException($"WinSCP's official {stage} did not provide a complete installer link and checksum after 3 attempts. The site may be temporarily serving an incomplete or verification page. Try again shortly. ({e.Message})",e);}
  }
  throw new InvalidOperationException();
 }
 private static IEnumerable<Uri> Links(string html){
  foreach(Match match in Regex.Matches(html,"\\bhref\\s*=\\s*([\"'])(.*?)\\1",RegexOptions.IgnoreCase|RegexOptions.Singleline)){
   if(Uri.TryCreate(Origin,WebUtility.HtmlDecode(match.Groups[2].Value),out var uri)&&uri.Scheme=="https")yield return uri;
  }
 }
}
