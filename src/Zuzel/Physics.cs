using System.Numerics;

namespace Zuzel;
public readonly record struct Drive(float Gas,float Steer);
public sealed class Rider
{
    public int Id; public Vector2 Pos,Vel,PreviousPosition; public float PreviousHeading,Heading,Distance,PreviousPhase,FinishTime;
    public bool Finished,Wall,Disqualified; public float Gas,Steer,LegExtension; public int WallHits;
    public float Speed=>Vel.Length();
    public int Lap=>Math.Clamp((int)(Math.Max(0,Distance)/Race.Length)+1,1,4);
}
public sealed class Race
{
    public const float A=240,R=340,Cx=640,Cy=400,Inner=292,Outer=388;
    public const float Length=4*A+2*MathF.PI*R;
    public Rider[] Riders=new Rider[4]; public List<int> Results=new(); public float Time;
    public Race(){Reset();}
    public const float GridDistance=36;
    public void Reset(int gridRotation=0){Time=0;Results.Clear();for(int i=0;i<4;i++){int gate=(i+gridRotation)%4;Riders[i]=new Rider{Id=i,Pos=new(Cx+GridDistance,Cy-R-30+gate*20),Heading=MathF.PI,Distance=-GridDistance};Riders[i].PreviousPhase=Phase(Riders[i].Pos);Riders[i].PreviousPosition=Riders[i].Pos;Riders[i].PreviousHeading=Riders[i].Heading;}}
    public void Stage(float dt,Drive[] inputs){Step(dt,inputs);Time=0;foreach(var r in Riders){if(r.Finished)continue;float frontX=r.Pos.X+MathF.Cos(r.Heading)*19;if(frontX<=Cx&&r.Pos.Y<Cy-Inner+20){r.Disqualified=true;r.Finished=true;r.FinishTime=float.PositiveInfinity;r.Vel=Vector2.Zero;r.Gas=0;}}CompleteClassification();}
    void CompleteClassification(){if(Riders.All(r=>r.Finished))foreach(var r in Riders.Where(r=>r.Disqualified))if(!Results.Contains(r.Id))Results.Add(r.Id);}
    public static float Wrap(float n){while(n>MathF.PI)n-=2*MathF.PI;while(n< -MathF.PI)n+=2*MathF.PI;return n;}
    public static float Phase(Vector2 p){float d;if(p.X<Cx-A){float t=MathF.Atan2(p.Y-Cy,p.X-(Cx-A));if(t> -MathF.PI/2)t-=2*MathF.PI;d=A+R*(-MathF.PI/2-t);}
        else if(p.X>Cx+A){float t=MathF.Atan2(p.Y-Cy,p.X-(Cx+A));d=3*A+MathF.PI*R+R*(MathF.PI/2-t);}
        else if(p.Y<Cy)d=Cx-p.X;else d=A+MathF.PI*R+p.X-(Cx-A);
        return (d+Length)%Length;}
    public static (Vector2 Point,float Heading) TrackPoint(float d,float lane=0){d=(d%Length+Length)%Length;float rr=R+lane;if(d<A)return(new(Cx-d,Cy-rr),MathF.PI);
        if(d<A+MathF.PI*R){float t=-MathF.PI/2-(d-A)/R;return(new(Cx-A+MathF.Cos(t)*rr,Cy+MathF.Sin(t)*rr),t-MathF.PI/2);}
        if(d<3*A+MathF.PI*R)return(new(Cx-A+d-A-MathF.PI*R,Cy+rr),0);
        if(d<3*A+2*MathF.PI*R){float t=MathF.PI/2-(d-3*A-MathF.PI*R)/R;return(new(Cx+A+MathF.Cos(t)*rr,Cy+MathF.Sin(t)*rr),t-MathF.PI/2);}
        return(new(Cx+A-(d-3*A-2*MathF.PI*R),Cy-rr),MathF.PI);}
    public Drive AI(Rider r){float lane=-20+r.Id*13;float look=75+r.Speed*.25f;var target=TrackPoint(Phase(r.Pos)+look,lane);float desired=MathF.Atan2(target.Point.Y-r.Pos.Y,target.Point.X-r.Pos.X);float error=Wrap(desired-r.Heading);return new(Math.Abs(error)>.9f?.35f:1,Math.Clamp(error*2.8f,-1,1));}
    public static void UpdatePose(Rider r,float dt){float arc=Math.Clamp((Math.Abs(r.Pos.X-Cx)-A)/85,0,1);if(arc<=0){r.LegExtension=0;return;}float moving=Math.Clamp((r.Speed-30)/35,0,1);float target=r.Finished?0:arc*moving;r.LegExtension+=Math.Clamp(target-r.LegExtension,-3.5f*dt,3.5f*dt);}
    public void Step(float dt,Drive[] input){Time+=dt;foreach(var r in Riders){r.PreviousPosition=r.Pos;r.PreviousHeading=r.Heading;if(r.Finished)continue;var drive=input[r.Id];r.Gas=Math.Clamp(drive.Gas,0,1);r.Steer=Math.Clamp(drive.Steer,-1,1);r.Wall=false;
        float speed=r.Speed;
        // Steering must remain usable after an impact, including at zero speed.
        float yaw=(.8f+speed/2400)*r.Steer;r.Heading=Wrap(r.Heading+yaw*dt);
        Vector2 f=new(MathF.Cos(r.Heading),MathF.Sin(r.Heading));Vector2 lateral=r.Vel-f*Vector2.Dot(r.Vel,f);
        r.Vel-=lateral*Math.Min(1,3.6f*dt);float forward=Vector2.Dot(r.Vel,f);
        float force=r.Gas*112-10-forward*.13f-(r.Gas<.05f?32:0);r.Vel+=f*(force*dt);
        if(Vector2.Dot(r.Vel,f)<0)r.Vel-=f*Vector2.Dot(r.Vel,f);if(r.Vel.Length()>245)r.Vel=Vector2.Normalize(r.Vel)*245;
        r.Pos+=r.Vel*dt;Constrain(r);
    }
    for(int i=0;i<4;i++)for(int j=i+1;j<4;j++){var a=Riders[i];var b=Riders[j];if(a.Finished||b.Finished)continue;Vector2 delta=a.Pos-b.Pos;float dist=delta.Length();if(dist<13&&dist>.001f){Vector2 normal=delta/dist;float penetration=(13-dist)*.5f;a.Pos+=normal*penetration;b.Pos-=normal*penetration;float approach=Vector2.Dot(a.Vel-b.Vel,normal);if(approach<0){Vector2 impulse=normal*approach*.65f;a.Vel-=impulse;b.Vel+=impulse;}Constrain(a);Constrain(b);}}
    foreach(var r in Riders){UpdatePose(r,dt);if(r.Finished)continue;float phase=Phase(r.Pos),delta=phase-r.PreviousPhase;if(delta>Length/2)delta-=Length;if(delta< -Length/2)delta+=Length;r.Distance+=delta;r.PreviousPhase=phase;if(r.Distance>=4*Length){r.Finished=true;r.FinishTime=Time;Results.Add(r.Id);r.Vel=Vector2.Zero;}}
    CompleteClassification();}
    static void Constrain(Rider r){Vector2 c=new(Math.Clamp(r.Pos.X,Cx-A,Cx+A),Cy);Vector2 v=r.Pos-c;float dist=v.Length();if(dist<Inner||dist>Outer){Vector2 n=dist>.01f?v/dist:new(0,-1);r.Pos=c+n*Math.Clamp(dist,Inner+.25f,Outer-.25f);float vn=Vector2.Dot(r.Vel,n);if((dist>Outer&&vn>0)||(dist<Inner&&vn<0)){r.Vel-=n*vn*1.12f;if(Math.Abs(vn)>20)r.Vel*=.8f;}r.Wall=true;r.WallHits++;}}
    public IEnumerable<Rider> Ordered()=>Riders.OrderBy(r=>r.Disqualified?2:r.Finished?0:1).ThenBy(r=>r.Finished?r.FinishTime:-r.Distance);
}
