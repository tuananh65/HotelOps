SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
DECLARE @lan bigint=(SELECT user_id FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE username='lan.dang');
DECLARE @branch bigint,@department bigint,@day bigint,@now datetime2(7)=SYSUTCDATETIME(),@midnight datetime2(7),@historical datetime2(7);
SELECT @branch=primary_branch_id,@department=department_id FROM dbo.users WHERE user_id=@lan;
IF @lan IS NULL OR @department IS NULL THROW 50000,'Demo account scope unavailable',1;
IF EXISTS(SELECT 1 FROM dbo.operational_tasks WHERE task_code LIKE 'WT-DEMO-20261120-%') THROW 50000,'Demo already exists; no duplicate writes',1;
SET @midnight=DATEADD(hour,-7,CONVERT(datetime2(7),CONVERT(date,DATEADD(hour,7,@now))));
SET @historical=DATEADD(day,-4,DATEADD(hour,8,@midnight));
SELECT @day=workday_id FROM dbo.workdays WHERE user_id=@lan AND branch_id=@branch AND status='WORKING' AND started_at<DATEADD(minute,-25,@now);
IF @day IS NULL OR @now<DATEADD(minute,25,@midnight) THROW 50000,'Not enough current-day time for demo; review required',1;
DECLARE @ranges TABLE(n int,workday_id bigint,start_at datetime2(7),end_at datetime2(7),title nvarchar(200));
DECLARE @j int=0,@historyDay bigint;
WHILE @j<2 BEGIN
 DECLARE @start datetime2(7)=DATEADD(day,@j,@historical),@end datetime2(7)=DATEADD(hour,4,DATEADD(day,@j,@historical));
 IF EXISTS(SELECT 1 FROM dbo.workdays WHERE user_id=@lan AND started_at<@end AND (ended_at IS NULL OR ended_at>@start)) THROW 50000,'Demo workday overlaps existing workday',1;
 INSERT dbo.workdays(user_id,branch_id,department_id,work_date,is_unscheduled,unscheduled_reason,status,started_at,ended_at)
 VALUES(@lan,@branch,@department,CONVERT(date,DATEADD(hour,7,@start)),1,N'[DEMO] Ca mẫu so sánh thời gian, không phải chấm công thực tế','ENDED',@start,@end);
 SET @historyDay=SCOPE_IDENTITY();
 INSERT @ranges VALUES(@j+1,@historyDay,DATEADD(minute,30,@start),DATEADD(minute,90+@j*30,@start),CASE WHEN @j=0 THEN N'[DEMO] Sắp xếp xe dụng cụ — 60 phút' ELSE N'[DEMO] Chuẩn bị đồ vải — 90 phút' END);
 SET @j=@j+1;
END;
INSERT @ranges VALUES(3,@day,DATEADD(minute,-23,@now),DATEADD(minute,-18,@now),N'[DEMO] Kiểm tra dụng cụ — 5 phút'),(4,@day,DATEADD(minute,-14,@now),DATEADD(minute,-4,@now),N'[DEMO] Sắp xếp đồ vải — 10 phút');
IF EXISTS(SELECT 1 FROM @ranges r JOIN dbo.work_intervals i ON i.user_id=@lan AND i.effective_started_at<r.end_at AND (i.effective_ended_at IS NULL OR i.effective_ended_at>r.start_at)) THROW 50000,'Demo interval overlaps existing interval',1;
DECLARE @n int=1,@task bigint,@interval bigint,@s datetime2(7),@e datetime2(7),@title nvarchar(200),@d bigint;
WHILE @n<=4 BEGIN
 SELECT @s=start_at,@e=end_at,@title=title,@d=workday_id FROM @ranges WHERE n=@n;
 INSERT dbo.operational_tasks(task_code,branch_id,department_id,title,description,source_type,status,progress_percent,actual_start_at,completed_at,completion_note,created_by,created_at)
 VALUES(CONCAT('WT-DEMO-20261120-',@n),@branch,@department,@title,N'[DEMO] Số liệu mẫu phục vụ so sánh nhật ký và thử đề nghị điều chỉnh; không phải công việc thực tế.','MANUAL','IN_PROGRESS',0,@s,NULL,N'[DEMO] Kết quả mẫu',@lan,@s);
 SET @task=SCOPE_IDENTITY();
 INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES(@task,'IN_PROGRESS');
 INSERT dbo.task_completion_criteria(task_id,label,is_required,sort_order,outcome,note) VALUES(@task,N'[DEMO] Hoàn thành hoạt động mẫu',1,1,'PASS',N'[DEMO] Dữ liệu thực hành');
 UPDATE dbo.operational_tasks SET status='COMPLETED',progress_percent=100,completed_at=@e WHERE task_id=@task;
 UPDATE dbo.task_progress_versions SET workflow_status='CLOSED' WHERE task_id=@task;
 INSERT dbo.operational_task_assignments(task_id,assignee_id,assigned_by,assigned_at,assignment_status,ended_at,note) VALUES(@task,@lan,@lan,@s,'COMPLETED',@e,N'[DEMO] Phân công mẫu');
 INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,closed_by,closed_at,effective_started_at,effective_ended_at)
 VALUES(@d,@lan,@branch,'TASK',@task,@lan,@s,@lan,@e,@s,@e);
 SET @interval=SCOPE_IDENTITY();
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,request_id,user_agent,occurred_at)
 VALUES(@branch,NULL,'WORK_INTERVAL',CONVERT(varchar(50),@interval),'CREATE',CONCAT(N'{"demo":true,"taskId":',@task,N',"workdayId":',@d,N',"minutes":',DATEDIFF(minute,@s,@e),N'}'),'WORKTIME-DEMO-20261120',N'User-requested demonstration fixture, not actual work',@now);
 SET @n=@n+1;
END;
SELECT n,workday_id,CONVERT(varchar(30),start_at,126) startUtc,CONVERT(varchar(30),end_at,126) endUtc,title FROM @ranges;
ROLLBACK; -- dry run; installation wrapper replaces only this final marker.
