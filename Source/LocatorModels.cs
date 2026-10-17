using System;
using System.Collections.Generic;
using System.Linq;
public static class LocatorIdentity {
 public static string Current(){try{using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")){var value=key==null?null:key.GetValue("MachineGuid") as string;if(string.IsNullOrWhiteSpace(value))return "";using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("CorePilot-local-map:"+value))).Replace("-","");}}catch{return "";}}
}
public class LocatorSample {
 public int Group,Index,L3;public bool Pinned;public int WrongCpuSamples;public double WallMs,ThreadCpuMs;public int ObservedCcd=-1;
}
public class LocatorReport {
 public string MachineBinding="";public string Topology="",StartedUtc="",FinishedUtc="",ObservationSource="Ryzen Master 用户逐组观察";
 public bool Completed,Cancelled;public string Error="";public List<LocatorSample> Samples=new List<LocatorSample>();
 public bool Valid(List<Cpu> cpus){
  if(!Completed||Cancelled||Error!=""||Topology!=string.Join(";",cpus.Select(c=>c.Key+":"+c.Core+":"+c.Cache))||Samples.Count!=cpus.Count)return false;
  if(Samples.Select(s=>s.Group+":"+s.Index).Distinct().Count()!=cpus.Count||Samples.Any(s=>!s.Pinned||s.WrongCpuSamples!=0||s.WallMs<3000||s.ThreadCpuMs<1000||s.ObservedCcd<0||s.ObservedCcd>1))return false;
  if(Samples.Any(s=>!cpus.Any(c=>c.Group==s.Group&&c.Index==s.Index&&c.Cache==s.L3)))return false;
  var domains=Samples.GroupBy(s=>s.Group+":"+s.L3).ToArray();return domains.Length==2&&domains.All(g=>g.Select(s=>s.ObservedCcd).Distinct().Count()==1)&&domains.Select(g=>g.First().ObservedCcd).Distinct().Count()==2;
 }
}
