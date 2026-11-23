using System.Reflection;
using System.Runtime.InteropServices;

namespace Zuzel;
// Four real recording voices, continuously mixed to a native Windows PCM output.
public sealed class EngineAudio:IDisposable
{
    [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format{public ushort tag,channels;public uint rate,bytes;public ushort align,bits,extra;}
    [StructLayout(LayoutKind.Sequential)] struct Header{public IntPtr data;public uint length,recorded;public UIntPtr user;public uint flags,loops;public IntPtr next;public UIntPtr reserved;}
    [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr h,uint device,ref Format f,IntPtr cb,IntPtr instance,uint flags);
    [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr h);
    readonly IntPtr[] headers=new IntPtr[4],buffers=new IntPtr[4];readonly short[] sample;readonly double[] phases={0,5123,14573,23451};readonly float[] rates={1,1,1,1},gains=new float[4],pans=new float[4];readonly object sync=new();IntPtr handle;Thread? worker;volatile bool running;public bool Available{get;private set;}public string Error{get;private set;}="";
    public EngineAudio(){sample=LoadSample();try{var f=new Format{tag=1,channels=2,rate=22050,bytes=88200,align=4,bits=16};uint result=waveOutOpen(out handle,uint.MaxValue,ref f,IntPtr.Zero,IntPtr.Zero,0);if(result!=0)throw new Exception($"Urządzenie audio: {result}");for(int i=0;i<4;i++){buffers[i]=Marshal.AllocHGlobal(1024*4);headers[i]=Marshal.AllocHGlobal(Marshal.SizeOf<Header>());Marshal.StructureToPtr(new Header{data=buffers[i],length=1024*4},headers[i],false);if(waveOutPrepareHeader(handle,headers[i],(uint)Marshal.SizeOf<Header>())!=0)throw new Exception("Bufor audio");}running=true;Available=true;worker=new Thread(Mix){IsBackground=true,Name="Silniki PCM"};worker.Start();}catch(Exception e){Error=e.Message;Available=false;}}
    static short[] LoadSample(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ZuzelArena.Assets.engine.wav")??Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n=>n.EndsWith("engine.wav")).Select(n=>Assembly.GetExecutingAssembly().GetManifestResourceStream(n)).First()!;using var br=new BinaryReader(stream);br.ReadBytes(12);while(stream.Position<stream.Length){var tag=new string(br.ReadChars(4));int size=br.ReadInt32();if(tag=="data"){byte[] bytes=br.ReadBytes(size);short[] data=new short[bytes.Length/2];Buffer.BlockCopy(bytes,0,data,0,bytes.Length);return data;}stream.Position+=size+(size%2);}throw new Exception("Brak próbki audio");}
    public void Update(Race race,bool active,bool muted){lock(sync)for(int i=0;i<4;i++){var r=race.Riders[i];rates[i]=.65f+r.Speed/300+r.Gas*.15f;gains[i]=active&&!muted&&!r.Finished?.12f+r.Gas*.055f:0;pans[i]=Math.Clamp((r.Pos.X-640)/700,-.8f,.8f);}}
    void Mix(){var pcm=new short[2048];float[] rr=new float[4],gg=new float[4],pp=new float[4];bool[] queued=new bool[4];while(running){for(int b=0;b<4;b++){var hdr=Marshal.PtrToStructure<Header>(headers[b]);if(queued[b]&&(hdr.flags&1)==0)continue;lock(sync){Array.Copy(rates,rr,4);Array.Copy(gains,gg,4);Array.Copy(pans,pp,4);}for(int n=0;n<1024;n++){double left=0,right=0;for(int v=0;v<4;v++){double p=phases[v];int ix=(int)p;double fract=p-ix;double raw=sample[ix]*(1-fract)+sample[(ix+1)%sample.Length]*fract; // Crossfade the recording seam.
                    if(ix>sample.Length-1024){double cross=(ix-(sample.Length-1024))/1024.0;raw=raw*(1-cross)+sample[ix-(sample.Length-1024)]*cross;}left+=raw*gg[v]*(1-pp[v]);right+=raw*gg[v]*(1+pp[v]);phases[v]=(p+rr[v])%(sample.Length-1);}
                pcm[n*2]=(short)Math.Clamp(left,-30000,30000);pcm[n*2+1]=(short)Math.Clamp(right,-30000,30000);}
            Marshal.Copy(pcm,0,buffers[b],pcm.Length);waveOutWrite(handle,headers[b],(uint)Marshal.SizeOf<Header>());queued[b]=true;}Thread.Sleep(4);}}
    public void Dispose(){running=false;worker?.Join(1000);if(handle!=IntPtr.Zero){waveOutReset(handle);for(int i=0;i<4;i++){if(headers[i]!=IntPtr.Zero){waveOutUnprepareHeader(handle,headers[i],(uint)Marshal.SizeOf<Header>());Marshal.FreeHGlobal(headers[i]);}if(buffers[i]!=IntPtr.Zero)Marshal.FreeHGlobal(buffers[i]);}waveOutClose(handle);}Available=false;}
}
