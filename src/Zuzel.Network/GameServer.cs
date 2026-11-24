using System.Diagnostics;
using LiteNetLib;
namespace Zuzel;

public sealed class GameServer:IDisposable
{
    public RoomDirectory Directory{get;}
    readonly NetManager transport;readonly Dictionary<int,(OnlineRoom Room,int Slot)> players=new();readonly Dictionary<int,double> pending=new();readonly Dictionary<int,int> epochs=new();double lastSnapshot;readonly Stopwatch clock=Stopwatch.StartNew();
    public int Port=>transport.LocalPort;
    public Action<string>? Log;
    public GameServer(int port=9050,int maxRooms=16){Directory=new(maxRooms);var listener=new EventBasedNetListener();transport=new(listener){AutoRecycle=true,DisconnectTimeout=6000,UpdateTime=5,ChannelsCount=2,IPv6Enabled=false};
        listener.ConnectionRequestEvent+=request=>{if(transport.ConnectedPeersCount>=maxRooms*4+8)request.Reject();else request.AcceptIfKey(Wire.Key);};
        listener.PeerConnectedEvent+=peer=>pending[peer.Id]=clock.Elapsed.TotalSeconds;
        listener.PeerDisconnectedEvent+=(peer,info)=>{if(players.Remove(peer.Id,out var p)){Directory.Leave(peer.Id.ToString());Log?.Invoke($"leave room={p.Room.Number} slot={p.Slot+1}");}pending.Remove(peer.Id);epochs.Remove(peer.Id);};
        listener.NetworkReceiveEvent+=(peer,d,channel,method)=>{try{if(d.AvailableBytes>160)return;byte kind=d.GetByte();if(kind==Wire.Join&&!players.ContainsKey(peer.Id)){string name=d.GetString(24);var mode=(CompetitionMode)d.GetByte();var assignment=Directory.Join(peer.Id.ToString(),name,mode);if(assignment==null){var w=new LiteNetLib.Utils.NetDataWriter();w.Put(Wire.Error);w.Put("Serwer pełny. Spróbuj później.");peer.Send(w,DeliveryMethod.ReliableOrdered);return;}players[peer.Id]=assignment.Value;pending.Remove(peer.Id);Log?.Invoke($"join room={assignment.Value.Room.Number} slot={assignment.Value.Slot+1}");Send(peer,assignment.Value,true);}
            else if(players.TryGetValue(peer.Id,out var seat)){if(kind==Wire.Input){uint seq=d.GetUInt();seat.Room.SetInput(seat.Slot,seq,new(d.GetFloat(),d.GetFloat()),clock.Elapsed.TotalSeconds);}else if(kind==Wire.Ready)seat.Room.SetReady(seat.Slot,d.GetBool(),d.GetInt());}}
            catch(Exception e)when(e is IndexOutOfRangeException or ArgumentException or InvalidOperationException or InvalidDataException){peer.Disconnect();}};
        if(!transport.Start(port))throw new IOException($"Nie można otworzyć UDP {port}.");
    }
    void Send(NetPeer peer,(OnlineRoom Room,int Slot) p,bool force=false){bool changed=!epochs.TryGetValue(peer.Id,out int epoch)||epoch!=p.Room.Epoch;peer.Send(Wire.Encode(p.Room.Snapshot(p.Slot)),changed||force?DeliveryMethod.ReliableOrdered:DeliveryMethod.Sequenced);epochs[peer.Id]=p.Room.Epoch;}
    public void Poll()=>transport.PollEvents();
    public void Step(float dt){double now=clock.Elapsed.TotalSeconds;Directory.Step(dt,now);if(now-lastSnapshot<1d/30)return;lastSnapshot=now;foreach(var peer in transport.ConnectedPeerList.ToArray()){if(players.TryGetValue(peer.Id,out var p))Send(peer,p);else if(pending.TryGetValue(peer.Id,out double since)&&now-since>8)peer.Disconnect();}}
    public void Run(CancellationToken cancellation){double previous=clock.Elapsed.TotalSeconds,accumulator=0;while(!cancellation.IsCancellationRequested){Poll();double now=clock.Elapsed.TotalSeconds;accumulator+=Math.Min(.1,now-previous);previous=now;int steps=0;while(accumulator>=1d/120&&steps++<12){Step(1f/120);accumulator-=1d/120;}Thread.Sleep(2);}}
    public void Dispose()=>transport.Stop();
}
