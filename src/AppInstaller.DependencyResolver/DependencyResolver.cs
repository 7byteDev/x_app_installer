using AppInstaller.Core;
using AppInstaller.Models;
namespace AppInstaller.DependencyResolver;
public sealed class DependencyResolver:IDependencyResolver {
 public IReadOnlyList<ApplicationDefinition> Resolve(IEnumerable<string> selected,IReadOnlyList<ApplicationDefinition> catalog,IReadOnlyDictionary<string,string?> installed){
  var roots=selected.ToHashSet();var map=catalog.ToDictionary(a=>a.Id);var colors=new Dictionary<string,int>();var sorted=new List<string>();
  var minimum=new Dictionary<string,string?>();var maximum=new Dictionary<string,string?>();
  void Constrain(string id,string? min,string? max){
   if(!map.ContainsKey(id))throw new InvalidOperationException($"Unknown dependency: {id}.");
   minimum.TryGetValue(id,out var oldMin);maximum.TryGetValue(id,out var oldMax);
   if(min is not null&&(oldMin is null||Versions.Parse(min)>Versions.Parse(oldMin)))minimum[id]=min;
   if(max is not null&&(oldMax is null||Versions.Parse(max)<Versions.Parse(oldMax)))maximum[id]=max;
  }
  void Visit(string id){
   if(colors.TryGetValue(id,out var c)){if(c==1)throw new InvalidOperationException($"Circular dependency involving {id}.");return;}
   if(!map.TryGetValue(id,out var app))throw new InvalidOperationException($"Unknown dependency: {id}.");
   colors[id]=1;Constrain(id,app.MinimumVersion,app.MaximumVersionExclusive);
   foreach(var d in app.Dependencies){Constrain(d.Id,d.MinimumVersion,d.MaximumVersionExclusive);Visit(d.Id);}
   colors[id]=2;sorted.Add(id);
  }
  foreach(var id in roots)Visit(id);
  var result=new List<ApplicationDefinition>();
  foreach(var id in sorted){
   minimum.TryGetValue(id,out var min);maximum.TryGetValue(id,out var max);
   if(min is not null&&max is not null&&Versions.Parse(min)>=Versions.Parse(max))throw new InvalidOperationException($"Conflicting requirements for {map[id].Name}: minimum {min}, maximum exclusive {max}.");
   installed.TryGetValue(id,out var version);
   if(roots.Contains(id)||!Versions.Compatible(version,min,max))result.Add(map[id] with{MinimumVersion=min,MaximumVersionExclusive=max});
  }
  return result;
 }
}