using AppInstaller.UI.ViewModels;
namespace AppInstaller.UI;
internal static class UiSmokeTests {
 public static IReadOnlyList<string> RunStatus(MainViewModel vm){
  var checks=new List<string>();void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks.Add(message);}
  var app=new AppItem(new(){Id="status-fixture",Name="Status fixture",Enabled=true});
  app.Installed="1.0";app.Release=new("2.0",new("https://example.com/fixture"),"fixture",null,null);app.UpdateDetectedStatus();
  Check(app.Status=="Newer version available"&&app.StatusBrush.Color.G==208&&app.CanSelect,"Newer release is yellow and selectable");
  app.IsSelected=true;app.Installed="2.0";app.UpdateDetectedStatus();
  Check(app.Status=="Latest installed"&&app.StatusBrush.Color.G==214&&!app.CanSelect&&!app.IsSelected,"Latest installed is green, deselected and disabled");
  app.IsSelected=true;Check(!app.IsSelected,"Current app cannot be manually selected");
  app.Installed="Not detected";app.UpdateDetectedStatus();
  Check(app.Status=="Not installed"&&app.StatusBrush.Color.R==255&&app.StatusBrush.Color.G==107&&app.CanSelect,"Not installed is red and selectable");
  app.Installed="3.0";app.UpdateDetectedStatus();Check(!app.CanSelect,"Installed version newer than official stays disabled");
  app.Release=app.Release with{IsStale=true};app.UpdateDetectedStatus();Check(app.CanSelect&&app.Status!="Latest installed","Stale release does not imply latest installed");
  app.Release=null;app.UpdateDetectedStatus();Check(app.CanSelect&&app.Status!="Latest installed","Unavailable release does not imply latest installed");
  vm.SelectAll(true);Check(vm.Apps.All(a=>a.CanInstall||!a.IsSelected||a.Automatic),"Select All excludes current applications");vm.SelectAll(false);
  return checks;
 }
 public static IReadOnlyList<string> RunActivity(MainViewModel vm){
  var checks=new List<string>();
  foreach(var item in new[]{("SUCCESS",Microsoft.UI.Colors.MediumSeaGreen), ("WARNING",Microsoft.UI.Colors.Gold), ("ERROR",Microsoft.UI.Colors.IndianRed)}){
   var entry=new ActivityLogItem(new(DateTimeOffset.Now,item.Item1,$"Activity display test: {item.Item1}"));
   var color=entry.Foreground.Color;
   var correct=item.Item1 switch{"SUCCESS"=>color.G>color.R&&color.G>color.B,"WARNING"=>color.R>200&&color.G>150&&color.B<150,_=>color.R>200&&color.G<150&&color.B<150};
   if(!correct||!entry.Text.Contains(item.Item1))throw new InvalidOperationException("Activity formatting failed: "+item.Item1);
   vm.Logs.Add(entry);checks.Add(item.Item1+" color and copyable text passed");
  }
  return checks;
 }
 public static IReadOnlyList<string> Run(MainViewModel vm){
  var checks=new List<string>();void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
  vm.SelectAll(true);Check(vm.Apps.All(a=>a.CanInstall||!a.IsSelected||a.Automatic),"Only eligible applications can be selected");
  vm.SelectAll(false);Check(vm.Apps.All(a=>!a.IsSelected),"Deselect All clears selection");
  var java=vm.Apps.Single(a=>a.Id=="java");var savedJava=java.Installed;java.Installed="Not detected";java.IsSelected=true;Check(java.IsSelected&&java.CanSelect,"Java can be selected directly");vm.SelectAll(false);java.Installed=savedJava;
  var original=vm.Apps.ToDictionary(a=>a.Id,a=>a.Installed);foreach(var a in vm.Apps)a.Installed="Not detected";
  vm.Apps.Single(a=>a.Id=="rea").IsSelected=true;
  Check(new[]{"node","java","ghidra"}.All(id=>vm.Apps.Single(a=>a.Id==id).IsSelected),"Selecting REA adds Node, Java and Ghidra");
  Check(new[]{"node","java","ghidra"}.All(id=>!vm.Apps.Single(a=>a.Id==id).CanSelect),"Required dependencies are locked against deselection");
  java.IsSelected=false;Check(java.IsSelected,"A required Java dependency is reselected if changed programmatically");
  vm.Apps.Single(a=>a.Id=="rea").IsSelected=false;Check(vm.Apps.All(a=>!a.IsSelected),"Deselecting REA releases its automatic prerequisites");
  foreach(var a in vm.Apps)a.Installed=original[a.Id];vm.SelectedApp=vm.Apps.Single(a=>a.Id=="rea");
  Check(vm.Dependencies.Contains("Java")&&vm.Dependencies.Contains("Ghidra")&&vm.Dependencies.Contains("12.2"),"REA details show required apps and version limits");
  vm.Filter("AI & LLM");Check(vm.FilteredApps.Count==5,"Category filtering works");
  vm.Filter("All Apps","node");Check(vm.FilteredApps.Count==1,"Search works");vm.Filter("All Apps");
  Check(vm.Apps.All(a=>a.Release is not null),"All 30 release sources loaded");vm.SelectedApp=vm.Apps.First();return checks;
 }
}
