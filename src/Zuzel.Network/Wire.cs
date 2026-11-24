using System.Numerics;
using LiteNetLib.Utils;
namespace Zuzel;

public enum RoomPhase:byte{Lobby,Countdown,Racing,Results,Finished}
public sealed record RiderState(float X,float Y,float Vx,float Vy,float Heading,float Distance,float Leg,float Time,bool Finished,bool Excluded);
public sealed record RoomState(int Room,int Epoch,long Tick,RoomPhase Phase,CompetitionMode Mode,int Heat,float Remaining,float Time,int Slot,string[] Names,bool[] Ready,int[] Points,int[] Awarded,int[] Order,RiderState[] Riders);
public static class Wire
{
    public const string Key="Zuzel-online-v1";
    public const byte Join=1,Input=2,Ready=3,State=4,Error=5;
    public static string CleanName(string? text){string n=new((text??"").Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='_'||c=='-').Take(18).ToArray());return string.IsNullOrWhiteSpace(n)?"Zawodnik":n.Trim();}
    public static NetDataWriter JoinPacket(string name,CompetitionMode mode){var w=new NetDataWriter();w.Put(Join);w.Put(CleanName(name));w.Put((byte)mode);return w;}
    public static NetDataWriter InputPacket(uint sequence,Drive input){var w=new NetDataWriter();w.Put(Input);w.Put(sequence);w.Put(Math.Clamp(input.Gas,0,1));w.Put(Math.Clamp(input.Steer,-1,1));return w;}
    public static NetDataWriter ReadyPacket(bool ready,int epoch){var w=new NetDataWriter();w.Put(Ready);w.Put(ready);w.Put(epoch);return w;}
    public static NetDataWriter Encode(RoomState s){var w=new NetDataWriter();w.Put(State);w.Put(s.Room);w.Put(s.Epoch);w.Put(s.Tick);w.Put((byte)s.Phase);w.Put((byte)s.Mode);w.Put((byte)s.Heat);w.Put(s.Remaining);w.Put(s.Time);w.Put((byte)s.Slot);for(int i=0;i<4;i++){w.Put(s.Names[i]);w.Put(s.Ready[i]);w.Put((byte)s.Points[i]);w.Put((byte)s.Awarded[i]);w.Put((byte)s.Order[i]);var r=s.Riders[i];w.Put(r.X);w.Put(r.Y);w.Put(r.Vx);w.Put(r.Vy);w.Put(r.Heading);w.Put(r.Distance);w.Put(r.Leg);w.Put(r.Time);w.Put(r.Finished);w.Put(r.Excluded);}return w;}
    public static RoomState Decode(NetDataReader d){int room=d.GetInt(),epoch=d.GetInt();long tick=d.GetLong();var phase=(RoomPhase)d.GetByte();var mode=(CompetitionMode)d.GetByte();int heat=d.GetByte();float remaining=d.GetFloat(),time=d.GetFloat();int slot=d.GetByte();if(slot>3||heat<1||heat>15||!Enum.IsDefined(phase)||!Enum.IsDefined(mode))throw new InvalidDataException("Nieprawidłowy stan");var names=new string[4];var ready=new bool[4];var points=new int[4];var awarded=new int[4];var order=new int[4];var riders=new RiderState[4];for(int i=0;i<4;i++){names[i]=d.GetString(24);ready[i]=d.GetBool();points[i]=d.GetByte();awarded[i]=d.GetByte();order[i]=d.GetByte();riders[i]=new(d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetFloat(),d.GetBool(),d.GetBool());if(order[i]>3||!float.IsFinite(riders[i].X)||!float.IsFinite(riders[i].Y))throw new InvalidDataException("Nieprawidłowy zawodnik");}return new(room,epoch,tick,phase,mode,heat,remaining,time,slot,names,ready,points,awarded,order,riders);}
    public static RiderState Capture(Rider r)=>new(r.Pos.X,r.Pos.Y,r.Vel.X,r.Vel.Y,r.Heading,r.Distance,r.LegExtension,float.IsFinite(r.FinishTime)?r.FinishTime:0,r.Finished,r.Disqualified);
    public static void Apply(Rider r,RiderState s){r.PreviousPosition=r.Pos;r.PreviousHeading=r.Heading;r.Pos=new(s.X,s.Y);r.Vel=new(s.Vx,s.Vy);r.Heading=s.Heading;r.Distance=s.Distance;r.LegExtension=s.Leg;r.FinishTime=s.Time;r.Finished=s.Finished;r.Disqualified=s.Excluded;}
}
