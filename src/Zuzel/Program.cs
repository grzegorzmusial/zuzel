using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Zuzel;
static class Program
{
    [STAThread] static int Main(string[] args){ApplicationConfiguration.Initialize();if(args.Length==2&&args[0]=="--pose-preview"){using var poses=new RiderSprites();poses.SavePosePreview(args[1]);return 0;}if(args.Contains("--self-test"))return Verification.Run();
        using var form=new ZuzelForm();if(args.Length==2&&(args[0]=="--render-preview"||args[0]=="--preview-race"||args[0]=="--preview-results")){using var bmp=new Bitmap(1920,1080);if(args[0]=="--preview-race")form.PrepareRacePreview();if(args[0]=="--preview-results")form.PrepareResultsPreview();using(var graphics=Graphics.FromImage(bmp))form.Render(graphics,bmp.Width,bmp.Height);bmp.Save(args[1]);return 0;}if(args.Length==2&&args[0]=="--render-stress")return form.RenderStress(args[1]);if(args.Length==2&&args[0]=="--smoke-test")form.StartSmokeTest(args[1]);Application.Run(form);return 0;}
}
enum Scene{Menu,Countdown,Race,Paused,Results}
sealed class Dust{public Vector2 Pos,Vel;public float Life,Size;}
public sealed class ZuzelForm:Form
{
    readonly GameKeyboard keyboard;readonly RiderSprites riderSprites=new();
    readonly Race race=new();readonly Tournament tournament=new();readonly Bindings bindings=Bindings.Load();readonly EngineAudio audio=new();readonly System.Windows.Forms.Timer timer=new(){Interval=1};readonly Stopwatch clock=Stopwatch.StartNew();
    readonly Bitmap track,skids=new(1280,800);Bitmap? trackCache;readonly List<(Vector2 A,Vector2 B)> pendingSkids=new();readonly Vector2[] priorPositions=new Vector2[4];readonly Random random=new(512);readonly List<Dust> dust=new();readonly Color[] colors={Color.FromArgb(240,65,52),Color.FromArgb(54,130,244),Color.FromArgb(247,245,227),Color.FromArgb(255,209,54)};
    readonly Font titleFont=new("Bahnschrift",37,FontStyle.Bold,GraphicsUnit.Pixel),labelFont=new("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel),bodyFont=new("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel),boldFont=new("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel),bigFont=new("Bahnschrift",72,FontStyle.Bold,GraphicsUnit.Pixel);
    readonly Color lime=Color.FromArgb(215,246,94),dark=Color.FromArgb(18,27,25),dim=Color.FromArgb(156,172,164);
    Scene scene=Scene.Menu,resume=Scene.Race;double previous;float countdown,accumulator;bool fullscreen;Rectangle oldBounds;readonly Dictionary<string,RectangleF> buttons=new();RectangleF worldRect;float uiScale=1;readonly Drive[] controls=new Drive[4];bool focused=true;bool showFps;double lastPaint,fps;string? smokePath;int smokeFrames,smokePaints;double smokeMaxGap;
    public void PrepareResultsPreview(){tournament.Start(CompetitionMode.Team);for(int heat=0;heat<3;heat++){race.Reset(heat);for(int n=0;n<180*120&&race.Results.Count<4;n++){for(int i=0;i<4;i++)controls[i]=race.AI(race.Riders[i]);race.Step(1f/120,controls);}tournament.Record(race);if(heat<2)tournament.Advance();}scene=Scene.Results;}
    public void PrepareRacePreview(){scene=Scene.Race;for(int n=0;n<1440;n++){for(int i=0;i<4;i++)controls[i]=race.AI(race.Riders[i]);race.Step(1f/120,controls);}}
    public void StartSmokeTest(string path){smokePath=path;Shown+=(_,_)=>StartRace();}
    public int RenderStress(string path)
    {
        using var target=new Bitmap(1920,1080);using var graphics=Graphics.FromImage(target);var report=new System.Text.StringBuilder();bool success=true;
        for(int mode=0;mode<2;mode++){
            scene=Scene.Race;race.Reset();keyboard.Clear();double steeringDebt=0;double maximum=0;var watch=Stopwatch.StartNew();
            for(int frame=0;frame<360;frame++){
                for(int step=0;step<32;step++){
                    for(int i=0;i<4;i++)controls[i]=race.AI(race.Riders[i]);
                    if(mode==1){var desired=controls[0];steeringDebt+=desired.Steer;bool left=steeringDebt<-.5,right=steeringDebt>.5;if(left)steeringDebt++;if(right)steeringDebt--;
                        keyboard.Process(desired.Gas>.5?0x100:0x101,(int)Keys.W);keyboard.Process(left?0x100:0x101,(int)Keys.A);keyboard.Process(right?0x100:0x101,(int)Keys.D);
                        controls[0]=new(keyboard.IsDown((int)Keys.W)?1:0,(keyboard.IsDown((int)Keys.D)?1:0)-(keyboard.IsDown((int)Keys.A)?1:0));
                        // Exercise the native message filter's held-key repeat path too.
                        for(int repeat=0;repeat<8;repeat++)keyboard.Process(0x100,(int)Keys.W);
                    }
                    race.Step(1f/120,controls);
                }
                audio.Update(race,true,false);double begin=watch.Elapsed.TotalMilliseconds;graphics.ResetTransform();Render(graphics,target.Width,target.Height);maximum=Math.Max(maximum,watch.Elapsed.TotalMilliseconds-begin);
            }
            report.AppendLine($"{(mode==0?"AI":"KEYBOARD")} frames=360 finished={race.Results.Count} avg_ms={watch.Elapsed.TotalMilliseconds/360:F2} max_render_ms={maximum:F2}");success&=race.Results.Count==4;
        }
        keyboard.Clear();File.WriteAllText(path,report.ToString());target.Save(Path.ChangeExtension(path,"png"));return success?0:1;
    }
    public ZuzelForm(){Text="Żużel — 1.5";ClientSize=new(1440,1020);MinimumSize=new(960,740);StartPosition=FormStartPosition.CenterScreen;BackColor=dark;DoubleBuffered=true;KeyPreview=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);track=LoadTrack();keyboard=new GameKeyboard(()=>IsHandleCreated?Handle:IntPtr.Zero,()=>focused&&ActiveForm==this);keyboard.Pressed+=key=>HandleKey(this,new KeyEventArgs(key));timer.Tick+=Tick;timer.Start();Shown+=(_,_)=>SetFullscreen(true);MouseClick+=HandleClick;Deactivate+=(_,_)=>{focused=false;keyboard.Clear();if(scene is Scene.Race or Scene.Countdown){resume=scene;scene=Scene.Paused;}};Activated+=(_,_)=>focused=true;FormClosing+=(_,_)=>{timer.Stop();bindings.Save();audio.Dispose();};}
    Bitmap LoadTrack(){var assembly=Assembly.GetExecutingAssembly();string? name=assembly.GetManifestResourceNames().FirstOrDefault(n=>n.EndsWith("track.png"));if(name!=null){using var s=assembly.GetManifestResourceStream(name)!;using var source=new Bitmap(s);return TrackArtwork.Build(source);}return MakeTrack();}
    static GraphicsPath Oval(float radius){var p=new GraphicsPath();float a=Race.A;p.AddLine(640-a,400-radius,640+a,400-radius);p.AddArc(640+a-radius,400-radius,2*radius,2*radius,270,180);p.AddLine(640+a,400+radius,640-a,400+radius);p.AddArc(640-a-radius,400-radius,2*radius,2*radius,90,180);p.CloseFigure();return p;}
    Bitmap MakeTrack(){var bmp=new Bitmap(1280,800);using var g=Graphics.FromImage(bmp);g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.FromArgb(162,163,149));using(var path=Oval(375)){g.FillPath(Brushes.Silver,path);using var pen=new Pen(Color.FromArgb(232,210,120),7);g.DrawPath(pen,path);}using(var path=Oval(371)){using var dirt=new SolidBrush(Color.FromArgb(125,101,65));g.FillPath(dirt,path);g.SetClip(path);for(int i=0;i<145000;i++){int x=random.Next(1280),y=random.Next(800);using var b=new SolidBrush(Color.FromArgb(35,random.Next(120,235),random.Next(90,180),random.Next(50,100)));g.FillRectangle(b,x,y,random.Next(1,4),1);}g.ResetClip();}for(int i=0;i<80;i++){using var path=Oval(286+i*1.05f);using var pen=new Pen(Color.FromArgb(i%4==0?70:28,57,43,26),.6f);g.DrawPath(pen,path);}using(var path=Oval(284)){using var green=new SolidBrush(Color.FromArgb(57,91,42));g.FillPath(green,path);g.SetClip(path);for(int i=0;i<16;i++){using var stripe=new SolidBrush(i%2==0?Color.FromArgb(61,97,43):Color.FromArgb(54,86,38));g.FillRectangle(stripe,i*80,100,80,600);}for(int i=0;i<45000;i++){using var b=new SolidBrush(Color.FromArgb(25,random.Next(130,210),random.Next(140,220),60));g.FillRectangle(b,random.Next(1280),random.Next(800),1,1);}using var line=new Pen(Color.FromArgb(145,230,232,206),1);g.DrawRectangle(line,235,225,810,350);g.DrawLine(line,640,225,640,575);g.DrawEllipse(line,588,348,104,104);g.ResetClip();using var kerb=new Pen(Color.FromArgb(235,231,210),3);g.DrawPath(kerb,path);}return bmp;}
    void StartCompetition(){tournament.Start(bindings.Mode);StartRace();}
    void ContinueCompetition(){if(tournament.Advance())StartRace();else if(tournament.Complete)StartCompetition();}
    void StartRace(){race.Reset(tournament.CurrentHeat-1);dust.Clear();pendingSkids.Clear();trackCache?.Dispose();trackCache=null;using(var g=Graphics.FromImage(skids))g.Clear(Color.Transparent);countdown=3.4f;accumulator=0;scene=Scene.Countdown;}
    void TogglePause(){if(scene==Scene.Paused)scene=resume;else if(scene is Scene.Race or Scene.Countdown){resume=scene;scene=Scene.Paused;}}
    void HandleKey(object? sender,KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){if(scene==Scene.Menu)StartCompetition();else if(scene==Scene.Results)ContinueCompetition();else if(scene==Scene.Paused)TogglePause();e.Handled=true;}else if(e.KeyCode==Keys.Escape){if(scene==Scene.Menu)Close();else if(scene==Scene.Results)scene=Scene.Menu;else TogglePause();}else if(e.KeyCode==Keys.P)TogglePause();else if(e.KeyCode==Keys.R){if(scene==Scene.Menu)StartCompetition();else if(scene==Scene.Results)ContinueCompetition();else if(scene==Scene.Paused)StartRace();}else if(e.KeyCode==Keys.F2)Configure();else if(e.KeyCode==Keys.M){bindings.Muted=!bindings.Muted;bindings.Save();}else if(e.KeyCode==Keys.F3)showFps=!showFps;else if(e.KeyCode==Keys.F11)SetFullscreen(!fullscreen);else if(scene==Scene.Menu&&e.KeyCode>=Keys.D1&&e.KeyCode<=Keys.D4){bindings.Humans=e.KeyCode-Keys.D1+1;bindings.Save();}Invalidate();}
    void SetFullscreen(bool enabled){if(fullscreen==enabled)return;fullscreen=enabled;if(enabled){oldBounds=Bounds;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Normal;Bounds=Screen.FromControl(this).Bounds;}else{FormBorderStyle=FormBorderStyle.Sizable;Bounds=oldBounds;}}
    void Configure(){if(scene is Scene.Race or Scene.Countdown){resume=scene;scene=Scene.Paused;}using var dialog=new ControlSetup(bindings,colors);dialog.ShowDialog(this);bindings.Save();}
    void HandleClick(object? sender,MouseEventArgs e){var p=new PointF(e.X/uiScale,e.Y/uiScale);foreach(var (name,rect) in buttons){if(!rect.Contains(p))continue;switch(name){case "start":StartCompetition();break;case "mode-individual":bindings.Mode=CompetitionMode.Individual;bindings.Save();break;case "mode-team":bindings.Mode=CompetitionMode.Team;bindings.Save();break;case "next":ContinueCompetition();break;case "new-competition":StartCompetition();break;case "controls":Configure();break;case "pause":TogglePause();break;case "sound":bindings.Muted=!bindings.Muted;bindings.Save();break;case "menu":scene=Scene.Menu;break;case "resume":TogglePause();break;case "again":StartRace();break;default:if(name.StartsWith("human")){bindings.Humans=int.Parse(name[5..]);bindings.Save();}break;}Invalidate();break;}}
    bool Down(int key)=>focused&&keyboard.IsDown(key);
    void Tick(object? sender,EventArgs e){double now=clock.Elapsed.TotalSeconds;if(now-previous<1d/65)return;float dt=(float)Math.Min(now-previous,.06);if(smokePath!=null){smokeFrames++;if(previous>0)smokeMaxGap=Math.Max(smokeMaxGap,now-previous);if(scene==Scene.Paused)scene=resume;}previous=now;for(int i=0;i<4;i++){Drive pad=Pads.Read(i);if(smokePath==null&&i<bindings.Humans){var k=bindings.Keys[i];float steer=(Down(k[2])?1:0)-(Down(k[1])?1:0);controls[i]=new(Math.Max(Down(k[0])?1:0,pad.Gas),Math.Abs(steer)>0?steer:pad.Steer);}else controls[i]=scene==Scene.Countdown?default:race.AI(race.Riders[i]);}
        if(scene==Scene.Countdown){int stageSteps=Math.Max(1,(int)Math.Ceiling(dt*120));for(int n=0;n<stageSteps;n++)race.Stage(dt/stageSteps,controls);countdown-=dt;if(countdown<=0){scene=Scene.Race;accumulator=0;}}
        if(scene==Scene.Race){accumulator+=dt;int steps=0;while(accumulator>=1f/120&&steps++<10){for(int i=0;i<4;i++)priorPositions[i]=race.Riders[i].Pos;race.Step(1f/120,controls);using var g=Graphics.FromImage(skids);using var pen=new Pen(Color.FromArgb(18,40,28,16),1.5f);for(int i=0;i<4;i++){var r=race.Riders[i];if(r.Speed>60&&Math.Abs(r.Steer)>.15){g.DrawLine(pen,priorPositions[i].X,priorPositions[i].Y,r.Pos.X,r.Pos.Y);pendingSkids.Add((priorPositions[i],r.Pos));if(random.Next(6)==0&&dust.Count<700)dust.Add(new Dust{Pos=r.Pos-new Vector2(MathF.Cos(r.Heading),MathF.Sin(r.Heading))*9,Vel=-r.Vel*.13f+new Vector2(random.Next(-10,11),random.Next(-10,11)),Life=.8f,Size=random.Next(2,5)});}}accumulator-=1f/120;}
            for(int i=dust.Count-1;i>=0;i--){var p=dust[i];p.Life-=dt;p.Pos+=p.Vel*dt;p.Size+=dt*8;if(p.Life<=0)dust.RemoveAt(i);}if(race.Results.Count==4){tournament.Record(race);scene=Scene.Results;float best=race.Riders.Take(bindings.Humans).Where(r=>!r.Disqualified).Select(r=>r.FinishTime).DefaultIfEmpty(0).Min();if(best>0&&smokePath==null&&(bindings.Best<=0||best<bindings.Best)){bindings.Best=best;bindings.Save();}}}
        audio.Update(race,scene is Scene.Race or Scene.Countdown,bindings.Muted);Invalidate();if(smokePath!=null&&(scene==Scene.Results||clock.Elapsed.TotalSeconds>100)){File.WriteAllText(smokePath,$"finished={race.Results.Count}\ntime={race.Time:F2}\nframes={smokeFrames}\npaints={smokePaints}\nmax_frame_gap_ms={smokeMaxGap*1000:F1}\naudio={audio.Available}\n");using var preview=new Bitmap(ClientSize.Width,ClientSize.Height);using(var previewGraphics=Graphics.FromImage(preview))Render(previewGraphics,preview.Width,preview.Height);preview.Save(Path.ChangeExtension(smokePath,"png"));Close();}}
    static string TimeText(float t)=>$"{(int)t/60:00}:{t%60:00.00}";
    void TextAt(Graphics g,string text,Font f,Color color,float x,float y){using var b=new SolidBrush(color);g.DrawString(text,f,b,x,y);}
    void Fill(Graphics g,Color c,float x,float y,float w,float h){using var b=new SolidBrush(c);g.FillRectangle(b,x,y,w,h);}
    void Button(Graphics g,string id,string text,RectangleF r,bool primary=false){buttons[id]=r;Fill(g,primary?lime:Color.FromArgb(38,51,46),r.X,r.Y,r.Width,r.Height);if(!primary){using var pen=new Pen(Color.FromArgb(76,93,82));g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}var size=g.MeasureString(text,boldFont);TextAt(g,text,boldFont,primary?dark:Color.FromArgb(229,235,225),r.X+(r.Width-size.Width)/2,r.Y+(r.Height-size.Height)/2);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);smokePaints++;double now=clock.Elapsed.TotalSeconds;if(lastPaint>0)fps=fps==0?1/(now-lastPaint):fps*.9+.1/(now-lastPaint);lastPaint=now;Render(e.Graphics,ClientSize.Width,ClientSize.Height);}
    public void Render(Graphics g,int width,int height)
    {
        g.Clear(dark);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        uiScale=Math.Clamp(width/1440f,.65f,1.5f);g.ScaleTransform(uiScale,uiScale);float w=width/uiScale,h=height/uiScale;buttons.Clear();
        float scale=Math.Min((w-8)/1280,(h-8)/800);worldRect=new((w-1280*scale)/2,(h-800*scale)/2,1280*scale,800*scale);
        int cw=Math.Max(1,(int)Math.Round(worldRect.Width*uiScale)),ch=Math.Max(1,(int)Math.Round(worldRect.Height*uiScale));
        bool rebuilt=trackCache==null||trackCache.Width!=cw||trackCache.Height!=ch;
        if(rebuilt){trackCache?.Dispose();trackCache=new Bitmap(cw,ch);using var cg=Graphics.FromImage(trackCache);cg.InterpolationMode=InterpolationMode.HighQualityBicubic;cg.DrawImage(track,new Rectangle(0,0,cw,ch));cg.DrawImage(skids,new Rectangle(0,0,cw,ch));}
        else if(pendingSkids.Count>0){using var cg=Graphics.FromImage(trackCache!);cg.ScaleTransform(cw/1280f,ch/800f);using var pen=new Pen(Color.FromArgb(18,40,28,16),1.5f);foreach(var mark in pendingSkids)cg.DrawLine(pen,mark.A.X,mark.A.Y,mark.B.X,mark.B.Y);}
        pendingSkids.Clear();var cached=g.Save();g.ResetTransform();g.DrawImageUnscaled(trackCache!,(int)Math.Round(worldRect.X*uiScale),(int)Math.Round(worldRect.Y*uiScale));g.Restore(cached);
        var world=g.Save();g.TranslateTransform(worldRect.X,worldRect.Y);g.ScaleTransform(scale,scale);
        foreach(var p in dust){using var b=new SolidBrush(Color.FromArgb((int)(p.Life*35),181,157,107));g.FillEllipse(b,p.Pos.X-p.Size,p.Pos.Y-p.Size,p.Size*2,p.Size*2);}
        using(var line=new Pen(Color.FromArgb(220,245,240,218),1.5f))g.DrawLine(line,640,6,640,114);
        if(scene==Scene.Countdown){using var tape=new Pen(Color.White,3);g.DrawLine(tape,640,6,640,114);}
        foreach(var r in race.Riders)DrawBike(g,r);
        if(scene is Scene.Race or Scene.Paused)DrawFieldScores(g);
        g.Restore(world);
        if(scene==Scene.Menu)DrawMenu(g,w,h);
        if(scene==Scene.Countdown){var excluded=race.Riders.Where(r=>r.Disqualified).ToArray();string text=excluded.Length>0?"TAŚMA!":countdown>.4?Math.Ceiling(countdown-.4).ToString():"START";string sub=excluded.Length>0?"WYKLUCZENI: "+string.Join(", ",excluded.Select(r=>(r.Id+1).ToString())):"CZEKAJ NA TAŚMĘ · FALSTART WYKLUCZA";OverlayText(g,text,sub,w,h);}
        if(scene==Scene.Paused){Panel(g,w/2-240,h/2-140,480,260);TextAt(g,"PAUZA",bigFont,lime,w/2-130,h/2-130);Button(g,"resume","WRÓĆ NA TOR",new(w/2-205,h/2-25,410,48),true);Button(g,"again","NOWY BIEG",new(w/2-205,h/2+36,195,42));Button(g,"menu","MENU",new(w/2+5,h/2+36,200,42));}
        if(scene==Scene.Results)DrawResults(g,w,h);
        if(showFps)TextAt(g,$"FPS {fps:0} · 1.5",boldFont,lime,12,10);
    }
    string RiderName(int id)=>id<bindings.Humans?$"Gracz {id+1}":$"AI {id+1}";
    string TeamScore()=> $"DRUŻYNA A  {tournament.TeamPoints(0)}  :  {tournament.TeamPoints(1)}  DRUŻYNA B";
    void DrawFieldScores(Graphics g)
    {
        Panel(g,374,241,532,315);TextAt(g,"ŻUŻEL",titleFont,Color.White,395,251);TextAt(g,$"CZAS {TimeText(race.Time)}",boldFont,lime,690,266);
        TextAt(g,$"{(tournament.Mode==CompetitionMode.Team?"MECZ DRUŻYNOWY":"TURNIEJ INDYWIDUALNY")} · BIEG {tournament.CurrentHeat} / {Tournament.HeatCount}",labelFont,lime,396,308);
        TextAt(g,"POZ.  ZAWODNIK",labelFont,dim,396,337);TextAt(g,"BIEG / SUMA PUNKTÓW",labelFont,dim,655,337);
        var sorted=race.Ordered().ToArray();for(int place=0;place<4;place++){var r=sorted[place];float y=360+place*33;Fill(g,colors[r.Id],395,y,4,23);string team=tournament.Mode==CompetitionMode.Team?$" [{(Tournament.TeamOf(r.Id)==0?"A":"B")}]":"";TextAt(g,$"{place+1}.  {RiderName(r.Id)}{team}",boldFont,colors[r.Id],410,y);string status=r.Disqualified?"TAŚMA":r.Finished?TimeText(r.FinishTime):$"{r.Lap}/4  {r.Speed*.4f:0} km/h";TextAt(g,$"{status}   ·   {tournament.Points[r.Id]} pkt",bodyFont,r.Disqualified?Color.Salmon:Color.White,655,y);}
        TextAt(g,tournament.Mode==CompetitionMode.Team?TeamScore():"Punkty po biegu: 3 / 2 / 1 / 0 · wykluczenie: 0",labelFont,lime,400,502);
        TextAt(g,"P pauza · F2 klawisze · F11 okno · M dźwięk · F3 FPS",labelFont,dim,400,533);
    }
    void Panel(Graphics g,float x,float y,float w,float h){Fill(g,Color.FromArgb(242,15,28,22),x,y,w,h);using var p=new Pen(Color.FromArgb(115,145,100));g.DrawRectangle(p,x,y,w,h);}
    void DrawMenu(Graphics g,float w,float h)
    {
        float x=w/2-310,y=worldRect.Y+worldRect.Height/2-224;Panel(g,x,y,620,448);TextAt(g,"ŻUŻEL",titleFont,Color.White,x+30,y+17);TextAt(g,"15 biegów · 4 okrążenia w każdym · punkty się sumują",bodyFont,dim,x+32,y+71);
        Button(g,"mode-individual","INDYWIDUALNY",new(x+32,y+108,270,42),bindings.Mode==CompetitionMode.Individual);Button(g,"mode-team","DRUŻYNOWY",new(x+316,y+108,272,42),bindings.Mode==CompetitionMode.Team);
        TextAt(g,"GRACZE PRZY JEDNYM KOMPUTERZE",labelFont,lime,x+32,y+172);for(int i=1;i<=4;i++)Button(g,"human"+i,i.ToString(),new(x+32+(i-1)*58,y+195,48,40),bindings.Humans==i);TextAt(g,$"Gracze: {bindings.Humans} · AI: {4-bindings.Humans}",bodyFont,Color.White,x+300,y+203);
        TextAt(g,bindings.Mode==CompetitionMode.Team?"Drużyna A: 1 + 3    /    Drużyna B: 2 + 4":"Klasyfikacja indywidualna czterech zawodników",bodyFont,Color.White,x+32,y+252);
        Button(g,"start",bindings.Mode==CompetitionMode.Team?"ROZPOCZNIJ MECZ  /  SPACJA":"ROZPOCZNIJ TURNIEJ  /  SPACJA",new(x+32,y+297,556,51),true);
        TextAt(g,"Punkty: 3 / 2 / 1 / 0 · Pola startowe zmieniają się po biegu.",labelFont,lime,x+32,y+367);TextAt(g,"F2 klawisze · F11 okno · F3 FPS · M dźwięk",labelFont,dim,x+32,y+391);TextAt(g,"Czekaj na podniesienie taśmy. Wjazd w taśmę = wykluczenie.",labelFont,dim,x+32,y+415);
    }
    void OverlayText(Graphics g,string text,string sub,float w,float h){var s=g.MeasureString(text,bigFont);TextAt(g,text,bigFont,Color.FromArgb(110,0,0,0),(w-s.Width)/2+3,h/2-80+3);TextAt(g,text,bigFont,lime,(w-s.Width)/2,h/2-80);var ss=g.MeasureString(sub,labelFont);Fill(g,Color.FromArgb(220,17,29,22),(w-ss.Width)/2-14,h/2+6,ss.Width+28,30);TextAt(g,sub,labelFont,Color.White,(w-ss.Width)/2,h/2+12);}
    void DrawResults(Graphics g,float w,float h)
    {
        float x=w/2-325,y=worldRect.Y+worldRect.Height/2-222;Panel(g,x,y,650,444);
        TextAt(g,tournament.Complete?"KLASYFIKACJA KOŃCOWA":$"BIEG {tournament.CurrentHeat} / {Tournament.HeatCount}",titleFont,lime,x+28,y+16);
        string sub=tournament.Complete?tournament.ResultText():tournament.Mode==CompetitionMode.Team?TeamScore():"Wynik biegu i suma punktów turnieju";TextAt(g,sub,bodyFont,Color.White,x+30,y+71);
        TextAt(g,"POZ.   ZAWODNIK",labelFont,dim,x+30,y+116);TextAt(g,"TEN BIEG",labelFont,dim,x+335,y+116);TextAt(g,"SUMA",labelFont,dim,x+514,y+116);
        var order=tournament.Order();for(int row=0;row<4;row++){int id=order[row];float yy=y+144+row*39;var rider=race.Riders[id];Fill(g,colors[id],x+30,yy,4,27);string team=tournament.Mode==CompetitionMode.Team?$" [{(Tournament.TeamOf(id)==0?"A":"B")}]":"";TextAt(g,$"{tournament.Rank(id)}.   {RiderName(id)}{team}",boldFont,colors[id],x+46,yy);TextAt(g,rider.Disqualified?"TAŚMA · 0":$"+{tournament.HeatPoints(id)} pkt",bodyFont,rider.Disqualified?Color.Salmon:Color.White,x+335,yy);TextAt(g,$"{tournament.Points[id]} pkt",boldFont,lime,x+514,yy);}
        string footer=tournament.Mode==CompetitionMode.Team?TeamScore():tournament.Complete?"Równe sumy punktów oznaczają miejsce ex aequo.":$"Zakończono {tournament.History.Count} z {Tournament.HeatCount} biegów. Punkty są zachowane.";TextAt(g,footer,labelFont,dim,x+30,y+307);
        Button(g,tournament.Complete?"new-competition":"next",tournament.Complete?(tournament.Mode==CompetitionMode.Team?"NOWY MECZ  /  SPACJA":"NOWY TURNIEJ  /  SPACJA"):$"NASTĘPNY BIEG ({tournament.CurrentHeat+1} / {Tournament.HeatCount})  /  SPACJA",new(x+30,y+343,590,45),true);Button(g,"menu","MENU",new(x+30,y+399,590,30));
    }
    void DrawBike(Graphics g,Rider r){float alpha=scene==Scene.Race?Math.Clamp(accumulator*120,0,1):1;var display=RiderSprites.Presentation(r,alpha);if(riderSprites.Draw(g,r,alpha)){if(r.Disqualified){using var cross=new Pen(Color.Red,2);g.DrawLine(cross,display.Position.X-9,display.Position.Y-9,display.Position.X+9,display.Position.Y+9);g.DrawLine(cross,display.Position.X-9,display.Position.Y+9,display.Position.X+9,display.Position.Y-9);}else{TextAt(g,(r.Id+1).ToString(),labelFont,colors[r.Id],display.Position.X+24,display.Position.Y-7);}return;}var state=g.Save();g.TranslateTransform(r.Pos.X,r.Pos.Y);g.RotateTransform(r.Heading*180/MathF.PI);using var shadow=new SolidBrush(Color.FromArgb(100,0,0,0));g.FillEllipse(shadow,-12,0,30,13);using var tire=new SolidBrush(Color.FromArgb(27,28,24));g.FillRectangle(tire,-14,-3,10,6);g.FillRectangle(tire,10,-2.5f,9,5);using var spoke=new Pen(Color.FromArgb(192,194,177),1);g.DrawLine(spoke,-12,0,17,0);g.DrawLine(spoke,-8,-4,8,4);using var body=new SolidBrush(colors[r.Id]);g.FillPolygon(body,new[]{new PointF(-10,-3),new PointF(9,-4),new PointF(12,0),new PointF(-8,4)});using var engine=new SolidBrush(Color.FromArgb(185,187,168));g.FillEllipse(engine,-3,-2,8,5);using var suit=new SolidBrush(Color.FromArgb(35,43,42));g.FillEllipse(suit,-8,-5,14,9);using var arm=new Pen(colors[r.Id],3);g.DrawLine(arm,1,-4,8,-6);g.DrawLine(arm,0,3,9,4);using var boot=new Pen(Color.FromArgb(201,198,168),3);g.DrawLine(boot,-6,1,-12,8);g.DrawLine(spoke,10,-7,10,6);g.FillEllipse(body,2,-6,10,10);using var visor=new SolidBrush(Color.FromArgb(27,39,40));g.FillRectangle(visor,9,-4,3,4);g.FillEllipse(Brushes.White,4,-5,3,2);g.Restore(state);if(r.Disqualified){using var cross=new Pen(Color.Red,2);g.DrawLine(cross,r.Pos.X-9,r.Pos.Y-9,r.Pos.X+9,r.Pos.Y+9);g.DrawLine(cross,r.Pos.X-9,r.Pos.Y+9,r.Pos.X+9,r.Pos.Y-9);}if(!r.Finished){string n=(r.Id+1).ToString();var f=labelFont;TextAt(g,n,f,Color.Black,r.Pos.X+26,r.Pos.Y-6);TextAt(g,n,f,colors[r.Id],r.Pos.X+25,r.Pos.Y-7);}}
    protected override void Dispose(bool disposing){if(disposing){riderSprites.Dispose();keyboard.Dispose();timer.Dispose();trackCache?.Dispose();track.Dispose();skids.Dispose();titleFont.Dispose();labelFont.Dispose();bodyFont.Dispose();boldFont.Dispose();bigFont.Dispose();}base.Dispose(disposing);}
}

sealed class ControlSetup:Form
{
    readonly Bindings bindings;int waitingPlayer=-1,waitingAction;Button? waiting;readonly HashSet<Keys> reserved=new(){Keys.Escape,Keys.Space,Keys.Enter,Keys.P,Keys.R,Keys.M,Keys.F2,Keys.F11,Keys.Menu,Keys.LWin,Keys.RWin};
    public ControlSetup(Bindings b,Color[] colors){bindings=b;Text="Sterowanie — czterech graczy";ClientSize=new(720,410);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(20,32,26);ForeColor=Color.White;Font=new Font("Segoe UI",10);KeyPreview=true;
        Controls.Add(new Label{Text="Kliknij przycisk i naciśnij nowy klawisz. Każdy gracz ma własny gaz i skręt.",AutoSize=true,Location=new(24,20)});
        string[] headings={"GAZ","SKRĘT W LEWO","SKRĘT W PRAWO"};for(int a=0;a<3;a++)Controls.Add(new Label{Text=headings[a],Location=new(185+a*170,61),AutoSize=true});
        for(int i=0;i<4;i++){int player=i;Controls.Add(new Label{Text=$"GRACZ {i+1}",ForeColor=colors[i],Location=new(24,101+i*49),AutoSize=true});for(int a=0;a<3;a++){int action=a;var btn=new Button{Text=Bindings.Name(b.Keys[i][a]),Location=new(170+a*170,91+i*49),Size=new(155,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(36,52,43),Tag=new[]{i,a}};btn.Click+=(_,_)=>{if(waiting!=null)waiting.Text=Bindings.Name(bindings.Keys[waitingPlayer][waitingAction]);waiting=btn;waitingPlayer=player;waitingAction=action;btn.Text="Naciśnij klawisz…";};Controls.Add(btn);}}
        Controls.Add(new Label{Text="Pady XInput: RT lub A = gaz, lewy drążek / krzyżak = skręt. Pad 1 → gracz 1 itd.\nCzęść klawiatur blokuje wiele klawiszy naraz. Wtedy zmień klawisze lub użyj padów.\nP pauza · F11 pełny ekran · M dźwięk · Esc anuluje przypisywanie.",Location=new(24,295),Size=new(670,72)});var done=new Button{Text="ZAPISZ I WRÓĆ",Location=new(485,367),Size=new(210,32),DialogResult=DialogResult.OK};Controls.Add(done);AcceptButton=done;}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(waiting!=null){var key=keyData&Keys.KeyCode;if(key==Keys.Escape){waiting.Text=Bindings.Name(bindings.Keys[waitingPlayer][waitingAction]);waiting=null;return true;}if(reserved.Contains(key)){System.Media.SystemSounds.Beep.Play();return true;}int val=(int)key;if(bindings.Keys.SelectMany(k=>k).Contains(val)&&bindings.Keys[waitingPlayer][waitingAction]!=val){MessageBox.Show(this,"Ten klawisz jest już przypisany. Wybierz inny.","Sterowanie");return true;}bindings.Keys[waitingPlayer][waitingAction]=val;waiting.Text=Bindings.Name(val);waiting=null;return true;}return base.ProcessCmdKey(ref msg,keyData);}
}
