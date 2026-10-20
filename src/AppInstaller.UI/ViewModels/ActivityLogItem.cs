using AppInstaller.Models;
using Microsoft.UI.Xaml.Media;
namespace AppInstaller.UI.ViewModels;
public sealed class ActivityLogItem(LogEntry entry){
 public string Text=>entry.ToString();
 public string Level=>entry.Level;
 public SolidColorBrush Foreground {get;}=new(entry.Level switch{
  "SUCCESS"=>Windows.UI.Color.FromArgb(255,79,214,147),
  "WARNING"=>Windows.UI.Color.FromArgb(255,255,208,92),
  "ERROR"=>Windows.UI.Color.FromArgb(255,255,107,107),
  _=>Windows.UI.Color.FromArgb(255,184,202,218)
 });
 public override string ToString()=>Text;
}