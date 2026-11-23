using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Zuzel;
// Project the photographic material onto the exact capsule used by the physics.
// Both kerbs and the two semicircles are drawn from the same shared dimensions.
static class TrackArtwork
{
    public const float InnerKerb=Race.Inner-6,OuterWall=Race.Outer+6;
    public static GraphicsPath Oval(float radius){var p=new GraphicsPath();float a=Race.A,cx=Race.Cx,cy=Race.Cy;p.AddLine(cx-a,cy-radius,cx+a,cy-radius);p.AddArc(cx+a-radius,cy-radius,2*radius,2*radius,270,180);p.AddLine(cx+a,cy+radius,cx-a,cy+radius);p.AddArc(cx-a-radius,cy-radius,2*radius,2*radius,90,180);p.CloseFigure();return p;}
    static float Radius(double angle,float radius){double ux=Math.Cos(angle),uy=Math.Sin(angle);double straight=Math.Abs(uy)>.00001?radius/Math.Abs(uy):double.MaxValue;if(straight*Math.Abs(ux)<=Race.A)return (float)straight;return (float)(Race.A*Math.Abs(ux)+Math.Sqrt(radius*radius-Race.A*Race.A*uy*uy));}
    public static Bitmap Build(Bitmap photo){const int w=1280,h=800,n=2048;using var scaled=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(scaled))g.DrawImage(photo,new Rectangle(0,0,w,h));int[] src=new int[w*h];var locked=scaled.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);Marshal.Copy(locked.Scan0,src,0,src.Length);scaled.UnlockBits(locked);
        int Sample(double x,double y)=>src[Math.Clamp((int)y,0,h-1)*w+Math.Clamp((int)x,0,w-1)];
        var inner=new float[n];var outer=new float[n];
        for(int i=0;i<n;i++){double angle=i*Math.PI*2/n,ux=Math.Cos(angle),uy=Math.Sin(angle);float limit=(float)Math.Min(Math.Abs(ux)<.00001?9999:639/Math.Abs(ux),Math.Abs(uy)<.00001?9999:399/Math.Abs(uy));float si=Radius(angle,InnerKerb),so=limit;int run=0;
            for(int r=180;r<limit-35;r++){int c=Sample(640+ux*r,400+uy*r),red=(c>>16)&255,green=(c>>8)&255,blue=c&255;bool dirt=red>green*1.1&&green>blue*1.12;if(dirt)run++;else run=0;if(run>=7){si=r-7;break;}}
            for(int r=(int)si+45;r<limit;r++){int c=Sample(640+ux*r,400+uy*r),red=(c>>16)&255,green=(c>>8)&255,blue=c&255;bool border=green>red*.94&&blue>red*.78&&red>115;if(border){so=r;break;}}
            inner[i]=si;outer[i]=Math.Max(si+50,so);
        }
        float Smooth(float[] a,int i){float sum=0;for(int j=-6;j<=6;j++)sum+=a[(i+j+n)%n];return sum/13;}
        var smInner=Enumerable.Range(0,n).Select(i=>Smooth(inner,i)).ToArray();var smOuter=Enumerable.Range(0,n).Select(i=>Smooth(outer,i)).ToArray();int[] pixels=new int[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){double dx=x-640,dy=y-400,distance=Math.Sqrt(dx*dx+dy*dy),angle=Math.Atan2(dy,dx);if(angle<0)angle+=2*Math.PI;int i=(int)(angle/(2*Math.PI)*n)%n;float targetIn=Radius(angle,InnerKerb),targetOut=Radius(angle,OuterWall);if(distance>targetOut+3){int noise=((x*73856093)^(y*19349663))&15;pixels[y*w+x]=Color.FromArgb(150+noise,151+noise,139+noise).ToArgb();continue;}double sourceDistance=distance<targetIn?distance/targetIn*smInner[i]:distance<targetOut?smInner[i]+(distance-targetIn)/(targetOut-targetIn)*(smOuter[i]-smInner[i]):smOuter[i]+distance-targetOut;double ux=distance>.01?dx/distance:0,uy=distance>.01?dy/distance:0;pixels[y*w+x]=Sample(640+ux*sourceDistance,400+uy*sourceDistance);}
        var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);locked=result.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);result.UnlockBits(locked);
        using(var g=Graphics.FromImage(result)){g.SmoothingMode=SmoothingMode.AntiAlias;using var inside=Oval(InnerKerb);using var outside=Oval(OuterWall);using var kerb=new Pen(Color.FromArgb(230,229,211),2.6f);using var wall=new Pen(Color.FromArgb(236,235,215),4);using var rim=new Pen(Color.FromArgb(220,188,65),1.4f);g.DrawPath(kerb,inside);g.DrawPath(wall,outside);using var rimPath=Oval(OuterWall+2.5f);g.DrawPath(rim,rimPath);}return result;
    }
}
