
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            DECLARE @code varchar(30)=@fixtureCode,@branch bigint=(SELECT branch_id FROM dbo.branches WHERE branch_code='BR001'),
            @actor bigint,@task bigint,@progress bigint,@floor bigint,@type bigint,@checklist bigint,@template bigint,@shift bigint,@day bigint,
            @dept bigint,@now datetime2(7)=SYSUTCDATETIME();
            SELECT @dept=department_id FROM dbo.departments WHERE branch_id=@branch AND department_code='HK';
            INSERT dbo.users(employee_code,username,email,full_name,primary_branch_id,department_id,employment_status,account_status,created_at)
            VALUES(@code,@code,@code+'@example.invalid',@code,@branch,@dept,'ACTIVE','ACTIVE',@now);
            SET @actor=SCOPE_IDENTITY();
            INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at) VALUES(@actor,@branch,'STANDARD',DATEADD(hour,-1,@now));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active) SELECT @actor,role_id,@branch,DATEADD(hour,-1,@now),1 FROM dbo.roles WHERE role_code='HK_STAFF';
            INSERT dbo.shift_templates(branch_id,shift_code,shift_name,start_time,end_time,break_minutes,crosses_midnight,is_active) VALUES(@branch,@code,@code,'00:00','23:59',0,0,1);
            SET @template=SCOPE_IDENTITY();
            INSERT dbo.work_shifts(branch_id,department_id,shift_template_id,shift_date,scheduled_start_at,scheduled_end_at,required_staff_count,status,created_by,created_at)
            VALUES(@branch,@dept,@template,CONVERT(date,@now),DATEADD(hour,-1,@now),DATEADD(hour,8,@now),1,'PLANNED',@actor,@now);
            SET @shift=SCOPE_IDENTITY();
            INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at) VALUES(@shift,@actor,'ASSIGNED',@actor,DATEADD(hour,-1,@now));
            INSERT dbo.inspection_checklists(checklist_code,checklist_name,is_active,created_by,created_at) VALUES(@code,@code,1,@actor,@now);
            SET @checklist=SCOPE_IDENTITY();
            INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES(@code,@code,1,@branch,25,@checklist);
            SET @type=SCOPE_IDENTITY();
            INSERT dbo.floors(branch_id,floor_code,floor_name,floor_number,is_active) VALUES(@branch,@code,@code,1,1);
            SET @floor=SCOPE_IDENTITY();
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active) VALUES(@branch,@floor,@type,@code,'DIRTY',1);
            DECLARE @room bigint=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,actual_start_at,created_by,created_at)
            SELECT @code,@room,reference_id,'IN_PROGRESS','ON_HOLD',DATEADD(minute,-20,@now),@actor,@now FROM dbo.reference_values WHERE group_code='CLEANING_TYPE' AND value_code='CHECKOUT';
            SET @task=SCOPE_IDENTITY();
            INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES(@task,@actor,@actor,DATEADD(hour,-1,@now),'ACTIVE');
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at)
            SELECT @task,50,'BLOCKED',reference_id,'FS014 synthetic blocker',@actor,@now FROM dbo.reference_values WHERE group_code='BLOCKER' AND value_code='GUEST_IN_ROOM';
            SET @progress=SCOPE_IDENTITY();
            INSERT dbo.fs014_cleaning_blocker_resolutions(progress_id) VALUES(@progress);
            INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES(@type,'FS014 item',1,0);
            INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES(@task);
            INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT @task,item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id=@type;
            INSERT dbo.workdays(user_id,branch_id,department_id,work_date,is_unscheduled,status,started_at)
            VALUES(@actor,@branch,@dept,CONVERT(date,@now),0,'WORKING',DATEADD(hour,-1,@now));
            SET @day=SCOPE_IDENTITY();
            INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at,closed_by,closed_at,effective_ended_at)
            VALUES(@day,@actor,@branch,'CLEANING',@task,@actor,DATEADD(minute,-20,@now),DATEADD(minute,-20,@now),@actor,DATEADD(minute,-10,@now),DATEADD(minute,-10,@now));
            COMMIT; SELECT @actor AS Actor,@branch AS Branch,@task AS TaskId,@progress AS ProgressId;
