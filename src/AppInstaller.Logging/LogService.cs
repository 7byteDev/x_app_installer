
using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.Logging;
public sealed class LogService:ILogService {
 public event Action<LogEntry>? Entry;
 public string? FilePath {get;set;}
 public string MinimumLevel {get;set;}="INFO";
 private readonly object gate=new();
 public void Write(string level,string message) {
  string[] levels=["DEBUG","INFO","SUCCESS","WARNING","ERROR"];
  if(Array.IndexOf(levels,level)<Array.IndexOf(levels,MinimumLevel))return;var entry=new LogEntry(DateTimeOffset.Now,level,message);
  lock(gate){if(FilePath is {} path){try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.AppendAllText(path,entry+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}}Entry?.Invoke(entry);
 }
}
