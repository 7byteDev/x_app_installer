using System.Reflection;
namespace AppInstaller.UI;
internal static class AppVersionInfo {
 private static readonly Assembly Assembly=typeof(AppVersionInfo).Assembly;
 public static string Build {get;}=Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"Unknown";
 public static string Version=>Build.Split('+')[0];
 public static string Label=>"v"+Version;
 public static string Details=>$"X App Installer {Version}\nBuild: {Build}\nDeveloper: Özgür Ulusoy\nWebsite: https://7byte.dev";
}
