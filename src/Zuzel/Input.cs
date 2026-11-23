using System.Runtime.InteropServices;
using System.Text.Json;
namespace Zuzel;
// Consume game keys before WinForms translates repeated key messages into UI input.
// The simulation reads this small state table; no synchronous keyboard polling.
public sealed class GameKeyboard:IMessageFilter,IDisposable
{
    readonly bool[] held=new bool[256];readonly Func<IntPtr> target;readonly Func<bool> enabled;
    public event Action<Keys>? Pressed;
    public GameKeyboard(Func<IntPtr> target,Func<bool> enabled){this.target=target;this.enabled=enabled;Application.AddMessageFilter(this);}
    public bool IsDown(int key)=>key>=0&&key<held.Length&&held[key];
    public bool Process(int message,int key){if(key<0||key>=held.Length)return false;if(message==0x100){if(!held[key]){held[key]=true;Pressed?.Invoke((Keys)key);}return true;}if(message==0x101){held[key]=false;return true;}return message==0x102;}
    public bool PreFilterMessage(ref Message m){if(!enabled()||m.HWnd!=target())return false;return Process(m.Msg,(int)m.WParam);}
    public void Clear()=>Array.Clear(held);
    public void Dispose(){Application.RemoveMessageFilter(this);Clear();}
}
public sealed class Bindings
{
    public int Humans{get;set;}=4;
    public int[][] Keys{get;set;}=new[]{new[]{(int)System.Windows.Forms.Keys.W,(int)System.Windows.Forms.Keys.A,(int)System.Windows.Forms.Keys.D},new[]{(int)System.Windows.Forms.Keys.Up,(int)System.Windows.Forms.Keys.Left,(int)System.Windows.Forms.Keys.Right},new[]{(int)System.Windows.Forms.Keys.I,(int)System.Windows.Forms.Keys.J,(int)System.Windows.Forms.Keys.L},new[]{(int)System.Windows.Forms.Keys.NumPad8,(int)System.Windows.Forms.Keys.NumPad4,(int)System.Windows.Forms.Keys.NumPad6}};
    public float Best{get;set;}public bool Muted{get;set;}public CompetitionMode Mode{get;set;}
    public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZuzelArena");
    public static Bindings Load(){try{var b=JsonSerializer.Deserialize<Bindings>(File.ReadAllText(Path.Combine(Folder,"settings.json")))!;if(b.Keys.Length!=4||b.Keys.Any(k=>k.Length!=3))return new();b.Humans=Math.Clamp(b.Humans,1,4);return b;}catch{return new();}}
    public void Save(){try{Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"settings.json"),JsonSerializer.Serialize(this));}catch{}}
    public static string Name(int k)=>((System.Windows.Forms.Keys)k) switch{System.Windows.Forms.Keys.Up=>"↑",System.Windows.Forms.Keys.Left=>"←",System.Windows.Forms.Keys.Right=>"→",System.Windows.Forms.Keys.NumPad8=>"Num 8",System.Windows.Forms.Keys.NumPad4=>"Num 4",System.Windows.Forms.Keys.NumPad6=>"Num 6",var key=>key.ToString()};
}
public static class Pads
{
    [StructLayout(LayoutKind.Sequential)] public struct Gamepad{public ushort buttons;public byte lt,rt;public short lx,ly,rx,ry;}
    [StructLayout(LayoutKind.Sequential)] public struct State{public uint packet;public Gamepad pad;}
    [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint GetState(uint index,out State state);
    public static bool[] Connected=new bool[4];
    public static Drive Read(int i){try{if(GetState((uint)i,out var s)!=0){Connected[i]=false;return default;}Connected[i]=true;float gas=Math.Max(s.pad.rt/255f,(s.pad.buttons&0x1000)!=0?1:0);float steer=Math.Abs((int)s.pad.lx)<6000?0:s.pad.lx/32767f;if((s.pad.buttons&4)!=0)steer=-1;if((s.pad.buttons&8)!=0)steer=1;return new(gas,steer);}catch{return default;}}
}
