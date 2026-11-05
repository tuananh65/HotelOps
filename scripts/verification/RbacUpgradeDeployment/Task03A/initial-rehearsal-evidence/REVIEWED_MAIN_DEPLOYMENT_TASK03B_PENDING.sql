/* REVIEWED CANDIDATE ONLY. Separate Task03B approval required for HotelOpsDB.
 Session gate is not an authorization credential. Do not bypass guards. No department/fixture cleanup.
 Audit tag: RBAC03A_20261021_24e14e196c6a */
IF DB_NAME()<>N'HotelOpsDB' OR DB_ID()<>22
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND CONVERT(varchar(23),create_date,121)=N'2026-10-18 02:41:14.627')
 THROW 53000,'Wrong reviewed server/database identity. Stop.',1;
IF CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval')) IS NULL
 OR CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval'))<>N'Task03B:RBAC03A_20261021_24e14e196c6a'
 THROW 53001,'Separate Task03B approval/session gate is not set. No upgrade permitted.',1;
IF COL_LENGTH('dbo.role_permissions','data_scope') IS NULL
 THROW 53002,'Reviewed target already had data_scope; missing column requires a new review.',1;
IF @@TRANCOUNT<>0 THROW 53004,'Existing transaction not allowed before deployment preflight.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[areas])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[areas] ORDER BY [area_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'581247F519A5FBB4888AD8ECCEE4D2E0045191CADEAE49365994BFE94509DB17' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[asset_condition_history])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[asset_condition_history] ORDER BY [condition_history_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'B963DD15B5F3BA97A8714F50ABD77F9D84A1F90DA14AC3FC4E6A576DDB14C695' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[asset_handovers])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[asset_handovers] ORDER BY [handover_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8EE500CEEC79721FEB79E870B2ADCCD79319B90D9EDC0A24CC98A4825D718260' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[asset_location_history])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[asset_location_history] ORDER BY [location_history_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'341994F04038286B8D8E10B0022732E6F4C351DAC5BBB80994E6BF7956BB8C4A' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[asset_types])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[asset_types] ORDER BY [asset_type_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'B62B9DF0FACB83B3FA156E9ED6CB19FFE16EE8D140A6DD3C3726B40AE537189B' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[assets])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[assets] ORDER BY [asset_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'95A57987FB9C17D667510286AFD6C8A75B4B4FAB1E426A8FB30DF3700117CE38' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[attachments])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[attachments] ORDER BY [attachment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'3D82AF9A334B05E2E7964AA5D3CF6F5A9830538917D3B551CF2211346F653081' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[audit_logs])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[audit_logs] ORDER BY [audit_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'93B7614882CBF6727CD4DEF1BF920BB43666D6E2083C10F26AAEE522EF59948C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[branches])<>2 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[branches] ORDER BY [branch_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'874190641D8BCD3D14FEBFFF3F6FC3EE0D389E260B6B1C6E962F484C12778BD2' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[cleaning_assignments])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[cleaning_assignments] ORDER BY [assignment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'D8BE6457699FB9F6194BE65CD1823AA3351F478B92CC5AC860674D372A8AE91C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[cleaning_progress_logs])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[cleaning_progress_logs] ORDER BY [progress_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8A8E3EDEDACA724F0F4C2CB58DBF7AFC9809154FDBBB8F1C2B0B7C3A8AC54B8E' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[cleaning_reworks])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[cleaning_reworks] ORDER BY [rework_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'4961A1DBF70903CF0395656086B94248D675D03ACD8F2115FB7C3228616488E7' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[cleaning_tasks])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[cleaning_tasks] ORDER BY [cleaning_task_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'D62DDD1CA50F4135B8ED4AA68A76D1AEE6706D57350DCBD34EF5F22F97F3DDAF' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[departments])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[departments] ORDER BY [department_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'D3B8357ECDC612513B03187E4754F39AB67BCE6FF933213F46DA4611338A8215' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[floors])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[floors] ORDER BY [floor_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'7114D9D9D02CCBE04A0348CEB1B6A86E807B875BAB28AC6C840B2D68E9869C58' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[incident_events])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[incident_events] ORDER BY [event_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'CE25C5C069DEE92B7589D84B94B8CFF2DF6A045A83CBCFDB6FFDB59286E1C0B8' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[incidents])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[incidents] ORDER BY [incident_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'A7EB25B0C2BCF6C2B28CB9B77892D296E42683B7708EFD8F070C8CF99F8B85D3' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inspection_checklist_items])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inspection_checklist_items] ORDER BY [item_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'0A5E4432DC148BDAC96D44FB58F5FA9B8037967DDEBB1543E7E25A9D5E84AAA4' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inspection_checklist_versions])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inspection_checklist_versions] ORDER BY [version_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'FAD591F85F27C053866605F445B60E02F270744E538FC2F1442BC6F65F87CAF8' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inspection_checklists])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inspection_checklists] ORDER BY [checklist_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'94EB0C78DE103AF6841F2C60F61E3257D910A6421C9C650ACBB2F3237ADB918A' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inspection_defects])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inspection_defects] ORDER BY [defect_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'EDD265D3C0C43002BF48BD9F14A387E2CA8F6BFAAF6670FCAE761C05AADFB46C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inspection_results])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inspection_results] ORDER BY [result_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'0273AC818994A37B461F485D2BFBC716A8DC9715C4180F5C1C1761F48383B8D1' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[internal_request_assignments])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[internal_request_assignments] ORDER BY [assignment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'72E4C3B70C3790C49FC594A2D3CD06D1D85AD5A520235A8331E3591471AB12C8' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[internal_request_events])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[internal_request_events] ORDER BY [event_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8742C1A65C96FE906F7A9F53F5F791E165E398FEDFECCD70A7174683B4506AD2' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[internal_request_feedback])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[internal_request_feedback] ORDER BY [feedback_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'CD0E644029123D859FFDA9936AB10AE9D9A1191584E280859A5EC7BF5D75E3AA' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[internal_request_types])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[internal_request_types] ORDER BY [request_type_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'9E1C37DA9F6A412B39E27E987593AC5F37B874B6A0C82BF58AAD58BA69712BF2' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[internal_requests])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[internal_requests] ORDER BY [internal_request_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8030BF07700368B9CF2E018078901E9B8336C352C5809E58DDAFF98EC5BC7275' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[inventory_items])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[inventory_items] ORDER BY [item_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'3EC32E1C20139394856DBC06E75A5F32E2AA4F43348642EDA925C82C52736DA7' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[item_categories])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[item_categories] ORDER BY [item_category_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'30CD7B8353F3EEA8CD495BB6B3A7E9653A6C0741C258229FC529B057A994309C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[maintenance_work_logs])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[maintenance_work_logs] ORDER BY [log_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'DE1B862C221D8BBC04172691ABE07822CA414B1FD3DF6989E9FA58ABA6EEBB3E' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[maintenance_works])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[maintenance_works] ORDER BY [maintenance_work_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'EB43BFCE561E9C6EA940A77184B0529F03A6219FCB6DC3F2AE057EC8D4C8EB74' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[material_fulfillment_lines])<>11 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[material_fulfillment_lines] ORDER BY [fulfillment_line_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'D8964FDB02547F7541857DFC46CABD144154F9A9B402F142DB533795268FB15E' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[material_fulfillments])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[material_fulfillments] ORDER BY [fulfillment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'5A02DFF2487B0F1F5377A25EE6097A058F71110B335651D5FD85218AADCE02C7' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[material_request_approvals])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[material_request_approvals] ORDER BY [approval_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'2453CF7F1DEBACC116DD4B7CF1C65ADC47CD8C0420D59D2E82E98F322495604F' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[material_request_lines])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[material_request_lines] ORDER BY [line_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'15774A5BFC22DBC7DC2A4B2312CADC71E819C76237D9E4A5A596ACEB81337EA3' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[material_requests])<>13 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[material_requests] ORDER BY [request_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'0EA3D340A5F3133497217CD08F96F1E569BC4B358F86E8EFE8DF533B2040366B' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[notification_recipients])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[notification_recipients] ORDER BY [recipient_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'7F9530A540FBB60543C5EB45CE0EA6505B1559899CAF9BCF5155C6B440E7B766' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[notifications])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[notifications] ORDER BY [notification_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'AF0C9F56C9FFD5411DBC5AD75797701936C7B75AF94D0CCB1D5C1918FB60CF3B' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[operational_task_assignments])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[operational_task_assignments] ORDER BY [assignment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'4D87B81EDA23D3E5F886AD442C1F900F3C7B7FBE98DDCEF8E4BC3BE2835E3BC2' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[operational_task_dependencies])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[operational_task_dependencies] ORDER BY [dependency_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'4EA4C42FF2DCE755543ECB54C1B02BF7872073159F8D2A220627F7D3F9994E88' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[operational_task_progress])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[operational_task_progress] ORDER BY [progress_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'419E2D42BC856FDF4C77C89CFE16D52207237AB05472232A47D0C65B3F500D86' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[operational_tasks])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[operational_tasks] ORDER BY [task_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'4B43846EF67781D01A1E89EAFBA7031C7C1007FB5DB91B19CD9259CD067F0B9D' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[permissions])<>47 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[permissions] ORDER BY [permission_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'B587BBBC41E5631608ACC9BD6F37F9BE83E85667729003ACE0C09492F9311360' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[preventive_maintenance_runs])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[preventive_maintenance_runs] ORDER BY [run_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'C23359AF098CAFDCAC9083860B168066D915C1FF04AA14A3E365BCC6FC197AAC' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[preventive_maintenance_schedules])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[preventive_maintenance_schedules] ORDER BY [schedule_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'466E57A3E0C481A5C469CCE212D7581393438351E0895B26B3210B8F0F1E6418' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[reference_values])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[reference_values] ORDER BY [reference_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'46B687179E95CBEE4D54F00E24F05BE1E17382E356A7CA301864B90D71136872' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[role_permissions])<>182 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[role_permissions] ORDER BY [role_permission_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8CDECEF2BE15E05397C2895B808BB17BBCC81BB7B6FFC90143A2697DE00CA34C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[roles])<>11 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[roles] ORDER BY [role_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'68FEE49DAD7ED1A99BAA0534887EA3D1137B1B078C25DCEF861F70D5009415D9' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[room_blocks])<>11 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[room_blocks] ORDER BY [block_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'B04A446DF67BA596D4F2F806FC0271CA9EE7E92534D03A84DF08FDDEEB41596C' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[room_inspections])<>13 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[room_inspections] ORDER BY [inspection_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'93AF5D2E5BBAE58A8A83624BFACC8271F8D1E20803977363085A1F3666BD0B38' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[room_status_history])<>31 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[room_status_history] ORDER BY [history_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'B8C5C45F6EB1A4EF5A17DBF0F435D8AC25EF1A83FC2676C65F9C442CFA52B514' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[room_types])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[room_types] ORDER BY [room_type_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'EAA21374EE29836514D2530A512B8156A32AEAC1E2DB02A5BC2DE789A4058CA8' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[rooms])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[rooms] ORDER BY [room_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'52ABEDF832B6FEFD02AECD534E947C2E6DEE43291F0D2B35B90A8483F5040573' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[shift_assignments])<>17 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[shift_assignments] ORDER BY [shift_assignment_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'2297B8E3BC77BB806E25393617DB974F21428263CA8E02953F5DBA9767BFFD82' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[shift_attendance])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[shift_attendance] ORDER BY [attendance_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'14ACBA0BB14F74F55CFC94974AC37CCA227E7B2EE342CD20E70EB991B08AE49D' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[shift_templates])<>11 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[shift_templates] ORDER BY [shift_template_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'3DA429248220FE4AE4E00FB45F4A7227BE45388EAD245077F7DB8630C008DB79' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stock_document_lines])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stock_document_lines] ORDER BY [line_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'C8ECDC809865F9E1509659BD6F36EE20E3FC8A3B0A74EE73AD9E75987A900990' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stock_documents])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stock_documents] ORDER BY [document_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'644E5DF9DD7D0A8DEBDA66C9344CE298085B77CE505EFC3483EB3793F402E900' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stock_movements])<>15 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stock_movements] ORDER BY [movement_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'35609540B1491FFC1E2019C22D43B55F2E88E365AA72B5132D881E92605D0A56' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stock_thresholds])<>14 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stock_thresholds] ORDER BY [threshold_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'2A45FBDA48EA8AD8653D32B48349F9D7C297127A2038F4207D1E32BE2C70C859' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stocktake_lines])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stocktake_lines] ORDER BY [stocktake_line_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'1E2E134044C44C6FCDA08DA05EB1AF2D770AA5CBF9BB676F54E3A6F92C43E886' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[stocktakes])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[stocktakes] ORDER BY [stocktake_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'05536FD87985BBF26276C6C133226517DEE0D4C19D409BF4A312267103C4CE54' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[system_settings])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[system_settings] ORDER BY [setting_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'778FCFF25B5233B639979F28D873AC3B76609EF71A6F6772DC76714A9F6EA437' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[units_of_measure])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[units_of_measure] ORDER BY [uom_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'E5C290518CAEA78B1A299ABAD1DA1A78BB0D6A071F8D7B2405F174A2C251A097' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[uom_conversions])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[uom_conversions] ORDER BY [conversion_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'4D6F3162556411AE26C8D1FBC7D26434D1826527512188CBE488A299E0E8431A' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[user_branch_access])<>28 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[user_branch_access] ORDER BY [access_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'181BDB61F4DD50C399AA57500C01019DA4408821B423003A4E40FA71305ABC6D' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[user_roles])<>30 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[user_roles] ORDER BY [user_role_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'C2CE3A85D53ECD51B40E23F48AFF20208399936BCFE2F61C730C97A5B0363771' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[user_sessions])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[user_sessions] ORDER BY [session_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'EB605F1F2F01B67CC905A0BBAA64E2ECBF90316629485A391F4C99CC1B3DDD5E' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[users])<>26 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[users] ORDER BY [user_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'3F9F62FADD0EA1B5A81D04ED6A5FD3D43D617B736233DF84BBA1DBBABE898455' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[warehouses])<>10 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[warehouses] ORDER BY [warehouse_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'E4FE54A44128249E731609592D980152126D0F14FD41E461F185A44DE698D4A6' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[work_categories])<>12 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[work_categories] ORDER BY [category_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'38DB6D6D7C6ADF46643AA516043CE519895874BA5EDEB0269B684DFA7BB9C81D' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;
IF (SELECT COUNT_BIG(*) FROM [dbo].[work_shifts])<>13 OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM [dbo].[work_shifts] ORDER BY [shift_id] FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>N'8F6FB639F7D92FA3CC52A03077F6646F0BC4976954172707CEEBFF6F4E95F930' THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;

/* HotelOps - Upgrade Authentication -> RBAC (2026-10-21)
  SHARED TEMPLATE - NOT EXECUTABLE UNTIL THE TARGET IS REVIEWED.
  Replace __REVIEWED_TARGET_DATABASE__ at exactly three executable locations:
    USE, pre-step DB_NAME guard, transactional DB_NAME guard.
  Start with a disposable AUTH-READY backup clone, not the original HotelOpsDB.
  The expected target is deliberately repeated to fail closed across GO batches.
  No hard-coded employee IDs; manual review of branch DH/OM and legacy depts required.
  This script DOES NOT create branches/users or drop business tables.
  Data scopes and role catalog are normalized. Adding data_scope is a separate additive step; catalog changes are transactional.
  Run only after SQL Server backup; test on an isolated copy; requires downtime.
  The original 001 Authentication schema update MUST have run already.
  Not runtime-tested on your instance: static validation only.
  This file is a template, not a one-click universal migration. Different user/role
  assignments may require reviewed @SelectedDh choices and a separate conflict resolution.
  To upgrade a real client/production DB, require a separate migration review.
*/
USE [HotelOpsDB];

GO
IF DB_NAME()<>N'HotelOpsDB' OR DB_ID()<>22
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND CONVERT(varchar(23),create_date,121)=N'2026-10-18 02:41:14.627')
 THROW 53000,'Wrong reviewed server/database identity. Stop.',1;
IF CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval')) IS NULL
 OR CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval'))<>N'Task03B:RBAC03A_20261021_24e14e196c6a'
 THROW 53001,'Separate Task03B approval/session gate is not set. No upgrade permitted.',1;
IF COL_LENGTH('dbo.role_permissions','data_scope') IS NULL
 THROW 53002,'Reviewed target already had data_scope; missing column requires a new review.',1;

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

GO
IF DB_NAME()<>N'HotelOpsDB' OR DB_ID()<>22
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND CONVERT(varchar(23),create_date,121)=N'2026-10-18 02:41:14.627')
 THROW 53000,'Wrong reviewed server/database identity. Stop.',1;
IF CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval')) IS NULL
 OR CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval'))<>N'Task03B:RBAC03A_20261021_24e14e196c6a'
 THROW 53001,'Separate Task03B approval/session gate is not set. No upgrade permitted.',1;
IF COL_LENGTH('dbo.role_permissions','data_scope') IS NULL
 THROW 53002,'Reviewed target already had data_scope; missing column requires a new review.',1;

/* Additive schema pre-step. SQL Server compiles column references per batch;
   scope column must exist before the next batch is compiled. On later errors,
   this column may remain even though the authorization-catalog transaction rolls back. */
IF DB_NAME() <> N'HotelOpsDB'
    THROW 51700, 'Stop: selected database differs from reviewed target.', 1;
IF OBJECT_ID(N'dbo.role_permissions', N'U') IS NULL
    THROW 51720, 'Missing role_permissions table.', 1;
IF COL_LENGTH(N'dbo.role_permissions', N'data_scope') IS NULL
BEGIN
    ALTER TABLE dbo.role_permissions ADD data_scope VARCHAR(20) NOT NULL
      CONSTRAINT DF_role_permissions_data_scope DEFAULT ('BRANCH') WITH VALUES;
END;

GO
IF DB_NAME()<>N'HotelOpsDB' OR DB_ID()<>22
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND CONVERT(varchar(23),create_date,121)=N'2026-10-18 02:41:14.627')
 THROW 53000,'Wrong reviewed server/database identity. Stop.',1;
IF CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval')) IS NULL
 OR CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval'))<>N'Task03B:RBAC03A_20261021_24e14e196c6a'
 THROW 53001,'Separate Task03B approval/session gate is not set. No upgrade permitted.',1;
IF COL_LENGTH('dbo.role_permissions','data_scope') IS NULL
 THROW 53002,'Reviewed target already had data_scope; missing column requires a new review.',1;

/* OPTIONAL human-reviewed DH selection if original DB has multiple DH per branch.
   Leave empty if there is exactly one active DH per branch.
   Examples for TEST SEED ONLY (do not paste as universal assignments):
     INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);
*/
BEGIN TRY
    IF DB_NAME()<>N'HotelOpsDB' THROW 51701, 'Safety guard: incorrect reviewed target database.', 1;
    IF @@TRANCOUNT<>0 THROW 51702, 'Execute outside an existing transaction.', 1;
    IF OBJECT_ID(N'dbo.users',N'U') IS NULL OR OBJECT_ID(N'dbo.user_roles',N'U') IS NULL
       OR OBJECT_ID(N'dbo.roles',N'U') IS NULL OR OBJECT_ID(N'dbo.permissions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.user_branch_access',N'U') IS NULL OR OBJECT_ID(N'dbo.role_permissions',N'U') IS NULL
        THROW 51703, 'Expected baseline RBAC/Auth tables are missing.', 1;
    IF COL_LENGTH(N'dbo.users',N'failed_login_count') IS NULL
       OR COL_LENGTH(N'dbo.users',N'locked_until') IS NULL
       OR COL_LENGTH(N'dbo.users',N'temporary_password_expires_at') IS NULL
       OR COL_LENGTH(N'dbo.user_sessions',N'session_key_hash') IS NULL
        THROW 51704, 'Authentication schema prerequisite missing. Apply approved Authentication update first.', 1;

    -- Choices are by real user ID, explicitly reviewed for EACH branch.
    DECLARE @SelectedDh TABLE(branch_id BIGINT PRIMARY KEY,user_id BIGINT NOT NULL);
 INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);
    -- Optional reviewed mappings, example for disposable sample only:
    -- INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);

    BEGIN TRANSACTION;

 DECLARE @DeploymentBefore TABLE(entity_type nvarchar(100) PRIMARY KEY,rows_json nvarchar(max));
 INSERT INTO @DeploymentBefore VALUES (N'roles',(SELECT role_id,role_code,role_name,description,is_system_role,is_active FROM dbo.roles ORDER BY role_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 INSERT INTO @DeploymentBefore VALUES (N'permissions',(SELECT permission_id,permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions ORDER BY permission_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 INSERT INTO @DeploymentBefore VALUES (N'role_permissions',(SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 INSERT INTO @DeploymentBefore VALUES (N'users',(SELECT user_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 INSERT INTO @DeploymentBefore VALUES (N'user_roles',(SELECT user_role_id,user_id,role_id,branch_id,is_active,assigned_at,expires_at FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 INSERT INTO @DeploymentBefore VALUES (N'user_branch_access',(SELECT access_id,user_id,branch_id,access_level,revoked_at FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH,INCLUDE_NULL_VALUES));
    -- Approved for this dataset only; shared Release remains unmodified.
    IF NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
      WHERE ur.user_id=19 AND ur.branch_id=2 AND ur.is_active=1 AND r.role_code='WAREHOUSE_KEEPER')
      THROW 53003,'Reviewed Storekeeper conflict no longer matches baseline.',1;
    UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
      WHERE ur.user_id=19 AND ur.branch_id=2 AND ur.is_active=1 AND r.role_code='WAREHOUSE_KEEPER';
    UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME()
      WHERE user_id=1 AND branch_id=2 AND access_level='MANAGER' AND revoked_at IS NULL;

    -- Rename role codes without changing IDs, so historic user_roles FKs survive.
    DECLARE @RoleAliases TABLE(old_code VARCHAR(60) PRIMARY KEY,new_code VARCHAR(60) UNIQUE);
    INSERT @RoleAliases VALUES
      ('GENERAL_MANAGER','OPERATIONS_MANAGER'),
      ('HK_MANAGER','HK_SUPERVISOR'),
      ('MAINT_MANAGER','MAINTENANCE_LEAD'),
      ('TECHNICIAN','MAINTENANCE_STAFF'),
      ('WAREHOUSE_KEEPER','STOREKEEPER');

    IF EXISTS (
        SELECT 1 FROM @RoleAliases a JOIN dbo.roles r_old ON r_old.role_code=a.old_code
        JOIN dbo.roles r_new ON r_new.role_code=a.new_code
    ) THROW 51705, 'Both old and new role codes are present; alias merge requires manual review.', 1;

    UPDATE r SET r.role_code=a.new_code
    FROM dbo.roles r JOIN @RoleAliases a ON a.old_code=r.role_code;

    IF OBJECT_ID(N'dbo.user_roles',N'U') IS NULL
       OR (SELECT COUNT(*) FROM dbo.roles WHERE role_code='HK_SUPERVISOR')<>1
       OR (SELECT COUNT(*) FROM dbo.roles WHERE role_code='HK_INSPECTOR')<>1
        THROW 51706, 'Legacy HK roles do not match expected baseline.', 1;

    DECLARE @SupervisorId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='HK_SUPERVISOR');
    DECLARE @InspectorId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='HK_INSPECTOR');
    -- Avoid unique index conflicts; reviewer must resolve duplicate role assignments first.
    IF EXISTS (
      SELECT 1 FROM dbo.user_roles oldrole JOIN dbo.user_roles newer
        ON newer.user_id=oldrole.user_id AND newer.role_id=@SupervisorId
           AND (newer.branch_id=oldrole.branch_id OR (newer.branch_id IS NULL AND oldrole.branch_id IS NULL))
      WHERE oldrole.role_id=@InspectorId AND oldrole.is_active=1
    ) THROW 51707, 'Some Inspector users already have Supervisor grants in that branch; review duplicates.', 1;

    INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_by,assigned_at,expires_at,is_active)
    SELECT user_id,@SupervisorId,branch_id,assigned_by,SYSUTCDATETIME(),expires_at,1
    FROM dbo.user_roles WHERE role_id=@InspectorId AND is_active=1;
    UPDATE dbo.user_roles SET is_active=0 WHERE role_id=@InspectorId AND is_active=1;

    -- Preflight all current Department Head duplicates. Explicit choices required.
    DECLARE @DepartmentHeadRoleId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='DEPT_MANAGER');
    IF @DepartmentHeadRoleId IS NULL THROW 51708, 'DEPT_MANAGER baseline role missing.', 1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id=@DepartmentHeadRoleId AND ur.is_active=1
       AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
       GROUP BY ur.branch_id HAVING COUNT(*)>1
         AND NOT EXISTS (SELECT 1 FROM @SelectedDh s WHERE s.branch_id=ur.branch_id)
    ) THROW 51709, 'Multiple DH per branch. Add reviewed branch/user choices in @SelectedDh; no automatic selection.', 1;

    IF EXISTS (
       SELECT 1 FROM @SelectedDh s WHERE NOT EXISTS (
          SELECT 1 FROM dbo.user_roles ur JOIN dbo.users u ON u.user_id=ur.user_id
          WHERE ur.role_id=@DepartmentHeadRoleId AND ur.user_id=s.user_id
            AND ur.branch_id=s.branch_id AND ur.is_active=1
            AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
            AND u.primary_branch_id=s.branch_id AND u.account_status='ACTIVE'
       )
    ) THROW 51710, 'Chosen DH must be active and assigned to the corresponding branch.', 1;

    UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN @SelectedDh s ON s.branch_id=ur.branch_id
    WHERE ur.role_id=@DepartmentHeadRoleId AND ur.is_active=1 AND ur.user_id<>s.user_id;

    -- DH has branch-level oversight; detach head identity from line department.
    UPDATE u SET department_id=NULL FROM dbo.users u JOIN @SelectedDh s ON s.user_id=u.user_id
    WHERE u.primary_branch_id=s.branch_id AND u.department_id IS NOT NULL;

    -- Do not permit active users to possess DH and Storekeeper roles at once.
    IF EXISTS (
       SELECT 1 FROM dbo.user_roles dh JOIN dbo.user_roles wh
        ON wh.user_id=dh.user_id AND wh.branch_id=dh.branch_id
       JOIN dbo.roles wr ON wr.role_id=wh.role_id
       WHERE dh.role_id=@DepartmentHeadRoleId AND dh.is_active=1 AND wh.is_active=1
         AND wr.role_code='STOREKEEPER'
         AND (dh.expires_at IS NULL OR dh.expires_at>SYSUTCDATETIME())
         AND (wh.expires_at IS NULL OR wh.expires_at>SYSUTCDATETIME())
    ) THROW 51711, 'User is both DH and Storekeeper. Disable warehouse assignment after review.', 1;

    -- One OM may manage only one branch. Do not infer or revoke the wrong branch.
    DECLARE @OperationsId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='OPERATIONS_MANAGER');
    IF @OperationsId IS NULL THROW 51712, 'Operations Manager role missing.', 1;
    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id=@OperationsId AND ur.is_active=1 AND ur.branch_id IS NULL
         AND NOT EXISTS (
            SELECT 1 FROM dbo.users u WHERE u.user_id=ur.user_id
            AND u.primary_branch_id IS NOT NULL
         )
    ) THROW 51713, 'OM role with NULL branch and unknown primary branch.', 1;
    UPDATE ur SET branch_id=u.primary_branch_id FROM dbo.user_roles ur
    JOIN dbo.users u ON ur.user_id=u.user_id
    WHERE ur.role_id=@OperationsId AND ur.branch_id IS NULL AND ur.is_active=1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur JOIN dbo.user_branch_access uba ON uba.user_id=ur.user_id
       WHERE ur.role_id=@OperationsId AND ur.is_active=1 AND uba.revoked_at IS NULL
         AND uba.branch_id<>ur.branch_id AND uba.access_level='MANAGER'
    ) THROW 51714, 'OM has MANAGER access in another branch. Revoke after explicit review.', 1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id IN (@OperationsId,@DepartmentHeadRoleId) AND ur.is_active=1
       GROUP BY ur.role_id,ur.branch_id HAVING COUNT(*)>1
    ) THROW 51715, 'More than one active OM or DH assignment in a branch.', 1;

    -- Existing departments outside HK, MT, WH, FO are NOT automatically removed.
    -- An account may still reference them; review + retire separately.

DECLARE @SeedRoles TABLE (role_code VARCHAR(60) NOT NULL PRIMARY KEY, role_name NVARCHAR(150) NOT NULL, description NVARCHAR(1000) NULL, is_system_role BIT NOT NULL, is_active BIT NOT NULL);

INSERT INTO @SeedRoles (role_code,role_name,description,is_system_role,is_active) VALUES
('SYS_ADMIN',N'Quản trị hệ thống',N'Quản trị user, role, danh mục, cấu hình và audit; không được mặc định vượt qua quyền tác nghiệp.',1,1),
('OPERATIONS_MANAGER',N'Quản lý vận hành',N'Quản lý vận hành một chi nhánh; xử lý escalation, tài sản, điều chỉnh hoặc đảo giao dịch kho theo phân nhiệm và audit.',1,1),
('HK_SUPERVISOR',N'Giám sát Buồng phòng',N'Phân công buồng phòng theo chi nhánh và ca; kiểm tra độc lập, không tự nghiệm thu công việc mình thực hiện.',0,1),
('HK_STAFF',N'Nhân viên buồng phòng',N'Dọn phòng, sửa lỗi và xử lý các nhiệm vụ được giao; quyền thao tác cá nhân theo OWN.',0,1),
('HK_INSPECTOR',N'Vai trò kiểm tra phòng cũ',N'Vai trò cũ ngừng hoạt động; người dùng được chuyển sang HK_SUPERVISOR có phân nhiệm độc lập.',0,0),
('MAINTENANCE_LEAD',N'Trưởng nhóm Bảo trì',N'Điều phối bảo trì theo ca, phân loại sự cố, nghiệm thu độc lập; không tự nghiệm thu phần đã thực hiện.',0,1),
('MAINTENANCE_STAFF',N'Nhân viên bảo trì',N'Thực hiện sửa chữa và nhật ký theo phân công; không có quyền tự nghiệm thu.',0,1),
('STOREKEEPER',N'Nhân viên kho',N'Ghi sổ phiếu nhập, xuất, trả, chuyển và kiểm kê theo phạm vi; không tự duyệt chênh lệch hoặc điều chỉnh/đảo kho.',0,1),
('DEPT_MANAGER',N'Department Head',N'Quản lý điều phối cả bốn bộ phận nghiệp vụ trong một cơ sở; ngang cấp Operations Manager, có quyền theo BRANCH hoặc OWN.',0,1),
('STAFF',N'Vai trò nhân viên cũ',N'Vai trò generic cũ ngừng hoạt động; không sử dụng cho tài khoản mới.',0,0),
('RECEPTION',N'Lễ tân',N'Ghi nhận check-in, check-out và đổi phòng; không trực tiếp ghi đè tình trạng phòng hoặc phát hành READY.',0,1),
('OWNER',N'Chủ sở hữu',N'Chỉ xem dashboard và báo cáo toàn hệ thống; không có quyền tác nghiệp.',1,1);

DECLARE @SeedPermissions TABLE (permission_code VARCHAR(100) NOT NULL PRIMARY KEY, permission_name NVARCHAR(150) NOT NULL, module_code VARCHAR(60) NOT NULL, resource_code VARCHAR(60) NOT NULL, action_code VARCHAR(40) NOT NULL, description NVARCHAR(MAX) NULL);

INSERT INTO @SeedPermissions (permission_code,permission_name,module_code,resource_code,action_code,description) VALUES
('USER_VIEW',N'Xem người dùng','ADMIN','user','view',N'Xem người dùng'),
('USER_MANAGE',N'Quản lý người dùng','ADMIN','user','manage',N'Quản lý người dùng'),
('ROLE_MANAGE',N'Quản lý vai trò và phân quyền','ADMIN','role','manage',N'Quản lý vai trò và phân quyền'),
('MASTER_DATA_MANAGE',N'Quản lý danh mục nền (chi nhánh, tầng, khu vực, phòng, bộ phận)','ADMIN','master_data','manage',N'Quản lý danh mục nền (chi nhánh, tầng, khu vực, phòng, bộ phận)'),
('SETTINGS_MANAGE',N'Quản lý cấu hình hệ thống','ADMIN','setting','manage',N'Quản lý cấu hình hệ thống'),
('AUDIT_VIEW',N'Xem nhật ký kiểm toán','ADMIN','audit_log','view',N'Xem nhật ký kiểm toán'),
('ROOM_VIEW',N'Xem phòng','ROOM','room','view',N'Xem phòng'),
('ROOM_STATUS_UPDATE',N'Điều chỉnh trạng thái phòng thủ công (ngoại lệ, sửa sai)','ROOM','room','update_status',N'Điều chỉnh trạng thái phòng thủ công (ngoại lệ, sửa sai)'),
('ROOM_CHECKOUT',N'Ghi nhận trả phòng (OCCUPIED → DIRTY)','ROOM','room','checkout',N'Lễ tân ghi nhận khách trả phòng; phòng chuyển OCCUPIED → DIRTY'),
('ROOM_BLOCK_MANAGE',N'Khóa / mở khóa phòng','ROOM','room_block','manage',N'Khóa / mở khóa phòng'),
('CLEANING_VIEW',N'Xem công việc dọn phòng','HOUSEKEEPING','cleaning_task','view',N'Xem công việc dọn phòng'),
('CLEANING_ASSIGN',N'Tạo, giao, giao lại, hủy công việc dọn phòng','HOUSEKEEPING','cleaning_task','assign',N'Tạo, giao, giao lại, hủy công việc dọn phòng'),
('CLEANING_EXECUTE',N'Thực hiện dọn phòng (bắt đầu, tiến độ, hoàn tất)','HOUSEKEEPING','cleaning_task','execute',N'Thực hiện dọn phòng (bắt đầu, tiến độ, hoàn tất)'),
('INSPECTION_VIEW',N'Xem kiểm tra phòng','INSPECTION','room_inspection','view',N'Xem kiểm tra phòng'),
('INSPECTION_PERFORM',N'Thực hiện kiểm tra phòng, tạo lỗi và yêu cầu làm lại','INSPECTION','room_inspection','perform',N'Thực hiện kiểm tra phòng, tạo lỗi và yêu cầu làm lại'),
('CHECKLIST_MANAGE',N'Quản lý checklist kiểm tra','INSPECTION','checklist','manage',N'Quản lý checklist kiểm tra'),
('DEFECT_RESOLVE',N'Xử lý lỗi kiểm tra / làm lại','INSPECTION','defect','resolve',N'Xử lý lỗi kiểm tra / làm lại'),
('INCIDENT_VIEW',N'Xem sự cố','INCIDENT','incident','view',N'Xem sự cố'),
('INCIDENT_CREATE',N'Báo cáo sự cố','INCIDENT','incident','create',N'Báo cáo sự cố'),
('INCIDENT_MANAGE',N'Phân công, đổi trạng thái, đóng sự cố','INCIDENT','incident','manage',N'Phân công, đổi trạng thái, đóng sự cố'),
('MAINTENANCE_VIEW',N'Xem công việc bảo trì','MAINTENANCE','maintenance_work','view',N'Xem công việc bảo trì'),
('MAINTENANCE_EXECUTE',N'Thực hiện bảo trì, ghi nhật ký','MAINTENANCE','maintenance_work','execute',N'Thực hiện bảo trì, ghi nhật ký'),
('MAINTENANCE_REVIEW',N'Nghiệm thu / đóng công việc bảo trì','MAINTENANCE','maintenance_work','review',N'Nghiệm thu / đóng công việc bảo trì'),
('PM_MANAGE',N'Quản lý bảo trì định kỳ','MAINTENANCE','pm_schedule','manage',N'Quản lý bảo trì định kỳ'),
('ASSET_VIEW',N'Xem tài sản','ASSET','asset','view',N'Xem tài sản'),
('ASSET_MANAGE',N'Quản lý tài sản (điều chuyển, bàn giao, tình trạng)','ASSET','asset','manage',N'Quản lý tài sản (điều chuyển, bàn giao, tình trạng)'),
('INVENTORY_VIEW',N'Xem tồn kho và phiếu kho','INVENTORY','stock','view',N'Xem tồn kho và phiếu kho'),
('INVENTORY_POST',N'Lập và ghi sổ phiếu kho','INVENTORY','stock_document','post',N'Lập và ghi sổ phiếu kho'),
('STOCK_APPROVE',N'Duyệt phiếu kho và kết quả kiểm kê','INVENTORY','stock_document','approve',N'Duyệt phiếu kho và kết quả kiểm kê'),
('STOCKTAKE_MANAGE',N'Thực hiện kiểm kê','INVENTORY','stocktake','manage',N'Thực hiện kiểm kê'),
('ITEM_MASTER_MANAGE',N'Quản lý danh mục vật tư, đơn vị, ngưỡng tồn','INVENTORY','item','manage',N'Quản lý danh mục vật tư, đơn vị, ngưỡng tồn'),
('MR_CREATE',N'Tạo yêu cầu vật tư','INVENTORY','material_request','create',N'Tạo yêu cầu vật tư'),
('MR_APPROVE_L1',N'Duyệt yêu cầu vật tư cấp 1','INVENTORY','material_request','approve_l1',N'Duyệt yêu cầu vật tư cấp 1'),
('MR_APPROVE_L2',N'Duyệt yêu cầu vật tư cấp 2','INVENTORY','material_request','approve_l2',N'Duyệt yêu cầu vật tư cấp 2'),
('MR_FULFILL',N'Cấp phát vật tư','INVENTORY','material_fulfillment','fulfill',N'Cấp phát vật tư'),
('IR_CREATE',N'Tạo yêu cầu nội bộ','REQUEST','internal_request','create',N'Tạo yêu cầu nội bộ'),
('IR_PROCESS',N'Tiếp nhận, giao và xử lý yêu cầu nội bộ','REQUEST','internal_request','process',N'Tiếp nhận, giao và xử lý yêu cầu nội bộ'),
('TASK_VIEW',N'Xem công việc vận hành','TASK','operational_task','view',N'Xem công việc vận hành'),
('TASK_MANAGE',N'Tạo, giao, hủy công việc vận hành','TASK','operational_task','manage',N'Tạo, giao, hủy công việc vận hành'),
('TASK_EXECUTE',N'Cập nhật tiến độ công việc được giao','TASK','operational_task','execute',N'Cập nhật tiến độ công việc được giao'),
('SHIFT_VIEW',N'Xem ca làm việc','SHIFT','work_shift','view',N'Xem ca làm việc'),
('SHIFT_MANAGE',N'Xếp ca, phân công ca, xác nhận chấm công','SHIFT','work_shift','manage',N'Xếp ca, phân công ca, xác nhận chấm công'),
('ATTENDANCE_RECORD',N'Chấm công vào/ra','SHIFT','attendance','record',N'Chấm công vào/ra'),
('DASHBOARD_VIEW',N'Xem dashboard','MONITOR','dashboard','view',N'Xem dashboard'),
('REPORT_VIEW',N'Xem báo cáo','MONITOR','report','view',N'Xem báo cáo'),
('ROOM_CHECKIN',N'Ghi nhận khách nhận phòng (AVAILABLE → OCCUPIED)','ROOM','room','checkin',N'Lễ tân ghi nhận khách nhận phòng; phòng chuyển AVAILABLE → OCCUPIED'),
('ROOM_RELEASE',N'Phát hành phòng sẵn sàng (READY → AVAILABLE)','ROOM','room','release',N'Lễ tân xác nhận phòng READY đủ điều kiện đưa vào sử dụng; chuyển READY → AVAILABLE'),
('ASSET_RETIRE',N'Thanh lý hoặc ngừng quản lý tài sản','ASSET','asset','retire',N'OM chỉ thanh lý khi kiểm tra đủ điều kiện và không còn phụ thuộc mở.'),
('ASSET_TRANSFER',N'Bàn giao và điều chuyển tài sản','ASSET','asset','transfer',N'OM điều chuyển tài sản trong phạm vi cho phép, ghi biên bản và audit.'),
('INCIDENT_ESCALATION_HANDLE',N'Xử lý sự cố được leo thang','INCIDENT','incident','handle_escalation',N'OM xử lý escalation trong chi nhánh, không tự xác minh sửa chữa.'),
('IR_OVERDUE_HANDLE',N'Xử lý yêu cầu nội bộ quá hạn','REQUEST','internal_request','handle_overdue',N'OM xử lý quá hạn trong chi nhánh theo trạng thái và quy tắc nghiệp vụ.'),
('MR_APPROVE_FALLBACK',N'Duyệt yêu cầu vật tư dự phòng','INVENTORY','material_request','approve_fallback',N'Chỉ OM được duyệt khi DH là requester; người duyệt phải khác requester.'),
('STOCK_ADJUST_POST',N'Đăng điều chỉnh tồn kho','INVENTORY','stock_adjustment','post',N'OM chỉ đăng điều chỉnh đã có đề nghị, đúng chi nhánh, khác người lập.'),
('STOCK_REVERSE',N'Đảo giao dịch kho đã đăng','INVENTORY','stock_transaction','reverse',N'Chỉ đảo chứng từ hợp lệ, đúng chi nhánh, có lý do và kiểm tra tồn.'),
('INCIDENT_TRIAGE',N'Phân loại và điều phối sự cố kỹ thuật','INCIDENT','incident','triage',N'Lead phân loại/điều phối sự cố kỹ thuật trong chi nhánh; sự cố nghiêm trọng chuyển OM.'),
('MAINTENANCE_ASSIGN',N'Phân công công việc bảo trì','MAINTENANCE','maintenance_work','assign',N'Lead phân công hoặc điều phối công việc MT đúng chi nhánh và ca; kiểm tra người được giao.'),
('STOCK_ISSUE_POST',N'Ghi sổ phiếu xuất kho','INVENTORY','stock_document','post_issue',N'Storekeeper ghi sổ xuất kho theo yêu cầu đã duyệt, trong giới hạn tồn.'),
('STOCK_RECEIPT_POST',N'Ghi sổ phiếu nhập kho','INVENTORY','stock_document','post_receipt',N'Storekeeper ghi sổ phiếu nhập hợp lệ trong kho thuộc chi nhánh được cấp.'),
('STOCK_RETURN_POST',N'Ghi sổ phiếu trả kho','INVENTORY','stock_document','post_return',N'Storekeeper ghi sổ phiếu trả kho hợp lệ theo chứng từ nguồn.'),
('STOCK_TRANSFER_POST',N'Ghi sổ phiếu chuyển kho','INVENTORY','stock_document','post_transfer',N'Storekeeper chuyển kho theo quy trình, không vượt phạm vi được phép.'),
('STOCKTAKE_PERFORM',N'Thực hiện kiểm kê kho','INVENTORY','stocktake','perform',N'Chuẩn bị, ghi số đếm và gửi kết quả kiểm kê; không tự duyệt chênh lệch.'),
('ROOM_OCCUPANCY_MOVE',N'Ghi nhận chuyển phòng','ROOM','room_occupancy','move',N'Reception ghi nhận chuyển occupancy từ phòng nguồn sang phòng đích trong cùng chi nhánh; không ghi đè room readiness. Backend phải kiểm tra điều kiện và thực hiện giao dịch nguyên tử.');

DECLARE @SeedGrants TABLE (role_code VARCHAR(60) NOT NULL,permission_code VARCHAR(100) NOT NULL,data_scope VARCHAR(20) NOT NULL,PRIMARY KEY(role_code,permission_code));

INSERT INTO @SeedGrants (role_code,permission_code,data_scope) VALUES
('SYS_ADMIN','USER_VIEW','ALL'),
('SYS_ADMIN','USER_MANAGE','ALL'),
('SYS_ADMIN','ROLE_MANAGE','ALL'),
('SYS_ADMIN','MASTER_DATA_MANAGE','ALL'),
('SYS_ADMIN','SETTINGS_MANAGE','ALL'),
('SYS_ADMIN','AUDIT_VIEW','ALL'),
('OPERATIONS_MANAGER','ROOM_VIEW','BRANCH'),
('OPERATIONS_MANAGER','CLEANING_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INSPECTION_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INCIDENT_VIEW','BRANCH'),
('OPERATIONS_MANAGER','MAINTENANCE_VIEW','BRANCH'),
('OPERATIONS_MANAGER','ASSET_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INVENTORY_VIEW','BRANCH'),
('OPERATIONS_MANAGER','IR_CREATE','BRANCH'),
('OPERATIONS_MANAGER','TASK_VIEW','BRANCH'),
('OPERATIONS_MANAGER','SHIFT_VIEW','BRANCH'),
('OPERATIONS_MANAGER','DASHBOARD_VIEW','BRANCH'),
('OPERATIONS_MANAGER','REPORT_VIEW','BRANCH'),
('HK_SUPERVISOR','ROOM_VIEW','BRANCH'),
('HK_SUPERVISOR','CLEANING_VIEW','BRANCH'),
('HK_SUPERVISOR','CLEANING_ASSIGN','BRANCH'),
('HK_SUPERVISOR','INSPECTION_VIEW','BRANCH'),
('HK_SUPERVISOR','INSPECTION_PERFORM','BRANCH'),
('HK_SUPERVISOR','INCIDENT_VIEW','BRANCH'),
('HK_SUPERVISOR','INCIDENT_CREATE','BRANCH'),
('HK_SUPERVISOR','MR_CREATE','OWN'),
('HK_SUPERVISOR','IR_CREATE','OWN'),
('HK_SUPERVISOR','TASK_VIEW','BRANCH'),
('HK_SUPERVISOR','TASK_EXECUTE','OWN'),
('HK_SUPERVISOR','SHIFT_VIEW','BRANCH'),
('HK_SUPERVISOR','ATTENDANCE_RECORD','OWN'),
('HK_STAFF','ROOM_VIEW','BRANCH'),
('HK_STAFF','CLEANING_VIEW','OWN'),
('HK_STAFF','CLEANING_EXECUTE','OWN'),
('HK_STAFF','DEFECT_RESOLVE','OWN'),
('HK_STAFF','INCIDENT_VIEW','OWN'),
('HK_STAFF','INCIDENT_CREATE','BRANCH'),
('HK_STAFF','MR_CREATE','OWN'),
('HK_STAFF','IR_CREATE','OWN'),
('HK_STAFF','IR_PROCESS','OWN'),
('HK_STAFF','TASK_VIEW','OWN'),
('HK_STAFF','TASK_EXECUTE','OWN'),
('HK_STAFF','SHIFT_VIEW','OWN'),
('HK_STAFF','ATTENDANCE_RECORD','OWN'),
('MAINTENANCE_LEAD','ROOM_VIEW','BRANCH'),
('MAINTENANCE_LEAD','ROOM_BLOCK_MANAGE','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_VIEW','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_CREATE','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_VIEW','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_EXECUTE','OWN'),
('MAINTENANCE_LEAD','MAINTENANCE_REVIEW','BRANCH'),
('MAINTENANCE_LEAD','PM_MANAGE','BRANCH'),
('MAINTENANCE_LEAD','ASSET_VIEW','BRANCH'),
('MAINTENANCE_LEAD','MR_CREATE','OWN'),
('MAINTENANCE_LEAD','IR_CREATE','OWN'),
('MAINTENANCE_LEAD','IR_PROCESS','BRANCH'),
('MAINTENANCE_LEAD','TASK_VIEW','BRANCH'),
('MAINTENANCE_LEAD','TASK_EXECUTE','OWN'),
('MAINTENANCE_LEAD','SHIFT_VIEW','BRANCH'),
('MAINTENANCE_LEAD','ATTENDANCE_RECORD','OWN'),
('MAINTENANCE_STAFF','ROOM_VIEW','BRANCH'),
('MAINTENANCE_STAFF','INCIDENT_VIEW','BRANCH'),
('MAINTENANCE_STAFF','INCIDENT_CREATE','BRANCH'),
('MAINTENANCE_STAFF','MAINTENANCE_VIEW','OWN'),
('MAINTENANCE_STAFF','MAINTENANCE_EXECUTE','OWN'),
('MAINTENANCE_STAFF','ASSET_VIEW','BRANCH'),
('MAINTENANCE_STAFF','MR_CREATE','OWN'),
('MAINTENANCE_STAFF','IR_CREATE','OWN'),
('MAINTENANCE_STAFF','IR_PROCESS','OWN'),
('MAINTENANCE_STAFF','TASK_VIEW','OWN'),
('MAINTENANCE_STAFF','TASK_EXECUTE','OWN'),
('MAINTENANCE_STAFF','SHIFT_VIEW','OWN'),
('MAINTENANCE_STAFF','ATTENDANCE_RECORD','OWN'),
('STOREKEEPER','ASSET_VIEW','BRANCH'),
('STOREKEEPER','INVENTORY_VIEW','BRANCH'),
('STOREKEEPER','MR_CREATE','OWN'),
('STOREKEEPER','MR_FULFILL','BRANCH'),
('STOREKEEPER','IR_CREATE','OWN'),
('STOREKEEPER','IR_PROCESS','OWN'),
('STOREKEEPER','TASK_VIEW','OWN'),
('STOREKEEPER','TASK_EXECUTE','OWN'),
('STOREKEEPER','SHIFT_VIEW','OWN'),
('STOREKEEPER','ATTENDANCE_RECORD','OWN'),
('DEPT_MANAGER','USER_VIEW','BRANCH'),
('DEPT_MANAGER','ROOM_VIEW','BRANCH'),
('DEPT_MANAGER','INCIDENT_VIEW','BRANCH'),
('DEPT_MANAGER','INCIDENT_CREATE','BRANCH'),
('DEPT_MANAGER','ASSET_VIEW','BRANCH'),
('DEPT_MANAGER','INVENTORY_VIEW','BRANCH'),
('DEPT_MANAGER','MR_CREATE','BRANCH'),
('DEPT_MANAGER','MR_APPROVE_L1','BRANCH'),
('DEPT_MANAGER','IR_CREATE','BRANCH'),
('DEPT_MANAGER','IR_PROCESS','BRANCH'),
('DEPT_MANAGER','TASK_VIEW','BRANCH'),
('DEPT_MANAGER','TASK_MANAGE','BRANCH'),
('DEPT_MANAGER','TASK_EXECUTE','OWN'),
('DEPT_MANAGER','SHIFT_VIEW','BRANCH'),
('DEPT_MANAGER','SHIFT_MANAGE','BRANCH'),
('DEPT_MANAGER','ATTENDANCE_RECORD','OWN'),
('DEPT_MANAGER','DASHBOARD_VIEW','BRANCH'),
('DEPT_MANAGER','REPORT_VIEW','BRANCH'),
('STAFF','ROOM_VIEW','BRANCH'),
('STAFF','INCIDENT_VIEW','OWN'),
('STAFF','INCIDENT_CREATE','BRANCH'),
('STAFF','MR_CREATE','OWN'),
('STAFF','IR_CREATE','OWN'),
('STAFF','IR_PROCESS','OWN'),
('STAFF','TASK_VIEW','OWN'),
('STAFF','TASK_EXECUTE','OWN'),
('STAFF','SHIFT_VIEW','OWN'),
('STAFF','ATTENDANCE_RECORD','OWN'),
('RECEPTION','ROOM_VIEW','BRANCH'),
('RECEPTION','ROOM_CHECKOUT','BRANCH'),
('RECEPTION','CLEANING_VIEW','BRANCH'),
('RECEPTION','INCIDENT_VIEW','BRANCH'),
('RECEPTION','INCIDENT_CREATE','BRANCH'),
('RECEPTION','IR_CREATE','OWN'),
('RECEPTION','TASK_VIEW','OWN'),
('RECEPTION','TASK_EXECUTE','OWN'),
('RECEPTION','SHIFT_VIEW','OWN'),
('RECEPTION','ATTENDANCE_RECORD','OWN'),
('RECEPTION','ROOM_CHECKIN','BRANCH'),
('OPERATIONS_MANAGER','ASSET_MANAGE','BRANCH'),
('OPERATIONS_MANAGER','ASSET_RETIRE','BRANCH'),
('OPERATIONS_MANAGER','ASSET_TRANSFER','BRANCH'),
('OPERATIONS_MANAGER','INCIDENT_ESCALATION_HANDLE','BRANCH'),
('OPERATIONS_MANAGER','IR_OVERDUE_HANDLE','BRANCH'),
('OPERATIONS_MANAGER','MR_APPROVE_FALLBACK','BRANCH'),
('OPERATIONS_MANAGER','STOCK_ADJUST_POST','BRANCH'),
('OPERATIONS_MANAGER','STOCK_REVERSE','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_TRIAGE','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_ASSIGN','BRANCH'),
('STOREKEEPER','STOCK_ISSUE_POST','BRANCH'),
('STOREKEEPER','STOCK_RECEIPT_POST','BRANCH'),
('STOREKEEPER','STOCK_RETURN_POST','BRANCH'),
('STOREKEEPER','STOCK_TRANSFER_POST','BRANCH'),
('STOREKEEPER','STOCKTAKE_PERFORM','BRANCH'),
('RECEPTION','ROOM_OCCUPANCY_MOVE','BRANCH'),
('OWNER','DASHBOARD_VIEW','ALL'),
('OWNER','REPORT_VIEW','ALL');

    IF (SELECT COUNT(*) FROM @SeedRoles)<>12 OR (SELECT COUNT(*) FROM @SeedRoles WHERE is_active=1)<>10
       OR (SELECT COUNT(*) FROM @SeedPermissions)<>62 OR (SELECT COUNT(*) FROM @SeedGrants)<>140
        THROW 51620, 'Catalog generator count mismatch.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.permissions p JOIN @SeedPermissions d ON d.permission_code=p.permission_code
        WHERE p.module_code<>d.module_code OR p.resource_code<>d.resource_code OR p.action_code<>d.action_code
    ) THROW 51621, 'Existing permission code conflicts with resource/action; manual review required.', 1;

    -- Existing unknown roles are not silently deactivated.
    IF EXISTS (SELECT 1 FROM dbo.roles r LEFT JOIN @SeedRoles d ON d.role_code=r.role_code
               WHERE d.role_code IS NULL AND r.is_active=1)
        THROW 51622, 'Unknown active role. Stop and review before replacing grants.', 1;

    UPDATE r SET r.role_name=d.role_name, r.description=d.description,
                 r.is_system_role=d.is_system_role, r.is_active=d.is_active
    FROM dbo.roles r JOIN @SeedRoles d ON d.role_code=r.role_code;

    INSERT INTO dbo.roles (role_code,role_name,description,is_system_role,is_active,created_at)
    SELECT d.role_code,d.role_name,d.description,d.is_system_role,d.is_active,SYSUTCDATETIME()
    FROM @SeedRoles d WHERE NOT EXISTS (SELECT 1 FROM dbo.roles r WHERE r.role_code=d.role_code);

    UPDATE p SET p.permission_name=d.permission_name, p.description=d.description
    FROM dbo.permissions p JOIN @SeedPermissions d ON d.permission_code=p.permission_code;

    INSERT INTO dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
    SELECT d.permission_code,d.permission_name,d.module_code,d.resource_code,d.action_code,d.description
    FROM @SeedPermissions d
    WHERE NOT EXISTS (SELECT 1 FROM dbo.permissions p WHERE p.permission_code=d.permission_code);

    -- The snapshot is the source of truth for catalog grants of these 12 roles.
    -- Removing a grant is deliberate. This does NOT delete business records.
    DELETE rp FROM dbo.role_permissions rp
    JOIN dbo.roles r ON r.role_id=rp.role_id
    JOIN @SeedRoles sr ON sr.role_code=r.role_code
    JOIN dbo.permissions p ON p.permission_id=rp.permission_id
    WHERE NOT EXISTS (
       SELECT 1 FROM @SeedGrants g
       WHERE g.role_code=r.role_code AND g.permission_code=p.permission_code
    );

    UPDATE rp SET rp.data_scope=g.data_scope
    FROM dbo.role_permissions rp
    JOIN dbo.roles r ON r.role_id=rp.role_id
    JOIN dbo.permissions p ON p.permission_id=rp.permission_id
    JOIN @SeedGrants g ON g.role_code=r.role_code AND g.permission_code=p.permission_code
    WHERE rp.data_scope<>g.data_scope;

    INSERT INTO dbo.role_permissions (role_id,permission_id,data_scope,granted_at,granted_by)
    SELECT r.role_id,p.permission_id,g.data_scope,SYSUTCDATETIME(),NULL
    FROM @SeedGrants g
    JOIN dbo.roles r ON r.role_code=g.role_code
    JOIN dbo.permissions p ON p.permission_code=g.permission_code
    WHERE NOT EXISTS (SELECT 1 FROM dbo.role_permissions rp
                      WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);

    IF (SELECT COUNT(*) FROM dbo.roles WHERE is_active=1)<>10
       OR (SELECT COUNT(*) FROM dbo.roles WHERE is_active=0)<>2
       OR (SELECT COUNT(*) FROM dbo.role_permissions rp
           JOIN dbo.roles r ON r.role_id=rp.role_id
           JOIN @SeedRoles sr ON sr.role_code=r.role_code)<>140
       OR EXISTS (
         SELECT r.role_code,p.permission_code,rp.data_scope
         FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
              JOIN dbo.permissions p ON p.permission_id=rp.permission_id
              JOIN @SeedRoles sr ON sr.role_code=r.role_code
         EXCEPT SELECT role_code,permission_code,data_scope FROM @SeedGrants
       ) OR EXISTS (
         SELECT role_code,permission_code,data_scope FROM @SeedGrants
         EXCEPT
         SELECT r.role_code,p.permission_code,rp.data_scope
         FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
              JOIN dbo.permissions p ON p.permission_id=rp.permission_id
              JOIN @SeedRoles sr ON sr.role_code=r.role_code
       )
        THROW 51623, 'Catalog postconditions failed; transaction rolled back.', 1;

    IF (SELECT COUNT(*) FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
        WHERE r.role_code='OWNER')<>2
       OR EXISTS (SELECT 1 FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
                  WHERE r.role_code='OWNER' AND rp.data_scope<>'ALL')
        THROW 51624, 'OWNER must have only two ALL grants.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.role_permissions') AND name=N'CK_role_permissions_data_scope')
    BEGIN
        ALTER TABLE dbo.role_permissions WITH CHECK ADD CONSTRAINT CK_role_permissions_data_scope
        CHECK (data_scope IN ('ALL','BRANCH','DEPARTMENT','OWN'));
    END;

    -- Ensure old legacy roles cannot accidentally grant authorization via backend.
    IF EXISTS (SELECT 1 FROM dbo.roles WHERE role_code IN ('GENERAL_MANAGER','HK_MANAGER','MAINT_MANAGER','TECHNICIAN','WAREHOUSE_KEEPER') AND is_active=1)
        THROW 51717, 'One or more legacy role aliases remained enabled.', 1;


 -- Actual migration-level before/after audit, no staff impersonation or credential payload.
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'roles',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT role_id,role_code,role_name,description,is_system_role,is_active FROM dbo.roles ORDER BY role_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["role_code","role_name","description","is_system_role","is_active"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'roles' AND b.rows_json<>(SELECT role_id,role_code,role_name,description,is_system_role,is_active FROM dbo.roles ORDER BY role_id FOR JSON PATH,INCLUDE_NULL_VALUES);
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'permissions',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT permission_id,permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions ORDER BY permission_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["permission_code","permission_name","description","module_code","resource_code","action_code"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'permissions' AND b.rows_json<>(SELECT permission_id,permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions ORDER BY permission_id FOR JSON PATH,INCLUDE_NULL_VALUES);
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'role_permissions',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["role_id","permission_id","data_scope"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'role_permissions' AND b.rows_json<>(SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH,INCLUDE_NULL_VALUES);
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'users',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT user_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["department_id"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'users' AND b.rows_json<>(SELECT user_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES);
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'user_roles',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT user_role_id,user_id,role_id,branch_id,is_active,assigned_at,expires_at FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["role_id","branch_id","is_active","assigned_at"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'user_roles' AND b.rows_json<>(SELECT user_role_id,user_id,role_id,branch_id,is_active,assigned_at,expires_at FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES);
 INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 SELECT NULL,N'user_branch_access',N'deployment:RBAC03A_20261021_24e14e196c6a','UPDATE',
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT N'RBAC03A_20261021_24e14e196c6a' deployment_id,JSON_QUERY((SELECT access_id,user_id,branch_id,access_level,revoked_at FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH,INCLUDE_NULL_VALUES)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["revoked_at"]',SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type=N'user_branch_access' AND b.rows_json<>(SELECT access_id,user_id,branch_id,access_level,revoked_at FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH,INCLUDE_NULL_VALUES);
    COMMIT TRANSACTION;
    PRINT N'RBAC UPGRADE COMMITTED. REVIEW DH/OM BRANCH, retired departments and sample accounts before API testing.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

GO

