using Safety008;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

try{
    if(args.Length==2&&args[0]=="offline"){await Safety008.Offline.Run(Path.GetFullPath(args[1]));}
    else if(args.Length==6&&args[0]=="run"){await Worker.Run(args[1],Path.GetFullPath(args[2]),Path.GetFullPath(args[3]),Path.GetFullPath(args[4]),Path.GetFullPath(args[5]));}
    else if(args.Length==3&&args[0]=="request-stop"&&args[2]=="CONFIRM_OWNER_STOP"){
        var session=JsonSerializer.Deserialize<Session>(await File.ReadAllTextAsync(args[1]),Durable.Json)??throw new InvalidOperationException("Session required");
        await using var pipe=new NamedPipeClientStream(".","HotelOps008_"+session.Run,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.ConnectAsync(5000);
        using var writer=new StreamWriter(pipe,Encoding.UTF8,1024,true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new{run=session.Run,marker=session.Marker,databaseGuid=session.DatabaseGuid}));Console.WriteLine(await reader.ReadLineAsync());
    }
    else {Console.Error.WriteLine("Explicit offline <new-output>, run <authorized-mode> <authority> <release> <repo> <output>, or request-stop <SESSION.json> CONFIRM_OWNER_STOP required");Environment.ExitCode=1;}
}catch(Exception e){Console.Error.WriteLine("SAFETY008_BLOCKED: "+e.GetType().Name);Environment.ExitCode=1;}
