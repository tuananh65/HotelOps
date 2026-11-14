using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Data;

internal sealed class PreconditionFault(string category):Exception("Sanitized precondition validation failure")
{ internal string Category {get;}=category; }
internal sealed class PreconditionStepFailure(string step,string category,Exception original):Exception("Named precondition step failed",original)
{ internal string Step {get;}=step;internal string Category {get;}=category; }

internal sealed class PreconditionTrace(string output,string file,HotelOpsDbContext? db=null)
{
    private readonly List<StepResult> steps=[];private string status="RUNNING";private object? document;
    private sealed class StepResult(string id,string kind){public string Id{get;}=id;public string Kind{get;}=kind;public string Status{get;set;}="STARTED";public string? ExceptionType{get;set;}public string? Category{get;set;}public FailureDiagnostics? Diagnostic{get;set;}}
    private async Task Persist()=>await File.WriteAllTextAsync(Path.Combine(output,file),JsonSerializer.Serialize(new{status,document,steps},new JsonSerializerOptions{WriteIndented=true}),CancellationToken.None);
    internal async Task Document(long documentId,string documentStatus,string documentType){document=new{documentId,documentStatus,documentType};await Persist();}
    internal async Task<T> Step<T>(string id,string kind,Func<Task<T>> work)
    {
        var row=new StepResult(id,kind);steps.Add(row);await Persist();
        try{var result=await work();row.Status="PASS";await Persist();return result;}
        catch(Exception error)
        {
            status="FAIL";row.Status="FAIL";row.ExceptionType=error.GetType().Name;
            row.Category=error is PreconditionFault fault?fault.Category:kind=="SQL"?"SQL_QUERY_OR_TRANSLATION":kind=="JSON"?"JSON_EXTRACTION":"HARNESS_OPERATION";
            row.Diagnostic=FailureDiagnostics.Capture(error,"A004A-059 / "+id,null,null,db,output);
            await Persist();throw new PreconditionStepFailure(id,row.Category,error);
        }
    }
    internal Task<T> Step<T>(string id,string kind,Func<T> work)=>Step(id,kind,()=>Task.FromResult(work()));
    internal async Task Check(string id,Func<bool> work)=>await Step(id,"INVARIANT",()=>work()?true:throw new PreconditionFault("BUSINESS_INVARIANT"));
    internal async Task Complete(){status="PASS";await Persist();}
    internal static T One<T>(IReadOnlyCollection<T> rows,string kind)
        =>rows.Count==1?rows.Single():throw new PreconditionFault(kind+(rows.Count==0?"_NO_ROW":"_MULTIPLE_ROWS"));
    internal static T Fixture<T>(IDictionary<int,T> map,int key)=>map.TryGetValue(key,out var value)?value:throw new PreconditionFault("FIXTURE_MISSING");
    internal static JsonNode Field(JsonNode? node,string name)
    {
        if(node is not JsonObject obj)throw new PreconditionFault("JSON_WRONG_TYPE");
        if(!obj.ContainsKey(name))throw new PreconditionFault("JSON_MISSING_FIELD");
        return obj[name]??throw new PreconditionFault("JSON_NULL_FIELD");
    }
    internal static JsonNode SingleElement(JsonNode node)
    {
        if(node is not JsonArray array)throw new PreconditionFault("JSON_WRONG_TYPE");
        if(array.Count!=1)throw new PreconditionFault("JSON_CARDINALITY");
        return array[0]??throw new PreconditionFault("JSON_NULL_FIELD");
    }
    internal static long Long(JsonNode node){var value=JsonSerializer.SerializeToElement(node);return value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out var result)?result:throw new PreconditionFault("JSON_WRONG_TYPE");}
    internal static decimal Decimal(JsonNode node){var value=JsonSerializer.SerializeToElement(node);return value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var result)?result:throw new PreconditionFault("JSON_WRONG_TYPE");}
    internal static string Text(JsonNode node){var value=JsonSerializer.SerializeToElement(node);return value.ValueKind==JsonValueKind.String?value.GetString()!:throw new PreconditionFault("JSON_WRONG_TYPE");}
}
