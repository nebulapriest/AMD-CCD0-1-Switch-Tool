using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
public class OriginalState {
 public int Id; public long Creation; public string Path="",Role=""; public uint Priority;public bool RestorePriority;public ulong Affinity;public uint[] CpuSets=new uint[0];[System.Xml.Serialization.XmlIgnore]public string Failure="";public string Key{get{return Id+"|"+Creation;}}
}
public class IsolationJournal {public string Topology="";public List<OriginalState> Items=new List<OriginalState>();}
public class Grant {public Rule Rule;public string SourcePath="",Reason="";public bool Explicit,Inherited;public string[] Lineage=new string[0];}
public class ProcessView {public Entry Process;public Grant Grant;public string Status="";}
public class CycleResult {public List<ProcessView> Processes=new List<ProcessView>();public List<string> Messages=new List<string>();public int PendingRecovery;}
public class RuleResolver {
 Dictionary<string,Grant> leases=new Dictionary<string,Grant>();
 static string Key(Entry e){return e.Id+"|"+e.Creation;}
 public static bool Ordinary(Entry e,int session,string self,string windows){return e.Creation>0&&e.Path!=""&&e.Session==session&&e.Session!=0&&e.Id!=Process.GetCurrentProcess().Id&&!e.Path.Equals(self,StringComparison.OrdinalIgnoreCase)&&!e.Path.StartsWith(windows.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
 static Rule RootRule(string path,Settings settings){var rule=ApplicationGroups.Find(settings,path);if(rule!=null)return rule.Enabled?rule:null;if(settings.DefaultExclusions.Any(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase)))return new Rule{Path=path,Preset="All",IncludeChildren=true};return null;}
 public Dictionary<int,Grant> Resolve(List<Entry> processes,Settings settings,int session,string self,string windows){
  var byPid=processes.ToDictionary(e=>e.Id);var result=new Dictionary<int,Grant>();var nextLeases=new Dictionary<string,Grant>();
  Func<Entry,HashSet<int>,Grant> visit=null;visit=(e,stack)=>{
   Grant done;if(result.TryGetValue(e.Id,out done))return done;if(!stack.Add(e.Id))return new Grant{Reason="父进程关系异常"};
   var direct=ApplicationGroups.Find(settings,e.Path);var rule=RootRule(e.Path,settings);Grant selected=null;
   if(e.Id==Process.GetCurrentProcess().Id||e.Path.Equals(self,StringComparison.OrdinalIgnoreCase)||e.Session!=session||e.Creation<=0)selected=new Grant{Reason="控制器 / 其他会话 / 身份不可读"};
   else if(rule!=null)selected=new Grant{Rule=rule,SourcePath=e.Path,Reason="主动配置",Explicit=true,Lineage=new[]{e.Path}};
   else if(direct==null){Entry parent;bool canUseLease=true;if(byPid.TryGetValue(e.Parent,out parent)&&parent.Creation>0&&parent.Creation<=e.Creation){canUseLease=false;var inherited=visit(parent,new HashSet<int>(stack));if(inherited.Rule!=null&&inherited.Rule.IncludeChildren)selected=new Grant{Rule=inherited.Rule,SourcePath=inherited.SourcePath,Reason="继承 "+Path.GetFileName(inherited.SourcePath),Inherited=true,Lineage=inherited.Lineage.Concat(new[]{e.Path}).ToArray()};}
    if(selected==null&&canUseLease){Grant lease;if(leases.TryGetValue(Key(e),out lease)){Rule retained=null;string source="";bool blocked=false;foreach(var ancestor in lease.Lineage.Take(Math.Max(0,lease.Lineage.Length-1))){var explicitAncestor=ApplicationGroups.Find(settings,ancestor);if(explicitAncestor!=null&&(!explicitAncestor.Enabled||!explicitAncestor.IncludeChildren)){blocked=true;break;}var grant=RootRule(ancestor,settings);if(grant!=null&&grant.IncludeChildren){retained=grant;source=ancestor;}}if(!blocked&&retained!=null)selected=new Grant{Rule=retained,SourcePath=source,Reason="继承已确认的启动链 · "+Path.GetFileName(source),Inherited=true,Lineage=lease.Lineage};}}
   }
   if(selected==null)selected=Ordinary(e,session,self,windows)?new Grant{Rule=new Rule{Path=e.Path,Preset=CcdDiscovery.Default(settings),Priority="Keep",IncludeChildren=false},Reason="默认 "+CcdDiscovery.Default(settings)}:new Grant{Reason="系统 / 服务 / 不可读进程"};
   result[e.Id]=selected;if(selected.Inherited)nextLeases[Key(e)]=selected;return selected;
  };
  foreach(var e in processes)visit(e,new HashSet<int>());leases=nextLeases;return result;
 }
}
public class SchedulerEngine {
 readonly List<Cpu> cpus;readonly IsolationRecovery recovery;readonly RuleResolver resolver=new RuleResolver();readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly object sync=new object();Settings settings;Dictionary<string,string> states=new Dictionary<string,string>();volatile bool enabled;int revision;
 public bool Enabled{get{return enabled;}}
 public SchedulerEngine(List<Cpu> cpu,Settings config,string journal){cpus=cpu;settings=AppStore.Clone(config);recovery=new IsolationRecovery(journal,AppStore.Fingerprint(cpu));enabled=config.IsolationEnabled;}
 public void Configure(Settings config){lock(sync){settings=AppStore.Clone(config);enabled=config.IsolationEnabled;revision++;}}
 public static void Validate(Settings config,List<Cpu> cpus){if(!config.CcdVerified||config.VerifiedFingerprint!=AppStore.Fingerprint(cpus))throw new Exception("本机 CCD 映射尚未核对。");if(cpus.Any(c=>c.Group!=0))throw new Exception("当前版本仅支持单处理器组。");var groups=config.CcdGroups.Count>0?config.CcdGroups.Select(g=>g.Cores).ToArray():new[]{config.CCD0,config.CCD1};if(groups.Length==0||groups.Any(g=>g.Length==0)||groups.SelectMany(g=>g).Distinct().Count()!=cpus.Count||!groups.SelectMany(g=>g).OrderBy(x=>x).SequenceEqual(cpus.Select(c=>c.Key).OrderBy(x=>x)))throw new Exception("CCD 分组必须覆盖全部核心且不重叠。");}
 public static Rule Materialize(Grant grant,Entry target,Settings config,List<Cpu> cpus){var r=grant.Rule;string[] cores=r.Preset=="Custom"?r.Cores:CcdDiscovery.Keys(config,r.Preset,cpus);return new Rule{Path=target.Path,Mode="Hard",Preset=r.Preset,Cores=cores.ToArray(),Priority=r.Priority,IncludeChildren=r.IncludeChildren};}
 public static List<Entry> Snapshot(){var parents=Native.Parents();var list=new List<Entry>();foreach(var process in Process.GetProcesses()){using(process){try{var e=new Entry{Id=process.Id,Name=process.ProcessName,Path=Native.PathOf(process.Id),Parent=parents.ContainsKey(process.Id)?parents[process.Id]:0};try{e.Session=process.SessionId;e.Creation=process.StartTime.ToUniversalTime().ToFileTimeUtc();}catch{e.Session=-1;}e.Product=e.Name;list.Add(e);}catch{}}}return list;}
 public async Task<CycleResult> CycleAsync(Func<List<Entry>> provider=null){if(!await gate.WaitAsync(0))return null;try{return await Task.Run(()=>{
  Settings config;int version;lock(sync){config=AppStore.Clone(settings);version=revision;}var result=new CycleResult();recovery.RecoverStartup(result.Messages);var processes=provider==null?Snapshot():provider();var current=resolver.Resolve(processes,config,Process.GetCurrentProcess().SessionId,Native.PathOf(Process.GetCurrentProcess().Id),Environment.GetFolderPath(Environment.SpecialFolder.Windows));var keep=new HashSet<string>();var next=new Dictionary<string,string>();
  if(enabled)Validate(config,cpus);
  foreach(var e in processes){var decision=current[e.Id];var view=new ProcessView{Process=e,Grant=decision,Status=enabled?decision.Reason:"已暂停"};if(enabled&&version==revision&&decision.Rule!=null){string key=e.Id+"|"+e.Creation;try{var r=Materialize(decision,e,config,cpus);recovery.Capture(e,decision.Reason,r.Priority!="Keep");keep.Add(key);Native.Apply(e,r,cpus,r.Cores);view.Status=decision.Reason+" · "+(r.Preset=="All"?"全部 CPU":r.Preset)+" · 已校验";}catch(Exception ex){view.Status=decision.Reason+" · 失败："+ex.Message;}next[key]=view.Status;if(!states.ContainsKey(key)||states[key]!=view.Status)result.Messages.Add(e.Name+" PID "+e.Id+" "+view.Status);}result.Processes.Add(view);}
  if(!enabled||version!=revision)keep.Clear();recovery.RestoreExcept(keep,result.Messages);states=next;result.PendingRecovery=recovery.Count;foreach(var message in result.Messages)AppStore.Log(message);return result;
 });}finally{gate.Release();}}
 public async Task<CycleResult> StopAsync(){enabled=false;Interlocked.Increment(ref revision);await gate.WaitAsync();try{return await Task.Run(()=>{var result=new CycleResult();recovery.RestoreExcept(new HashSet<string>(),result.Messages);result.PendingRecovery=recovery.Count;foreach(var line in result.Messages)AppStore.Log(line);return result;});}finally{gate.Release();}}
}
