using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
public static class AppStore {
 public static string TestDirectory;
 public static string DirectoryPath {get{return TestDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CorePilot");}}
 public static string FilePath {get{return Path.Combine(DirectoryPath,"settings.xml");}}
 public static string Fingerprint(System.Collections.Generic.List<Cpu> cpus){return string.Join(";",cpus.Select(c=>c.Key+":"+c.Core+":"+c.Cache));}
 public static Settings Read(string file){if(!File.Exists(file))return new Settings();if(new FileInfo(file).Length>4*1024*1024)throw new InvalidDataException("配置文件超过 4 MB，已停止读取。");using(var r=File.OpenRead(file))return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(r);}
 public static void Write(string file,Settings value){Directory.CreateDirectory(Path.GetDirectoryName(file));var temp=file+".tmp";using(var w=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(w,value);if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);}
 public static Settings Clone(Settings value){using(var memory=new MemoryStream()){var serializer=new XmlSerializer(typeof(Settings));serializer.Serialize(memory,value);memory.Position=0;return (Settings)serializer.Deserialize(memory);}}
 static bool LocalEvidence(string source,System.Collections.Generic.List<Cpu> cpus){try{using(var reader=File.OpenRead(source)){var r=(LocatorReport)new XmlSerializer(typeof(LocatorReport)).Deserialize(reader);return r.MachineBinding==LocatorIdentity.Current()&&r.Valid(cpus);}}catch{return false;}}
 public static Settings Load(System.Collections.Generic.List<Cpu> cpus){var config=Read(FilePath);var source=Path.Combine(DirectoryPath,"VerifiedCoreMap.xml");if(File.Exists(source)){LocatorReport r;using(var reader=File.OpenRead(source))r=(LocatorReport)new XmlSerializer(typeof(LocatorReport)).Deserialize(reader);if(r.MachineBinding!=""&&r.MachineBinding==LocatorIdentity.Current()&&r.Valid(cpus)&&config.LocatorEvidenceId!=r.FinishedUtc){config.CCD0=r.Samples.Where(x=>x.ObservedCcd==0).Select(x=>x.Group+":"+x.Index).ToArray();config.CCD1=r.Samples.Where(x=>x.ObservedCcd==1).Select(x=>x.Group+":"+x.Index).ToArray();config.CcdVerified=true;config.VerifiedFingerprint=r.Topology;config.Topology=r.Topology;config.LocatorEvidenceId=r.FinishedUtc;config.VerificationNote="逐核验证与用户 Ryzen Master 观察确认";Write(FilePath,config);}}
 if(TestDirectory!=null)return config;
 string fingerprint=Fingerprint(cpus),machine=LocatorIdentity.Current();bool manual=config.CcdVerified&&config.VerifiedFingerprint==fingerprint&&(config.LocatorEvidenceId!=""||config.VerificationNote.StartsWith("用户手动核对"))&&(config.MachineBinding==machine||(config.MachineBinding==""&&LocalEvidence(source,cpus)));
 bool changed=config.Topology!=fingerprint||(config.MachineBinding!=""&&config.MachineBinding!=machine);var detection=CcdDiscovery.Scan(cpus);config.CpuBrand=detection.Brand;
 if(changed){config.CcdVerified=false;config.IsolationEnabled=false;config.CcdGroups.Clear();config.CCD0=new string[0];config.CCD1=new string[0];config.LocatorEvidenceId="";foreach(var rule in config.Rules.Where(r=>r.Preset=="Custom"))rule.Enabled=false;manual=false;}
 if(detection.Ready){
  if(!manual){config.CcdGroups=detection.Groups;config.CCD0=detection.Groups[0].Cores;config.CCD1=detection.Groups.Count>1?detection.Groups[1].Cores:new string[0];config.VerificationNote=detection.Reason;}
  else {config.CcdGroups=detection.Groups;foreach(var group in config.CcdGroups){if(new System.Collections.Generic.HashSet<string>(group.Cores).SetEquals(config.CCD0))group.Number=0;else if(new System.Collections.Generic.HashSet<string>(group.Cores).SetEquals(config.CCD1))group.Number=1;}}
  config.CcdVerified=true;config.VerifiedFingerprint=fingerprint;config.Topology=fingerprint;config.MachineBinding=machine;if(changed)config.IsolationEnabled=true;
 }else if(!manual){config.CcdVerified=false;config.IsolationEnabled=false;config.VerificationNote="待手动核对："+detection.Reason;}
 if(cpus.Any(c=>c.Group!=0)){config.IsolationEnabled=false;config.VerificationNote+=" 当前硬绑定引擎不支持多个 Windows 处理器组，已暂停自动绑定。";}
 Write(FilePath,config);return config;}
 public static void Log(string value){try{Directory.CreateDirectory(DirectoryPath);var file=Path.Combine(DirectoryPath,"activity.log");if(File.Exists(file)&&new FileInfo(file).Length>2*1024*1024)File.Move(file,file+"."+DateTime.Now.ToString("yyyyMMddHHmmssfff"));File.AppendAllText(file,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+value+Environment.NewLine);}catch{}}
}
