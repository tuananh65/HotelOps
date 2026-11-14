using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Safety008;

internal sealed record Session(string Run,string Marker,Guid DatabaseGuid,string SourceIdentity);
internal sealed record StopRequest(string Run,string Marker,string DatabaseGuid,string SourceIdentity,string Command,string RequestId,string Signature);
internal enum ChildKind { NormalBrowserExit,OwnerCancellation,TimedOut,Failure,ControllerLost }
internal sealed record ChildOutcome(ChildKind Kind,int NativeExit,bool Quiesced);
internal sealed record ProcessIdentity(int Pid,int ParentPid,string Executable,DateTime StartedUtc)
{
    internal static ProcessIdentity Capture(Process p,int parent)=>new(p.Id,parent,p.MainModule?.FileName??throw new InvalidOperationException("Process path unavailable"),p.StartTime.ToUniversalTime());
    internal bool Matches(Process p)=>!p.HasExited&&p.Id==Pid&&p.StartTime.ToUniversalTime()==StartedUtc&&string.Equals(p.MainModule?.FileName,Executable,StringComparison.OrdinalIgnoreCase)&&(ParentPid<=0||ProcessTree.Parents().GetValueOrDefault(Pid)==ParentPid);
}
internal sealed class StopValidator(Session session,byte[] key)
{
    private readonly HashSet<string> seen=[];
    private string Tag(StopRequest r)=>Convert.ToHexStringLower(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(string.Join('|',r.Run,r.Marker,Guid.ParseExact(r.DatabaseGuid,"D").ToString("D"),r.SourceIdentity,r.Command,r.RequestId))));
    internal StopRequest Sign(){var r=new StopRequest(session.Run,session.Marker,session.DatabaseGuid.ToString("D"),session.SourceIdentity,"REQUEST_GRACEFUL_STOP",Guid.NewGuid().ToString("N"),"");return r with{Signature=Tag(r)};}
    internal bool Accept(string raw)
    {
        lock(seen){
        try{
            var r=JsonSerializer.Deserialize<StopRequest>(raw,Durable.Json);if(r==null||r.Run!=session.Run||r.Marker!=session.Marker||r.SourceIdentity!=session.SourceIdentity||r.Command!="REQUEST_GRACEFUL_STOP"||r.DatabaseGuid?.Length!=36||!Guid.TryParseExact(r.DatabaseGuid,"D",out var guid)||guid!=session.DatabaseGuid||!Guid.TryParseExact(r.RequestId,"N",out _)||r.Signature?.Length!=64||seen.Contains(r.RequestId))return false;
            var expected=Convert.FromHexString(Tag(r));var actual=Convert.FromHexString(r.Signature);if(!CryptographicOperations.FixedTimeEquals(expected,actual))return false;seen.Add(r.RequestId);return true;
        }catch(JsonException){return false;}catch(FormatException){return false;}catch(ArgumentException){return false;}
        }
    }
}
internal sealed class Lifecycle(Durable journal)
{
    internal async Task<ChildOutcome> Supervise(Func<CancellationToken,Task<ChildOutcome>> child,Func<bool> controllerAlive,TimeSpan timeout,CancellationToken ownerCancellation)
    {
        await journal.Event("HARNESS_SUPERVISING_CHILD",new{timeoutSeconds=timeout.TotalSeconds});
        if(!controllerAlive())return new(ChildKind.ControllerLost,-1,false);
        if(ownerCancellation.IsCancellationRequested)return new(ChildKind.OwnerCancellation,0,true);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(ownerCancellation);
        var work=child(cancellation.Token);var deadline=DateTime.UtcNow+timeout;ChildKind? requested=null;
        while(!work.IsCompleted){
            if(!controllerAlive()){requested=ChildKind.ControllerLost;break;}
            if(ownerCancellation.IsCancellationRequested){requested=ChildKind.OwnerCancellation;break;}
            if(DateTime.UtcNow>=deadline){requested=ChildKind.TimedOut;break;}
            await Task.Delay(25,CancellationToken.None);
        }
        if(requested!=null){await journal.Event("CHILD_COOPERATIVE_QUIESCENCE_REQUEST",new{reason=requested.ToString()});cancellation.Cancel();}
        var ended=await Task.WhenAny(work,Task.Delay(TimeSpan.FromSeconds(15)));
        if(ended!=work){await journal.Event("CHILD_QUIESCENCE_UNCONFIRMED",new{cleanupAllowed=false,forceKillPerformed=false});return new(requested??ChildKind.Failure,-1,false);}
        ChildOutcome outcome;try{outcome=await work;}catch{outcome=new(ChildKind.Failure,1,false);}
        if(requested==null&&!controllerAlive())requested=ChildKind.ControllerLost;
        if(requested==null&&ownerCancellation.IsCancellationRequested)requested=ChildKind.OwnerCancellation;
        if(requested!=null)outcome=outcome with{Kind=requested.Value};
        await journal.Event("CHILD_TERMINAL",outcome);
        // No cleanup callback exists in a finally/error path. The caller must separately
        // establish owned process shutdown and execute the guarded transaction adapter.
        return outcome;
    }
}
internal static class OwnerStopPipe
{
    internal static async Task Listen(Session session,StopValidator validator,Durable journal,Action accepted,CancellationToken ct)
    {
        while(!ct.IsCancellationRequested){
            try{
                await using var server=new NamedPipeServerStream("HotelOps008_"+session.Run,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct);using var reader=new StreamReader(server,Encoding.UTF8,false,1024,true);using var writer=new StreamWriter(server,Encoding.UTF8,1024,true){AutoFlush=true};
                var raw=await reader.ReadLineAsync(ct);
                if(raw==null||raw.Length>4096){await writer.WriteLineAsync("REJECTED");continue;}
                using var request=JsonDocument.Parse(raw);var r=request.RootElement;
                if(r.GetProperty("run").GetString()!=session.Run||r.GetProperty("marker").GetString()!=session.Marker||!Guid.TryParseExact(r.GetProperty("databaseGuid").GetString(),"D",out var guid)||guid!=session.DatabaseGuid){await writer.WriteLineAsync("REJECTED");continue;}
                var signed=validator.Sign();
                await Durable.File(Path.Combine(journal.DirectoryPath,"STOP_REQUEST_"+signed.RequestId+".json"),signed);
                await journal.Event("OWNER_AUTHENTICATED_STOP_QUEUED",new{session.Run,session.Marker,scope="CURRENT_WINDOWS_USER_ONLY_NAMED_PIPE",ownerAction="EXPLICIT_STOP_CLIENT_REQUEST",secretKeyPersisted=false});await writer.WriteLineAsync("QUEUED_FOR_BOUND_STOP_VALIDATION");return;
            }catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or IOException){await journal.Event("INVALID_OWNER_PIPE_REQUEST_IGNORED",new{childTerminated=false,exceptionType=e.GetType().Name});}
        }
    }
    internal static async Task Poll(Session session,StopValidator validator,Durable journal,Action accepted,CancellationToken ct)
    {
        var seen=new Dictionary<string,string>();
        while(!ct.IsCancellationRequested){
            foreach(var file in Directory.EnumerateFiles(journal.DirectoryPath,"STOP_REQUEST*.json")){
                try{
                    var info=new FileInfo(file);if(info.Attributes.HasFlag(FileAttributes.ReparsePoint)){await journal.Event("REPARSE_STOP_FILE_IGNORED",new{childTerminated=false});continue;}var raw=info.Length>65536?"OVERSIZE":await File.ReadAllTextAsync(file,ct);var hash=Durable.Hash(raw);
                    if(seen.GetValueOrDefault(file)==hash)continue;seen[file]=hash;
                    if(validator.Accept(raw)){await journal.Event("OWNER_BOUND_STOP_ACCEPTED",new{session.Run,session.Marker,invalidSignalClosesChild=false});accepted();return;}
                    await journal.Event("INVALID_OR_REPLAY_STOP_IGNORED",new{inputDigest=hash,childTerminated=false,cleanupTriggered=false});
                }catch(IOException){await journal.Event("INCOMPLETE_STOP_FILE_IGNORED",new{childTerminated=false});}
            }
            try{await Task.Delay(100,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
        }
    }
}
