using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkFixture
{
    internal static string EvidenceRoot=>Path.GetFullPath("artifacts/dev3-142a-rework-evidence");
    private static readonly List<string> ownedFiles=[];
    internal static async Task SeedEvidenceAsync(HotelOpsDbContext db,long actor,long inspection,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new Exception("Evidence fixture transaction required");
        Directory.CreateDirectory(EvidenceRoot);
        if((File.GetAttributes(EvidenceRoot)&FileAttributes.ReparsePoint)!=0)throw new Exception("Private fixture root cannot be a link");
        var name=Guid.NewGuid().ToString("N")+".png";var path=Path.Combine(EvidenceRoot,name);
        var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=");
        await using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.Asynchronous))
        {ownedFiles.Add(path);await file.WriteAsync(bytes,ct);}
        var evidence=new Attachment{EntityType="INSPECTION",EntityId=inspection,FileName="synthetic-defect.png",OriginalFileName="synthetic-defect.png",
            MimeType="image/png",FileSizeBytes=bytes.Length,StorageKey=name,UploadedBy=actor,UploadedAt=DateTime.UtcNow};
        db.Attachments.Add(evidence);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_defect_attachments(defect_id,attachment_id) SELECT defect_id,{evidence.AttachmentId} FROM dbo.inspection_defects WHERE inspection_id={inspection}",ct);
    }
    internal static void CleanupEvidenceFiles()
    {
        foreach(var path in ownedFiles)File.Delete(path);
        ownedFiles.Clear();
    }
    // Invoked only after the exact instance/database/GUID/run guard in SprintApproveFixture.
    internal static async Task SeedAsync(HotelOpsDbContext db,long actor,long cleaner,long branch,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new Exception("Fixture transaction required");
        await SprintReworkAuthorityFixture.InstallAsync(db,ct);
        var department=new Department{BranchId=branch,DepartmentCode="HK",DepartmentName="Synthetic housekeeping",IsActive=true};
        db.Departments.Add(department);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.users SET department_id={department.DepartmentId} WHERE user_id={cleaner};
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at)
              SELECT {cleaner},role_id,{branch},DATEADD(minute,-1,SYSUTCDATETIME()) FROM dbo.roles WHERE role_code='HK_STAFF' AND is_active=1;
            IF @@ROWCOUNT<>1 THROW 51992,'Existing HK staff role required',1;
            """,ct);
        // GBR-046: prove actual browser creation without any scheduled shift.

    }
    internal static Task CleanupAsync(HotelOpsDbContext db,long cleaner,long branch,long inspection,CancellationToken ct)
        => CleanupCoreAsync(db,cleaner,branch,inspection,ct);
    private static async Task CleanupCoreAsync(HotelOpsDbContext db,long cleaner,long branch,long inspection,CancellationToken ct)
    {
        // Retained next-round source evidence must survive until the last source-based test.
        var retainedFiles=await db.Database.SqlQuery<string>($"""
            SELECT a.storage_key AS Value FROM dbo.attachments a JOIN dbo.room_inspections i ON i.inspection_id=a.entity_id
            JOIN dbo.room_inspections original ON original.inspection_id={inspection}
            JOIN dbo.rooms room ON room.room_id=i.room_id AND room.branch_id={branch}
            WHERE a.entity_type='INSPECTION' AND i.inspection_id<>original.inspection_id
              AND i.cleaning_task_id=original.cleaning_task_id AND i.room_id=original.room_id
              AND i.version_id=original.version_id AND i.inspector_user_id=original.inspector_user_id
              AND a.uploaded_by=original.inspector_user_id
            """).ToArrayAsync(ct);
        if(retainedFiles.Any(name=>Path.GetFileName(name)!=name||name.Contains(':')))throw new Exception("Owned source evidence filename required");
        // Exact disposable identity and all proof ownership are checked before any deletion.
        // Drop the owned trial proof store normally; never disable its immutability guard.
        await SprintReworkAuthorityFixture.CleanupAsync(db,inspection,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @task bigint=(SELECT cleaning_task_id FROM dbo.room_inspections WHERE inspection_id={inspection});
            DECLARE @children TABLE(id bigint PRIMARY KEY);
            INSERT @children SELECT i.inspection_id FROM dbo.room_inspections i JOIN dbo.room_inspections original ON original.inspection_id={inspection}
              WHERE i.cleaning_task_id=@task AND i.inspection_id<>original.inspection_id AND i.room_id=original.room_id
                AND i.inspector_user_id=original.inspector_user_id AND i.version_id=original.version_id;
            DELETE r FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
              JOIN dbo.cleaning_reworks w ON w.rework_id=n.source_id WHERE n.source_type='CLEANING_REWORK' AND w.cleaning_task_id=@task
                AND(w.inspection_id={inspection} OR w.inspection_id IN(SELECT id FROM @children));
            DELETE n FROM dbo.notifications n JOIN dbo.cleaning_reworks w ON w.rework_id=n.source_id WHERE n.source_type='CLEANING_REWORK' AND w.cleaning_task_id=@task
              AND(w.inspection_id={inspection} OR w.inspection_id IN(SELECT id FROM @children));
            DELETE d FROM dbo.cleaning_rework_defects d JOIN dbo.cleaning_reworks r ON r.rework_id=d.rework_id WHERE r.cleaning_task_id=@task
              AND(r.inspection_id={inspection} OR r.inspection_id IN(SELECT id FROM @children));
            DELETE dbo.cleaning_reworks WHERE cleaning_task_id=@task AND(inspection_id={inspection} OR inspection_id IN(SELECT id FROM @children));
            UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id IN(SELECT id FROM @children);
            DELETE l FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id
              WHERE d.inspection_id={inspection} OR d.inspection_id IN(SELECT id FROM @children);
            DELETE dbo.attachments WHERE entity_type='INSPECTION' AND(entity_id={inspection} OR entity_id IN(SELECT id FROM @children));
            DELETE dbo.inspection_defects WHERE inspection_id IN(SELECT id FROM @children);
            DELETE dbo.inspection_results WHERE inspection_id IN(SELECT id FROM @children);
            DELETE dbo.room_inspections WHERE inspection_id IN(SELECT id FROM @children);
            DELETE a FROM dbo.shift_assignments a JOIN dbo.work_shifts s ON s.shift_id=a.shift_id WHERE s.branch_id={branch} AND a.user_id={cleaner};
            DELETE dbo.work_shifts WHERE branch_id={branch};
            DELETE dbo.shift_templates WHERE branch_id={branch} AND shift_code='D3-REWORK';
            UPDATE dbo.users SET department_id=NULL WHERE user_id={cleaner};
            DELETE dbo.departments WHERE branch_id={branch} AND department_code='HK';
            """,ct);
        foreach(var name in retainedFiles)File.Delete(Path.Combine(EvidenceRoot,name));
        CleanupEvidenceFiles();
    }
}
