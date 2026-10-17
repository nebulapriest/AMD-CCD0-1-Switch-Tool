using System.Collections.Generic;
public class ProbeReport {
 public string Brand="", Vendor="", Error="";
 public int Family,Model; public bool Hypervisor;
 public List<ProbeCpu> Cpus=new List<ProbeCpu>();
}
public class ProbeCpu {
 public int Group,Index,WindowsCore,WindowsL3,ApicId,HardwareCore,L3Domain,L3Sharing,L3SizeKiB;
}
