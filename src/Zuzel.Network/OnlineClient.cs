using System.Diagnostics;
using LiteNetLib;
namespace Zuzel;

public sealed class OnlineClient:IDisposable
{
    readonly NetManager transport;readonly string name;readonly CompetitionMode mode;NetPeer? peer;uint sequence;double lastSend,lastState;readonly Stopwatch clock=Stopwatch.StartNew();
    public RoomState? Latest{get;private set;}public RoomState? Previous{get;private set;}public event Action<RoomState>? StateReceived;
    public string Status{get;private set;}="Łączenie z serwerem…";public bool Failed{get;private set;}
    public double SecondsSinceState=>clock.Elapsed.TotalSeconds-lastState;public int Ping=>peer?.Ping??0;
    public float Interpolation=>Math.Clamp((float)(SecondsSinceState*30),0,1);
    public OnlineClient(string host,int port,string name,CompetitionMode mode){this.name=Wire.CleanName(name);this.mode=mode;var listener=new EventBasedNetListener();transport=new(listener){AutoRecycle=true,DisconnectTimeout=6000,UpdateTime=5,ChannelsCount=2,IPv6Enabled=false};listener.PeerConnectedEvent+=p=>{peer=p;Status="Przydzielanie pokoju…";p.Send(Wire.JoinPacket(this.name,this.mode),DeliveryMethod.ReliableOrdered);};listener.PeerDisconnectedEvent+=(_,_)=>{Failed=true;Status="Połączenie zakończone. Wciśnij Esc, aby wrócić.";peer=null;};listener.NetworkReceiveEvent+=(_,d,_,_)=>{try{byte kind=d.GetByte();if(kind==Wire.State){var state=Wire.Decode(d);if(Latest!=null&&Latest.Room==state.Room&&state.Tick<=Latest.Tick)return;Previous=Latest;Latest=state;lastState=clock.Elapsed.TotalSeconds;Status=$"Pokój {state.Room} · zawodnik {state.Slot+1}";StateReceived?.Invoke(state);}else if(kind==Wire.Error){Status=d.GetString(100);Failed=true;}}catch(Exception e)when(e is IndexOutOfRangeException or ArgumentException or InvalidDataException or InvalidOperationException){Failed=true;Status="Nieprawidłowa odpowiedź serwera.";}};
        if(!transport.Start())throw new IOException("Nie można uruchomić sieci.");try{transport.Connect(host,port,Wire.Key);}catch{transport.Stop();throw;}
    }
    public void Poll(){transport.PollEvents();if(Latest==null&&clock.Elapsed.TotalSeconds>12){Failed=true;Status="Serwer nie odpowiada. Sprawdź internet i wróć klawiszem Esc.";}if(Latest!=null&&SecondsSinceState>8){Failed=true;Status="Utracono połączenie. Wciśnij Esc, aby wrócić.";}}
    public void SendInput(Drive input){if(peer==null||clock.Elapsed.TotalSeconds-lastSend<1d/60)return;lastSend=clock.Elapsed.TotalSeconds;peer.Send(Wire.InputPacket(++sequence,input),DeliveryMethod.Sequenced);}
    public void Ready(bool ready=true){if(peer!=null&&Latest!=null)peer.Send(Wire.ReadyPacket(ready,Latest.Epoch),DeliveryMethod.ReliableOrdered);}
    public void Dispose()=>transport.Stop();
}
