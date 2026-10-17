using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Xml.Serialization;
class Program {
 [StructLayout(LayoutKind.Sequential)] struct GroupAffinity {public UIntPtr Mask;public ushort Group,R0,R1,R2;}
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentThread();
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetThreadGroupAffinity(IntPtr thread,ref GroupAffinity affinity,out GroupAffinity previous);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetSystemCpuSetInformation(IntPtr p,uint size,out uint required,IntPtr process,uint flags);
 static int Leaf(uint n)=>unchecked((int)n);
 static string RegText(params int[] regs){return Encoding.ASCII.GetString(regs.SelectMany(BitConverter.GetBytes).ToArray());}
 static int Main(){var result=new ProbeReport();try{if(!X86Base.IsSupported)throw new Exception("CPUID unavailable");var v=X86Base.CpuId(0,0);result.Vendor=RegText(v.Ebx,v.Edx,v.Ecx);var f=X86Base.CpuId(1,0);result.Family=(f.Eax>>8)&15;if(result.Family==15)result.Family+=(f.Eax>>20)&255;result.Model=((f.Eax>>4)&15)|((f.Eax>>12)&240);result.Hypervisor=(f.Ecx&unchecked((int)0x80000000))!=0;
 var max=X86Base.CpuId(Leaf(0x80000000),0);if((uint)max.Eax<0x8000001e)throw new Exception("AMD extended topology not supported");for(uint leaf=0x80000002;leaf<=0x80000004;leaf++){var x=X86Base.CpuId(Leaf(leaf),0);result.Brand+=RegText(x.Eax,x.Ebx,x.Ecx,x.Edx);}result.Brand=result.Brand.Trim('\0',' ');
 uint needed;GetSystemCpuSetInformation(IntPtr.Zero,0,out needed,IntPtr.Zero,0);IntPtr p=Marshal.AllocHGlobal((int)needed);try{if(!GetSystemCpuSetInformation(p,needed,out needed,IntPtr.Zero,0))throw new System.ComponentModel.Win32Exception();for(int o=0;o<needed;){var q=IntPtr.Add(p,o);int size=Marshal.ReadInt32(q);if(size<8||o+size>needed)throw new Exception("Malformed topology");if(Marshal.ReadInt32(q,4)==0&&size>=24)result.Cpus.Add(new ProbeCpu{Group=(ushort)Marshal.ReadInt16(q,12),Index=Marshal.ReadByte(q,14),WindowsCore=Marshal.ReadByte(q,15),WindowsL3=Marshal.ReadByte(q,16)});o+=size;}}finally{Marshal.FreeHGlobal(p);}
 Thread.BeginThreadAffinity();try{foreach(var cpu in result.Cpus){var affinity=new GroupAffinity{Group=(ushort)cpu.Group,Mask=new UIntPtr(1UL<<cpu.Index)};GroupAffinity previous;if(!SetThreadGroupAffinity(GetCurrentThread(),ref affinity,out previous))throw new System.ComponentModel.Win32Exception();try{var t=X86Base.CpuId(Leaf(0x8000001e),0);cpu.ApicId=t.Eax;cpu.HardwareCore=t.Ebx&255;bool l3=false;for(int i=0;i<16;i++){var c=X86Base.CpuId(Leaf(0x8000001d),i);if((c.Eax&31)==0)break;if(((c.Eax>>5)&7)!=3)continue;cpu.L3Sharing=((c.Eax>>14)&4095)+1;int shift=0;while((1<<shift)<cpu.L3Sharing)shift++;cpu.L3Domain=(int)((uint)cpu.ApicId>>shift);long bytes=(long)((c.Ebx&4095)+1)*(((c.Ebx>>12)&1023)+1)*(((c.Ebx>>22)&1023)+1)*((uint)c.Ecx+1L);cpu.L3SizeKiB=(int)(bytes/1024);l3=true;break;}if(!l3)throw new Exception("L3 cache missing");}finally{GroupAffinity ignored;if(!SetThreadGroupAffinity(GetCurrentThread(),ref previous,out ignored))throw new System.ComponentModel.Win32Exception();}}}finally{Thread.EndThreadAffinity();}
 // Full model and CCD validation is performed by the caller against the bundled official catalogue.
 if(result.Vendor!="AuthenticAMD")throw new Exception("Not an AMD processor");
 }catch(Exception ex){result.Error=ex.Message;}new XmlSerializer(typeof(ProbeReport)).Serialize(Console.Out,result);return result.Error==""?0:1;}
}

