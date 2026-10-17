using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

public class CoreLocator:Form {
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentThread();
 [DllImport("kernel32.dll",SetLastError=true)]static extern UIntPtr SetThreadAffinityMask(IntPtr thread,UIntPtr mask);
 [DllImport("kernel32.dll")]static extern uint GetCurrentProcessorNumber();
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetThreadTimes(IntPtr h,out long creation,out long exit,out long kernel,out long user);
 readonly string file;readonly List<Cpu> cpus;readonly List<Cpu[]> groups;LocatorReport report=new LocatorReport();
 Label title=new Label(),instruction=new Label(),state=new Label();ProgressBar progress=new ProgressBar();Button ccd0=new Button(),ccd1=new Button(),repeat=new Button(),stop=new Button();CheckBox observed=new CheckBox();
 volatile bool cancel;bool running;int stage;static double sink;
 public static string Fingerprint(List<Cpu> cpus){return string.Join(";",cpus.Select(c=>c.Key+":"+c.Core+":"+c.Cache));}
 public CoreLocator(string output){file=System.IO.Path.GetFullPath(output);cpus=Native.Topology();groups=cpus.GroupBy(c=>c.Group+":"+c.Cache).OrderBy(g=>g.Min(c=>c.Index)).Select(g=>g.ToArray()).ToList();if(cpus.Any(c=>c.Group!=0)||groups.Count!=2)throw new Exception("定位工具需要单处理器组、两个完整缓存域。");report.MachineBinding=LocatorIdentity.Current();report.Topology=Fingerprint(cpus);report.StartedUtc=DateTime.UtcNow.ToString("o");
  Text="CorePilot · 逐核定位验证";Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(760,440);StartPosition=FormStartPosition.CenterScreen;TopMost=true;MaximizeBox=false;
  title.SetBounds(20,18,720,45);title.Font=new Font(Font.FontFamily,15,FontStyle.Bold);instruction.SetBounds(20,74,720,100);state.SetBounds(20,180,720,50);progress.SetBounds(20,240,720,24);observed.SetBounds(20,276,720,50);observed.Text="我已观察本组每个 CPU 的负载变化，始终属于下方选择的同一个 CCD";
  ccd0.SetBounds(20,340,230,38);ccd0.Text="本组属于 CCD0（4.7 GHz）";ccd1.SetBounds(260,340,230,38);ccd1.Text="本组属于 CCD1（4.55 GHz）";repeat.SetBounds(500,340,110,38);repeat.Text="重测本组";stop.SetBounds(620,340,120,38);stop.Text="停止并关闭";
  Controls.AddRange(new Control[]{title,instruction,state,progress,observed,ccd0,ccd1,repeat,stop});observed.CheckedChanged+=(s,e)=>UpdateButtons();ccd0.Click+=(s,e)=>Confirm(0);ccd1.Click+=(s,e)=>Confirm(1);repeat.Click+=(s,e)=>BeginGroup();stop.Click+=(s,e)=>Close();FormClosing+=(s,e)=>{cancel=true;report.Cancelled=!report.Completed;Save();};Shown+=(s,e)=>BeginGroup();UpdateButtons();
 }
 void UpdateButtons(){ccd0.Enabled=ccd1.Enabled=!running&&observed.Checked&&report.Samples.Count(s=>s.L3==groups[stage][0].Cache)==groups[stage].Length;repeat.Enabled=!running;}
 void Save(){try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));string temp=file+".tmp";using(var w=File.Create(temp))new XmlSerializer(typeof(LocatorReport)).Serialize(w,report);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}catch(Exception ex){state.Text="报告保存失败："+ex.Message;}}
 void Post(Action action){if(IsDisposed||Disposing)return;try{BeginInvoke(new Action(()=>{if(!IsDisposed)action();}));}catch(InvalidOperationException){}}
 public static LocatorSample RunCpu(Cpu cpu,int milliseconds,Func<bool> cancelled){if(cpu.Group!=0)throw new Exception("仅支持 Group 0");Thread.BeginThreadAffinity();UIntPtr previous=UIntPtr.Zero;var sample=new LocatorSample{Group=cpu.Group,Index=cpu.Index,L3=cpu.Cache};try{
  previous=SetThreadAffinityMask(GetCurrentThread(),new UIntPtr(1UL<<cpu.Index));if(previous==UIntPtr.Zero)throw new Win32Exception();sample.Pinned=true;
  long c,x,k0,u0,k1,u1;if(!GetThreadTimes(GetCurrentThread(),out c,out x,out k0,out u0))throw new Win32Exception();var watch=Stopwatch.StartNew();double value=1.125;
  while(watch.ElapsedMilliseconds<milliseconds&&!cancelled()){if(GetCurrentProcessorNumber()!=cpu.Index)sample.WrongCpuSamples++;for(int j=0;j<4096;j++)value=Math.Sqrt(value+1.234567);}
  Interlocked.Exchange(ref sink,value);if(!GetThreadTimes(GetCurrentThread(),out c,out x,out k1,out u1))throw new Win32Exception();sample.ThreadCpuMs=(k1-k0+u1-u0)/10000.0;sample.WallMs=watch.Elapsed.TotalMilliseconds;
 }finally{if(previous!=UIntPtr.Zero&&SetThreadAffinityMask(GetCurrentThread(),previous)==UIntPtr.Zero)throw new Win32Exception();Thread.EndThreadAffinity();}return sample;}
 void BeginGroup(){if(running)return;running=true;observed.Checked=false;report.Error="";var selected=groups[stage];report.Samples.RemoveAll(s=>s.L3==selected[0].Cache);title.Text="测试 "+(stage==0?"A":"B")+"：CPU "+selected.First().Index+"–"+selected.Last().Index;instruction.Text="请保持 Ryzen Master 核心面板可见。\r\n每个逻辑 CPU 单独运行 4 秒，只使用一个测试线程。\r\n观察负载依次落在哪些核心、这些核心属于哪个 CCD；本组结束后再确认。";progress.Maximum=selected.Length;progress.Value=0;UpdateButtons();Save();
  Task.Factory.StartNew(()=>{try{int done=0;foreach(var cpu in selected){if(cancel)break;Post(()=>state.Text="当前只加载 CPU "+cpu.Index+"（4 秒），请观察 Ryzen Master…");var sample=RunCpu(cpu,4000,()=>cancel);if(cancel)break;Post(()=>{report.Samples.Add(sample);progress.Value=++done;Save();});}Post(()=>{running=false;state.Text=cancel?"测试已停止":"本组负载已停止。确认所属 CCD，或选择重测。";UpdateButtons();});}catch(Exception ex){Post(()=>{running=false;report.Error=ex.Message;state.Text="定位失败："+ex.Message;Save();UpdateButtons();});}},TaskCreationOptions.LongRunning);
 }
 void Confirm(int ccd){if(running||!observed.Checked)return;var selected=groups[stage];var samples=report.Samples.Where(s=>s.L3==selected[0].Cache).ToArray();if(samples.Length!=selected.Length||samples.Any(s=>s.WrongCpuSamples>0||s.WallMs<3000||s.ThreadCpuMs<1000)){MessageBox.Show("本组有效单核负载或亲和性校验不足，请重测。");return;}if(stage==1&&report.Samples.Any(s=>s.L3!=selected[0].Cache&&s.ObservedCcd==ccd)){MessageBox.Show("两组被标记为同一个 CCD，请核对或重测。不会接受这个映射。");return;}foreach(var s in samples)s.ObservedCcd=ccd;Save();if(stage==0){stage=1;BeginGroup();return;}
  report.Completed=true;report.FinishedUtc=DateTime.UtcNow.ToString("o");if(!report.Valid(cpus)){report.Completed=false;report.Error="完整映射校验失败";Save();MessageBox.Show(report.Error);return;}Save();title.Text="逐核定位完成";instruction.Text="CPU 0–11 → CCD"+report.Samples.First(s=>s.Index==0).ObservedCcd+"\r\nCPU 12–23 → CCD"+report.Samples.First(s=>s.Index==12).ObservedCcd+"\r\n结果来自你在 Ryzen Master 中的逐组观察；已记录每个 CPU 的实际落核校验。";state.Text="结果已保存。可以关闭窗口，返回聊天继续完善自动分配。";ccd0.Enabled=ccd1.Enabled=repeat.Enabled=observed.Enabled=false;
 }
 [STAThread]public static void Main(string[] args){try{Application.EnableVisualStyles();Application.Run(new CoreLocator(args.Length>0?args[0]:System.IO.Path.Combine(AppStore.DirectoryPath,"VerifiedCoreMap.xml")));}catch(Exception ex){MessageBox.Show(ex.ToString(),"定位失败");}}
}
