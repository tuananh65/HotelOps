using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class HistoricalInspectionCleaner
    {
        public long InspectionId { get; set; }
        public long UserId { get; set; }
        public string Name { get; set; } = "";
        public long AssignmentId { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    // Historical completion proof, independent of the room's CURRENT cleaning cycle
    // and of the cleaner's later employment/role state. An assignment alone is not proof.
    // Caller verifies the immutable attribution capability and scopes the inspection query.
    private IQueryable<HistoricalInspectionCleaner> HistoricalInspectionCleaners() =>
        db.Database.SqlQueryRaw<HistoricalInspectionCleaner>("""
            SELECT i.inspection_id AS InspectionId,a.cleaner_user_id AS UserId,u.full_name AS Name,
                a.assignment_id AS AssignmentId,a.completed_at AS CompletedAt
            FROM dbo.fe04_cleaner_attributions a
            JOIN dbo.room_inspections i ON i.cleaning_task_id=a.cleaning_task_id AND i.room_id=a.room_id
                AND i.created_at>=a.completed_at
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id AND t.room_id=a.room_id
            JOIN dbo.rooms r ON r.room_id=a.room_id AND r.branch_id=a.branch_id
            JOIN dbo.users u ON u.user_id=a.cleaner_user_id
            JOIN dbo.cleaning_assignments ca ON ca.assignment_id=a.assignment_id
                AND ca.cleaning_task_id=a.cleaning_task_id AND ca.assignee_user_id=a.cleaner_user_id
                AND ca.assigned_at<=a.completed_at AND (ca.ended_at IS NULL OR ca.ended_at>=a.completed_at)
            JOIN dbo.cleaning_progress_logs p ON p.progress_id=a.completion_progress_id
                AND p.cleaning_task_id=a.cleaning_task_id AND p.logged_by=a.cleaner_user_id
                AND p.status='COMPLETED' AND p.progress_percent=100 AND p.logged_at=a.completed_at
            WHERE DATALENGTH(a.completion_revision)=8 AND a.writer_transaction_id>0
            """);
}
