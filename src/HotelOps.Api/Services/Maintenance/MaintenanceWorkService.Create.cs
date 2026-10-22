using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public async Task<MaintenanceWorkDetailDto?> CreateWorkAsync(long incidentId,
        CreateMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        ValidatePlannedTimes(request.PlannedStartAt, request.PlannedEndAt);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var incident = await LockIncidentAsync(incidentId, cancellationToken);
        if (incident is null)
            return null;
        if (incident.Status != "OPEN")
            throw new ValidationException("Corrective work can be created only for an OPEN incident.");
        await RequireUserAsync(request.ActorUserId, "ActorUserId", cancellationToken);
        if (request.DepartmentId is long departmentId && !await _dbContext.Departments.AsNoTracking()
            .AnyAsync(department => department.DepartmentId == departmentId &&
                department.BranchId == incident.BranchId, cancellationToken))
            throw new ValidationException("DepartmentId must identify a department in the incident branch.");
        if (request.PriorityId is long priorityId && !await _dbContext.ReferenceValues.AsNoTracking()
            .AnyAsync(value => value.ReferenceId == priorityId && value.GroupCode == "PRIORITY" &&
                value.IsActive, cancellationToken))
            throw new ValidationException("PriorityId must identify an active PRIORITY reference value.");

        // Every corrective writer takes the same Incident lock before this cardinality check.
        if (await _dbContext.MaintenanceWorks.AsNoTracking().AnyAsync(work =>
            work.IncidentId == incidentId &&
            (work.Status == "PENDING" || work.Status == "IN_PROGRESS" ||
             work.Status == "ON_HOLD" || work.Status == "COMPLETED"), cancellationToken))
            throw new ValidationException("The incident already has an active corrective maintenance work.");

        var now = DateTime.UtcNow;
        var work = new MaintenanceWork
        {
            WorkNo = await AllocateWorkNoAsync(now, cancellationToken),
            IncidentId = incidentId,
            AssigneeId = null,
            DepartmentId = request.DepartmentId,
            Title = request.Title.Trim(),
            Instruction = request.Instruction,
            PriorityId = request.PriorityId,
            Status = "PENDING",
            PlannedStartAt = request.PlannedStartAt,
            PlannedEndAt = request.PlannedEndAt,
            CreatedBy = request.ActorUserId,
            CreatedAt = now
        };
        _dbContext.MaintenanceWorks.Add(work);
        await _dbContext.SaveChangesAsync(cancellationToken);
        AddAudit("maintenance_works", work.MaintenanceWorkId, incident.BranchId,
            request.ActorUserId, now, null, WorkSnapshot(work));
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await RequireDetailAsync(work.MaintenanceWorkId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<string> AllocateWorkNoAsync(DateTime now, CancellationToken cancellationToken)
    {
        var prefix = "MW-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
        var resource = "HotelOps:MaintenanceWorkNo:" + prefix;
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50002, 'Could not acquire maintenance number allocation lock.', 1;",
            cancellationToken);
        var maximum = await _dbContext.Database.SqlQuery<long>($@"
            SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING(work_no, 13, 40))), CAST(0 AS bigint)) AS [Value]
            FROM maintenance_works WITH (UPDLOCK, HOLDLOCK)
            WHERE work_no LIKE {prefix + "%"}")
            .SingleAsync(cancellationToken);
        return prefix + checked(maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
    }
}
