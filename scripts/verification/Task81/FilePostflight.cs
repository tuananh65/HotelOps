using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length==4&&args[0]=="names")
{
    if(File.Exists(args[3]))throw new ArgumentException("Unused output required");
    var beforeNames=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[1]))!.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var afterNames=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[2]))!.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missing=beforeNames.Except(afterNames,StringComparer.OrdinalIgnoreCase).ToArray();
    var added=afterNames.Except(beforeNames,StringComparer.OrdinalIgnoreCase).ToArray();
    await File.WriteAllTextAsync(args[3],JsonSerializer.Serialize(new{before=beforeNames.Count,after=afterNames.Count,missing,added}));
    Console.WriteLine($"Missing {missing.Length}; outside-scope added {added.Length}");
    if(missing.Length+added.Length>0)Environment.ExitCode=1;return;
}
if(args.Length==3&&args[0]=="capture")
{
    if(File.Exists(args[2]))throw new ArgumentException("Unused capture output required");
    var rootPath=Directory.GetCurrentDirectory();
    var paths=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[1]))!;
    var records=new ConcurrentBag<object>();
    await Parallel.ForEachAsync(paths,new ParallelOptions{MaxDegreeOfParallelism=8},async(path,ct)=>
    {
        var full=Path.Combine(rootPath,path);var bytes=await File.ReadAllBytesAsync(full,ct);
        records.Add(new{path,sha256=Convert.ToHexString(SHA256.HashData(bytes)),lastWriteUtc=File.GetLastWriteTimeUtc(full).ToString("o")});
    });
    await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(new{root=rootPath,files=records.ToArray(),finishedUtc=DateTimeOffset.UtcNow}));
    Console.WriteLine($"Captured {records.Count} existing files with parallel byte reads");return;
}
if(args.Length!=2||File.Exists(args[1]))throw new ArgumentException("Manifest and unused output required");
using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var root=doc.RootElement.GetProperty("root").GetString()!;
var rows=doc.RootElement.GetProperty("files").EnumerateArray().Select(r=>new{
    path=r.GetProperty("path").GetString()!,sha=r.GetProperty("sha256").GetString()!,
    time=r.GetProperty("lastWriteUtc").GetString()!}).ToArray();
var changes=new ConcurrentBag<object>();var readErrors=new ConcurrentBag<object>();
await Parallel.ForEachAsync(rows,new ParallelOptions{MaxDegreeOfParallelism=8},async(row,ct)=>
{
    try
    {
        var path=Path.Combine(root,row.path);
        var bytes=await File.ReadAllBytesAsync(path,ct);
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        var sameTime=File.GetLastWriteTimeUtc(path)==DateTime.Parse(row.time,null,System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
        if(hash!=row.sha||!sameTime)changes.Add(new{row.path,bytesUnchanged=hash==row.sha,lastWriteUnchanged=sameTime});
    }
    catch(Exception e){readErrors.Add(new{row.path,error=e.GetType().Name});}
});
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{checkedFiles=rows.Length,changes=changes.ToArray(),readErrors=readErrors.ToArray(),finishedUtc=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Checked {rows.Length}; changed {changes.Count}; read errors {readErrors.Count}");
if(!changes.IsEmpty||!readErrors.IsEmpty)Environment.ExitCode=1;
