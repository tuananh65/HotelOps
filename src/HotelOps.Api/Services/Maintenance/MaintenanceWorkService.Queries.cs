using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public async Task<MaintenancePagedResponse> GetWorksAsync(MaintenanceQueryRequest request, CancellationToken cancellationToken)
    {
        return await ListAsync(_dbContext.MaintenanceWorks.AsNoTracking(), request, cancellationToken);
    }

    internal static IQueryable<MaintenanceWork> Filter(IQueryable<MaintenanceWork> query, MaintenanceQueryRequest request)
    {
        if (request.Page <= 0)
            throw new ValidationException("Page must be greater than zero.");
        if (request.PageSize <= 0 || request.PageSize > 100)
            throw new ValidationException("PageSize must be between 1 and 100.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            throw new ValidationException("Pagination offset exceeds the supported range.");
        var status = request.Status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(status) && !IsWorkStatus(status) && status is not ("PENDING_ASSIGNMENT" or "ASSIGNED"))
            throw new ValidationException("Unknown maintenance status.");

        var keyword = request.Keyword?.Trim();
        if (keyword?.Length > 200) throw new ValidationException("Keyword must not exceed 200 characters.");
        if (!string.IsNullOrEmpty(keyword)) query = query.Where(w => w.WorkNo.Contains(keyword) || w.Title.Contains(keyword));
        var source = request.Source?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(source) && source is not ("INCIDENT" or "PREVENTIVE")) throw new ValidationException("Unknown maintenance source.");
        if (source == "INCIDENT") query = query.Where(w => w.IncidentId != null);
        if (source == "PREVENTIVE") query = query.Where(w => w.IncidentId == null && w.PreventiveMaintenanceRuns.Any());
        if (request.IncidentId is long incidentId)
            query = query.Where(work => work.IncidentId == incidentId);
        if (request.AssigneeId is long assigneeId)
            query = query.Where(work => work.AssigneeId == assigneeId);
        if (request.DepartmentId is long departmentId)
            query = query.Where(work => work.DepartmentId == departmentId);
        if (request.PriorityId is long priorityId)
            query = query.Where(work => work.PriorityId == priorityId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(work => work.Status == status);

        return query;
    }

    internal static async Task<MaintenancePagedResponse> ListAsync(IQueryable<MaintenanceWork> query, MaintenanceQueryRequest request, CancellationToken cancellationToken)
    {
        query = Filter(query, request);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(work => work.CreatedAt)
            .ThenByDescending(work => work.MaintenanceWorkId)
            .Skip((int)offset).Take(request.PageSize)
            .Select(work => new MaintenanceWorkListDto
            {
                MaintenanceWorkId = work.MaintenanceWorkId,
                WorkNo = work.WorkNo,
                AssigneeName = work.Assignee != null ? work.Assignee.FullName : null,
                PriorityName = work.Priority != null && work.Priority.GroupCode == "PRIORITY" ? work.Priority.DisplayName : null,
                RoomNumber = work.Incident != null ? (work.Incident.Room != null ? work.Incident.Room.RoomNumber : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Room != null ? r.Schedule.Room.RoomNumber : null).FirstOrDefault(),
                AreaName = work.Incident != null ? (work.Incident.Area != null ? work.Incident.Area.AreaName : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Area != null ? r.Schedule.Area.AreaName : null).FirstOrDefault(),
                AssetCode = work.Incident != null ? (work.Incident.Asset != null ? work.Incident.Asset.AssetCode : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Asset != null ? r.Schedule.Asset.AssetCode : null).FirstOrDefault(),
                AssetName = work.Incident != null ? (work.Incident.Asset != null ? work.Incident.Asset.AssetName : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Asset != null ? r.Schedule.Asset.AssetName : null).FirstOrDefault(),

                Source = work.IncidentId != null ? "INCIDENT" : work.PreventiveMaintenanceRuns.Any() ? "PREVENTIVE" : null,
                IncidentId = work.IncidentId,
                AssigneeId = work.AssigneeId,
                DepartmentId = work.DepartmentId,
                Title = work.Title,
                PriorityId = work.PriorityId,
                Status = work.Status,
                PlannedStartAt = work.PlannedStartAt,
                PlannedEndAt = work.PlannedEndAt,
                ActualStartAt = work.ActualStartAt,
                ResolvedAt = work.ResolvedAt,
                ReviewedBy = work.ReviewedBy,
                ReviewedAt = work.ReviewedAt,
                CreatedBy = work.CreatedBy,
                CreatedAt = work.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new MaintenancePagedResponse
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)(((long)totalCount + request.PageSize - 1) / request.PageSize)
        };
    }

    public async Task<MaintenanceWorkDetailDto?> GetWorkAsync(long id, CancellationToken cancellationToken)
    {
        var result = await DetailAsync(_dbContext.MaintenanceWorks.AsNoTracking(), id, cancellationToken, true);
        if (result != null)
        {
            var usages = await UsageDetails(id, cancellationToken);
            foreach (var log in result.MaintenanceWorkLogs) log.MaterialUsages = usages.Where(x => x.MaintenanceWorkLogId == log.LogId).ToArray();
        }
        return result;
    }

    internal static async Task<MaintenanceWorkDetailDto?> DetailAsync(IQueryable<MaintenanceWork> query, long id,
        CancellationToken cancellationToken, bool includeMaterialReferences = false)
    {
        return await query
            .Where(work => work.MaintenanceWorkId == id)
            .Select(work => new MaintenanceWorkDetailDto
            {
                MaintenanceWorkId = work.MaintenanceWorkId,
                MaterialUsageAvailable = includeMaterialReferences,
                Source = work.IncidentId != null ? "INCIDENT" : work.PreventiveMaintenanceRuns.Any() ? "PREVENTIVE" : null,
                WorkNo = work.WorkNo,
                AssigneeName = work.Assignee != null ? work.Assignee.FullName : null,
                PriorityName = work.Priority != null && work.Priority.GroupCode == "PRIORITY" ? work.Priority.DisplayName : null,
                RoomNumber = work.Incident != null ? (work.Incident.Room != null ? work.Incident.Room.RoomNumber : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Room != null ? r.Schedule.Room.RoomNumber : null).FirstOrDefault(),
                AreaName = work.Incident != null ? (work.Incident.Area != null ? work.Incident.Area.AreaName : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Area != null ? r.Schedule.Area.AreaName : null).FirstOrDefault(),
                AssetCode = work.Incident != null ? (work.Incident.Asset != null ? work.Incident.Asset.AssetCode : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Asset != null ? r.Schedule.Asset.AssetCode : null).FirstOrDefault(),
                AssetName = work.Incident != null ? (work.Incident.Asset != null ? work.Incident.Asset.AssetName : null)
                    : work.PreventiveMaintenanceRuns.Select(r => r.Schedule.Asset != null ? r.Schedule.Asset.AssetName : null).FirstOrDefault(),

                IncidentId = work.IncidentId,
                AssigneeId = work.AssigneeId,
                DepartmentId = work.DepartmentId,
                Title = work.Title,
                PriorityId = work.PriorityId,
                Status = work.Status,
                PlannedStartAt = work.PlannedStartAt,
                PlannedEndAt = work.PlannedEndAt,
                ActualStartAt = work.ActualStartAt,
                ResolvedAt = work.ResolvedAt,
                ReviewedBy = work.ReviewedBy,
                ReviewedAt = work.ReviewedAt,
                CreatedBy = work.CreatedBy,
                CreatedAt = work.CreatedAt,
                Instruction = work.Instruction,
                ResolutionNote = work.ResolutionNote,
                MaintenanceWorkLogs = work.MaintenanceWorkLogs
                    .OrderBy(log => log.LoggedAt).ThenBy(log => log.LogId)
                    .Select(log => new MaintenanceWorkLogDto
                    {
                        LogId = log.LogId,
                        MaintenanceWorkId = log.MaintenanceWorkId,
                        WorkNote = log.WorkNote,
                        SpentMinutes = log.SpentMinutes,
                        MaterialIssueId = includeMaterialReferences ? log.MaterialIssueId : null,
                        Status = log.Status,
                        LoggedBy = log.LoggedBy,
                        LoggedAt = log.LoggedAt,
                    }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool IsWorkStatus(string status) =>
        status is "PENDING" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CLOSED" or "CANCELLED";
}
