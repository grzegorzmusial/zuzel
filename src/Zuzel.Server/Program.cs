using Zuzel;
int port=int.TryParse(Environment.GetEnvironmentVariable("PORT"),out var p)?p:9050;
int max=int.TryParse(Environment.GetEnvironmentVariable("MAX_ROOMS"),out var m)?Math.Clamp(m,1,64):16;
using var stop=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();};
using var server=new GameServer(port,max){Log=Console.WriteLine};
Console.WriteLine($"Zuzel server 2.0 UDP {server.Port}; max rooms={max}; room 1 ready");
server.Run(stop.Token);
