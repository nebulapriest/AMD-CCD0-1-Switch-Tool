using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;using System.Diagnostics;using System.Xml.Serialization;using System.Runtime.InteropServices;
public class CcdGroup {public int Number,HardwareDomain;public string[] Cores=new string[0];public long L3Bytes;}
public class CpuReference {public string Name="",Family="",Series="",CodeName="",FormFactor="",Source="";public int Cores,Threads;public double L3MiB;public bool OneL3PerCcd;}
[XmlRoot("AmdCatalog")]public class CpuCatalog {[XmlElement("Processor")]public List<CpuReference> Items=new List<CpuReference>();}
public class CacheDomain {public string[] Cores=new string[0];public long Bytes;}
public class DiscoveryResult {public string Brand="",Reason="",Model="";public bool Ready;public List<CcdGroup> Groups=new List<CcdGroup>();}
public static class CcdDiscovery {
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetLogicalProcessorInformationEx(int relation,IntPtr buffer,ref uint length);
 public static string BasePath{get{return AppDomain.CurrentDomain.BaseDirectory;}}
 public static string Normalize(string name){return Regex.Replace(name.ToUpperInvariant().Replace("™","").Replace("®","").Replace("(TM)","").Replace("(R)",""),@"\s+"," ").Trim();}
 public static CpuReference Match(string brand,CpuCatalog catalog){string n=Normalize(brand);return catalog.Items.OrderByDescending(x=>Normalize(x.Name).Length).FirstOrDefault(x=>{var model=Normalize(x.Name);return n==model||n.StartsWith(model+" ",StringComparison.Ordinal);});}
 public static List<CacheDomain> ReadRelations(int relation){uint size=0;GetLogicalProcessorInformationEx(relation,IntPtr.Zero,ref size);if(size==0)throw new Exception("Windows 未提供缓存/核心拓扑。");var p=Marshal.AllocHGlobal((int)size);var list=new List<CacheDomain>();try{if(!GetLogicalProcessorInformationEx(relation,p,ref size))throw new System.ComponentModel.Win32Exception();for(int offset=0;offset<size;){var q=IntPtr.Add(p,offset);int kind=Marshal.ReadInt32(q),length=Marshal.ReadInt32(q,4);if(length<8||offset+length>size)throw new Exception("Windows 拓扑记录无效。");if(kind==relation&&(relation!=2||Marshal.ReadByte(q,8)==3)){int count=relation==2?(ushort)Marshal.ReadInt16(q,38):(ushort)Marshal.ReadInt16(q,30);int start=relation==2?40:32;if(count==0)count=1;if(start+count*16>length)throw new Exception("Windows 处理器组掩码无效。");var keys=new List<string>();for(int g=0;g<count;g++){int at=start+g*16;ulong mask=(ulong)Marshal.ReadInt64(q,at);int group=(ushort)Marshal.ReadInt16(q,at+8);for(int bit=0;bit<64;bit++)if((mask&(1UL<<bit))!=0)keys.Add(group+":"+bit);}list.Add(new CacheDomain{Cores=keys.ToArray(),Bytes=relation==2?(uint)Marshal.ReadInt32(q,12):0});}offset+=length;}}finally{Marshal.FreeHGlobal(p);}return list;}
 static bool Same(IEnumerable<string> a,IEnumerable<string> b){return new HashSet<string>(a).SetEquals(b);}
 public static DiscoveryResult Evaluate(List<Cpu> cpus,ProbeReport probe,CpuReference model,List<CacheDomain> caches,List<CacheDomain> physical){
  var result=new DiscoveryResult{Brand=probe.Brand,Model=model==null?"未收入官方参照表":model.Name};
  try{
   if(probe.Error!="")throw new Exception(probe.Error);if(probe.Vendor!="AuthenticAMD"||probe.Hypervisor)throw new Exception("非 AMD 裸机拓扑，需人工核对。");
   if(model==null||!model.OneL3PerCcd)throw new Exception("此型号尚无可靠的一 L3 对一 CCD 架构依据，不能按 L3 猜测 CCD。");
   if(probe.Cpus.Count!=cpus.Count||cpus.Select(c=>c.Key).Distinct().Count()!=cpus.Count)throw new Exception("逻辑 CPU 数量或编号不一致。");
   if(!Same(cpus.Select(c=>c.Key),probe.Cpus.Select(c=>c.Group+":"+c.Index)))throw new Exception("CPUID 与 Windows 逻辑 CPU 编号不一致。");
   if(cpus.Any(c=>probe.Cpus.Count(p=>p.Group==c.Group&&p.Index==c.Index&&p.WindowsCore==c.Core&&p.WindowsL3==c.Cache)!=1))throw new Exception("CPU Sets 核心或 L3 数据发生变化。");
   if(physical.Count>model.Cores||cpus.Count>model.Threads)throw new Exception("实测核心数量超出官方型号规格。");
   var windowsCores=cpus.GroupBy(c=>c.Group+":"+c.Core).Select(g=>g.Select(c=>c.Key)).ToList();
   if(windowsCores.Count!=physical.Count||windowsCores.Any(g=>physical.Count(x=>Same(x.Cores,g))!=1))throw new Exception("两个 Windows 拓扑接口的物理核心不一致。");
   if(physical.Any(g=>probe.Cpus.Where(p=>g.Cores.Contains(p.Group+":"+p.Index)).Select(p=>p.L3Domain+":"+p.HardwareCore).Distinct().Count()!=1))throw new Exception("SMT 兄弟线程的硬件归属不一致。");
   if(probe.Cpus.GroupBy(p=>p.L3Domain+":"+p.HardwareCore).Count()!=physical.Count)throw new Exception("硬件核心与 Windows 核心不是一一对应。");
   var domains=probe.Cpus.GroupBy(p=>p.L3Domain).OrderBy(g=>g.Key).ToArray();
   if(domains.Length!=caches.Count)throw new Exception("硬件 L3 域与 Windows L3 数量不一致。");
   foreach(var domain in domains){var keys=domain.Select(p=>p.Group+":"+p.Index).ToArray();var cache=caches.SingleOrDefault(x=>Same(x.Cores,keys));if(cache==null)throw new Exception("CPUID L3 掩码与 Windows L3 不一致。");if(domain.Select(p=>p.Group+":"+p.WindowsL3).Distinct().Count()!=1)throw new Exception("CPU Sets L3 分组不一致。");if(domain.Any(p=>p.L3SizeKiB*1024L!=cache.Bytes))throw new Exception("CPUID 与 Windows L3 容量不一致。");if(domain.Select(p=>p.HardwareCore).Distinct().Count()>8)throw new Exception("该架构 CCD 的核心数量异常。");result.Groups.Add(new CcdGroup{Number=result.Groups.Count,HardwareDomain=domain.Key,Cores=keys,L3Bytes=cache.Bytes});}
   if(!Same(cpus.Select(c=>c.Key),result.Groups.SelectMany(g=>g.Cores))||result.Groups.Sum(g=>g.Cores.Length)!=cpus.Count)throw new Exception("CCD 分组未完整覆盖或有重叠。");
   result.Ready=true;result.Reason="官方型号架构 + 逐逻辑核 CPUID + Windows CPU Sets、L3 与物理核心交叉核对通过。CCD 按硬件 L3 域编号排序；BIOS 标签未独立核对。";
  }catch(Exception ex){result.Ready=false;result.Reason=ex.Message;}return result;
 }
 public static DiscoveryResult Scan(List<Cpu> cpus){try{
  var exe=Path.Combine(BasePath,"TopologyRuntime","CorePilot.Topology.exe");var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(exe)};
  ProbeReport probe;using(var process=Process.Start(info)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(30000)){process.Kill();throw new Exception("硬件检测超时。");}var xml=output.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new Exception("硬件检测未完成："+error.GetAwaiter().GetResult());using(var reader=new StringReader(xml))probe=(ProbeReport)new XmlSerializer(typeof(ProbeReport)).Deserialize(reader);}
  CpuCatalog catalog;using(var reader=File.OpenRead(Path.Combine(BasePath,"Reference","AMD-processors.xml")))catalog=(CpuCatalog)new XmlSerializer(typeof(CpuCatalog)).Deserialize(reader);
  var result=Evaluate(cpus,probe,Match(probe.Brand,catalog),ReadRelations(2),ReadRelations(0));Directory.CreateDirectory(AppStore.DirectoryPath);using(var writer=File.Create(Path.Combine(AppStore.DirectoryPath,"hardware-detection.xml")))new XmlSerializer(typeof(DiscoveryResult)).Serialize(writer,result);return result;
 }catch(Exception ex){return new DiscoveryResult{Reason=ex.Message};}}
 public static string[] Keys(Settings config,string preset,List<Cpu> cpus){if(preset=="All")return cpus.Select(c=>c.Key).ToArray();if(preset=="CCD0")return config.CCD0;if(preset=="CCD1")return config.CCD1.Length>0?config.CCD1:config.CCD0;int index;if(preset.StartsWith("CCD")&&int.TryParse(preset.Substring(3),out index)){var group=config.CcdGroups.FirstOrDefault(g=>g.Number==index);return group==null?new string[0]:group.Cores;}return new string[0];}
 public static string Default(Settings config){if(config.DefaultCcd=="CCD0")return "CCD0";if(config.DefaultCcd=="CCD1"&&config.CCD1.Length>0)return "CCD1";if(config.CcdGroups.Any(g=>"CCD"+g.Number==config.DefaultCcd))return config.DefaultCcd;return config.CCD1.Length>0?"CCD1":"CCD0";}
 public static string Range(string[] keys){var slots=keys.Select(k=>k.Split(':')).GroupBy(x=>ushort.Parse(x[0])).OrderBy(g=>g.Key).SelectMany(g=>{var ids=g.Select(x=>int.Parse(x[1])).Distinct().OrderBy(x=>x).ToArray();var runs=new List<string>();for(int i=0;i<ids.Length;){int first=ids[i],last=first;while(++i<ids.Length&&ids[i]==last+1)last=ids[i];runs.Add(first==last?first.ToString():first+"–"+last);}return runs.Select((r,i)=>g.Key==0?"CPU "+r:"G"+g.Key+" · CPU "+r);});return string.Join(", ",slots);}
 public static string Describe(Settings config){return config.CcdGroups.Count>0?string.Join("\r\n",config.CcdGroups.OrderBy(g=>g.Number).Select(g=>"CCD"+g.Number+" · "+Range(g.Cores)+" · "+g.Cores.Length+" 逻辑 CPU · "+(g.L3Bytes/1048576)+" MB L3")):"CCD0 · "+Range(config.CCD0)+"\r\n"+(config.CCD1.Length>0?"CCD1 · "+Range(config.CCD1):"仅检测到一个 CCD");}
}
