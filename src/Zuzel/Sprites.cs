using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;

namespace Zuzel;
// One fixed motorcycle/body and one articulated, opaque two-part left leg.
// No crossfade or pixel morphing: every cached pose contains a single silhouette.
sealed class RiderSprites:IDisposable
{
    const int Steps=24;
    readonly Bitmap?[,] images=new Bitmap?[4,Steps+1];
    public bool HasStraightPose{get;private set;}
    static readonly PointF Hip=new(185,292),OriginalKnee=new(124,240),OriginalFoot=new(62,190);
    static readonly PointF FoldedKnee=new(187,247),FoldedFoot=new(252,300);
    static readonly PointF[] Thigh={new(202,275),new(141,221),new(105,259),new(171,315)};
    static readonly PointF[] Shin={new(141,221),new(95,165),new(58,157),new(27,183),new(38,211),new(105,259)};
    static Bitmap? Load(string suffix){var asm=Assembly.GetExecutingAssembly();var name=asm.GetManifestResourceNames().FirstOrDefault(n=>n.EndsWith(suffix));if(name==null)return null;using var stream=asm.GetManifestResourceStream(name)!;using var source=new Bitmap(stream);return new Bitmap(source);}
    static PointF Lerp(PointF a,PointF b,float t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
    static GraphicsPath Polygon(PointF[] points){var path=new GraphicsPath();path.AddPolygon(points);return path;}
    static void DrawBone(Graphics g,Bitmap source,PointF[] outline,PointF a,PointF b,PointF newA,PointF newB){float oldAngle=MathF.Atan2(b.Y-a.Y,b.X-a.X),newAngle=MathF.Atan2(newB.Y-newA.Y,newB.X-newA.X);float oldLength=MathF.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y)),newLength=MathF.Sqrt((newB.X-newA.X)*(newB.X-newA.X)+(newB.Y-newA.Y)*(newB.Y-newA.Y));var state=g.Save();g.TranslateTransform(newA.X,newA.Y);g.RotateTransform((newAngle-oldAngle)*180/MathF.PI);g.ScaleTransform(newLength/oldLength,newLength/oldLength);g.TranslateTransform(-a.X,-a.Y);using var path=Polygon(outline);g.SetClip(path,CombineMode.Intersect);g.DrawImage(source,new Rectangle(0,0,640,640));g.Restore(state);}
    public RiderSprites(){using var atlas=Load("riders.png");if(atlas==null)return;HasStraightPose=true;int cellW=atlas.Width/2,cellH=atlas.Height/2;
        for(int id=0;id<4;id++){
            using var source=new Bitmap(640,640,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(source)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(atlas,new Rectangle(0,0,640,640),new Rectangle((id%2)*cellW,(id/2)*cellH,cellW,cellH),GraphicsUnit.Pixel);}
            // Broad leg mask ends at the hip and never touches wheels, helmet or arms.
            using var legMask=Polygon(new[]{new PointF(20,140),new PointF(110,140),new PointF(212,275),new PointF(181,325),new PointF(20,235)});
            for(int step=0;step<=Steps;step++){
                float t=step/(float)Steps;t=t*t*(3-2*t);var knee=Lerp(FoldedKnee,OriginalKnee,t);var foot=Lerp(FoldedFoot,OriginalFoot,t);
                var frame=new Bitmap(144,144,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(frame)){
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(144f/640,144f/640);
                    // Draw the solid articulated leg first; the fixed torso covers its hip.
                    DrawBone(g,source,Shin,OriginalKnee,OriginalFoot,knee,foot);
                    DrawBone(g,source,Thigh,Hip,OriginalKnee,Hip,knee);
                    var state=g.Save();g.SetClip(legMask,CombineMode.Exclude);g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(source,new Rectangle(0,0,640,640));g.Restore(state);
                }images[id,step]=frame;
            }
        }
    }
    public static (Vector2 Position,float Heading) Presentation(Rider r,float alpha){alpha=Math.Clamp(alpha,0,1);return(Vector2.Lerp(r.PreviousPosition,r.Pos,alpha),r.PreviousHeading+Race.Wrap(r.Heading-r.PreviousHeading)*alpha);}
    public bool Draw(Graphics g,Rider r,float alpha=1){var image=images[r.Id,(int)MathF.Round(Math.Clamp(r.LegExtension,0,1)*Steps)];if(image==null)return false;var pose=Presentation(r,alpha);var saved=g.Save();g.TranslateTransform(pose.Position.X,pose.Position.Y);g.RotateTransform(pose.Heading*180/MathF.PI);g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.HighQuality;using var shadow=new SolidBrush(Color.FromArgb(65,0,0,0));g.FillEllipse(shadow,-18,-1,40,9);
        // Pixel anchors are constant for every pose. The tires lie at y=366.
        const float unit=40f/576;g.DrawImage(image,new RectangleF(-312*unit,-366*unit,640*unit,640*unit));g.Restore(saved);return true;}
    public void SavePosePreview(string path){using var bmp=new Bitmap(1200,360);using var g=Graphics.FromImage(bmp);g.Clear(Color.FromArgb(45,66,47));g.InterpolationMode=InterpolationMode.NearestNeighbor;for(int i=0;i<5;i++)if(images[0,i*Steps/4] is {} frame){g.DrawImage(frame,new Rectangle(10+i*238,0,238,238));using var font=new Font("Segoe UI",16);g.DrawString(i==0?"PROSTA":i==4?"ŁUK":"RUCH NOGI",font,Brushes.White,18+i*238,260);}bmp.Save(path);}
    public bool RigidPartsStayIdentical(){var regions=new[]{new Rectangle(60,72,14,14),new Rectangle(105,78,26,6),new Rectangle(7,78,20,6)};for(int id=0;id<4;id++){var reference=images[id,0];if(reference==null)return false;for(int step=1;step<=Steps;step++){var frame=images[id,step]!;foreach(var r in regions)for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)if(frame.GetPixel(x,y)!=reference.GetPixel(x,y))return false;}}return true;}
    public void Dispose(){foreach(var image in images)image?.Dispose();}
}
