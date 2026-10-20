using Microsoft.UI.Xaml;
namespace AppInstaller.UI;
public partial class App:Application {
 private Window? window;
 public App(){InitializeComponent();UnhandledException+=(_,e)=>File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"last-error.txt"),e.Exception.ToString());}
 protected override void OnLaunched(LaunchActivatedEventArgs args){window=new MainWindow();window.Activate();}
}
