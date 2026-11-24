using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // Records the supervisor's physical inspection. No invented checklist answers or execution time.
    public Task<object> PaperInspection(long id, PaperInspectionRequest request, CancellationToken ct) => atomic.Run(async () =>
    {
        var parent = await Load(id, "read", ct);
        await access.Require("INSPECTION_PERFORM", parent.BranchId, ct: ct);
        Output.Require(parent.Kind == "CLEANING", "CLEANING_PARENT_REQUIRED", 400);
        Output.Key(request.OperationKey);
        var prior=await db.WorkEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(prior!=null){
            Output.Require(prior.EventType==(request.Result=="PASS"?"INSPECTION_PASSED":"INSPECTION_FAILED")&&prior.Reason==request.Note?.Trim(),"IDEMPOTENCY_KEY_REUSED");
            var recorded=await db.WorkItems.AsNoTracking().SingleAsync(w=>w.ParentWorkId==id&&w.Kind=="INSPECTION"&&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey&&e.EventType=="PAPER_INSPECTION_RECORDED"),ct);
            return (object)new{work=Output.Record(parent),inspection=Output.Record(recorded)};
        }
        Output.Version(parent.RowVersion, request.RowVersion);
        Output.Key(request.OperationKey);
        Output.Require(request.Result is "PASS" or "FAIL", "INVALID_OUTCOME", 400);
        Output.Require(!await db.WorkEvents.AnyAsync(e => e.WorkId == id && e.ActorUserId == actor.Id && e.OperationKey == request.OperationKey, ct), "OPERATION_ALREADY_APPLIED");
        Output.Require(parent.Status is "PENDING_INSPECTION" or "REWORK_REQUIRED", "CLEANING_NOT_READY_FOR_REVIEW");
        var room = await db.Rooms.SingleAsync(r => r.RoomId == parent.RoomId && r.BranchId == parent.BranchId, ct);
        Output.Require(room.IsActive && room.TurnoverNo == parent.TurnoverNo && room.CurrentCleaningWorkId == id, "ROOM_CYCLE_NO_LONGER_CURRENT");
        Output.Require(!await db.WorkAssignments.AnyAsync(a => a.AssigneeUserId == actor.Id &&
            (a.WorkId == id || db.WorkItems.Any(w => w.WorkId == a.WorkId && w.ParentWorkId == id && w.Kind == "REWORK")), ct), "INSPECTOR_MUST_BE_INDEPENDENT", 403);
        var reworks = await db.WorkItems.Where(w => w.ParentWorkId == id && w.Kind == "REWORK" && !WorkRules.Terminal.Contains(w.Status)).ToArrayAsync(ct);
        Output.Require(reworks.All(w => w.Status == "PENDING_REINSPECTION"), "REWORK_NOT_SUBMITTED");
        var lastFailure = await db.WorkItems.Where(w => w.ParentWorkId == id && w.Kind == "INSPECTION" && w.Status == "FAILED")
            .MaxAsync(w => w.VerifiedAt, ct);
        var latestRework = reworks.Where(w => w.CompletedAt != null && (lastFailure == null || w.CompletedAt > lastFailure))
            .OrderByDescending(w => w.CompletedAt).FirstOrDefault();
        Output.Require(parent.Status != "REWORK_REQUIRED" || latestRework != null, "REWORK_NOT_SUBMITTED");
        var note = request.Note?.Trim();
        if (request.Result == "FAIL") Output.Reason(note);
        var open = await db.WorkItems.Where(w => w.ParentWorkId == id && w.Kind == "INSPECTION" && !WorkRules.Terminal.Contains(w.Status)).ToArrayAsync(ct);
        Output.Require(open.Length <= 1, "HOUSEKEEPING_WORK_ALREADY_ACTIVE");
        var inspection = open.SingleOrDefault();
        if (inspection != null)
        {
            Output.Require(request.InspectionRowVersion != null, "INSPECTION_VERSION_REQUIRED");
            Output.Version(inspection.RowVersion, request.InspectionRowVersion!);
            Output.Require(!await db.WorkAssignments.AnyAsync(a => a.WorkId == inspection.WorkId && a.AssignmentStatus == "ACTIVE" && a.AssigneeUserId != actor.Id, ct), "INSPECTION_ASSIGNED_TO_ANOTHER", 403);
            await CloseTime(inspection, "COMPLETE", ct);
        }
        else
        {
            Output.Require(request.InspectionRowVersion == null, "STALE_ROW_VERSION");
            inspection = new WorkItem { BranchId = parent.BranchId, Kind = "INSPECTION", Status = "ASSIGNED",
                WorkNo = Guid.NewGuid().ToString("N"), Title = "Kiểm tra " + room.Code,
                DepartmentId = parent.DepartmentId, RoomId = room.RoomId, TurnoverNo = room.TurnoverNo,
                ParentWorkId = id, CreatedBy = actor.Id, CreatedAt = Now };
            db.WorkItems.Add(inspection);
            await db.SaveChangesAsync(ct);
        }
        var findings = await db.InspectionFindings.Where(f => f.Status == "OPEN" &&
            db.WorkItems.Any(w => w.WorkId == f.InspectionWorkId && w.ParentWorkId == id)).ToArrayAsync(ct);
        if (request.Result == "PASS" && findings.Length > 0)
        {
            await access.Require("DEFECT_RESOLVE", parent.BranchId, ct: ct);
            Output.Require(request.ResolveFindings && latestRework != null, "PAPER_FINDINGS_CONFIRMATION_REQUIRED");
            Output.Reason(note);
            foreach (var finding in findings)
            {
                finding.ReworkWorkId = latestRework!.WorkId;
                finding.Status = "RESOLVED"; finding.ResolvedAt = Now; finding.ResolutionNote = note;
                audit.Add("Finding", finding.FindingId, "RESOLVED", parent.BranchId, after: new { note, latestRework.WorkId });
            }
        }
        if (request.Result == "FAIL")
            db.InspectionFindings.Add(new InspectionFinding { BranchId = parent.BranchId,
                InspectionWorkId = inspection.WorkId, Description = note!, Status = "OPEN" });
        // Persist append-only evidence before the trigger validates the final result.
        Event(inspection, "PAPER_INSPECTION_RECORDED", request.OperationKey, note, inspection.Status);
        await db.SaveChangesAsync(ct);
        var now = Now;
        var before = parent.Status;
        inspection.Status = request.Result == "PASS" ? "PASSED" : "FAILED";
        inspection.Result = request.Result; inspection.CompletionNote = note;
        inspection.CompletedAt = now; inspection.VerifiedAt = now; inspection.VerifiedBy = actor.Id;
        inspection.ClosedAt = now; inspection.ProgressPercent = 100;
        await EndAssignments(inspection, now, ct);
        parent.Status = request.Result == "PASS" ? "COMPLETED" : "REWORK_REQUIRED";
        if (request.Result == "PASS")
        {
            foreach (var rework in reworks) { rework.Status = "COMPLETED"; rework.ClosedAt = now; await EndAssignments(rework, now, ct); }
            parent.ClosedAt = now;
            await EndAssignments(parent, now, ct);
        }
        db.Entry(parent).Property(w => w.Title).IsModified = true;
        Event(parent, request.Result == "PASS" ? "INSPECTION_PASSED" : "INSPECTION_FAILED", request.OperationKey, note, before);
        var recipients = await db.WorkAssignments.Where(a => a.WorkId == id || reworks.Select(w => w.WorkId).Contains(a.WorkId))
            .Select(a => a.AssigneeUserId).Distinct().ToArrayAsync(ct);
        foreach (var recipient in recipients)
            notifications.Add(parent.BranchId, recipient, request.Result == "PASS" ? "INSPECTION_PASSED" : "INSPECTION_FAILED", parent.Title, id, null, request.OperationKey);
        await db.SaveChangesAsync(ct);
        if (request.Result == "PASS")
        {
            await readiness.Reevaluate(room.RoomId, inspection.WorkId, request.OperationKey, "INSPECTION_PASSED", ct);
        }
        return (object)new { work = Output.Record(parent), inspection = Output.Record(inspection) };
    }, ct);
}
