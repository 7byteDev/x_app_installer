
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppInstaller.Models;
using AppInstaller.Core;
namespace AppInstaller.UI.ViewModels;
public abstract class Observable:INotifyPropertyChanged {
 public event PropertyChangedEventHandler? PropertyChanged;
 protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
 protected bool Set<T>(ref T field,T value,[CallerMemberName]string? name=null){if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Changed(name);return true;}
}
public sealed class AppItem(ApplicationDefinition definition):Observable {
 public ApplicationDefinition Definition=>Release?.Dependencies is {} dependencies?definition with{Dependencies=dependencies}:definition;
 public string Id=>definition.Id;
 public string Name=>definition.Name;
 public string Category=>string.Join(" · ",definition.Categories);
 public Microsoft.UI.Xaml.Media.ImageSource Icon {get;}=definition.Id=="rea"
  ?new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri("ms-appx:///Assets/Apps/rea.svg"))
  :new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri($"ms-appx:///Assets/Apps/{definition.Id}.png"));
 public Microsoft.UI.Xaml.Media.SolidColorBrush Accent {get;}=new(Windows.UI.Color.FromArgb(255,Convert.ToByte(definition.Color.Substring(1,2),16),Convert.ToByte(definition.Color.Substring(3,2),16),Convert.ToByte(definition.Color.Substring(5,2),16)));
 private bool selected;public bool IsSelected {get=>selected;set{if(value&&!CanInstall&&!Automatic)return;Set(ref selected,value);}}
 private bool automatic;public bool Automatic {get=>automatic;set{Set(ref automatic,value);Changed(nameof(CanSelect));}}
 public bool IsLatestInstalled=>Release is {IsStale:false}&&Versions.AtLeast(Installed,Release.Version);
 public bool CanInstall=>definition.Enabled&&(!IsLatestInstalled||Status=="Needs dependencies");
 public bool CanSelect=>CanInstall&&!Automatic;
 private void UpdateEligibility(){Changed(nameof(IsLatestInstalled));Changed(nameof(CanInstall));Changed(nameof(CanSelect));if(IsLatestInstalled&&selected&&!Automatic&&Status!="Needs dependencies")IsSelected=false;}
 public Microsoft.UI.Xaml.Media.SolidColorBrush StatusBrush=>new(Status switch{
  "Latest installed"=>Windows.UI.Color.FromArgb(255,79,214,147),
  "Newer version available" or "Needs dependencies" or "Latest unconfirmed · cached" or "GitHub rate limited"=>Windows.UI.Color.FromArgb(255,255,208,92),
  "Not installed" or "Failed"=>Windows.UI.Color.FromArgb(255,255,107,107),
  _=>Windows.UI.Color.FromArgb(255,157,179,198)
 });
 public void UpdateDetectedStatus(){Status=Installed=="Not detected"?"Not installed":Release is null?"Installed · latest not checked":Release.IsStale?"Latest unconfirmed · cached":Installed=="Unknown"?"Installed · version unknown":IsLatestInstalled?"Latest installed":"Newer version available";}

 private string location="";public string Location {get=>location;set{Set(ref location,value);Changed(nameof(LocationVisibility));}}
 public Microsoft.UI.Xaml.Visibility LocationVisibility=>string.IsNullOrEmpty(Location)?Microsoft.UI.Xaml.Visibility.Collapsed:Microsoft.UI.Xaml.Visibility.Visible;
 private string sourceStatus="Latest release has not been checked.";public string SourceStatus {get=>sourceStatus;set=>Set(ref sourceStatus,value);}
 private string latest="Not checked";public string Latest {get=>latest;set=>Set(ref latest,value);}
 private string installed="Not detected";public string Installed {get=>installed;set{if(Set(ref installed,value))UpdateEligibility();}}
 private string status=definition.Enabled?"Ready to check":"Source unavailable";public string Status {get=>status;set{if(Set(ref status,value)){Changed(nameof(StatusBrush));UpdateEligibility();}}}
 private string reason="";public string SelectionReason {get=>reason;set{Set(ref reason,value);Changed(nameof(ReasonVisibility));}}
 public Microsoft.UI.Xaml.Visibility ReasonVisibility=>string.IsNullOrEmpty(SelectionReason)?Microsoft.UI.Xaml.Visibility.Collapsed:Microsoft.UI.Xaml.Visibility.Visible;
 private ReleaseInfo? release;public ReleaseInfo? Release {get=>release;set{release=value;Changed(nameof(Definition));Changed(nameof(PackageSize));UpdateEligibility();}}
 public string PackageSize=>Release?.Size is {} size?$"Download: {size/1048576.0:F1} MB":"Download size: determined before transfer";
}
public record CategoryItem(string Name,string Glyph,int Count) {
 public Microsoft.UI.Xaml.Media.Imaging.SvgImageSource Icon {get;}=new(new Uri("ms-appx:///Assets/Menu/"+(Name switch{"All Apps"=>"all-apps","Development"=>"development","AI & LLM"=>"ai-llm","Reverse Engineering"=>"reverse-engineering","System Tools"=>"system-tools","Network"=>"network","Multimedia"=>"multimedia","Compression"=>"compression",_=>"productivity"})+".svg"));
public override string ToString()=>$"{Glyph}    {Name}    {Count}";}
