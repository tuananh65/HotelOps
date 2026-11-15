using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkFixture
{
    // Invoked only after the exact instance/database/GUID/run guard in SprintApproveFixture.
    internal static async Task SeedAsync(HotelOpsDbContext db,long actor,long cleaner,long branch,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new Exception("Fixture transaction required");
        var department=new Department{BranchId=branch,DepartmentCode="HK",DepartmentName="Synthetic housekeeping",IsActive=true};
        db.Departments.Add(department);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.users SET department_id={department.DepartmentId} WHERE user_id={cleaner};
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at)
              SELECT {cleaner},role_id,{branch},DATEADD(minute,-1,SYSUTCDATETIME()) FROM dbo.roles WHERE role_code='HK_STAFF' AND is_active=1;
            IF @@ROWCOUNT<>1 THROW 51992,'Existing HK staff role required',1;
            """,ct);
        var template=new ShiftTemplate{BranchId=branch,ShiftCode="D3-REWORK",ShiftName="Synthetic rework shift",StartTime=new(0,0),EndTime=new(23,59),IsActive=true};
        db.ShiftTemplates.Add(template);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var now=DateTime.UtcNow;
        var shift=new WorkShift{BranchId=branch,DepartmentId=department.DepartmentId,ShiftTemplateId=template.ShiftTemplateId,
            ShiftDate=DateOnly.FromDateTime(now),ScheduledStartAt=now.AddMinutes(-30),ScheduledEndAt=now.AddMinutes(90),RequiredStaffCount=1,Status="PLANNED",CreatedBy=actor,CreatedAt=now};
        db.WorkShifts.Add(shift);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        db.ShiftAssignments.Add(new ShiftAssignment{ShiftId=shift.ShiftId,UserId=cleaner,AssignmentStatus="ASSIGNED",AssignedBy=actor,AssignedAt=now.AddMinutes(-1)});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
    }
    internal static Task CleanupAsync(HotelOpsDbContext db,long cleaner,long branch,long inspection,CancellationToken ct)
        =>db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE d FROM dbo.cleaning_rework_defects d JOIN dbo.cleaning_reworks r ON r.rework_id=d.rework_id WHERE r.inspection_id={inspection};
            DELETE dbo.cleaning_reworks WHERE inspection_id={inspection};
            DELETE a FROM dbo.shift_assignments a JOIN dbo.work_shifts s ON s.shift_id=a.shift_id WHERE s.branch_id={branch} AND a.user_id={cleaner};
            DELETE dbo.work_shifts WHERE branch_id={branch};
            DELETE dbo.shift_templates WHERE branch_id={branch} AND shift_code='D3-REWORK';
            UPDATE dbo.users SET department_id=NULL WHERE user_id={cleaner};
            DELETE dbo.departments WHERE branch_id={branch} AND department_code='HK';
            """,ct);
}
