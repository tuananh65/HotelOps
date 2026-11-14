using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Safety008;

internal sealed class Durable
{
    internal static readonly JsonSerializerOptions Json = new(){WriteIndented=false,PropertyNameCaseInsensitive=true};
    private readonly string directory;
    private readonly SemaphoreSlim gate=new(1);
    private long sequence;
    private string previous="GENESIS";
    internal Durable(string directory){this.directory=Path.GetFullPath(directory);Directory.CreateDirectory(this.directory);if(Directory.EnumerateFiles(this.directory,"*.event.json").Any())throw new InvalidOperationException("No automatic resume of a previous run");}
    internal string DirectoryPath=>directory;
    internal static string Hash(string s)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    internal static async Task File(string path,object value)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".pending";
        await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough|FileOptions.Asynchronous)){await stream.WriteAsync(bytes);await stream.FlushAsync();stream.Flush(true);}
        System.IO.File.Move(temporary,path,false);
    }
    internal async Task Event(string kind,object payload)
    {
        await gate.WaitAsync();try{
            var body=JsonSerializer.Serialize(new{sequence=sequence+1,utc=DateTime.UtcNow,kind,previous,payload},Json);
            var hash=Hash(body);await File(Path.Combine(directory,$"{sequence+1:D8}.event.json"),new{body,hash});sequence++;previous=hash;
        }finally{gate.Release();}
    }
    internal static void Verify(string dir)
    {
        long seq=0;var prior="GENESIS";
        foreach(var file in System.IO.Directory.EnumerateFiles(dir,"*.event.json").Order(StringComparer.Ordinal)){
            using var outer=JsonDocument.Parse(System.IO.File.ReadAllText(file));var body=outer.RootElement.GetProperty("body").GetString()!;
            if(Hash(body)!=outer.RootElement.GetProperty("hash").GetString())throw new InvalidOperationException("Journal digest mismatch");
            using var parsed=JsonDocument.Parse(body);var r=parsed.RootElement;
            if(r.GetProperty("sequence").GetInt64()!=++seq||r.GetProperty("previous").GetString()!=prior)throw new InvalidOperationException("Journal chain mismatch");prior=Hash(body);
        }
        if(System.IO.Directory.EnumerateFiles(dir,"*.pending").Any())throw new InvalidOperationException("Uncertain durable write; explicit recovery required");
    }
}
