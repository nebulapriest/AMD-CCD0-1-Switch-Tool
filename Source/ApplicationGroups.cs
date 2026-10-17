using System;using System.IO;using System.Linq;
public class ApplicationGroup {public string Id="",Name="";public string[] Members=new string[0];}
public static class ApplicationGroups {
 public static Settings Current;
 public const string Codex="app:chatgpt-codex";
 public static bool IsCodex(string path){
  if(string.IsNullOrEmpty(path))return false;
  try{var full=Path.GetFullPath(path);var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(full.StartsWith(Path.Combine(local,"OpenAI","Codex")+"\\",StringComparison.OrdinalIgnoreCase))return true;
   var packages=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps")+"\\";
   if(!full.StartsWith(packages,StringComparison.OrdinalIgnoreCase))return false;
   var package=full.Substring(packages.Length).Split('\\')[0];
   return package.StartsWith("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)&&package.EndsWith("__2p2nqsd0c76g0",StringComparison.OrdinalIgnoreCase);
  }catch{return false;}
 }
 public static bool IsGroup(string path){return path==Codex||path.StartsWith("appgroup:",StringComparison.Ordinal);}
 public static bool Contains(Settings config,string group,string path){if(group==path)return true;if(group==Codex)return IsCodex(path);var g=config.Groups.FirstOrDefault(x=>x.Id==group);return g!=null&&g.Members.Any(x=>x.Equals(path,StringComparison.OrdinalIgnoreCase));}
 public static string Key(string path){if(IsCodex(path))return Codex;var g=Current==null?null:Current.Groups.FirstOrDefault(x=>x.Members.Any(m=>m.Equals(path,StringComparison.OrdinalIgnoreCase)));return g==null?path:g.Id;}
 public static string Name(string path){var g=Current==null?null:Current.Groups.FirstOrDefault(x=>x.Id==path);return path==Codex?"ChatGPT / Codex · 全部组件":g!=null?g.Name+" · 应用组":Path.GetFileNameWithoutExtension(path);}
 public static Rule Find(Settings config,string path){return config.Rules.FirstOrDefault(r=>r.Path.Equals(path,StringComparison.OrdinalIgnoreCase))??config.Rules.FirstOrDefault(r=>IsGroup(r.Path)&&Contains(config,r.Path,path));}
}
