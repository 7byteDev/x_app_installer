
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AppInstaller.UI.ViewModels;
using AppInstaller.Models;
namespace AppInstaller.UI;
public sealed partial class MainWindow:Window {
 public MainViewModel ViewModel {get;}
 private bool initialized;
 public MainWindow(){
  InitializeComponent();AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","XAppInstaller.ico"));ViewModel=new(DispatcherQueue);Root.DataContext=ViewModel;
  ExtendsContentIntoTitleBar=false; // Windows owns a separate caption row, including DPI-aware caption button space.
  AppWindow.TitleBar.BackgroundColor=Windows.UI.Color.FromArgb(255,13,21,29);
  AppWindow.TitleBar.ForegroundColor=Windows.UI.Color.FromArgb(255,193,210,225);
  var area=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
  AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(1536,area.Width-20),Math.Min(1024,area.Height-20)));
  AppWindow.Move(new Windows.Graphics.PointInt32(area.X+10,area.Y+10));
  AppWindow.TitleBar.ButtonBackgroundColor=Windows.UI.Color.FromArgb(255,13,21,29);
  AppWindow.TitleBar.ButtonForegroundColor=Windows.UI.Color.FromArgb(255,193,210,225);
  if(AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter){presenter.PreferredMinimumWidth=Math.Min(1180,area.Width);presenter.PreferredMinimumHeight=Math.Min(740,area.Height);}
  SystemBackdrop=new Microsoft.UI.Xaml.Media.MicaBackdrop();
  Root.Loaded+=async(_,_)=>{if(initialized)return;initialized=true;await ViewModel.InitializeAsync();
   if(Environment.GetCommandLineArgs().Contains("--status-smoke-test"))File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"ui-status-tests.txt"),UiSmokeTests.RunStatus(ViewModel));
   if(Environment.GetCommandLineArgs().Contains("--activity-smoke-test"))File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"ui-activity-tests.txt"),UiSmokeTests.RunActivity(ViewModel));
   if(Environment.GetCommandLineArgs().Contains("--smoke-test")){
    var checks=UiSmokeTests.Run(ViewModel);
    File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"ui-smoke-tests.txt"),checks);
    ViewModel.Operation="Catalog refreshed";
   }};
  ViewModel.Logs.CollectionChanged+=(_,_)=>{if(ViewModel.Logs.Count>0)LogList.ScrollIntoView(ViewModel.Logs.Last());};
  Closed+=(_,_)=>ViewModel.Dispose();
  AppWindow.Closing+=(_,e)=>{if(ViewModel.Busy){e.Cancel=true;ViewModel.Cancel();}};
 }
 private void CopyActivity_Click(object sender,RoutedEventArgs e){
  try{var data=new Windows.ApplicationModel.DataTransfer.DataPackage();data.SetText(string.Join(Environment.NewLine,ViewModel.Logs.Select(item=>item.Text)));Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);Windows.ApplicationModel.DataTransfer.Clipboard.Flush();}
  catch(Exception error){ViewModel.Operation="Could not copy activity: "+error.Message;}
 }
 private async void Refresh_Click(object sender,RoutedEventArgs e)=>await ViewModel.RefreshAsync();
 private void Category_Changed(object sender,SelectionChangedEventArgs e){if(Root?.DataContext is MainViewModel vm)vm.Filter((CategoryList.SelectedItem as CategoryItem)?.Name??"All Apps",SearchBox?.Text??"");}
 private void Search_Changed(object sender,TextChangedEventArgs e){if(Root?.DataContext is MainViewModel vm)vm.Filter((CategoryList.SelectedItem as CategoryItem)?.Name??"All Apps",SearchBox.Text);}
 private void SelectAll_Click(object sender,RoutedEventArgs e)=>ViewModel.SelectAll(true);
 private void DeselectAll_Click(object sender,RoutedEventArgs e)=>ViewModel.SelectAll(false);
 private void Cancel_Click(object sender,RoutedEventArgs e)=>ViewModel.Cancel();
 private async void InstallSelected_Click(object sender,RoutedEventArgs e)=>await ViewModel.InstallAsync(false,ShowFailureAsync);
 private async void InstallAll_Click(object sender,RoutedEventArgs e)=>await ViewModel.InstallAsync(true,ShowFailureAsync);
 private async void Official_Click(object sender,RoutedEventArgs e){if(Uri.TryCreate(ViewModel.SelectedApp?.Definition.Homepage,UriKind.Absolute,out var uri)&&uri.Scheme=="https")await Windows.System.Launcher.LaunchUriAsync(uri);}
 private async Task<FailureAction> ShowFailureAsync(InstallationFailure failure){
  var dialog=new ContentDialog{XamlRoot=Root.XamlRoot,RequestedTheme=ElementTheme.Dark,Title=$"{failure.App.Name} — {failure.Step}",Content=$"{failure.Message}\n\n{(failure.ExitCode is {} code?$"Installer exit code: {code}":failure.Step==InstallationState.Checking?"Installation has not started.":"No installer exit code was returned.")}\n\n{failure.Resolution}",PrimaryButtonText="Retry",SecondaryButtonText="Skip",CloseButtonText="Cancel remaining"};
  return await dialog.ShowAsync() switch {ContentDialogResult.Primary=>FailureAction.Retry,ContentDialogResult.Secondary=>FailureAction.Skip,_=>FailureAction.Cancel};
 }
 private async void Settings_Click(object sender,RoutedEventArgs e){
  var s=ViewModel.Settings;
  var downloads=new TextBox{Header="Download directory",Text=s.DownloadDirectory};
  var temp=new TextBox{Header="Temporary directory",Text=s.TemporaryDirectory};
  CheckBox Box(string label,bool value)=>new(){Content=label,IsChecked=value};
  var delete=Box("Delete installers after successful installation",s.DeleteInstallers);
  var startup=Box("Check latest releases at startup",s.CheckOnStartup);
  var preview=Box("Include preview releases where supported by the source",s.IncludePrerelease);
  var signatures=Box("Verify digital signatures (required for protected installers)",s.VerifySignatures);
  var restore=Box("Create restore point (not supported on this build)",false);restore.IsEnabled=false;
  var shutdown=Box("Shut down after all applications succeed",s.ShutdownAfterSuccess);
  var saveLogs=Box("Save activity logs to disk",s.SaveLogs);
  var level=new ComboBox{Header="Logging level",ItemsSource=new[]{"DEBUG","INFO","SUCCESS","WARNING","ERROR"},SelectedItem=s.LogLevel,HorizontalAlignment=HorizontalAlignment.Stretch};
  var panel=new StackPanel{Spacing=12,MinWidth=460};foreach(var child in new UIElement[]{downloads,temp,delete,startup,preview,signatures,restore,shutdown,saveLogs,level})panel.Children.Add(child);
  var dialog=new ContentDialog{XamlRoot=Root.XamlRoot,RequestedTheme=ElementTheme.Dark,Title="Settings",Content=new ScrollViewer{Content=panel,MaxHeight=600},PrimaryButtonText="Save",CloseButtonText="Cancel"};
  if(await dialog.ShowAsync()==ContentDialogResult.Primary){
   if(!Path.IsPathFullyQualified(downloads.Text)||!Path.IsPathFullyQualified(temp.Text)){ViewModel.Operation="Settings not saved: use absolute directory paths.";return;}
   s.DownloadDirectory=downloads.Text;s.TemporaryDirectory=temp.Text;s.DeleteInstallers=delete.IsChecked==true;s.CheckOnStartup=startup.IsChecked==true;s.IncludePrerelease=preview.IsChecked==true;s.VerifySignatures=signatures.IsChecked==true;s.CreateRestorePoint=false;s.ShutdownAfterSuccess=shutdown.IsChecked==true;s.SaveLogs=saveLogs.IsChecked==true;s.LogLevel=level.SelectedItem as string??"INFO";
   try{ViewModel.SaveSettings();await ViewModel.RefreshAsync();}catch(Exception error){ViewModel.Operation="Could not save settings: "+error.Message;}
  }
 }
}
