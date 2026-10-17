using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

public class Cpu {
 public uint Id; public ushort Group; public byte Index, Core, Cache;
 public string Key {get{return Group+":"+Index;}}
 public override string ToString(){return "CPU "+Index+"    Group "+Group+"    物理核 "+Core+"    L3 "+Cache;}
}
public class Rule {
 public string Path="", Priority="Keep", Mode="Hard", Preset="Custom";
 public string[] Cores=new string[0]; public bool Enabled=true, Game, IncludeChildren=true;
 public override string ToString(){return (Enabled?"● ":"○ ")+(Game?"[游戏] ":"")+System.IO.Path.GetFileName(Path)+"  |  "+Preset+"  |  "+Priority;}
}
public class Settings {
 public string DefaultCcd="CCD1";
 public List<CcdGroup> CcdGroups=new List<CcdGroup>();public string CpuBrand="",MachineBinding="";
 public List<ApplicationGroup> Groups=new List<ApplicationGroup>();
 public string Topology=""; public string[] CCD0=new string[0], CCD1=new string[0];
 public List<Rule> Rules=new List<Rule>();
 public string LocatorEvidenceId="";public bool IsolationEnabled, CcdVerified; public string VerifiedFingerprint="", VerificationNote=""; public string[] DefaultExclusions=new string[0];
}
public class Entry {
 public long Creation; public int Id, Parent, Session; public string Path="", Name="", Product="", Company="", State="", Owner="";
}
public static class Native {
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetSystemCpuSetInformation(IntPtr p,uint n,out uint needed,IntPtr process,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetProcessDefaultCpuSets(IntPtr p,uint[] ids,uint count);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessDefaultCpuSets(IntPtr p,[Out] uint[] ids,uint count,out uint required);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetProcessAffinityMask(IntPtr p,UIntPtr mask);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessAffinityMask(IntPtr p,out UIntPtr process,out UIntPtr system);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref uint size);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr h,out long creation,out long exit,out long kernel,out long user);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsProcessCritical(IntPtr h,out bool critical);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetPriorityClass(IntPtr h,uint value);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint GetPriorityClass(IntPtr h);
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string system,string name,out long luid);
 [StructLayout(LayoutKind.Sequential, Pack=1)] struct Privileges {public uint Count; public long Luid; public uint Attributes;}
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr token,bool disable,ref Privileges state,uint length,IntPtr previous,IntPtr needed);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ProcessInfo {public uint Size,Usage,Id;public UIntPtr Heap;public uint Module,Threads,Parent;public int BasePriority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32FirstW(IntPtr snapshot,ref ProcessInfo info);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32NextW(IntPtr snapshot,ref ProcessInfo info);
 public static Dictionary<int,int> Parents(){var result=new Dictionary<int,int>();IntPtr snapshot=CreateToolhelp32Snapshot(2,0);if(snapshot==new IntPtr(-1))throw new Win32Exception();try{var info=new ProcessInfo{Size=(uint)Marshal.SizeOf(typeof(ProcessInfo))};if(!Process32FirstW(snapshot,ref info))throw new Win32Exception();do{result[(int)info.Id]=(int)info.Parent;}while(Process32NextW(snapshot,ref info));return result;}finally{CloseHandle(snapshot);}}
 public static bool EnableDebug(){IntPtr token;if(!OpenProcessToken(Process.GetCurrentProcess().Handle,0x28,out token))return false;try{long luid;if(!LookupPrivilegeValue(null,"SeDebugPrivilege",out luid))return false;var state=new Privileges{Count=1,Luid=luid,Attributes=2};return AdjustTokenPrivileges(token,false,ref state,0,IntPtr.Zero,IntPtr.Zero)&&Marshal.GetLastWin32Error()==0;}finally{CloseHandle(token);}}
 public static string PathOf(int pid){IntPtr h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return "";try{var b=new StringBuilder(32768);uint n=(uint)b.Capacity;return QueryFullProcessImageName(h,0,b,ref n)?b.ToString():"";}finally{CloseHandle(h);}}
 public static List<Cpu> Topology(){uint n;GetSystemCpuSetInformation(IntPtr.Zero,0,out n,IntPtr.Zero,0);if(n==0)throw new Win32Exception();IntPtr p=Marshal.AllocHGlobal((int)n);var list=new List<Cpu>();try{
  if(!GetSystemCpuSetInformation(p,n,out n,IntPtr.Zero,0))throw new Win32Exception();for(int o=0;o<n;){IntPtr q=IntPtr.Add(p,o);int size=Marshal.ReadInt32(q);if(size<8||o+size>n)throw new Exception("无效拓扑");if(Marshal.ReadInt32(q,4)==0&&size>=24)list.Add(new Cpu{Id=(uint)Marshal.ReadInt32(q,8),Group=(ushort)Marshal.ReadInt16(q,12),Index=Marshal.ReadByte(q,14),Core=Marshal.ReadByte(q,15),Cache=Marshal.ReadByte(q,16)});o+=size;}
 }finally{Marshal.FreeHGlobal(p);}if(list.Count==0)throw new Exception("未检测到 CPU");return list;}
 static uint Priority(string s){switch(s){case "Idle":return 0x40;case "BelowNormal":return 0x4000;case "Normal":return 0x20;case "AboveNormal":return 0x8000;case "High":return 0x80;default:throw new Exception("无效优先级");}}
 static uint[] ReadSets(IntPtr h){uint n;GetProcessDefaultCpuSets(h,null,0,out n);var ids=new uint[n];if(!GetProcessDefaultCpuSets(h,ids,n,out n))throw new Win32Exception();return ids.OrderBy(x=>x).ToArray();}
 public static void Apply(Entry entry,Rule rule,List<Cpu> all,string[] keys){
  if(keys.Length==0)throw new Exception("核心分组为空");var selected=all.Where(c=>keys.Contains(c.Key)).ToArray();if(selected.Length!=keys.Distinct().Count())throw new Exception("核心配置与本机拓扑不匹配");
  if(rule.Mode!="Hard"&&rule.Mode!="Sets")throw new Exception("无效绑定模式");
  if(rule.Mode=="Hard"&&all.Any(c=>c.Group!=0))throw new Exception("多处理器组暂不支持硬绑定，请选择 CPU Sets");
  IntPtr h=OpenProcess(0x1200,false,entry.Id);if(h==IntPtr.Zero)throw new Win32Exception();try{
   var b=new StringBuilder(32768);uint n=(uint)b.Capacity;if(!QueryFullProcessImageName(h,0,b,ref n)||!string.Equals(b.ToString(),rule.Path,StringComparison.OrdinalIgnoreCase))throw new Exception("进程已退出或路径已变化");
   long created,exit,kernel,user;if(entry.Creation!=0&&(!GetProcessTimes(h,out created,out exit,out kernel,out user)||created!=entry.Creation))throw new Exception("进程实例已变化");
   if(rule.Mode=="Hard"){
    if(ReadSets(h).Length>0&&!SetProcessDefaultCpuSets(h,null,0))throw new Win32Exception();
    ulong bits=0;foreach(var c in selected)bits|=1UL<<c.Index;UIntPtr current,system;
    if(!GetProcessAffinityMask(h,out current,out system))throw new Win32Exception();if((bits&system.ToUInt64())!=bits)throw new Exception("所选核心不在系统允许范围内");
    if(current.ToUInt64()!=bits&&!SetProcessAffinityMask(h,new UIntPtr(bits)))throw new Win32Exception();
    if(!GetProcessAffinityMask(h,out current,out system)||current.ToUInt64()!=bits)throw new Exception("硬亲和性回读失败");
   }else{if(all.All(c=>c.Group==0)){UIntPtr prior,system;if(!GetProcessAffinityMask(h,out prior,out system))throw new Win32Exception();if(prior!=system&&!SetProcessAffinityMask(h,system))throw new Win32Exception();}var ids=selected.Select(c=>c.Id).OrderBy(x=>x).ToArray();if(!ReadSets(h).SequenceEqual(ids)&&!SetProcessDefaultCpuSets(h,ids,(uint)ids.Length))throw new Win32Exception();if(!ReadSets(h).SequenceEqual(ids))throw new Exception("CPU Sets 回读失败");}
   if(rule.Priority!="Keep"){uint value=Priority(rule.Priority);if(GetPriorityClass(h)!=value&&!SetPriorityClass(h,value))throw new Exception("核心已应用，优先级失败："+new Win32Exception().Message);if(GetPriorityClass(h)!=value)throw new Exception("优先级回读失败");}
  }finally{CloseHandle(h);}
 }
 static bool Matches(IntPtr h,int pid,string path,long stamp){var b=new StringBuilder(32768);uint n=(uint)b.Capacity;long c,x,k,u;if(!QueryFullProcessImageName(h,0,b,ref n)||!GetProcessTimes(h,out c,out x,out k,out u))throw new Win32Exception();return string.Equals(path,b.ToString(),StringComparison.OrdinalIgnoreCase)&&c==stamp;}
 public static OriginalState Capture(Entry e){IntPtr h=OpenProcess(0x1200,false,e.Id);if(h==IntPtr.Zero)throw new Win32Exception();try{if(!Matches(h,e.Id,e.Path,e.Creation))throw new Exception("进程实例已变化");bool critical;if(!IsProcessCritical(h,out critical))throw new Win32Exception();if(critical)throw new Exception("关键系统进程不参与隔离");UIntPtr affinity,system;if(!GetProcessAffinityMask(h,out affinity,out system))throw new Win32Exception();return new OriginalState{Id=e.Id,Creation=e.Creation,Path=e.Path,Affinity=affinity.ToUInt64(),Priority=GetPriorityClass(h),CpuSets=ReadSets(h)};}finally{CloseHandle(h);}}
 public static bool SameInstance(OriginalState s){IntPtr h=OpenProcess(0x1000,false,s.Id);if(h==IntPtr.Zero)return false;try{return Matches(h,s.Id,s.Path,s.Creation);}finally{CloseHandle(h);}}
 public static bool RestoreOriginal(OriginalState s){IntPtr h=OpenProcess(0x1200,false,s.Id);if(h==IntPtr.Zero){int err=Marshal.GetLastWin32Error();if(err==87)return false;throw new Win32Exception(err);}try{if(!Matches(h,s.Id,s.Path,s.Creation))return false;if(!SetProcessAffinityMask(h,new UIntPtr(s.Affinity)))throw new Win32Exception();if(!SetProcessDefaultCpuSets(h,s.CpuSets,(uint)s.CpuSets.Length))throw new Win32Exception();if(s.RestorePriority&&!SetPriorityClass(h,s.Priority))throw new Win32Exception();UIntPtr actual,system;if(!GetProcessAffinityMask(h,out actual,out system)||actual.ToUInt64()!=s.Affinity||!ReadSets(h).SequenceEqual(s.CpuSets.OrderBy(x=>x))||(s.RestorePriority&&GetPriorityClass(h)!=s.Priority))throw new Exception("隔离前设置恢复回读失败");return true;}finally{CloseHandle(h);}}
 public static void Reset(Entry e){IntPtr h=OpenProcess(0x1200,false,e.Id);if(h==IntPtr.Zero)throw new Win32Exception();try{var b=new StringBuilder(32768);uint n=(uint)b.Capacity;if(!QueryFullProcessImageName(h,0,b,ref n)||!string.Equals(e.Path,b.ToString(),StringComparison.OrdinalIgnoreCase))throw new Exception("进程变化，请刷新");UIntPtr a,s;if(!GetProcessAffinityMask(h,out a,out s))throw new Win32Exception();if(!SetProcessDefaultCpuSets(h,null,0)||!SetProcessAffinityMask(h,s)||!SetPriorityClass(h,0x20))throw new Win32Exception();}finally{CloseHandle(h);}}
}
