namespace Zuzel;

public enum CompetitionMode{Individual,Team}
public sealed record HeatResult(int Number,int[] Points,int[] FinishOrder,bool[] Excluded);
public sealed class Tournament
{
    public const int HeatCount=15;
    public CompetitionMode Mode{get;private set;}
    public int CurrentHeat{get;private set;}=1;
    public int[] Points{get;}=new int[4];
    public List<HeatResult> History{get;}=new();
    public bool Complete=>History.Count==HeatCount;
    public bool HeatRecorded=>History.Count==CurrentHeat;
    public static int TeamOf(int rider)=>rider%2; // red/white against blue/yellow
    public int TeamPoints(int team)=>Enumerable.Range(0,4).Where(i=>TeamOf(i)==team).Sum(i=>Points[i]);
    public void Start(CompetitionMode mode){Mode=mode;CurrentHeat=1;Array.Clear(Points);History.Clear();}
    public bool Record(Race race){if(HeatRecorded||Complete)return false;if(race.Results.Count!=4||race.Riders.Any(r=>!r.Finished))throw new InvalidOperationException("Bieg jeszcze się nie zakończył.");var awarded=new int[4];for(int position=0;position<4;position++){int id=race.Results[position];awarded[id]=race.Riders[id].Disqualified?0:3-position;Points[id]+=awarded[id];}History.Add(new(CurrentHeat,awarded,race.Results.ToArray(),race.Riders.Select(r=>r.Disqualified).ToArray()));return true;}
    public bool Advance(){if(!HeatRecorded||Complete)return false;CurrentHeat++;return true;}
    public int HeatPoints(int id)=>HeatRecorded?History[^1].Points[id]:0;
    public int[] Order()=>Enumerable.Range(0,4).OrderByDescending(i=>Points[i]).ThenBy(i=>i).ToArray();
    public int Rank(int id)=>1+Points.Count(p=>p>Points[id]);
    public string ResultText(){if(!Complete)return "";if(Mode==CompetitionMode.Team){int a=TeamPoints(0),b=TeamPoints(1);return a==b?$"Remis drużyn: {a} : {b}":$"Wygrywa drużyna {(a>b?"A":"B")} · {a} : {b}";}int best=Points.Max();var winners=Enumerable.Range(0,4).Where(i=>Points[i]==best).Select(i=>(i+1).ToString()).ToArray();return winners.Length==1?$"Wygrywa zawodnik {winners[0]} · {best} pkt":$"Ex aequo: zawodnicy {string.Join(", ",winners)} · {best} pkt";}
}
