using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public async Task<MaintenancePagedResponse> GetWorksAsync(MaintenanceQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.Page <= 0)
            throw new ValidationException("Page must be greater than zero.");
        if (request.PageSize <= 0 || request.PageSize > 100)
            throw new ValidationException("PageSize must be between 1 and 100.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            throw new ValidationException("Pagination offset exceeds the supported range.");
        var status = request.Status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(status) && !IsWorkStatus(status))
            throw new ValidationException("Unknown maintenance status.");

        var query = _dbContext.MaintenanceWorks.AsNoTracking();
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

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(work => work.CreatedAt)
            .ThenByDescending(work => work.MaintenanceWorkId)
            .Skip((int)offset).Take(request.PageSize)
            .Select(work => new MaintenanceWorkListDto
            {
                MaintenanceWorkId = work.MaintenanceWorkId,
                WorkNo = work.WorkNo,
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

    public Task<MaintenanceWorkDetailDto?> GetWorkAsync(long id, CancellationToken cancellationToken)
    {
        return _dbContext.MaintenanceWorks.AsNoTracking()
            .Where(work => work.MaintenanceWorkId == id)
            .Select(work => new MaintenanceWorkDetailDto
            {
                MaintenanceWorkId = work.MaintenanceWorkId,
                WorkNo = work.WorkNo,
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
                        MaterialIssueId = log.MaterialIssueId,
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
