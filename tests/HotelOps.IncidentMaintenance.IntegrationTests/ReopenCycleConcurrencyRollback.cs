using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

sealed partial class IntegritySuite
{
    async Task ReopenConcurrency()
    {
        var p = await Prepare("CLOSED");
        Func<HotelOpsDbContext, Task> reopen = async d => { await Maintenance(d).ReopenWorkAsync(p.Work, ReopenInput(), default); };
        await OrderedRace("reopen/reopen", reopen, reopen, (a, b) => Check(a && !b, "One cycle creation winner"));
        Check((await Cycles(p.Work)).TotalCount == 2, "No duplicate cycles on overlapping reopen");
        foreach (var status in new[] { "OPEN", "CLOSED" }) foreach (var reopenFirst in new[] { true, false })
        {
            p = await Prepare("CLOSED"); var target = p;
            reopen = async d => { await Maintenance(d).ReopenWorkAsync(target.Work, ReopenInput(), default); };
            Func<HotelOpsDbContext, Task> generic = async d => { await new IncidentService(d).ChangeIncidentStatusAsync(target.Incident, Change(status, prefix), default); };
            await OrderedRace("reopen/generic " + status + "; reopenFirst=" + reopenFirst, reopenFirst ? reopen : generic, reopenFirst ? generic : reopen,
                (a, b) => Check(reopenFirst ? a && b == (status == "OPEN") : a == (status == "CLOSED") && b, "Generic ownership/no-op respects serialized state"));
            await AssertPair(p.Work, "PENDING", "OPEN");
        }
        foreach (var action in new[] { "verify", "cancel", "plain-log", "material-log" }) foreach (var reopenFirst in new[] { true, false })
        {
            IssuedSource? source = null;
            if (action == "material-log") { source = await IssueSource(); await Command(source.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await Command(source.Work, "verify", new { ActorUserId = verifier, Note = prefix }); p = (source.Incident, source.Work); }
            else p = await Prepare("CLOSED");
            var target = p; var issued = source;
            reopen = async d => { await Maintenance(d).ReopenWorkAsync(target.Work, ReopenInput(), default); };
            Func<HotelOpsDbContext, Task> command = async d =>
            {
                var m = Maintenance(d);
                if (action == "verify") await m.VerifyWorkAsync(target.Work, new() { ActorUserId = verifier, Note = prefix }, default);
                else if (action == "cancel") await m.CancelWorkAsync(target.Work, new() { ActorUserId = verifier, Reason = prefix }, default);
                else if (action == "plain-log") await m.CreateLogAsync(target.Work, new() { ActorUserId = actor, WorkNote = prefix }, default);
                else await m.CreateLogAsync(target.Work, UsageInput(issued!.Document.Lines[0].LineId, 1), default);
            };
            await OrderedRace("reopen/" + action + "; reopenFirst=" + reopenFirst, reopenFirst ? reopen : command, reopenFirst ? command : reopen,
                (a, b) => Check(reopenFirst ? a && !b : !a && b, "Old command cannot cross reopen boundary"));
            await AssertPair(target.Work, "PENDING", "OPEN");
        }
        foreach (var reopenFirst in new[] { true, false })
        {
            p = await Prepare("CLOSED"); var target = p; MaintenanceWorkDetailDto? observed = null;
            reopen = async d => { await Maintenance(d).ReopenWorkAsync(target.Work, ReopenInput(), default); };
            Func<HotelOpsDbContext, Task> read = async d => { observed = await Maintenance(d).GetWorkAsync(target.Work, default); };
            await OrderedRace("reopen/detail consistent; reopenFirst=" + reopenFirst, reopenFirst ? reopen : read, reopenFirst ? read : reopen,
                (a, b) => Check(a && b && (reopenFirst ? observed!.Status == "PENDING" && observed.CurrentCycleNo == 2 : observed!.Status == "CLOSED" && observed.CurrentCycleId == null), "No mixed old header/new cycle read"));
        }
    }
    async Task ReopenFaults()
    {
        var p = await Prepare("CLOSED");
        foreach (var flush in new[] { 1, 2, 3 })
        {
            await using var d = Db(new SaveFailure(flush)); await Rollback("reopen legacy/new/reset SQL flush " + flush, async () => { await Maintenance(d).ReopenWorkAsync(p.Work, ReopenInput(), default); });
        }
        await using (var d = Db(new CommitFailure())) await Rollback("reopen before commit", async () => { await Maintenance(d).ReopenWorkAsync(p.Work, ReopenInput(), default); });
        var opened = await Reopen(p.Work); var cycle = opened.CurrentCycleId!.Value; await ManagedStart(p.Work, cycle); await CycleCommand(p.Work, cycle, "submit-result");
        await using (var d = Db(new SaveFailure(1))) await Rollback("managed verify root/cycle/audit flush", async () => { await Maintenance(d).VerifyWorkAsync(p.Work, new() { ActorUserId = verifier, ExpectedCycleId = cycle, Note = prefix }, default); });
        await CycleCommand(p.Work, cycle, "return", verifier);
        foreach (var flush in new[] { 1, 2 })
        {
            await using var d = Db(new SaveFailure(flush)); await Rollback("managed plain log cycle flush " + flush, async () => { await Maintenance(d).CreateLogAsync(p.Work, new() { ActorUserId = actor, ExpectedCycleId = cycle, WorkNote = prefix }, default); });
        }
        await using (var d = Db(new SaveFailure(1))) await Rollback("managed cancel root/log/cycle/audit flush", async () => { await Maintenance(d).CancelWorkAsync(p.Work, new() { ActorUserId = verifier, ExpectedCycleId = cycle, Reason = prefix }, default); });
        var source = await IssueSource(); await Command(source.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await Command(source.Work, "verify", new { ActorUserId = verifier, Note = prefix });
        opened = await Reopen(source.Work); cycle = opened.CurrentCycleId!.Value; await ManagedStart(source.Work, cycle);
        foreach (var flush in new[] { 1, 2, 3 })
        {
            await using var d = Db(new SaveFailure(flush)); await Rollback("managed material cycle/log/usage/audit flush " + flush, async () => { await Maintenance(d).CreateLogAsync(source.Work, new() { ActorUserId = actor, ExpectedCycleId = cycle, WorkNote = prefix, MaterialUsages = [new(source.Document.Lines[0].LineId, usageUnit.UomId, 1)] }, default); });
        }
        p = await Prepare("CLOSED");
        foreach (var resource in new[] { "Incident", "Work" })
        {
            await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
            if (resource == "Incident") await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT incident_id FROM incidents WITH (UPDLOCK,HOLDLOCK) WHERE incident_id={p.Incident}");
            else await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT maintenance_work_id FROM maintenance_works WITH (UPDLOCK,HOLDLOCK) WHERE maintenance_work_id={p.Work}");
            await using var worker = Db(); await worker.Database.OpenConnectionAsync(); var spid = await worker.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(); using var cancel = new CancellationTokenSource();
            await Rollback("reopen cancellation waiting " + resource, async () =>
            {
                var task = Maintenance(worker).ReopenWorkAsync(p.Work, ReopenInput(), cancel.Token);
                try
                {
                    var wait = 0; for (var n = 0; n < 150 && wait == 0; n++) { wait = await blocker.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={spid} AND blocking_session_id<>0 AND wait_type LIKE 'LCK_M_%'").SingleAsync(); if (wait == 0) await Task.Delay(50); }
                    Check(wait == 1, "Reopen cancellation actual SQL wait");
                }
                finally { cancel.Cancel(); }
                await task;
            }); await tx.RollbackAsync();
        }
    }
}
