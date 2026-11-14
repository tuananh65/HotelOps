using System.Text.Json;

namespace Safety008;

internal sealed record Column(string Name,string Type,int PrimaryOrdinal);
internal sealed record Table(string Name,Column[] Columns)
{
    internal string[] Pk=>Columns.Where(c=>c.PrimaryOrdinal>0).OrderBy(c=>c.PrimaryOrdinal).Select(c=>c.Name).ToArray();
}
internal sealed record ForeignKey(string Name,string Child,string Parent,string[] ChildColumns,string[] ParentColumns,bool Disabled,bool Untrusted,string DeleteAction);
internal sealed record Row(string Table,SortedDictionary<string,string?> Pk,Dictionary<string,string?> Values,string Hash)
{
    internal string Key=>Table+":"+JsonSerializer.Serialize(Pk);
}
internal sealed record Snapshot(string SchemaHash,Table[] Tables,ForeignKey[] ForeignKeys,Dictionary<string,Row> Rows,string[] EnabledTriggers);
internal sealed record RootProof(string Table,SortedDictionary<string,string?> Pk,long Actor,string Method,string Route,int Status)
{
    internal string Key=>Table+":"+JsonSerializer.Serialize(Pk);
}
internal sealed record GraphProof(Dictionary<string,Row> Owned,string[] DeleteOrder,string[] Problems);

internal static class Graph
{
    internal static readonly string[] ActorColumns=["actor_user_id","actor_id","reported_by","created_by","requested_by","requester_id","fulfilled_by","moved_by","submitted_by","approved_by","posted_by","reviewed_by","assignee_id","approver_id","assigned_approver_id","to_user_id","from_user_id","custodian_user_id"];
    private static readonly Dictionary<string,(string Parent,string Column)> Components=new(){
        ["dbo.user_sessions"]=("dbo.users","user_id"),["dbo.user_roles"]=("dbo.users","user_id"),["dbo.user_branch_access"]=("dbo.users","user_id"),
        ["dbo.asset_location_history"]=("dbo.assets","asset_id"),["dbo.asset_condition_history"]=("dbo.assets","asset_id"),["dbo.asset_handovers"]=("dbo.assets","asset_id"),
        ["dbo.incident_events"]=("dbo.incidents","incident_id"),["dbo.maintenance_work_logs"]=("dbo.maintenance_works","maintenance_work_id"),["dbo.maintenance_material_usages"]=("dbo.maintenance_works","maintenance_work_id"),
        ["dbo.material_request_lines"]=("dbo.material_requests","request_id"),["dbo.material_request_approval_cycles"]=("dbo.material_requests","request_id"),["dbo.material_request_approval_line_snapshots"]=("dbo.material_request_approval_cycles","approval_cycle_id"),["dbo.material_request_approvals"]=("dbo.material_requests","request_id"),
        ["dbo.material_fulfillment_lines"]=("dbo.material_fulfillments","fulfillment_id"),["dbo.stock_document_lines"]=("dbo.stock_documents","document_id"),["dbo.stock_movements"]=("dbo.stock_document_lines","document_line_id"),["dbo.stock_receipt_line_details"]=("dbo.stock_document_lines","stock_document_line_id"),
        ["dbo.material_replenishment_lines"]=("dbo.material_replenishments","replenishment_id"),["dbo.material_replenishment_receipt_lines"]=("dbo.material_replenishment_lines","replenishment_line_id")};
    private static string Alias(string s)=>new(s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    internal static GraphProof Prove(Snapshot baseline,Snapshot current,IReadOnlySet<string> seeded,IReadOnlyList<RootProof> roots,IReadOnlySet<long> actors)
    {
        var problems=new List<string>();var owned=new Dictionary<string,Row>();
        if(baseline.SchemaHash!=current.SchemaHash)problems.Add("SCHEMA_DRIFT");
        if(current.EnabledTriggers.Length>0)problems.Add("UNREVIEWED_DML_TRIGGER");
        foreach(var old in baseline.Rows.Values)if(!current.Rows.TryGetValue(old.Key,out var now)||now.Hash!=old.Hash)problems.Add("PROTECTED_ROW_CHANGED:"+old.Key);
        foreach(var key in seeded){if(baseline.Rows.ContainsKey(key))problems.Add("SEED_SELECTS_BASELINE_ROW:"+key);else if(current.Rows.TryGetValue(key,out var row))owned.Add(key,row);else problems.Add("SEEDED_ROW_DISAPPEARED:"+key);}
        bool Has(string table,string? id)=>id!=null&&owned.Values.Any(r=>r.Table==table&&r.Pk.Count==1&&r.Pk.Values.Single()==id);
        bool ActorSafe(Row r)=>ActorColumns.Where(r.Values.ContainsKey).All(c=>r.Values[c]==null||long.TryParse(r.Values[c],out var n)&&actors.Contains(n));
        var aliases=new Dictionary<string,string>();foreach(var t in current.Tables){aliases[Alias(t.Name)]=t.Name;aliases[Alias(t.Name.Split('.').Last())]=t.Name;aliases[Alias(t.Name.Split('.').Last()).TrimEnd('S')]=t.Name;}
        aliases["MATERIALFULFILLMENT"]="dbo.material_fulfillments";aliases["USER"]="dbo.users";aliases["INCIDENT"]="dbo.incidents";aliases["MAINTENANCEWORK"]="dbo.maintenance_works";aliases["MATERIALREQUEST"]="dbo.material_requests";aliases["STOCKDOCUMENT"]="dbo.stock_documents";aliases["MATERIALREPLENISHMENT"]="dbo.material_replenishments";aliases["ASSET"]="dbo.assets";
        Row? Target(string? type,string? id){if(type==null||id==null||!aliases.TryGetValue(Alias(type),out var table))return null;return current.Rows.Values.SingleOrDefault(r=>r.Table==table&&r.Pk.Count==1&&r.Pk.Values.Single()==id);}
        foreach(var root in roots){
            if(baseline.Rows.ContainsKey(root.Key)||root.Status is not (200 or 201)||!actors.Contains(root.Actor)||!RootRoute(root)){problems.Add("INVALID_ACTION_ROOT:"+root.Key);continue;}
            if(!current.Rows.TryGetValue(root.Key,out var row)||!ActorSafe(row)){problems.Add("ACTION_ROOT_MISSING_OR_ACTOR_MISMATCH:"+root.Key);continue;}
            bool lineage=root.Table switch{
                "dbo.incidents"=>Has("dbo.branches",row.Values.GetValueOrDefault("branch_id"))&&row.Values.GetValueOrDefault("reported_by")==root.Actor.ToString(),
                "dbo.material_requests"=>Has("dbo.branches",row.Values.GetValueOrDefault("branch_id"))&&Has("dbo.departments",row.Values.GetValueOrDefault("department_id"))&&Has("dbo.warehouses",row.Values.GetValueOrDefault("source_warehouse_id"))&&row.Values.GetValueOrDefault("requester_id")==root.Actor.ToString(),
                "dbo.material_replenishments"=>Has("dbo.warehouses",row.Values.GetValueOrDefault("warehouse_id"))&&row.Values.GetValueOrDefault("requested_by")==root.Actor.ToString(),
                "dbo.stock_documents"=>new[]{row.Values.GetValueOrDefault("source_warehouse_id"),row.Values.GetValueOrDefault("destination_warehouse_id")}.Where(x=>x!=null).Any()&&new[]{row.Values.GetValueOrDefault("source_warehouse_id"),row.Values.GetValueOrDefault("destination_warehouse_id")}.Where(x=>x!=null).All(x=>Has("dbo.warehouses",x))&&row.Values.GetValueOrDefault("created_by")==root.Actor.ToString(),
                "dbo.maintenance_works"=>Has("dbo.incidents",row.Values.GetValueOrDefault("incident_id"))&&row.Values.GetValueOrDefault("created_by")==root.Actor.ToString(),
                _=>false};
            if(!lineage){problems.Add("ACTION_ROOT_PRIVATE_LINEAGE_MISSING:"+root.Key);continue;}
            var audit=current.Rows.Values.Any(a=>a.Table=="dbo.audit_logs"&&!baseline.Rows.ContainsKey(a.Key)&&a.Values.GetValueOrDefault("action")=="CREATE"&&a.Values.GetValueOrDefault("actor_user_id")==root.Actor.ToString()&&Target(a.Values.GetValueOrDefault("entity_type"),a.Values.GetValueOrDefault("entity_id"))?.Key==root.Key);
            if(!audit){problems.Add("ACTION_CREATE_AUDIT_PROOF_MISSING:"+root.Key);continue;}owned.TryAdd(row.Key,row);
        }
        int before;do{before=owned.Count;foreach(var row in current.Rows.Values.Where(r=>!baseline.Rows.ContainsKey(r.Key)&&!owned.ContainsKey(r.Key))){
            bool own=false;
            if(Components.TryGetValue(row.Table,out var rule)&&Has(rule.Parent,row.Values.GetValueOrDefault(rule.Column))&&ActorSafe(row))own=true;
            // Server-created aggregate roots need exact lineage plus the actual actor and CREATE audit.
            if(row.Table=="dbo.material_fulfillments"&&Has("dbo.material_requests",row.Values.GetValueOrDefault("request_id"))&&Has("dbo.warehouses",row.Values.GetValueOrDefault("warehouse_id"))&&ActorSafe(row)){
                own=current.Rows.Values.Any(a=>a.Table=="dbo.audit_logs"&&a.Values.GetValueOrDefault("action")=="CREATE"&&actors.Any(id=>a.Values.GetValueOrDefault("actor_user_id")==id.ToString())&&Target(a.Values.GetValueOrDefault("entity_type"),a.Values.GetValueOrDefault("entity_id"))?.Key==row.Key);
            }
            if(row.Table=="dbo.stock_documents"&&row.Values.GetValueOrDefault("reference_type")=="MATERIAL_FULFILLMENT"&&Has("dbo.material_fulfillments",row.Values.GetValueOrDefault("reference_id"))&&ActorSafe(row))own=true;
            if(row.Table=="dbo.audit_logs"){
                var target=Target(row.Values.GetValueOrDefault("entity_type"),row.Values.GetValueOrDefault("entity_id"));var actor=row.Values.GetValueOrDefault("actor_user_id");
                own=target!=null&&owned.ContainsKey(target.Key)&&(actor==null?target.Table=="dbo.users":long.TryParse(actor,out var id)&&actors.Contains(id));
            }
            if(own)owned.Add(row.Key,row);
        }}while(before!=owned.Count);
        foreach(var row in current.Rows.Values.Where(r=>!baseline.Rows.ContainsKey(r.Key)&&!owned.ContainsKey(r.Key)))problems.Add("UNPROVEN_NEW_ROW:"+row.Key);
        var edges=new List<(string Child,string Parent)>();
        foreach(var fk in current.ForeignKeys){
            if(fk.Disabled||fk.Untrusted){problems.Add("UNTRUSTED_FK:"+fk.Name);continue;}
            foreach(var child in current.Rows.Values.Where(r=>r.Table==fk.Child)){
                if(fk.ChildColumns.Any(c=>child.Values.GetValueOrDefault(c)==null))continue;
                var parents=current.Rows.Values.Where(r=>r.Table==fk.Parent&&fk.ChildColumns.Select((c,i)=>r.Values.GetValueOrDefault(fk.ParentColumns[i])==child.Values.GetValueOrDefault(c)).All(x=>x)).ToArray();
                if(parents.Length!=1){problems.Add("FK_PRINCIPAL_NOT_UNIQUE:"+child.Key);continue;}
                if(owned.ContainsKey(child.Key)&&!seeded.Contains(child.Key)&&new[]{"dbo.users","dbo.branches","dbo.departments","dbo.areas","dbo.warehouses","dbo.assets","dbo.inventory_items"}.Contains(fk.Parent)&&!owned.ContainsKey(parents[0].Key))problems.Add("INTERACTIVE_PRIVATE_PRINCIPAL_OUTSIDE_FIXTURE:"+child.Key+":"+fk.Name);
                if(owned.ContainsKey(parents[0].Key)&&!owned.ContainsKey(child.Key))problems.Add("EXTERNAL_FK_TO_OWNED:"+child.Key);
                if(owned.ContainsKey(child.Key)&&owned.ContainsKey(parents[0].Key)&&child.Key!=parents[0].Key)edges.Add((child.Key,parents[0].Key));
            }
        }
        foreach(var row in current.Rows.Values)foreach(var (type,id) in new[]{("entity_type","entity_id"),("reference_type","reference_id"),("source_type","source_id"),("from_location_type","from_location_id"),("to_location_type","to_location_id")}){
            // reference_values.reference_id is a PK, not a polymorphic reference.
            if(!row.Values.ContainsKey(type)||row.Values.GetValueOrDefault(id)==null)continue;var target=Target(row.Values.GetValueOrDefault(type),row.Values[id]);
            if(target==null){if(owned.ContainsKey(row.Key)||owned.Values.Any(o=>o.Pk.Values.Contains(row.Values[id])))problems.Add("UNKNOWN_POLYMORPHIC_REFERENCE:"+row.Key);continue;}
            if(owned.ContainsKey(target.Key)&&!owned.ContainsKey(row.Key))problems.Add("EXTERNAL_POLYMORPHIC_REFERENCE:"+row.Key);
            if(owned.ContainsKey(row.Key)&&!owned.ContainsKey(target.Key)&&!baseline.Rows.ContainsKey(target.Key))problems.Add("UNPROVEN_POLYMORPHIC_PRINCIPAL:"+row.Key);
        }
        var knownPairs=new HashSet<string>{"entity_type","reference_type","source_type","from_location_type","to_location_type"};
        foreach(var row in current.Rows.Values)foreach(var c in row.Values.Keys.Where(c=>c.EndsWith("_type",StringComparison.Ordinal)&&!knownPairs.Contains(c))){var id=c[..^5]+"_id";if(!row.Values.ContainsKey(id)||row.Pk.ContainsKey(id)||current.ForeignKeys.Any(f=>f.Child==row.Table&&f.ChildColumns.Contains(id)))continue;if(row.Values[id]!=null)problems.Add("UNREVIEWED_TYPED_PAIR:"+row.Table+":"+c);}
        var pending=owned.Keys.ToHashSet();var order=new List<string>();while(pending.Count>0){var parents=edges.Where(e=>pending.Contains(e.Child)&&pending.Contains(e.Parent)).Select(e=>e.Parent).ToHashSet();var leaves=pending.Except(parents).Order(StringComparer.Ordinal).ToArray();if(leaves.Length==0){problems.Add("OWNED_FK_CYCLE");break;}foreach(var key in leaves){order.Add(key);pending.Remove(key);}}
        return new(owned,order.ToArray(),problems.Distinct().ToArray());
    }
    internal static bool RootRoute(RootProof r)=>r.Method=="POST"&&(r.Table switch{
        "dbo.incidents"=>r.Route=="/api/incidents","dbo.material_requests"=>r.Route=="/api/material-requests","dbo.material_replenishments"=>r.Route=="/api/replenishments","dbo.stock_documents"=>r.Route=="/api/inventory/receipts","dbo.maintenance_works"=>System.Text.RegularExpressions.Regex.IsMatch(r.Route,@"^/api/incidents/[1-9]\d*/maintenance-works$"),_=>false});
}
