namespace Zuzel;

public sealed class PlayerSeat
{
    public required string Id;public required string Name;public bool Ready;public Drive Input;public uint Sequence;public bool HasSequence;public double LastInput;public double WindowStart;public int Messages;
}
public sealed class OnlineRoom
{
    public int Number{get;}public int Epoch{get;private set;}=1;public long Tick{get;private set;}
    public RoomPhase Phase{get;private set;}=RoomPhase.Lobby;public CompetitionMode Mode{get;private set;}
    public Race Race{get;}=new();public Tournament Tournament{get;}=new();public PlayerSeat?[] Seats{get;}=new PlayerSeat?[4];
    public float Remaining{get;private set;}public bool Locked{get;private set;}public int Count=>Seats.Count(s=>s!=null);public double EmptySince;
    readonly Drive[] drives=new Drive[4];public float MaxRaceTime{get;set;}=150;
    public OnlineRoom(int number,CompetitionMode mode){Number=number;Mode=mode;Tournament.Start(mode);}
    public bool CanJoin(CompetitionMode mode)=>!Locked&&Count<4&&(Count==0||Mode==mode);
    public int Join(string id,string name,CompetitionMode mode){if(!CanJoin(mode))return -1;if(Count==0&&Mode!=mode){Mode=mode;Tournament.Start(mode);}int slot=Array.FindIndex(Seats,s=>s==null);Seats[slot]=new(){Id=id,Name=Wire.CleanName(name)};return slot;}
    public void Leave(string id){int slot=Array.FindIndex(Seats,s=>s?.Id==id);if(slot>=0)Seats[slot]=null;if(Count==0)ResetLobby();}
    public void SetReady(int slot,bool value,int epoch){if(epoch!=Epoch||Seats[slot]==null||Phase is not(RoomPhase.Lobby or RoomPhase.Results or RoomPhase.Finished))return;Seats[slot]!.Ready=value;}
    public bool SetInput(int slot,uint seq,Drive input,double now){var s=Seats[slot];if(s==null||!float.IsFinite(input.Gas)||!float.IsFinite(input.Steer))return false;if(now-s.WindowStart>=1){s.WindowStart=now;s.Messages=0;}if(++s.Messages>120)return false;if(s.HasSequence&&unchecked((int)(seq-s.Sequence))<=0)return false;s.Sequence=seq;s.HasSequence=true;s.LastInput=now;s.Input=new(Math.Clamp(input.Gas,0,1),Math.Clamp(input.Steer,-1,1));return true;}
    void ClearReady(){foreach(var s in Seats)if(s!=null)s.Ready=false;}
    void StartHeat(){Race.Reset(Tournament.CurrentHeat-1);Phase=RoomPhase.Countdown;Remaining=3.4f;Epoch++;ClearReady();}
    void ResetLobby(){Phase=RoomPhase.Lobby;Locked=false;Remaining=0;Epoch++;Tournament.Start(Mode);Race.Reset();ClearReady();}
    public void Step(float dt,double now){Tick++;bool ready=Count>0&&Seats.Where(s=>s!=null).All(s=>s!.Ready);
        if(Phase==RoomPhase.Lobby){if(ready){Locked=true;Tournament.Start(Mode);StartHeat();}return;}
        if(Phase is RoomPhase.Results or RoomPhase.Finished){Remaining-=dt;if(ready||Remaining<=0){if(Phase==RoomPhase.Finished){ResetLobby();}else{Tournament.Advance();StartHeat();}}return;}
        for(int i=0;i<4;i++){var seat=Seats[i];drives[i]=seat==null?(Phase==RoomPhase.Countdown?default:Race.AI(Race.Riders[i])):now-seat.LastInput>.4?default:seat.Input;}
        if(Phase==RoomPhase.Countdown){Race.Stage(dt,drives);Remaining-=dt;if(Remaining<=0){Phase=RoomPhase.Racing;Epoch++;}return;}
        Race.Step(dt,drives);if(Race.Time>=MaxRaceTime){foreach(var r in Race.Riders)if(!r.Finished){r.Finished=true;r.Disqualified=true;r.FinishTime=0;r.Vel=System.Numerics.Vector2.Zero;if(!Race.Results.Contains(r.Id))Race.Results.Add(r.Id);}}
        if(Race.Results.Count==4){Tournament.Record(Race);Phase=Tournament.Complete?RoomPhase.Finished:RoomPhase.Results;Remaining=Tournament.Complete?30:12;Epoch++;ClearReady();}
    }
    public RoomState Snapshot(int slot){var order=Race.Ordered().Select(r=>r.Id).ToArray();return new(Number,Epoch,Tick,Phase,Mode,Tournament.CurrentHeat,Math.Max(0,Remaining),Race.Time,slot,Seats.Select((s,i)=>s?.Name??$"AI {i+1}").ToArray(),Seats.Select(s=>s?.Ready??false).ToArray(),Tournament.Points.ToArray(),Enumerable.Range(0,4).Select(Tournament.HeatPoints).ToArray(),order,Race.Riders.Select(Wire.Capture).ToArray());}
}
public sealed class RoomDirectory
{
    public List<OnlineRoom> Rooms{get;}=new(){new(1,CompetitionMode.Individual)};public int MaxRooms{get;}int next=2;
    public RoomDirectory(int maxRooms=16){MaxRooms=maxRooms;}
    public (OnlineRoom Room,int Slot)? Join(string id,string name,CompetitionMode mode){if(!Enum.IsDefined(mode))return null;foreach(var r in Rooms)if(r.Seats.Any(s=>s?.Id==id))return null;var room=Rooms.FirstOrDefault(r=>r.CanJoin(mode));if(room==null){if(Rooms.Count>=MaxRooms)return null;room=new(next++,mode);Rooms.Add(room);}return(room,room.Join(id,name,mode));}
    public void Leave(string id){foreach(var room in Rooms)room.Leave(id);Rooms.RemoveAll(r=>r.Number!=1&&r.Count==0);}
    public void Step(float dt,double now){foreach(var room in Rooms)if(room.Count>0)room.Step(dt,now);}
}
