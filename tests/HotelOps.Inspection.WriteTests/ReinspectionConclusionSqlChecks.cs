using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

internal static class ReinspectionConclusionSqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard,CreatedSqlSession login,CreatedSqlSession denied,
        ReinspectionWorkflowDetails bound,long branch,long foreign,long actor,long other,long room,
        Func<HotelOpsDbContext,CreatedSqlSession?,ReinspectionWorkflowCandidateController> controller,
        Func<HotelOpsDbContext,CreatedSqlSession,RoomTypeCreationService> service,bool allFixed)
    {
        var cases=new List<object>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);}
        int Status(IActionResult result)=>((ObjectResult)result).StatusCode??200;
        T Value<T>(IActionResult result)=>result is OkObjectResult{Value:T value}?value:throw new Exception("Unexpected status "+Status(result));
        var c=controller(db,login);var inspection=bound.InspectionId;var rework=bound.ReworkId;
        ReinspectionConclusionProposalRequest Input(ReinspectionWorkflowDetails s,Guid op,bool confirm=true)=>new(op,rework,s.ReworkRevision,s.BindingRevision,s.Defects.Select(d=>d.DefectId).ToArray(),confirm);
        async Task<IActionResult> Propose(ReinspectionWorkflowCandidateController call,ReinspectionWorkflowDetails s,Guid op,ReinspectionConclusionProposalRequest? input=null){call.Request.Headers.IfMatch=s.InspectionRevision;return await call.PrepareProposal(branch,inspection,input??Input(s,op),default);}
        async Task<ReinspectionWorkflowDetails> Read()=>Value<ReinspectionWorkflowDetails>(await c.Read(branch,inspection,rework,default));
        var op=Guid.NewGuid();
        if(allFixed){
            Check("Unanswered results cannot conclude proposal",Status(await Propose(c,bound,op))==400);
            Check("Confirmation is mandatory",Status(await Propose(c,bound,op,Input(bound,op,false)))==400);
            c.Request.Headers.Remove("If-Match");Check("Proposal requires If-Match",Status(await c.PrepareProposal(branch,inspection,Input(bound,op),default))==428);
        }
        c.Request.Headers.IfMatch=bound.InspectionRevision;
        var saved=Value<ReinspectionWorkflowDetails>(await c.Save(branch,inspection,new(rework,bound.ReworkRevision,bound.BindingRevision,bound.Defects.Select(d=>new ReinspectionDecisionDto(d.DefectId,allFixed)).ToArray()),default));
        var before=await db.AuditLogs.CountAsync();var originalLinks=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={rework} ORDER BY defect_id").ToArrayAsync();
        if(allFixed){
            c.Request.Headers.IfMatch=saved.InspectionRevision;
            Check("Wrong branch authorization returns 403",Status(await c.PrepareProposal(foreign,inspection,Input(saved,op),default))==403);
            Check("Account without existing grant returns 403",Status(await Propose(controller(db,denied),saved,op))==403);
            Check("Stale root revision rejected",Status(await Propose(c,bound,op))==412);
            Check("Wrong selected defect set rejected",Status(await Propose(c,saved,op,Input(saved,op) with{SelectedDefectIds=[long.MaxValue]}))==400);
            Check("Stale binding revision rejected",Status(await Propose(c,saved,op,Input(saved,op) with{BindingRevision=bound.BindingRevision}))==412);
            // Test-only fault installed only on the exact fresh owned target. Real SQL participant
            // failure AFTER proposal insert verifies rollback of proposal/root/binding/audit.
            await guard();await db.Database.ExecuteSqlRawAsync("EXEC(N'CREATE TRIGGER dbo.TR_fe05_test_proposal_audit_fault ON dbo.audit_logs AFTER INSERT AS BEGIN SET NOCOUNT ON; IF TRY_CONVERT(int,SESSION_CONTEXT(N''FE05.Test.ProposalAuditFault''))=1 AND EXISTS(SELECT 1 FROM inserted WHERE entity_type=''FE05_REINSPECTION_CANDIDATE'' AND new_values LIKE ''%proposalOnly%'') THROW 51996,''Owned trial audit participant fault'',1; END'); EXEC sys.sp_set_session_context @key=N'FE05.Test.ProposalAuditFault',@value=1;");
            bool failed=false;
            try{await Propose(c,saved,op);}catch(SqlException e)when(e.Number==51996){failed=true;}
            finally{await guard();await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'FE05.Test.ProposalAuditFault',@value=NULL; DROP TRIGGER dbo.TR_fe05_test_proposal_audit_fault;");}
            var afterFailure=await Read();
            Check("Actual audit failure rolls back proposal, root/binding revisions and audit",failed&&afterFailure.InspectionRevision==saved.InspectionRevision&&afterFailure.BindingRevision==saved.BindingRevision&&await db.AuditLogs.CountAsync()==before&&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_conclusion_proposals_v1 WHERE inspection_id={inspection}").SingleAsync()==0);
        }
        // Two real service calls with independent connections; the existing Users/catalog
        // locks serialize writers. Both retain the SAME operation identity/canonical request.
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
        async Task<HotelOpsDbContext> Open(){var next=new HotelOpsDbContext(options);await next.Database.OpenConnectionAsync();using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(environment));await using var q=next.Database.GetDbConnection().CreateCommand();q.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')";await using var r=await q.ExecuteReaderAsync();var e=expected.RootElement;if(!await r.ReadAsync()||r.GetString(0)!=e.GetProperty("server").GetString()||r.GetString(1)!=e.GetProperty("database").GetString()||r.GetString(2)!=e.GetProperty("owner").GetString())throw new Exception("Connection isolation rejected");return next;}
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IActionResult> Concurrent(Guid operation){await using var next=await Open();await gate.Task;return await Propose(controller(next,login),saved,operation);}
        var a=Concurrent(op);var b=Concurrent(allFixed?Guid.NewGuid():op);gate.SetResult();var concurrent=await Task.WhenAll(a,b);
        var proposals=concurrent.Where(r=>Status(r)==200).Select(Value<ReinspectionConclusionProposal>).ToArray();
        Check(allFixed?"Concurrent different operations yield one commit and one conflict":"Concurrent identical operation commits once and returns verified replay",(allFixed?concurrent.Count(r=>Status(r)==200)==1&&concurrent.Count(r=>Status(r)==409)==1:proposals.Count(p=>p.Replayed)==1)&&await db.AuditLogs.CountAsync()==before+1);
        var committed=proposals.Single(p=>!p.Replayed);op=committed.OperationId;
        Check(allFixed?"All Fixed persists proposed Completed":"Not Fixed persists proposed Failed and exact next-round source IDs",committed.ProposedReworkState==(allFixed?"COMPLETED":"FAILED")&&committed.NotFixedDefectIds.SequenceEqual(allFixed?[]:originalLinks)&&!committed.LifecycleApplied&&!committed.NextRoundCreationAvailable&&committed.InspectionRevision!=saved.InspectionRevision&&committed.BindingRevision!=saved.BindingRevision);
        await using(var next=await Open()){
            var read=Value<ReinspectionConclusionProposalRead>(await controller(next,login).ReadProposal(branch,inspection,rework,default));
            Check("Separate connection reload sees committed immutable proposal",read.Proposal?.OperationId==op&&read.Proposal.ProposalRevision==committed.ProposalRevision&&read.Proposal.ProposedReworkState==committed.ProposedReworkState);
        }
        Check("Repeated same operation replays despite old request ETag",Value<ReinspectionConclusionProposal>(await Propose(c,saved,op)).Replayed&&await db.AuditLogs.CountAsync()==before+1);
        Check("Different operation for reviewed source conflicts",Status(await Propose(c,saved,Guid.NewGuid()))==409);
        Check("Same operation with changed canonical revision conflicts",Status(await Propose(c,saved with{BindingRevision=bound.BindingRevision},op))==409);
        var now=await Read();c.Request.Headers.IfMatch=now.InspectionRevision;
        Check("Save cannot alter already-reviewed results",Status(await c.Save(branch,inspection,new(rework,now.ReworkRevision,now.BindingRevision,now.Defects.Select(d=>new ReinspectionDecisionDto(d.DefectId,!allFixed)).ToArray()),default))==409);
        var afterLinks=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={rework} ORDER BY defect_id").ToArrayAsync();
        Check("Original defect associations preserved and operational conclusion closed",originalLinks.SequenceEqual(afterLinks)&&Status(await c.Conclude(branch,inspection,rework,default))==503&&await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync()=="PENDING_REINSPECTION");
        if(allFixed){
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={other} WHERE inspection_id={inspection}");
            Check("Replay rechecks persisted reinspector",Status(await Propose(c,saved,op))==403);
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={actor} WHERE inspection_id={inspection}");
            Check("Changed persisted source invalidates completed replay",Status(await Propose(c,saved,op))==412);
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={room}");
            Check("Replay cannot bypass missing current cycle",Status(await Propose(c,saved,op)) is 404 or 409);
        }
        await File.WriteAllTextAsync(Path.Combine(output,allFixed?"fixed-proposal-results.json":"notfixed-proposal-results.json"),JsonSerializer.Serialize(new{success=true,proposalCommitted=true,authoritativeLifecycleApplied=false,syntheticSource=true,httpMiddlewareVerified=false,cases},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{cases.Count}/{cases.Count} PASS proposal storage/replay; authoritative lifecycle NOT_RUN");
    }
    internal static async Task VerifyStorageGuardsAsync(HotelOpsDbContext db,string output,Func<Task> guard)
    {
        var inspection=await db.Database.SqlQuery<long>($"SELECT inspection_id AS Value FROM dbo.fe05_reinspection_conclusion_proposals_v1 WHERE proposed_rework_state='FAILED'").SingleAsync();
        var cases=new List<object>();
        async Task Denied(string name,FormattableString sql,string message){
            await guard();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);bool rejected=false;
            var before=await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.fe05_reinspection_conclusion_proposals_v1 WHERE inspection_id={inspection} FOR JSON PATH) AS Value").SingleAsync();
            try{await guard();await db.Database.ExecuteSqlInterpolatedAsync(sql);}catch(SqlException e)when(e.Number==51997&&e.Message.Contains(message)){rejected=true;}
            finally{await tx.RollbackAsync();db.ChangeTracker.Clear();}
            var after=await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.fe05_reinspection_conclusion_proposals_v1 WHERE inspection_id={inspection} FOR JSON PATH) AS Value").SingleAsync();
            if(!rejected||before!=after)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);
        }
        await Denied("Direct result update cannot bypass reviewed storage freeze",$"UPDATE dbo.fe05_reinspection_defect_results_v1 SET fixed=1 WHERE inspection_id={inspection}","Reviewed proposal results cannot be mutated");
        await Denied("Persisted proposal cannot be rewritten",$"UPDATE dbo.fe05_reinspection_conclusion_proposals_v1 SET proposed_rework_state='COMPLETED' WHERE inspection_id={inspection}","Conclusion proposal is immutable");
        await Denied("Persisted proposal cannot be deleted",$"DELETE dbo.fe05_reinspection_conclusion_proposals_v1 WHERE inspection_id={inspection}","Conclusion proposal is immutable");
        await File.WriteAllTextAsync(Path.Combine(output,"storage-guard-results.json"),JsonSerializer.Serialize(new{success=true,proposalOnly=true,cases},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("3/3 PASS SQL proposal storage guards; no byte-immutability acceptance");
    }

}
