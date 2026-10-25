using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
namespace Arthur.LmuOverlay
{
 public interface IWheelButtonReader { List<WheelButton> Buttons { get; } }
 public sealed class WheelButton
 {
  public string DeviceId,DeviceName;public uint ObjectId;public int Number;public bool Pressed;
  public string Key {get{return DeviceId+"/"+ObjectId;}}
 }
 [DataContract] public sealed class TrainingWheelBinding
 {
  [DataMember] public string DeviceId,DeviceName;
  [DataMember] public uint ObjectId;
  [DataMember] public int Number;
  public bool Matches(WheelButton b){return b!=null&&DeviceId==b.DeviceId&&ObjectId==b.ObjectId;}
  public bool Same(TrainingWheelBinding b){return b!=null&&DeviceId==b.DeviceId&&ObjectId==b.ObjectId;}
  public override string ToString(){return (DeviceName??"Volante")+" • botão "+Number;}
  public static TrainingWheelBinding From(WheelButton b){return new TrainingWheelBinding{DeviceId=b.DeviceId,DeviceName=b.DeviceName,ObjectId=b.ObjectId,Number=b.Number};}
 }
 // No action on a held button after focus change, activation, unplug or reconnect.
 public sealed class TrainingButtonEdges
 {
  Dictionary<string,bool> previous=new Dictionary<string,bool>();
  public string Step(List<WheelButton> buttons,TrainingWheelBinding save,TrainingWheelBinding restore,bool enabled)
  {
   string action="";
   if(enabled)foreach(var b in buttons){bool down;if(b.Pressed&&previous.TryGetValue(b.Key,out down)&&!down){
    string candidate=save!=null&&save.Matches(b)?"save":restore!=null&&restore.Matches(b)?"restore":"";
    if(candidate.Length>0){if(action.Length>0){action="";break;}action=candidate;}
   }}
   previous=enabled?buttons.GroupBy(b=>b.Key).ToDictionary(g=>g.Key,g=>g.First().Pressed):new Dictionary<string,bool>();
   return action;
  }
 }
}
