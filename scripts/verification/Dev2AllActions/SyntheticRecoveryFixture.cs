using System.Text.Json;
using HotelOps.Api.Services.Dev2Authorization;

// Offline-only identities: never loaded by runtime fixture creation or recovery paths.
internal static class SyntheticRecoveryFixture
{
    internal static JsonDocument Create()
    {
        var permissionCount=Dev2DemoBindings.BoundDecisionIds.SelectMany(Dev2DemoBindings.PermissionCodes)
            .Where(code=>code.StartsWith("DEV2_DEMO_",StringComparison.Ordinal)).Distinct().Count();
        static Dictionary<string,long> Group(int first,int count,long start)=>Enumerable.Range(first,count)
            .ToDictionary(id=>id.ToString(System.Globalization.CultureInfo.InvariantCulture),id=>start+id);
        return JsonSerializer.SerializeToDocument(new {
            Marker="D2A0000000001", branches=new long[]{100,101}, users=Enumerable.Range(200,7).Select(x=>(long)x).ToArray(),
            roles=new long[]{300,301}, warehouse=400L, assetType=500L, category=501L, uom=502L,
            assets=Group(1,19,10000), incidents=Group(20,7,20000), works=Group(27,15,30000),
            requests=Group(42,10,40000), replenishments=Group(52,8,50000), receipts=Group(58,2,60000),
            createdReferenceIds=new long[]{70001}, newPermissionIds=Enumerable.Range(71000,permissionCount).Select(x=>(long)x).ToArray(),
            borrowedPriority=new {referenceId=77L,createdByFixture=false}
        });
    }
}
