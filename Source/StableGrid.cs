using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Keep native scrollbars and row objects alive between monitoring snapshots.
public class StableGrid : DataGridView {
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SystemParametersInfo(uint action,uint param,out bool value,uint flags);
 static bool AnimationsEnabled(){bool enabled;return SystemParametersInfo(0x1042,0,out enabled,0)&&enabled;}
 readonly Timer fade=new Timer{Interval=16};
 readonly Dictionary<DataGridViewRow,long> changed=new Dictionary<DataGridViewRow,long>();
 public StableGrid(){DoubleBuffered=true;fade.Tick+=(s,e)=>Animate();}
 public void MarkChanged(DataGridViewRow row){
  if(!Visible||!AnimationsEnabled())return;
  changed[row]=Stopwatch.GetTimestamp();fade.Start();
 }
 void Animate(){
  foreach(var item in changed.ToArray()){
   var row=item.Key;
   if(row.DataGridView!=this){changed.Remove(row);continue;}
   double elapsed=(Stopwatch.GetTimestamp()-item.Value)*1000.0/Stopwatch.Frequency;
   double amount=Math.Max(0,1-elapsed/230.0);
   row.DefaultCellStyle.BackColor=Color.FromArgb(255-(int)(16*amount),255-(int)(14*amount),255-(int)(19*amount));
   if(amount==0){row.DefaultCellStyle.BackColor=Color.Empty;changed.Remove(row);}
  }
  if(changed.Count==0)fade.Stop();
 }
 protected override void Dispose(bool disposing){if(disposing){fade.Stop();fade.Dispose();changed.Clear();}base.Dispose(disposing);}
}
