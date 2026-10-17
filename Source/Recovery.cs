using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
public class IsolationRecovery {
 readonly string file;readonly string topology;IsolationJournal journal;public bool Startup=true;
 public int Count {get{return journal.Items.Count;}}
 public IsolationRecovery(string path,string currentTopology){file=path;topology=currentTopology;if(File.Exists(file)){using(var r=File.OpenRead(file))journal=(IsolationJournal)new XmlSerializer(typeof(IsolationJournal)).Deserialize(r);}else journal=new IsolationJournal{Topology=currentTopology};}
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(file));var temp=file+".tmp";using(var w=File.Create(temp))new XmlSerializer(typeof(IsolationJournal)).Serialize(w,journal);if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);}
 public void Capture(Entry e,string role,bool restorePriority=false){
  if(journal.Topology!=topology&&journal.Items.Count>0)throw new Exception("尚有其他拓扑的恢复记录，不能开始隔离。");
  var existing=journal.Items.FirstOrDefault(s=>s.Id==e.Id&&s.Creation==e.Creation);if(existing!=null){if(restorePriority&&!existing.RestorePriority){existing.RestorePriority=true;Save();}return;}
  var original=Native.Capture(e);original.Role=role;original.RestorePriority=restorePriority;journal.Topology=topology;journal.Items.Add(original);
  try{Save();}catch{journal.Items.Remove(original);throw;} // The journal must be durable before changing any target.
 }
 public bool Restore(OriginalState item,List<string> messages){try{
   if(journal.Topology!=topology&&Native.SameInstance(item))throw new Exception("CPU 拓扑变化，保留原状态记录供人工处理。");
   bool restored=Native.RestoreOriginal(item);journal.Items.Remove(item);Save();if(restored)messages.Add("已恢复隔离前设置："+Path.GetFileName(item.Path)+" PID "+item.Id);return true;
  }catch(Exception ex){if(item.Failure!=ex.Message){messages.Add("恢复失败 PID "+item.Id+"："+ex.Message);item.Failure=ex.Message;}return false;}}
 public bool RestoreFor(Entry e,List<string> messages){var item=journal.Items.FirstOrDefault(s=>s.Id==e.Id&&s.Creation==e.Creation);return item==null||Restore(item,messages);}
 public void RestoreExcept(HashSet<string> keep,List<string> messages){foreach(var item in journal.Items.ToArray())if(!keep.Contains(item.Key))Restore(item,messages);}
 public void RecoverStartup(List<string> messages){if(!Startup)return;RestoreExcept(new HashSet<string>(),messages);Startup=false;if(journal.Items.Count>0)messages.Add("有未完成恢复记录；对应进程保留记录并在后续扫描重试。");}
}
