using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class BranchDeactivationDependencyReader
{
    private async Task<BranchDependencyState[]> TaskFacts(long branch,CancellationToken ct)
    {
        var tasks=db.OperationalTasks.AsNoTracking().Where(t=>t.BranchId==branch);
        var facts=new List<BranchDependencyState>();
        var completed=tasks.Where(t=>t.Status=="COMPLETED");
        if(await TaskVerificationStore.InstalledAsync(db,ct))
        {
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var proven=TaskVerificationStore.ProvenClosedTaskIds(db,now);
            Add("COMPLETED",await completed.LongCountAsync(t=>proven.Contains(t.TaskId),ct),"CLOSED");
            Add("COMPLETED_WITHOUT_CURRENT_VERIFICATION",await completed.LongCountAsync(t=>!proven.Contains(t.TaskId),ct),"UNRESOLVED");
        }
        else Add("COMPLETED_WITHOUT_VERIFICATION_CAPABILITY",await completed.LongCountAsync(ct),"UNRESOLVED");

        // Cancellation must match the workflow contract and have no active executor/interval.
        var cancelled=tasks.Where(t=>t.Status=="CANCELLED");
        if(await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN COL_LENGTH('dbo.task_progress_versions','workflow_status') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1'
                AND CONVERT(nvarchar(20),value)='1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1)
        {
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var valid=TaskWorkflowRead.Query(db,now).Where(w=>w.State=="CANCELLED").Select(w=>w.TaskId);
            var clean=cancelled.Where(t=>valid.Contains(t.TaskId)
                && !db.WorkIntervals.Any(w=>w.WorkItemKind=="TASK"&&w.WorkItemId==t.TaskId&&w.ClosedAt==null));
            var count=await clean.LongCountAsync(ct);
            Add("CANCELLED",count,"CLOSED");
            Add("CANCELLED_WITH_OPEN_EXECUTION_OR_MISSING_WORKFLOW",await cancelled.LongCountAsync(ct)-count,"UNRESOLVED");
        }
        else Add("CANCELLED_WITHOUT_WORKFLOW_CAPABILITY",await cancelled.LongCountAsync(ct),"UNRESOLVED");
        foreach(var group in await tasks.Where(t=>t.Status!="COMPLETED"&&t.Status!="CANCELLED")
            .GroupBy(t=>t.Status).Select(g=>new{Status=g.Key,Count=g.LongCount()}).ToArrayAsync(ct))
            Add(group.Status,group.Count,Classify("operational_tasks",group.Status));
        return facts.ToArray();
        void Add(string status,long count,string classification){if(count>0)facts.Add(new("operational_tasks",status,count,classification));}
    }

    private async Task<BranchDependencyState[]> MaterialFacts(long branch,CancellationToken ct)
    {
        var requests=await db.MaterialRequests.AsNoTracking().Where(r=>r.BranchId==branch)
            .Select(r=>new{r.RequestId,r.Status}).OrderBy(r=>r.RequestId).ToArrayAsync(ct);
        var facts=new List<BranchDependencyState>();
        foreach(var request in requests)
        {
            var classification=Classify("material_requests",request.Status);
            var status=request.Status;
            if(status is "FULLY_ISSUED" or "REMAINDER_CLOSED")
            {
                var evidence=await MaterialCompletionEvidence.ReadAsync(db,request.RequestId,branch,ct);
                classification=evidence.FullyIssuedProven||evidence.RemainderClosureProven?"CLOSED":"UNRESOLVED";
                if(classification=="UNRESOLVED")status+="_COMPLETION_UNPROVEN";
            }
            facts.Add(new("material_requests",status,1,classification));
        }
        return facts.GroupBy(f=>new{f.Kind,f.Status,f.Classification})
            .Select(g=>new BranchDependencyState(g.Key.Kind,g.Key.Status,g.Sum(f=>f.Count),g.Key.Classification)).ToArray();
    }
}
