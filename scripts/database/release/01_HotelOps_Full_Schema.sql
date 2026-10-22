/*
 HotelOps - Full Schema Installer (RELEASE CANDIDATE / SCHEMA ONLY)
 Source: HotelOps_RBAC_Final_Schema.sql, generated Oct 21, 2026
 Target: [HotelOpsDB] on Microsoft SQL Server 2022+

 SAFE GUARDS:
 - Only for a NEW database: fails if [HotelOpsDB] already exists.
 - No machine-specific MDF/LDF file paths.
 - Includes schema, keys, indexes, defaults, check constraints, extended properties.
 - RBAC catalog is delivered separately in 03_HotelOps_RBAC_Catalog_Seed.sql.
 - Follow with 03_HotelOps_RBAC_Catalog_Seed.sql and 04_HotelOps_Verification_ReadOnly.sql.
 - SQL Server runtime validation is still required on a disposable instance.
 - Uses NOEXEC preflight guards for a pre-existing/mis-selected database.
*/
USE [master];
GO
IF DB_ID(N'HotelOpsDB') IS NOT NULL
BEGIN
    PRINT 'ABORT: HotelOpsDB already exists. Nothing should be installed.';
    SET NOEXEC ON;
END;
GO
CREATE DATABASE [HotelOpsDB];
GO
USE [HotelOpsDB];
GO
IF DB_NAME() <> N'HotelOpsDB'
BEGIN
    PRINT 'ABORT: Target database could not be selected.';
    SET NOEXEC ON;
END;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
/****** Object:  Table [dbo].[areas]    Script Date: 10/21/2026 2:27:26 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[areas](
	[area_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[floor_id] [bigint] NULL,
	[area_code] [varchar](30) NOT NULL,
	[area_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[area_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[asset_condition_history]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[asset_condition_history](
	[condition_history_id] [bigint] IDENTITY(1,1) NOT NULL,
	[asset_id] [bigint] NOT NULL,
	[old_condition] [varchar](40) NULL,
	[new_condition] [varchar](40) NOT NULL,
	[note] [nvarchar](max) NULL,
	[related_incident_id] [bigint] NULL,
	[changed_by] [bigint] NOT NULL,
	[changed_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[condition_history_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[asset_handovers]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[asset_handovers](
	[handover_id] [bigint] IDENTITY(1,1) NOT NULL,
	[handover_no] [varchar](40) NOT NULL,
	[asset_id] [bigint] NOT NULL,
	[from_user_id] [bigint] NULL,
	[to_user_id] [bigint] NULL,
	[from_department_id] [bigint] NULL,
	[to_department_id] [bigint] NULL,
	[condition_note] [nvarchar](max) NULL,
	[handover_status] [varchar](30) NOT NULL,
	[handed_over_at] [datetime2](7) NULL,
	[accepted_at] [datetime2](7) NULL,
	[created_by] [bigint] NOT NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[handover_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[handover_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[asset_location_history]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[asset_location_history](
	[location_history_id] [bigint] IDENTITY(1,1) NOT NULL,
	[asset_id] [bigint] NOT NULL,
	[from_location_type] [varchar](30) NULL,
	[from_location_id] [bigint] NULL,
	[to_location_type] [varchar](30) NOT NULL,
	[to_location_id] [bigint] NOT NULL,
	[effective_at] [datetime2](7) NOT NULL,
	[moved_by] [bigint] NOT NULL,
	[reason] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[location_history_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[asset_types]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[asset_types](
	[asset_type_id] [bigint] IDENTITY(1,1) NOT NULL,
	[type_code] [varchar](40) NOT NULL,
	[type_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[asset_type_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[type_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[assets]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[assets](
	[asset_id] [bigint] IDENTITY(1,1) NOT NULL,
	[asset_code] [varchar](50) NOT NULL,
	[asset_name] [nvarchar](200) NOT NULL,
	[asset_type_id] [bigint] NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[serial_number] [varchar](100) NULL,
	[brand] [nvarchar](100) NULL,
	[model] [nvarchar](100) NULL,
	[purchase_date] [date] NULL,
	[warranty_expiry_date] [date] NULL,
	[acquisition_cost] [decimal](18, 2) NULL,
	[condition_status] [varchar](40) NOT NULL,
	[lifecycle_status] [varchar](40) NOT NULL,
	[current_room_id] [bigint] NULL,
	[current_area_id] [bigint] NULL,
	[current_department_id] [bigint] NULL,
	[custodian_user_id] [bigint] NULL,
	[description] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[asset_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[asset_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[attachments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[attachments](
	[attachment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[entity_type] [varchar](60) NOT NULL,
	[entity_id] [bigint] NOT NULL,
	[file_name] [nvarchar](255) NOT NULL,
	[original_file_name] [nvarchar](255) NULL,
	[mime_type] [varchar](100) NULL,
	[file_size_bytes] [bigint] NULL,
	[storage_key] [nvarchar](500) NOT NULL,
	[file_url] [nvarchar](max) NULL,
	[description] [nvarchar](max) NULL,
	[uploaded_by] [bigint] NOT NULL,
	[uploaded_at] [datetime2](7) NOT NULL,
	[deleted_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[attachment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[audit_logs]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[audit_logs](
	[audit_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NULL,
	[actor_user_id] [bigint] NULL,
	[entity_type] [varchar](100) NOT NULL,
	[entity_id] [varchar](100) NOT NULL,
	[action] [varchar](40) NOT NULL,
	[old_values] [nvarchar](max) NULL,
	[new_values] [nvarchar](max) NULL,
	[changed_fields] [nvarchar](max) NULL,
	[ip_address] [varchar](45) NULL,
	[request_id] [varchar](100) NULL,
	[user_agent] [nvarchar](max) NULL,
	[occurred_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[audit_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[branches]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[branches](
	[branch_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_code] [varchar](30) NOT NULL,
	[branch_name] [nvarchar](200) NOT NULL,
	[address] [nvarchar](max) NULL,
	[phone] [varchar](20) NULL,
	[email] [varchar](150) NULL,
	[timezone] [varchar](60) NULL,
	[is_active] [bit] NOT NULL,
	[created_at] [datetime2](7) NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[branch_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[branch_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cleaning_assignments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cleaning_assignments](
	[assignment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[cleaning_task_id] [bigint] NOT NULL,
	[assignee_user_id] [bigint] NOT NULL,
	[assigned_by] [bigint] NOT NULL,
	[assigned_at] [datetime2](7) NOT NULL,
	[ended_at] [datetime2](7) NULL,
	[assignment_status] [varchar](30) NOT NULL,
	[reason] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[assignment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cleaning_progress_logs]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cleaning_progress_logs](
	[progress_id] [bigint] IDENTITY(1,1) NOT NULL,
	[cleaning_task_id] [bigint] NOT NULL,
	[progress_percent] [int] NOT NULL,
	[status] [varchar](30) NULL,
	[blocker_type_id] [bigint] NULL,
	[note] [nvarchar](max) NULL,
	[logged_by] [bigint] NOT NULL,
	[logged_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[progress_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cleaning_reworks]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cleaning_reworks](
	[rework_id] [bigint] IDENTITY(1,1) NOT NULL,
	[cleaning_task_id] [bigint] NOT NULL,
	[inspection_id] [bigint] NOT NULL,
	[assignee_user_id] [bigint] NULL,
	[reason] [nvarchar](max) NOT NULL,
	[status] [varchar](30) NOT NULL,
	[requested_at] [datetime2](7) NOT NULL,
	[completed_at] [datetime2](7) NULL,
	[note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[rework_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cleaning_tasks]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cleaning_tasks](
	[cleaning_task_id] [bigint] IDENTITY(1,1) NOT NULL,
	[task_no] [varchar](40) NOT NULL,
	[room_id] [bigint] NOT NULL,
	[cleaning_type_id] [bigint] NOT NULL,
	[priority_id] [bigint] NULL,
	[instruction] [nvarchar](max) NULL,
	[scheduled_start_at] [datetime2](7) NULL,
	[due_at] [datetime2](7) NULL,
	[actual_start_at] [datetime2](7) NULL,
	[actual_end_at] [datetime2](7) NULL,
	[status] [varchar](40) NOT NULL,
	[completion_note] [nvarchar](max) NULL,
	[created_by] [bigint] NOT NULL,
	[created_at] [datetime2](7) NOT NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[cleaning_task_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[task_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[departments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[departments](
	[department_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[department_code] [varchar](30) NOT NULL,
	[department_name] [nvarchar](150) NOT NULL,
	[manager_user_id] [bigint] NULL,
	[scope_description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[department_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[floors]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[floors](
	[floor_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[floor_code] [varchar](30) NOT NULL,
	[floor_name] [nvarchar](100) NOT NULL,
	[floor_number] [int] NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[floor_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[incident_events]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[incident_events](
	[event_id] [bigint] IDENTITY(1,1) NOT NULL,
	[incident_id] [bigint] NOT NULL,
	[event_type] [varchar](50) NOT NULL,
	[from_status] [varchar](40) NULL,
	[to_status] [varchar](40) NULL,
	[note] [nvarchar](max) NULL,
	[actor_id] [bigint] NOT NULL,
	[occurred_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[event_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[incidents]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[incidents](
	[incident_id] [bigint] IDENTITY(1,1) NOT NULL,
	[incident_no] [varchar](40) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[room_id] [bigint] NULL,
	[area_id] [bigint] NULL,
	[asset_id] [bigint] NULL,
	[incident_title] [nvarchar](200) NOT NULL,
	[description] [nvarchar](max) NOT NULL,
	[severity_id] [bigint] NULL,
	[priority_id] [bigint] NULL,
	[status] [varchar](40) NOT NULL,
	[reported_by] [bigint] NOT NULL,
	[discovered_at] [datetime2](7) NOT NULL,
	[reported_at] [datetime2](7) NOT NULL,
	[resolved_at] [datetime2](7) NULL,
	[closed_at] [datetime2](7) NULL,
	[closure_note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[incident_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[incident_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inspection_checklist_items]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inspection_checklist_items](
	[item_id] [bigint] IDENTITY(1,1) NOT NULL,
	[version_id] [bigint] NOT NULL,
	[criterion] [nvarchar](250) NOT NULL,
	[description] [nvarchar](max) NULL,
	[pass_condition] [nvarchar](max) NULL,
	[is_required] [bit] NOT NULL,
	[sort_order] [int] NOT NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inspection_checklist_versions]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inspection_checklist_versions](
	[version_id] [bigint] IDENTITY(1,1) NOT NULL,
	[checklist_id] [bigint] NOT NULL,
	[version_no] [int] NOT NULL,
	[effective_at] [datetime2](7) NULL,
	[status] [varchar](30) NOT NULL,
	[published_by] [bigint] NULL,
PRIMARY KEY CLUSTERED
(
	[version_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inspection_checklists]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inspection_checklists](
	[checklist_id] [bigint] IDENTITY(1,1) NOT NULL,
	[checklist_code] [varchar](40) NOT NULL,
	[checklist_name] [nvarchar](200) NOT NULL,
	[room_type_id] [bigint] NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
	[created_by] [bigint] NULL,
	[created_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[checklist_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[checklist_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inspection_defects]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inspection_defects](
	[defect_id] [bigint] IDENTITY(1,1) NOT NULL,
	[inspection_id] [bigint] NOT NULL,
	[checklist_item_id] [bigint] NULL,
	[severity_id] [bigint] NULL,
	[description] [nvarchar](max) NOT NULL,
	[corrective_action] [nvarchar](max) NULL,
	[status] [varchar](30) NOT NULL,
	[assigned_to] [bigint] NULL,
	[resolved_at] [datetime2](7) NULL,
	[resolution_note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[defect_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inspection_results]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inspection_results](
	[result_id] [bigint] IDENTITY(1,1) NOT NULL,
	[inspection_id] [bigint] NOT NULL,
	[checklist_item_id] [bigint] NOT NULL,
	[outcome] [varchar](30) NOT NULL,
	[note] [nvarchar](max) NULL,
	[inspected_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[result_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[internal_request_assignments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[internal_request_assignments](
	[assignment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[internal_request_id] [bigint] NOT NULL,
	[assignee_id] [bigint] NOT NULL,
	[assigned_by] [bigint] NOT NULL,
	[assigned_at] [datetime2](7) NOT NULL,
	[ended_at] [datetime2](7) NULL,
	[status] [varchar](30) NOT NULL,
	[assignment_note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[assignment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[internal_request_events]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[internal_request_events](
	[event_id] [bigint] IDENTITY(1,1) NOT NULL,
	[internal_request_id] [bigint] NOT NULL,
	[event_type] [varchar](50) NOT NULL,
	[old_status] [varchar](40) NULL,
	[new_status] [varchar](40) NULL,
	[event_note] [nvarchar](max) NULL,
	[actor_id] [bigint] NOT NULL,
	[occurred_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[event_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[internal_request_feedback]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[internal_request_feedback](
	[feedback_id] [bigint] IDENTITY(1,1) NOT NULL,
	[internal_request_id] [bigint] NOT NULL,
	[rating] [int] NOT NULL,
	[comment] [nvarchar](max) NULL,
	[is_satisfied] [bit] NULL,
	[submitted_by] [bigint] NOT NULL,
	[submitted_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[feedback_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[internal_request_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[internal_request_types]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[internal_request_types](
	[request_type_id] [bigint] IDENTITY(1,1) NOT NULL,
	[type_code] [varchar](40) NOT NULL,
	[type_name] [nvarchar](150) NOT NULL,
	[default_department_id] [bigint] NULL,
	[default_priority_id] [bigint] NULL,
	[sla_minutes] [int] NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[request_type_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[type_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[internal_requests]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[internal_requests](
	[internal_request_id] [bigint] IDENTITY(1,1) NOT NULL,
	[request_no] [varchar](50) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[request_type_id] [bigint] NOT NULL,
	[requester_id] [bigint] NOT NULL,
	[requester_department_id] [bigint] NULL,
	[receiving_department_id] [bigint] NULL,
	[room_id] [bigint] NULL,
	[area_id] [bigint] NULL,
	[related_incident_id] [bigint] NULL,
	[subject] [nvarchar](200) NOT NULL,
	[description] [nvarchar](max) NOT NULL,
	[priority_id] [bigint] NULL,
	[status] [varchar](40) NOT NULL,
	[required_at] [datetime2](7) NULL,
	[due_at] [datetime2](7) NULL,
	[submitted_at] [datetime2](7) NULL,
	[completed_at] [datetime2](7) NULL,
	[closed_at] [datetime2](7) NULL,
	[cancellation_reason] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[internal_request_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[request_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[inventory_items]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[inventory_items](
	[item_id] [bigint] IDENTITY(1,1) NOT NULL,
	[item_code] [varchar](50) NOT NULL,
	[item_name] [nvarchar](200) NOT NULL,
	[category_id] [bigint] NOT NULL,
	[base_uom_id] [bigint] NOT NULL,
	[description] [nvarchar](max) NULL,
	[specification] [nvarchar](max) NULL,
	[is_consumable] [bit] NOT NULL,
	[is_active] [bit] NOT NULL,
	[created_at] [datetime2](7) NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[item_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[item_categories]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[item_categories](
	[item_category_id] [bigint] IDENTITY(1,1) NOT NULL,
	[category_code] [varchar](40) NOT NULL,
	[category_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[item_category_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[category_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[maintenance_work_logs]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[maintenance_work_logs](
	[log_id] [bigint] IDENTITY(1,1) NOT NULL,
	[maintenance_work_id] [bigint] NOT NULL,
	[work_note] [nvarchar](max) NOT NULL,
	[spent_minutes] [int] NOT NULL,
	[material_issue_id] [bigint] NULL,
	[status] [varchar](30) NULL,
	[logged_by] [bigint] NOT NULL,
	[logged_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[log_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[maintenance_works]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[maintenance_works](
	[maintenance_work_id] [bigint] IDENTITY(1,1) NOT NULL,
	[work_no] [varchar](40) NOT NULL,
	[incident_id] [bigint] NULL,
	[assignee_id] [bigint] NULL,
	[department_id] [bigint] NULL,
	[title] [nvarchar](200) NOT NULL,
	[instruction] [nvarchar](max) NULL,
	[priority_id] [bigint] NULL,
	[status] [varchar](40) NOT NULL,
	[planned_start_at] [datetime2](7) NULL,
	[planned_end_at] [datetime2](7) NULL,
	[actual_start_at] [datetime2](7) NULL,
	[resolved_at] [datetime2](7) NULL,
	[resolution_note] [nvarchar](max) NULL,
	[reviewed_by] [bigint] NULL,
	[reviewed_at] [datetime2](7) NULL,
	[created_by] [bigint] NOT NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[maintenance_work_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[work_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[material_fulfillment_lines]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[material_fulfillment_lines](
	[fulfillment_line_id] [bigint] IDENTITY(1,1) NOT NULL,
	[fulfillment_id] [bigint] NOT NULL,
	[request_line_id] [bigint] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[issued_qty] [decimal](18, 3) NOT NULL,
	[received_qty] [decimal](18, 3) NULL,
	[uom_id] [bigint] NOT NULL,
	[note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[fulfillment_line_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[material_fulfillments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[material_fulfillments](
	[fulfillment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[fulfillment_no] [varchar](50) NOT NULL,
	[request_id] [bigint] NOT NULL,
	[stock_document_id] [bigint] NULL,
	[warehouse_id] [bigint] NOT NULL,
	[fulfilled_by] [bigint] NOT NULL,
	[received_by] [bigint] NULL,
	[status] [varchar](30) NOT NULL,
	[fulfilled_at] [datetime2](7) NULL,
	[received_at] [datetime2](7) NULL,
	[note] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[fulfillment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[fulfillment_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[material_request_approvals]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[material_request_approvals](
	[approval_id] [bigint] IDENTITY(1,1) NOT NULL,
	[request_id] [bigint] NOT NULL,
	[approval_level] [int] NOT NULL,
	[approver_id] [bigint] NOT NULL,
	[decision] [varchar](30) NOT NULL,
	[decision_note] [nvarchar](max) NULL,
	[decided_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[approval_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[material_request_lines]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[material_request_lines](
	[line_id] [bigint] IDENTITY(1,1) NOT NULL,
	[request_id] [bigint] NOT NULL,
	[line_no] [int] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[uom_id] [bigint] NOT NULL,
	[requested_qty] [decimal](18, 3) NOT NULL,
	[approved_qty] [decimal](18, 3) NULL,
	[fulfilled_qty] [decimal](18, 3) NOT NULL,
	[note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[line_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[material_requests]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[material_requests](
	[request_id] [bigint] IDENTITY(1,1) NOT NULL,
	[request_no] [varchar](50) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[department_id] [bigint] NOT NULL,
	[requester_id] [bigint] NOT NULL,
	[source_warehouse_id] [bigint] NULL,
	[purpose] [nvarchar](max) NOT NULL,
	[needed_at] [datetime2](7) NULL,
	[priority_id] [bigint] NULL,
	[status] [varchar](40) NOT NULL,
	[submitted_at] [datetime2](7) NULL,
	[completed_at] [datetime2](7) NULL,
	[cancellation_reason] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[request_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[request_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[notification_recipients]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[notification_recipients](
	[recipient_id] [bigint] IDENTITY(1,1) NOT NULL,
	[notification_id] [bigint] NOT NULL,
	[user_id] [bigint] NOT NULL,
	[delivery_channel] [varchar](30) NOT NULL,
	[delivery_status] [varchar](30) NOT NULL,
	[delivered_at] [datetime2](7) NULL,
	[read_at] [datetime2](7) NULL,
	[dismissed_at] [datetime2](7) NULL,
	[failure_reason] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[recipient_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[notifications]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[notifications](
	[notification_id] [bigint] IDENTITY(1,1) NOT NULL,
	[notification_type] [varchar](60) NOT NULL,
	[title] [nvarchar](200) NOT NULL,
	[message] [nvarchar](max) NOT NULL,
	[priority] [varchar](20) NOT NULL,
	[source_type] [varchar](60) NULL,
	[source_id] [bigint] NULL,
	[action_url] [nvarchar](max) NULL,
	[created_by] [bigint] NULL,
	[created_at] [datetime2](7) NOT NULL,
	[expires_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[notification_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[operational_task_assignments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[operational_task_assignments](
	[assignment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[task_id] [bigint] NOT NULL,
	[assignee_id] [bigint] NOT NULL,
	[assigned_by] [bigint] NOT NULL,
	[assigned_at] [datetime2](7) NOT NULL,
	[ended_at] [datetime2](7) NULL,
	[assignment_status] [varchar](30) NOT NULL,
	[note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[assignment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[operational_task_dependencies]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[operational_task_dependencies](
	[dependency_id] [bigint] IDENTITY(1,1) NOT NULL,
	[predecessor_task_id] [bigint] NOT NULL,
	[successor_task_id] [bigint] NOT NULL,
	[dependency_type] [varchar](30) NOT NULL,
	[lag_minutes] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[dependency_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[operational_task_progress]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[operational_task_progress](
	[progress_id] [bigint] IDENTITY(1,1) NOT NULL,
	[task_id] [bigint] NOT NULL,
	[progress_percent] [int] NOT NULL,
	[old_status] [varchar](40) NULL,
	[new_status] [varchar](40) NULL,
	[description] [nvarchar](max) NULL,
	[blocker_reason] [nvarchar](max) NULL,
	[reported_by] [bigint] NOT NULL,
	[reported_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[progress_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[operational_tasks]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[operational_tasks](
	[task_id] [bigint] IDENTITY(1,1) NOT NULL,
	[task_code] [varchar](50) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[department_id] [bigint] NOT NULL,
	[category_id] [bigint] NULL,
	[title] [nvarchar](200) NOT NULL,
	[description] [nvarchar](max) NULL,
	[priority_id] [bigint] NULL,
	[room_id] [bigint] NULL,
	[area_id] [bigint] NULL,
	[parent_task_id] [bigint] NULL,
	[source_type] [varchar](50) NULL,
	[source_id] [bigint] NULL,
	[status] [varchar](40) NOT NULL,
	[progress_percent] [int] NOT NULL,
	[planned_start_at] [datetime2](7) NULL,
	[due_at] [datetime2](7) NULL,
	[actual_start_at] [datetime2](7) NULL,
	[completed_at] [datetime2](7) NULL,
	[completion_note] [nvarchar](max) NULL,
	[created_by] [bigint] NOT NULL,
	[created_at] [datetime2](7) NOT NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[task_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[task_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[permissions]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[permissions](
	[permission_id] [bigint] IDENTITY(1,1) NOT NULL,
	[permission_code] [varchar](100) NOT NULL,
	[permission_name] [nvarchar](150) NOT NULL,
	[module_code] [varchar](60) NOT NULL,
	[resource_code] [varchar](60) NOT NULL,
	[action_code] [varchar](40) NOT NULL,
	[description] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[permission_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[permission_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[preventive_maintenance_runs]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[preventive_maintenance_runs](
	[run_id] [bigint] IDENTITY(1,1) NOT NULL,
	[schedule_id] [bigint] NOT NULL,
	[maintenance_work_id] [bigint] NULL,
	[planned_at] [datetime2](7) NOT NULL,
	[performed_at] [datetime2](7) NULL,
	[result] [varchar](30) NULL,
	[next_due_at] [datetime2](7) NULL,
	[note] [nvarchar](max) NULL,
	[performed_by] [bigint] NULL,
PRIMARY KEY CLUSTERED
(
	[run_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[preventive_maintenance_schedules]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[preventive_maintenance_schedules](
	[schedule_id] [bigint] IDENTITY(1,1) NOT NULL,
	[schedule_code] [varchar](40) NOT NULL,
	[asset_id] [bigint] NULL,
	[room_id] [bigint] NULL,
	[area_id] [bigint] NULL,
	[frequency] [varchar](30) NOT NULL,
	[frequency_interval] [int] NOT NULL,
	[instruction] [nvarchar](max) NULL,
	[last_performed_at] [datetime2](7) NULL,
	[next_due_at] [datetime2](7) NOT NULL,
	[assigned_department_id] [bigint] NULL,
	[is_active] [bit] NOT NULL,
	[created_by] [bigint] NULL,
PRIMARY KEY CLUSTERED
(
	[schedule_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[schedule_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[reference_values]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[reference_values](
	[reference_id] [bigint] IDENTITY(1,1) NOT NULL,
	[group_code] [varchar](60) NOT NULL,
	[value_code] [varchar](60) NOT NULL,
	[display_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[sort_order] [int] NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[reference_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[role_permissions]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[role_permissions](
	[role_permission_id] [bigint] IDENTITY(1,1) NOT NULL,
	[role_id] [bigint] NOT NULL,
	[permission_id] [bigint] NOT NULL,
	[granted_at] [datetime2](7) NULL,
	[granted_by] [bigint] NULL,
	[data_scope] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[role_permission_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[roles]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[roles](
	[role_id] [bigint] IDENTITY(1,1) NOT NULL,
	[role_code] [varchar](60) NOT NULL,
	[role_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_system_role] [bit] NOT NULL,
	[is_active] [bit] NOT NULL,
	[created_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[role_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[role_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[room_blocks]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[room_blocks](
	[block_id] [bigint] IDENTITY(1,1) NOT NULL,
	[room_id] [bigint] NOT NULL,
	[incident_id] [bigint] NULL,
	[reason] [nvarchar](max) NOT NULL,
	[blocked_at] [datetime2](7) NOT NULL,
	[blocked_by] [bigint] NOT NULL,
	[expected_release_at] [datetime2](7) NULL,
	[released_at] [datetime2](7) NULL,
	[released_by] [bigint] NULL,
	[release_note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[block_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[room_inspections]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[room_inspections](
	[inspection_id] [bigint] IDENTITY(1,1) NOT NULL,
	[inspection_no] [varchar](40) NOT NULL,
	[room_id] [bigint] NOT NULL,
	[cleaning_task_id] [bigint] NULL,
	[version_id] [bigint] NOT NULL,
	[inspector_user_id] [bigint] NOT NULL,
	[status] [varchar](30) NOT NULL,
	[result] [varchar](30) NULL,
	[scheduled_at] [datetime2](7) NULL,
	[inspected_at] [datetime2](7) NULL,
	[summary_note] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[inspection_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[inspection_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[room_status_history]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[room_status_history](
	[history_id] [bigint] IDENTITY(1,1) NOT NULL,
	[room_id] [bigint] NOT NULL,
	[from_status] [varchar](40) NULL,
	[to_status] [varchar](40) NOT NULL,
	[trigger_type] [varchar](50) NULL,
	[trigger_id] [bigint] NULL,
	[changed_by] [bigint] NULL,
	[changed_at] [datetime2](7) NOT NULL,
	[reason] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[history_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[room_types]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[room_types](
	[room_type_id] [bigint] IDENTITY(1,1) NOT NULL,
	[type_code] [varchar](30) NOT NULL,
	[type_name] [nvarchar](150) NOT NULL,
	[description] [nvarchar](max) NULL,
	[standard_capacity] [int] NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[room_type_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[type_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[rooms]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[rooms](
	[room_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[floor_id] [bigint] NOT NULL,
	[area_id] [bigint] NULL,
	[room_type_id] [bigint] NOT NULL,
	[room_number] [varchar](30) NOT NULL,
	[operational_status] [varchar](40) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
	[created_at] [datetime2](7) NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[room_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[shift_assignments]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[shift_assignments](
	[shift_assignment_id] [bigint] IDENTITY(1,1) NOT NULL,
	[shift_id] [bigint] NOT NULL,
	[user_id] [bigint] NOT NULL,
	[assignment_status] [varchar](30) NOT NULL,
	[assigned_by] [bigint] NOT NULL,
	[assigned_at] [datetime2](7) NOT NULL,
	[confirmed_at] [datetime2](7) NULL,
	[cancellation_reason] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[shift_assignment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[shift_attendance]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[shift_attendance](
	[attendance_id] [bigint] IDENTITY(1,1) NOT NULL,
	[shift_assignment_id] [bigint] NOT NULL,
	[actual_check_in_at] [datetime2](7) NULL,
	[actual_check_out_at] [datetime2](7) NULL,
	[attendance_status] [varchar](30) NOT NULL,
	[late_minutes] [int] NULL,
	[overtime_minutes] [int] NULL,
	[note] [nvarchar](max) NULL,
	[verified_by] [bigint] NULL,
	[verified_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[attendance_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[shift_assignment_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[shift_templates]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[shift_templates](
	[shift_template_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[shift_code] [varchar](30) NOT NULL,
	[shift_name] [nvarchar](100) NOT NULL,
	[start_time] [time](7) NOT NULL,
	[end_time] [time](7) NOT NULL,
	[break_minutes] [int] NOT NULL,
	[crosses_midnight] [bit] NOT NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[shift_template_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stock_document_lines]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stock_document_lines](
	[line_id] [bigint] IDENTITY(1,1) NOT NULL,
	[document_id] [bigint] NOT NULL,
	[line_no] [int] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[uom_id] [bigint] NOT NULL,
	[conversion_factor] [decimal](18, 6) NOT NULL,
	[requested_qty] [decimal](18, 3) NULL,
	[actual_qty] [decimal](18, 3) NOT NULL,
	[base_qty] [decimal](18, 3) NOT NULL,
	[unit_cost] [decimal](18, 2) NULL,
	[note] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[line_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stock_documents]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stock_documents](
	[document_id] [bigint] IDENTITY(1,1) NOT NULL,
	[document_no] [varchar](50) NOT NULL,
	[document_type] [varchar](30) NOT NULL,
	[source_warehouse_id] [bigint] NULL,
	[destination_warehouse_id] [bigint] NULL,
	[status] [varchar](30) NOT NULL,
	[reference_type] [varchar](50) NULL,
	[reference_id] [bigint] NULL,
	[document_date] [date] NOT NULL,
	[reason] [nvarchar](max) NULL,
	[note] [nvarchar](max) NULL,
	[created_by] [bigint] NOT NULL,
	[approved_by] [bigint] NULL,
	[posted_by] [bigint] NULL,
	[created_at] [datetime2](7) NOT NULL,
	[approved_at] [datetime2](7) NULL,
	[posted_at] [datetime2](7) NULL,
	[cancelled_at] [datetime2](7) NULL,
	[reversal_of_document_id] [bigint] NULL,
PRIMARY KEY CLUSTERED
(
	[document_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[document_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stock_movements]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stock_movements](
	[movement_id] [bigint] IDENTITY(1,1) NOT NULL,
	[document_line_id] [bigint] NOT NULL,
	[warehouse_id] [bigint] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[signed_base_qty] [decimal](18, 3) NOT NULL,
	[unit_cost] [decimal](18, 2) NULL,
	[occurred_at] [datetime2](7) NOT NULL,
	[created_by] [bigint] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[movement_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stock_thresholds]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stock_thresholds](
	[threshold_id] [bigint] IDENTITY(1,1) NOT NULL,
	[warehouse_id] [bigint] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[minimum_qty] [decimal](18, 3) NOT NULL,
	[maximum_qty] [decimal](18, 3) NULL,
	[reorder_qty] [decimal](18, 3) NULL,
	[alert_enabled] [bit] NOT NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[threshold_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stocktake_lines]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stocktake_lines](
	[stocktake_line_id] [bigint] IDENTITY(1,1) NOT NULL,
	[stocktake_id] [bigint] NOT NULL,
	[item_id] [bigint] NOT NULL,
	[system_qty] [decimal](18, 3) NOT NULL,
	[counted_qty] [decimal](18, 3) NULL,
	[variance_qty] [decimal](18, 3) NULL,
	[discrepancy_reason] [nvarchar](max) NULL,
	[counted_at] [datetime2](7) NULL,
	[counted_by] [bigint] NULL,
PRIMARY KEY CLUSTERED
(
	[stocktake_line_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[stocktakes]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[stocktakes](
	[stocktake_id] [bigint] IDENTITY(1,1) NOT NULL,
	[stocktake_no] [varchar](50) NOT NULL,
	[warehouse_id] [bigint] NOT NULL,
	[snapshot_at] [datetime2](7) NOT NULL,
	[status] [varchar](30) NOT NULL,
	[counted_by] [bigint] NULL,
	[approved_by] [bigint] NULL,
	[started_at] [datetime2](7) NULL,
	[completed_at] [datetime2](7) NULL,
	[approved_at] [datetime2](7) NULL,
	[adjustment_document_id] [bigint] NULL,
	[note] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[stocktake_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[stocktake_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[system_settings]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[system_settings](
	[setting_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NULL,
	[setting_group] [varchar](60) NOT NULL,
	[setting_key] [varchar](100) NOT NULL,
	[setting_value] [nvarchar](max) NULL,
	[data_type] [varchar](30) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_sensitive] [bit] NOT NULL,
	[updated_by] [bigint] NULL,
	[updated_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[setting_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[units_of_measure]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[units_of_measure](
	[uom_id] [bigint] IDENTITY(1,1) NOT NULL,
	[uom_code] [varchar](30) NOT NULL,
	[uom_name] [nvarchar](100) NOT NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[uom_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[uom_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[uom_conversions]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[uom_conversions](
	[conversion_id] [bigint] IDENTITY(1,1) NOT NULL,
	[item_id] [bigint] NULL,
	[from_uom_id] [bigint] NOT NULL,
	[to_uom_id] [bigint] NOT NULL,
	[conversion_factor] [decimal](18, 6) NOT NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[conversion_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[user_branch_access]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[user_branch_access](
	[access_id] [bigint] IDENTITY(1,1) NOT NULL,
	[user_id] [bigint] NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[access_level] [varchar](30) NOT NULL,
	[is_primary] [bit] NOT NULL,
	[granted_by] [bigint] NULL,
	[granted_at] [datetime2](7) NOT NULL,
	[revoked_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[access_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[user_roles]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[user_roles](
	[user_role_id] [bigint] IDENTITY(1,1) NOT NULL,
	[user_id] [bigint] NOT NULL,
	[role_id] [bigint] NOT NULL,
	[branch_id] [bigint] NULL,
	[assigned_by] [bigint] NULL,
	[assigned_at] [datetime2](7) NOT NULL,
	[expires_at] [datetime2](7) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[user_role_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[user_sessions]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[user_sessions](
	[session_id] [bigint] IDENTITY(1,1) NOT NULL,
	[user_id] [bigint] NOT NULL,
	[session_key_hash] [varchar](255) NOT NULL,
	[device_info] [nvarchar](max) NULL,
	[ip_address] [varchar](45) NULL,
	[user_agent] [nvarchar](max) NULL,
	[created_at] [datetime2](7) NOT NULL,
	[expires_at] [datetime2](7) NOT NULL,
	[revoked_at] [datetime2](7) NULL,
	[last_used_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[session_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[session_key_hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[users]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[users](
	[user_id] [bigint] IDENTITY(1,1) NOT NULL,
	[employee_code] [varchar](40) NOT NULL,
	[username] [varchar](100) NOT NULL,
	[email] [varchar](150) NOT NULL,
	[password_hash] [varchar](255) NULL,
	[full_name] [nvarchar](200) NOT NULL,
	[phone] [varchar](20) NULL,
	[avatar_url] [nvarchar](max) NULL,
	[primary_branch_id] [bigint] NULL,
	[department_id] [bigint] NULL,
	[manager_user_id] [bigint] NULL,
	[employment_status] [varchar](30) NOT NULL,
	[account_status] [varchar](30) NOT NULL,
	[last_login_at] [datetime2](7) NULL,
	[created_at] [datetime2](7) NOT NULL,
	[updated_at] [datetime2](7) NULL,
	[deleted_at] [datetime2](7) NULL,
	[failed_login_count] [int] NOT NULL,
	[locked_until] [datetime2](7) NULL,
	[temporary_password_expires_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[user_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[employee_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[warehouses]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[warehouses](
	[warehouse_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[department_id] [bigint] NULL,
	[warehouse_code] [varchar](40) NOT NULL,
	[warehouse_name] [nvarchar](150) NOT NULL,
	[location_description] [nvarchar](max) NULL,
	[manager_user_id] [bigint] NULL,
	[is_active] [bit] NOT NULL,
	[created_at] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[warehouse_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[work_categories]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[work_categories](
	[category_id] [bigint] IDENTITY(1,1) NOT NULL,
	[category_code] [varchar](40) NOT NULL,
	[category_name] [nvarchar](150) NOT NULL,
	[department_id] [bigint] NULL,
	[description] [nvarchar](max) NULL,
	[is_active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[category_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED
(
	[category_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[work_shifts]    Script Date: 10/21/2026 2:27:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[work_shifts](
	[shift_id] [bigint] IDENTITY(1,1) NOT NULL,
	[branch_id] [bigint] NOT NULL,
	[department_id] [bigint] NOT NULL,
	[shift_template_id] [bigint] NOT NULL,
	[shift_date] [date] NOT NULL,
	[scheduled_start_at] [datetime2](7) NOT NULL,
	[scheduled_end_at] [datetime2](7) NOT NULL,
	[required_staff_count] [int] NOT NULL,
	[status] [varchar](30) NOT NULL,
	[note] [nvarchar](max) NULL,
	[created_by] [bigint] NOT NULL,
	[created_at] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[shift_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [areas_index_1]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [areas_index_1] ON [dbo].[areas]
(
	[branch_id] ASC,
	[area_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [assets_index_17]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [assets_index_17] ON [dbo].[assets]
(
	[branch_id] ASC,
	[lifecycle_status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [assets_index_18]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [assets_index_18] ON [dbo].[assets]
(
	[serial_number] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [attachments_index_44]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [attachments_index_44] ON [dbo].[attachments]
(
	[entity_type] ASC,
	[entity_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [audit_logs_index_45]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [audit_logs_index_45] ON [dbo].[audit_logs]
(
	[entity_type] ASC,
	[entity_id] ASC,
	[occurred_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [audit_logs_index_46]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [audit_logs_index_46] ON [dbo].[audit_logs]
(
	[actor_user_id] ASC,
	[occurred_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [cleaning_assignments_index_8]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [cleaning_assignments_index_8] ON [dbo].[cleaning_assignments]
(
	[cleaning_task_id] ASC,
	[assigned_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [cleaning_assignments_index_9]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [cleaning_assignments_index_9] ON [dbo].[cleaning_assignments]
(
	[assignee_user_id] ASC,
	[assignment_status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_cleaning_assignments_active]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_cleaning_assignments_active] ON [dbo].[cleaning_assignments]
(
	[cleaning_task_id] ASC
)
WHERE ([assignment_status]='ACTIVE')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [cleaning_tasks_index_6]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [cleaning_tasks_index_6] ON [dbo].[cleaning_tasks]
(
	[room_id] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [cleaning_tasks_index_7]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [cleaning_tasks_index_7] ON [dbo].[cleaning_tasks]
(
	[due_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [departments_index_4]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [departments_index_4] ON [dbo].[departments]
(
	[branch_id] ASC,
	[department_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [floors_index_0]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [floors_index_0] ON [dbo].[floors]
(
	[branch_id] ASC,
	[floor_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [incidents_index_14]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [incidents_index_14] ON [dbo].[incidents]
(
	[branch_id] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [incidents_index_15]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [incidents_index_15] ON [dbo].[incidents]
(
	[room_id] ASC,
	[reported_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [inspection_checklist_versions_index_11]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [inspection_checklist_versions_index_11] ON [dbo].[inspection_checklist_versions]
(
	[checklist_id] ASC,
	[version_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [inspection_results_index_13]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [inspection_results_index_13] ON [dbo].[inspection_results]
(
	[inspection_id] ASC,
	[checklist_item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_internal_request_assignments_active]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_internal_request_assignments_active] ON [dbo].[internal_request_assignments]
(
	[internal_request_id] ASC
)
WHERE ([status]='ACTIVE')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [internal_request_events_index_32]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [internal_request_events_index_32] ON [dbo].[internal_request_events]
(
	[internal_request_id] ASC,
	[occurred_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [internal_requests_index_30]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [internal_requests_index_30] ON [dbo].[internal_requests]
(
	[branch_id] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [internal_requests_index_31]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [internal_requests_index_31] ON [dbo].[internal_requests]
(
	[receiving_department_id] ASC,
	[due_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [material_fulfillment_lines_index_29]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [material_fulfillment_lines_index_29] ON [dbo].[material_fulfillment_lines]
(
	[fulfillment_id] ASC,
	[request_line_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [material_request_lines_index_28]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [material_request_lines_index_28] ON [dbo].[material_request_lines]
(
	[request_id] ASC,
	[line_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [material_requests_index_27]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [material_requests_index_27] ON [dbo].[material_requests]
(
	[branch_id] ASC,
	[department_id] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [notification_recipients_index_42]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [notification_recipients_index_42] ON [dbo].[notification_recipients]
(
	[notification_id] ASC,
	[user_id] ASC,
	[delivery_channel] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [notification_recipients_index_43]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [notification_recipients_index_43] ON [dbo].[notification_recipients]
(
	[user_id] ASC,
	[read_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_operational_task_assignments_active]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_operational_task_assignments_active] ON [dbo].[operational_task_assignments]
(
	[task_id] ASC
)
WHERE ([assignment_status]='ACTIVE')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [operational_task_dependencies_index_35]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [operational_task_dependencies_index_35] ON [dbo].[operational_task_dependencies]
(
	[predecessor_task_id] ASC,
	[successor_task_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [operational_tasks_index_33]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [operational_tasks_index_33] ON [dbo].[operational_tasks]
(
	[branch_id] ASC,
	[department_id] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [operational_tasks_index_34]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [operational_tasks_index_34] ON [dbo].[operational_tasks]
(
	[due_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [preventive_maintenance_runs_index_16]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [preventive_maintenance_runs_index_16] ON [dbo].[preventive_maintenance_runs]
(
	[schedule_id] ASC,
	[planned_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [reference_values_index_5]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [reference_values_index_5] ON [dbo].[reference_values]
(
	[group_code] ASC,
	[value_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [role_permissions_index_40]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [role_permissions_index_40] ON [dbo].[role_permissions]
(
	[role_id] ASC,
	[permission_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_room_blocks_active]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_room_blocks_active] ON [dbo].[room_blocks]
(
	[room_id] ASC
)
WHERE ([released_at] IS NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [room_inspections_index_12]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [room_inspections_index_12] ON [dbo].[room_inspections]
(
	[room_id] ASC,
	[inspected_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [room_status_history_index_10]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [room_status_history_index_10] ON [dbo].[room_status_history]
(
	[room_id] ASC,
	[changed_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [rooms_index_2]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [rooms_index_2] ON [dbo].[rooms]
(
	[branch_id] ASC,
	[room_number] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [rooms_index_3]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [rooms_index_3] ON [dbo].[rooms]
(
	[branch_id] ASC,
	[operational_status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [shift_assignments_index_37]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [shift_assignments_index_37] ON [dbo].[shift_assignments]
(
	[shift_id] ASC,
	[user_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [shift_templates_index_36]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [shift_templates_index_36] ON [dbo].[shift_templates]
(
	[branch_id] ASC,
	[shift_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [stock_document_lines_index_24]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [stock_document_lines_index_24] ON [dbo].[stock_document_lines]
(
	[document_id] ASC,
	[line_no] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [stock_documents_index_22]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [stock_documents_index_22] ON [dbo].[stock_documents]
(
	[document_type] ASC,
	[status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [stock_documents_index_23]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [stock_documents_index_23] ON [dbo].[stock_documents]
(
	[document_date] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [stock_movements_index_25]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [stock_movements_index_25] ON [dbo].[stock_movements]
(
	[warehouse_id] ASC,
	[item_id] ASC,
	[occurred_at] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [stock_thresholds_index_21]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [stock_thresholds_index_21] ON [dbo].[stock_thresholds]
(
	[warehouse_id] ASC,
	[item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [stocktake_lines_index_26]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [stocktake_lines_index_26] ON [dbo].[stocktake_lines]
(
	[stocktake_id] ASC,
	[item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [system_settings_index_47]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [system_settings_index_47] ON [dbo].[system_settings]
(
	[branch_id] ASC,
	[setting_group] ASC,
	[setting_key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [uom_conversions_index_20]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [uom_conversions_index_20] ON [dbo].[uom_conversions]
(
	[item_id] ASC,
	[from_uom_id] ASC,
	[to_uom_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [user_branch_access_index_41]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [user_branch_access_index_41] ON [dbo].[user_branch_access]
(
	[user_id] ASC,
	[branch_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_user_branch_access_primary]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_user_branch_access_primary] ON [dbo].[user_branch_access]
(
	[user_id] ASC
)
WHERE ([is_primary]=(1) AND [revoked_at] IS NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [user_roles_index_39]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [user_roles_index_39] ON [dbo].[user_roles]
(
	[user_id] ASC,
	[role_id] ASC,
	[branch_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [users_index_38]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE NONCLUSTERED INDEX [users_index_38] ON [dbo].[users]
(
	[primary_branch_id] ASC,
	[department_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [warehouses_index_19]    Script Date: 10/21/2026 2:27:27 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [warehouses_index_19] ON [dbo].[warehouses]
(
	[branch_id] ASC,
	[warehouse_code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[areas] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[asset_handovers] ADD  DEFAULT ('PENDING') FOR [handover_status]
GO
ALTER TABLE [dbo].[asset_types] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[assets] ADD  DEFAULT ('GOOD') FOR [condition_status]
GO
ALTER TABLE [dbo].[assets] ADD  DEFAULT ('IN_USE') FOR [lifecycle_status]
GO
ALTER TABLE [dbo].[branches] ADD  DEFAULT ('Asia/Ho_Chi_Minh') FOR [timezone]
GO
ALTER TABLE [dbo].[branches] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[cleaning_assignments] ADD  DEFAULT ('ACTIVE') FOR [assignment_status]
GO
ALTER TABLE [dbo].[cleaning_reworks] ADD  DEFAULT ('PENDING') FOR [status]
GO
ALTER TABLE [dbo].[cleaning_tasks] ADD  DEFAULT ('PENDING') FOR [status]
GO
ALTER TABLE [dbo].[departments] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[floors] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[incidents] ADD  DEFAULT ('OPEN') FOR [status]
GO
ALTER TABLE [dbo].[inspection_checklist_items] ADD  DEFAULT ((1)) FOR [is_required]
GO
ALTER TABLE [dbo].[inspection_checklist_items] ADD  DEFAULT ((0)) FOR [sort_order]
GO
ALTER TABLE [dbo].[inspection_checklist_items] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[inspection_checklist_versions] ADD  DEFAULT ('DRAFT') FOR [status]
GO
ALTER TABLE [dbo].[inspection_checklists] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[inspection_defects] ADD  DEFAULT ('OPEN') FOR [status]
GO
ALTER TABLE [dbo].[internal_request_assignments] ADD  DEFAULT ('ACTIVE') FOR [status]
GO
ALTER TABLE [dbo].[internal_request_types] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[internal_requests] ADD  DEFAULT ('DRAFT') FOR [status]
GO
ALTER TABLE [dbo].[inventory_items] ADD  DEFAULT ((1)) FOR [is_consumable]
GO
ALTER TABLE [dbo].[inventory_items] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[item_categories] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[maintenance_work_logs] ADD  DEFAULT ((0)) FOR [spent_minutes]
GO
ALTER TABLE [dbo].[maintenance_works] ADD  DEFAULT ('PENDING') FOR [status]
GO
ALTER TABLE [dbo].[material_fulfillments] ADD  DEFAULT ('PREPARING') FOR [status]
GO
ALTER TABLE [dbo].[material_request_approvals] ADD  DEFAULT ((1)) FOR [approval_level]
GO
ALTER TABLE [dbo].[material_request_lines] ADD  DEFAULT ((0)) FOR [fulfilled_qty]
GO
ALTER TABLE [dbo].[material_requests] ADD  DEFAULT ('DRAFT') FOR [status]
GO
ALTER TABLE [dbo].[notification_recipients] ADD  DEFAULT ('IN_APP') FOR [delivery_channel]
GO
ALTER TABLE [dbo].[notification_recipients] ADD  DEFAULT ('PENDING') FOR [delivery_status]
GO
ALTER TABLE [dbo].[notifications] ADD  DEFAULT ('NORMAL') FOR [priority]
GO
ALTER TABLE [dbo].[operational_task_assignments] ADD  DEFAULT ('ACTIVE') FOR [assignment_status]
GO
ALTER TABLE [dbo].[operational_task_dependencies] ADD  DEFAULT ('FINISH_TO_START') FOR [dependency_type]
GO
ALTER TABLE [dbo].[operational_task_dependencies] ADD  DEFAULT ((0)) FOR [lag_minutes]
GO
ALTER TABLE [dbo].[operational_tasks] ADD  DEFAULT ('TODO') FOR [status]
GO
ALTER TABLE [dbo].[operational_tasks] ADD  DEFAULT ((0)) FOR [progress_percent]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] ADD  DEFAULT ((1)) FOR [frequency_interval]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[reference_values] ADD  DEFAULT ((0)) FOR [sort_order]
GO
ALTER TABLE [dbo].[reference_values] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[role_permissions] ADD  CONSTRAINT [DF_role_permissions_data_scope]  DEFAULT ('BRANCH') FOR [data_scope]
GO
ALTER TABLE [dbo].[roles] ADD  DEFAULT ((0)) FOR [is_system_role]
GO
ALTER TABLE [dbo].[roles] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[room_inspections] ADD  DEFAULT ('PENDING') FOR [status]
GO
ALTER TABLE [dbo].[room_types] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[rooms] ADD  CONSTRAINT [DF_rooms_operational_status]  DEFAULT ('DIRTY') FOR [operational_status]
GO
ALTER TABLE [dbo].[rooms] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[shift_assignments] ADD  DEFAULT ('ASSIGNED') FOR [assignment_status]
GO
ALTER TABLE [dbo].[shift_attendance] ADD  DEFAULT ('NOT_STARTED') FOR [attendance_status]
GO
ALTER TABLE [dbo].[shift_attendance] ADD  DEFAULT ((0)) FOR [late_minutes]
GO
ALTER TABLE [dbo].[shift_attendance] ADD  DEFAULT ((0)) FOR [overtime_minutes]
GO
ALTER TABLE [dbo].[shift_templates] ADD  DEFAULT ((0)) FOR [break_minutes]
GO
ALTER TABLE [dbo].[shift_templates] ADD  DEFAULT ((0)) FOR [crosses_midnight]
GO
ALTER TABLE [dbo].[shift_templates] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[stock_document_lines] ADD  DEFAULT ((1)) FOR [conversion_factor]
GO
ALTER TABLE [dbo].[stock_document_lines] ADD  DEFAULT ((0)) FOR [actual_qty]
GO
ALTER TABLE [dbo].[stock_document_lines] ADD  DEFAULT ((0)) FOR [base_qty]
GO
ALTER TABLE [dbo].[stock_documents] ADD  DEFAULT ('DRAFT') FOR [status]
GO
ALTER TABLE [dbo].[stock_thresholds] ADD  DEFAULT ((0)) FOR [minimum_qty]
GO
ALTER TABLE [dbo].[stock_thresholds] ADD  DEFAULT ((1)) FOR [alert_enabled]
GO
ALTER TABLE [dbo].[stocktakes] ADD  DEFAULT ('DRAFT') FOR [status]
GO
ALTER TABLE [dbo].[system_settings] ADD  DEFAULT ('STRING') FOR [data_type]
GO
ALTER TABLE [dbo].[system_settings] ADD  DEFAULT ((0)) FOR [is_sensitive]
GO
ALTER TABLE [dbo].[units_of_measure] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[uom_conversions] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[user_branch_access] ADD  DEFAULT ('STANDARD') FOR [access_level]
GO
ALTER TABLE [dbo].[user_branch_access] ADD  DEFAULT ((0)) FOR [is_primary]
GO
ALTER TABLE [dbo].[user_roles] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[users] ADD  DEFAULT ('ACTIVE') FOR [employment_status]
GO
ALTER TABLE [dbo].[users] ADD  DEFAULT ('ACTIVE') FOR [account_status]
GO
ALTER TABLE [dbo].[users] ADD  CONSTRAINT [DF_users_failed_login_count]  DEFAULT ((0)) FOR [failed_login_count]
GO
ALTER TABLE [dbo].[warehouses] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[work_categories] ADD  DEFAULT ((1)) FOR [is_active]
GO
ALTER TABLE [dbo].[work_shifts] ADD  DEFAULT ((1)) FOR [required_staff_count]
GO
ALTER TABLE [dbo].[work_shifts] ADD  DEFAULT ('PLANNED') FOR [status]
GO
ALTER TABLE [dbo].[areas]  WITH CHECK ADD  CONSTRAINT [FK_areas_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[areas] CHECK CONSTRAINT [FK_areas_branch_id]
GO
ALTER TABLE [dbo].[areas]  WITH CHECK ADD  CONSTRAINT [FK_areas_floor_id] FOREIGN KEY([floor_id])
REFERENCES [dbo].[floors] ([floor_id])
GO
ALTER TABLE [dbo].[areas] CHECK CONSTRAINT [FK_areas_floor_id]
GO
ALTER TABLE [dbo].[asset_condition_history]  WITH CHECK ADD  CONSTRAINT [FK_asset_condition_history_asset_id] FOREIGN KEY([asset_id])
REFERENCES [dbo].[assets] ([asset_id])
GO
ALTER TABLE [dbo].[asset_condition_history] CHECK CONSTRAINT [FK_asset_condition_history_asset_id]
GO
ALTER TABLE [dbo].[asset_condition_history]  WITH CHECK ADD  CONSTRAINT [FK_asset_condition_history_changed_by] FOREIGN KEY([changed_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[asset_condition_history] CHECK CONSTRAINT [FK_asset_condition_history_changed_by]
GO
ALTER TABLE [dbo].[asset_condition_history]  WITH CHECK ADD  CONSTRAINT [FK_asset_condition_history_related_incident_id] FOREIGN KEY([related_incident_id])
REFERENCES [dbo].[incidents] ([incident_id])
GO
ALTER TABLE [dbo].[asset_condition_history] CHECK CONSTRAINT [FK_asset_condition_history_related_incident_id]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_asset_id] FOREIGN KEY([asset_id])
REFERENCES [dbo].[assets] ([asset_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_asset_id]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_created_by]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_from_department_id] FOREIGN KEY([from_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_from_department_id]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_from_user_id] FOREIGN KEY([from_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_from_user_id]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_to_department_id] FOREIGN KEY([to_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_to_department_id]
GO
ALTER TABLE [dbo].[asset_handovers]  WITH CHECK ADD  CONSTRAINT [FK_asset_handovers_to_user_id] FOREIGN KEY([to_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[asset_handovers] CHECK CONSTRAINT [FK_asset_handovers_to_user_id]
GO
ALTER TABLE [dbo].[asset_location_history]  WITH CHECK ADD  CONSTRAINT [FK_asset_location_history_asset_id] FOREIGN KEY([asset_id])
REFERENCES [dbo].[assets] ([asset_id])
GO
ALTER TABLE [dbo].[asset_location_history] CHECK CONSTRAINT [FK_asset_location_history_asset_id]
GO
ALTER TABLE [dbo].[asset_location_history]  WITH CHECK ADD  CONSTRAINT [FK_asset_location_history_moved_by] FOREIGN KEY([moved_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[asset_location_history] CHECK CONSTRAINT [FK_asset_location_history_moved_by]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_asset_type_id] FOREIGN KEY([asset_type_id])
REFERENCES [dbo].[asset_types] ([asset_type_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_asset_type_id]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_branch_id]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_current_area_id] FOREIGN KEY([current_area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_current_area_id]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_current_department_id] FOREIGN KEY([current_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_current_department_id]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_current_room_id] FOREIGN KEY([current_room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_current_room_id]
GO
ALTER TABLE [dbo].[assets]  WITH CHECK ADD  CONSTRAINT [FK_assets_custodian_user_id] FOREIGN KEY([custodian_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[assets] CHECK CONSTRAINT [FK_assets_custodian_user_id]
GO
ALTER TABLE [dbo].[attachments]  WITH CHECK ADD  CONSTRAINT [FK_attachments_uploaded_by] FOREIGN KEY([uploaded_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[attachments] CHECK CONSTRAINT [FK_attachments_uploaded_by]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [FK_audit_logs_actor_user_id] FOREIGN KEY([actor_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [FK_audit_logs_actor_user_id]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [FK_audit_logs_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [FK_audit_logs_branch_id]
GO
ALTER TABLE [dbo].[cleaning_assignments]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_assignments_assigned_by] FOREIGN KEY([assigned_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[cleaning_assignments] CHECK CONSTRAINT [FK_cleaning_assignments_assigned_by]
GO
ALTER TABLE [dbo].[cleaning_assignments]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_assignments_assignee_user_id] FOREIGN KEY([assignee_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[cleaning_assignments] CHECK CONSTRAINT [FK_cleaning_assignments_assignee_user_id]
GO
ALTER TABLE [dbo].[cleaning_assignments]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_assignments_cleaning_task_id] FOREIGN KEY([cleaning_task_id])
REFERENCES [dbo].[cleaning_tasks] ([cleaning_task_id])
GO
ALTER TABLE [dbo].[cleaning_assignments] CHECK CONSTRAINT [FK_cleaning_assignments_cleaning_task_id]
GO
ALTER TABLE [dbo].[cleaning_progress_logs]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_progress_logs_blocker_type_id] FOREIGN KEY([blocker_type_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[cleaning_progress_logs] CHECK CONSTRAINT [FK_cleaning_progress_logs_blocker_type_id]
GO
ALTER TABLE [dbo].[cleaning_progress_logs]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_progress_logs_cleaning_task_id] FOREIGN KEY([cleaning_task_id])
REFERENCES [dbo].[cleaning_tasks] ([cleaning_task_id])
GO
ALTER TABLE [dbo].[cleaning_progress_logs] CHECK CONSTRAINT [FK_cleaning_progress_logs_cleaning_task_id]
GO
ALTER TABLE [dbo].[cleaning_progress_logs]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_progress_logs_logged_by] FOREIGN KEY([logged_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[cleaning_progress_logs] CHECK CONSTRAINT [FK_cleaning_progress_logs_logged_by]
GO
ALTER TABLE [dbo].[cleaning_reworks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_reworks_assignee_user_id] FOREIGN KEY([assignee_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[cleaning_reworks] CHECK CONSTRAINT [FK_cleaning_reworks_assignee_user_id]
GO
ALTER TABLE [dbo].[cleaning_reworks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_reworks_cleaning_task_id] FOREIGN KEY([cleaning_task_id])
REFERENCES [dbo].[cleaning_tasks] ([cleaning_task_id])
GO
ALTER TABLE [dbo].[cleaning_reworks] CHECK CONSTRAINT [FK_cleaning_reworks_cleaning_task_id]
GO
ALTER TABLE [dbo].[cleaning_reworks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_reworks_inspection_id] FOREIGN KEY([inspection_id])
REFERENCES [dbo].[room_inspections] ([inspection_id])
GO
ALTER TABLE [dbo].[cleaning_reworks] CHECK CONSTRAINT [FK_cleaning_reworks_inspection_id]
GO
ALTER TABLE [dbo].[cleaning_tasks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_tasks_cleaning_type_id] FOREIGN KEY([cleaning_type_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[cleaning_tasks] CHECK CONSTRAINT [FK_cleaning_tasks_cleaning_type_id]
GO
ALTER TABLE [dbo].[cleaning_tasks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_tasks_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[cleaning_tasks] CHECK CONSTRAINT [FK_cleaning_tasks_created_by]
GO
ALTER TABLE [dbo].[cleaning_tasks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_tasks_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[cleaning_tasks] CHECK CONSTRAINT [FK_cleaning_tasks_priority_id]
GO
ALTER TABLE [dbo].[cleaning_tasks]  WITH CHECK ADD  CONSTRAINT [FK_cleaning_tasks_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[cleaning_tasks] CHECK CONSTRAINT [FK_cleaning_tasks_room_id]
GO
ALTER TABLE [dbo].[departments]  WITH CHECK ADD  CONSTRAINT [FK_departments_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[departments] CHECK CONSTRAINT [FK_departments_branch_id]
GO
ALTER TABLE [dbo].[departments]  WITH CHECK ADD  CONSTRAINT [FK_departments_manager_user_id] FOREIGN KEY([manager_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[departments] CHECK CONSTRAINT [FK_departments_manager_user_id]
GO
ALTER TABLE [dbo].[floors]  WITH CHECK ADD  CONSTRAINT [FK_floors_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[floors] CHECK CONSTRAINT [FK_floors_branch_id]
GO
ALTER TABLE [dbo].[incident_events]  WITH CHECK ADD  CONSTRAINT [FK_incident_events_actor_id] FOREIGN KEY([actor_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[incident_events] CHECK CONSTRAINT [FK_incident_events_actor_id]
GO
ALTER TABLE [dbo].[incident_events]  WITH CHECK ADD  CONSTRAINT [FK_incident_events_incident_id] FOREIGN KEY([incident_id])
REFERENCES [dbo].[incidents] ([incident_id])
GO
ALTER TABLE [dbo].[incident_events] CHECK CONSTRAINT [FK_incident_events_incident_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_area_id] FOREIGN KEY([area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_area_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_asset_id] FOREIGN KEY([asset_id])
REFERENCES [dbo].[assets] ([asset_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_asset_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_branch_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_priority_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_reported_by] FOREIGN KEY([reported_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_reported_by]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_room_id]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [FK_incidents_severity_id] FOREIGN KEY([severity_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [FK_incidents_severity_id]
GO
ALTER TABLE [dbo].[inspection_checklist_items]  WITH CHECK ADD  CONSTRAINT [FK_inspection_checklist_items_version_id] FOREIGN KEY([version_id])
REFERENCES [dbo].[inspection_checklist_versions] ([version_id])
GO
ALTER TABLE [dbo].[inspection_checklist_items] CHECK CONSTRAINT [FK_inspection_checklist_items_version_id]
GO
ALTER TABLE [dbo].[inspection_checklist_versions]  WITH CHECK ADD  CONSTRAINT [FK_inspection_checklist_versions_checklist_id] FOREIGN KEY([checklist_id])
REFERENCES [dbo].[inspection_checklists] ([checklist_id])
GO
ALTER TABLE [dbo].[inspection_checklist_versions] CHECK CONSTRAINT [FK_inspection_checklist_versions_checklist_id]
GO
ALTER TABLE [dbo].[inspection_checklist_versions]  WITH CHECK ADD  CONSTRAINT [FK_inspection_checklist_versions_published_by] FOREIGN KEY([published_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[inspection_checklist_versions] CHECK CONSTRAINT [FK_inspection_checklist_versions_published_by]
GO
ALTER TABLE [dbo].[inspection_checklists]  WITH CHECK ADD  CONSTRAINT [FK_inspection_checklists_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[inspection_checklists] CHECK CONSTRAINT [FK_inspection_checklists_created_by]
GO
ALTER TABLE [dbo].[inspection_checklists]  WITH CHECK ADD  CONSTRAINT [FK_inspection_checklists_room_type_id] FOREIGN KEY([room_type_id])
REFERENCES [dbo].[room_types] ([room_type_id])
GO
ALTER TABLE [dbo].[inspection_checklists] CHECK CONSTRAINT [FK_inspection_checklists_room_type_id]
GO
ALTER TABLE [dbo].[inspection_defects]  WITH CHECK ADD  CONSTRAINT [FK_inspection_defects_assigned_to] FOREIGN KEY([assigned_to])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[inspection_defects] CHECK CONSTRAINT [FK_inspection_defects_assigned_to]
GO
ALTER TABLE [dbo].[inspection_defects]  WITH CHECK ADD  CONSTRAINT [FK_inspection_defects_checklist_item_id] FOREIGN KEY([checklist_item_id])
REFERENCES [dbo].[inspection_checklist_items] ([item_id])
GO
ALTER TABLE [dbo].[inspection_defects] CHECK CONSTRAINT [FK_inspection_defects_checklist_item_id]
GO
ALTER TABLE [dbo].[inspection_defects]  WITH CHECK ADD  CONSTRAINT [FK_inspection_defects_inspection_id] FOREIGN KEY([inspection_id])
REFERENCES [dbo].[room_inspections] ([inspection_id])
GO
ALTER TABLE [dbo].[inspection_defects] CHECK CONSTRAINT [FK_inspection_defects_inspection_id]
GO
ALTER TABLE [dbo].[inspection_defects]  WITH CHECK ADD  CONSTRAINT [FK_inspection_defects_severity_id] FOREIGN KEY([severity_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[inspection_defects] CHECK CONSTRAINT [FK_inspection_defects_severity_id]
GO
ALTER TABLE [dbo].[inspection_results]  WITH CHECK ADD  CONSTRAINT [FK_inspection_results_checklist_item_id] FOREIGN KEY([checklist_item_id])
REFERENCES [dbo].[inspection_checklist_items] ([item_id])
GO
ALTER TABLE [dbo].[inspection_results] CHECK CONSTRAINT [FK_inspection_results_checklist_item_id]
GO
ALTER TABLE [dbo].[inspection_results]  WITH CHECK ADD  CONSTRAINT [FK_inspection_results_inspection_id] FOREIGN KEY([inspection_id])
REFERENCES [dbo].[room_inspections] ([inspection_id])
GO
ALTER TABLE [dbo].[inspection_results] CHECK CONSTRAINT [FK_inspection_results_inspection_id]
GO
ALTER TABLE [dbo].[internal_request_assignments]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_assignments_assigned_by] FOREIGN KEY([assigned_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[internal_request_assignments] CHECK CONSTRAINT [FK_internal_request_assignments_assigned_by]
GO
ALTER TABLE [dbo].[internal_request_assignments]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_assignments_assignee_id] FOREIGN KEY([assignee_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[internal_request_assignments] CHECK CONSTRAINT [FK_internal_request_assignments_assignee_id]
GO
ALTER TABLE [dbo].[internal_request_assignments]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_assignments_internal_request_id] FOREIGN KEY([internal_request_id])
REFERENCES [dbo].[internal_requests] ([internal_request_id])
GO
ALTER TABLE [dbo].[internal_request_assignments] CHECK CONSTRAINT [FK_internal_request_assignments_internal_request_id]
GO
ALTER TABLE [dbo].[internal_request_events]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_events_actor_id] FOREIGN KEY([actor_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[internal_request_events] CHECK CONSTRAINT [FK_internal_request_events_actor_id]
GO
ALTER TABLE [dbo].[internal_request_events]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_events_internal_request_id] FOREIGN KEY([internal_request_id])
REFERENCES [dbo].[internal_requests] ([internal_request_id])
GO
ALTER TABLE [dbo].[internal_request_events] CHECK CONSTRAINT [FK_internal_request_events_internal_request_id]
GO
ALTER TABLE [dbo].[internal_request_feedback]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_feedback_internal_request_id] FOREIGN KEY([internal_request_id])
REFERENCES [dbo].[internal_requests] ([internal_request_id])
GO
ALTER TABLE [dbo].[internal_request_feedback] CHECK CONSTRAINT [FK_internal_request_feedback_internal_request_id]
GO
ALTER TABLE [dbo].[internal_request_feedback]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_feedback_submitted_by] FOREIGN KEY([submitted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[internal_request_feedback] CHECK CONSTRAINT [FK_internal_request_feedback_submitted_by]
GO
ALTER TABLE [dbo].[internal_request_types]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_types_default_department_id] FOREIGN KEY([default_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[internal_request_types] CHECK CONSTRAINT [FK_internal_request_types_default_department_id]
GO
ALTER TABLE [dbo].[internal_request_types]  WITH CHECK ADD  CONSTRAINT [FK_internal_request_types_default_priority_id] FOREIGN KEY([default_priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[internal_request_types] CHECK CONSTRAINT [FK_internal_request_types_default_priority_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_area_id] FOREIGN KEY([area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_area_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_branch_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_priority_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_receiving_department_id] FOREIGN KEY([receiving_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_receiving_department_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_related_incident_id] FOREIGN KEY([related_incident_id])
REFERENCES [dbo].[incidents] ([incident_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_related_incident_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_request_type_id] FOREIGN KEY([request_type_id])
REFERENCES [dbo].[internal_request_types] ([request_type_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_request_type_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_requester_department_id] FOREIGN KEY([requester_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_requester_department_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_requester_id] FOREIGN KEY([requester_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_requester_id]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [FK_internal_requests_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [FK_internal_requests_room_id]
GO
ALTER TABLE [dbo].[inventory_items]  WITH CHECK ADD  CONSTRAINT [FK_inventory_items_base_uom_id] FOREIGN KEY([base_uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[inventory_items] CHECK CONSTRAINT [FK_inventory_items_base_uom_id]
GO
ALTER TABLE [dbo].[inventory_items]  WITH CHECK ADD  CONSTRAINT [FK_inventory_items_category_id] FOREIGN KEY([category_id])
REFERENCES [dbo].[item_categories] ([item_category_id])
GO
ALTER TABLE [dbo].[inventory_items] CHECK CONSTRAINT [FK_inventory_items_category_id]
GO
ALTER TABLE [dbo].[maintenance_work_logs]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_work_logs_logged_by] FOREIGN KEY([logged_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[maintenance_work_logs] CHECK CONSTRAINT [FK_maintenance_work_logs_logged_by]
GO
ALTER TABLE [dbo].[maintenance_work_logs]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_work_logs_maintenance_work_id] FOREIGN KEY([maintenance_work_id])
REFERENCES [dbo].[maintenance_works] ([maintenance_work_id])
GO
ALTER TABLE [dbo].[maintenance_work_logs] CHECK CONSTRAINT [FK_maintenance_work_logs_maintenance_work_id]
GO
ALTER TABLE [dbo].[maintenance_work_logs]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_work_logs_material_issue_id] FOREIGN KEY([material_issue_id])
REFERENCES [dbo].[stock_documents] ([document_id])
GO
ALTER TABLE [dbo].[maintenance_work_logs] CHECK CONSTRAINT [FK_maintenance_work_logs_material_issue_id]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_assignee_id] FOREIGN KEY([assignee_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_assignee_id]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_created_by]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_department_id]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_incident_id] FOREIGN KEY([incident_id])
REFERENCES [dbo].[incidents] ([incident_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_incident_id]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_priority_id]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [FK_maintenance_works_reviewed_by] FOREIGN KEY([reviewed_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [FK_maintenance_works_reviewed_by]
GO
ALTER TABLE [dbo].[material_fulfillment_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillment_lines_fulfillment_id] FOREIGN KEY([fulfillment_id])
REFERENCES [dbo].[material_fulfillments] ([fulfillment_id])
GO
ALTER TABLE [dbo].[material_fulfillment_lines] CHECK CONSTRAINT [FK_material_fulfillment_lines_fulfillment_id]
GO
ALTER TABLE [dbo].[material_fulfillment_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillment_lines_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[material_fulfillment_lines] CHECK CONSTRAINT [FK_material_fulfillment_lines_item_id]
GO
ALTER TABLE [dbo].[material_fulfillment_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillment_lines_request_line_id] FOREIGN KEY([request_line_id])
REFERENCES [dbo].[material_request_lines] ([line_id])
GO
ALTER TABLE [dbo].[material_fulfillment_lines] CHECK CONSTRAINT [FK_material_fulfillment_lines_request_line_id]
GO
ALTER TABLE [dbo].[material_fulfillment_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillment_lines_uom_id] FOREIGN KEY([uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[material_fulfillment_lines] CHECK CONSTRAINT [FK_material_fulfillment_lines_uom_id]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillments_fulfilled_by] FOREIGN KEY([fulfilled_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [FK_material_fulfillments_fulfilled_by]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillments_received_by] FOREIGN KEY([received_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [FK_material_fulfillments_received_by]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillments_request_id] FOREIGN KEY([request_id])
REFERENCES [dbo].[material_requests] ([request_id])
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [FK_material_fulfillments_request_id]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillments_stock_document_id] FOREIGN KEY([stock_document_id])
REFERENCES [dbo].[stock_documents] ([document_id])
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [FK_material_fulfillments_stock_document_id]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [FK_material_fulfillments_warehouse_id] FOREIGN KEY([warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [FK_material_fulfillments_warehouse_id]
GO
ALTER TABLE [dbo].[material_request_approvals]  WITH CHECK ADD  CONSTRAINT [FK_material_request_approvals_approver_id] FOREIGN KEY([approver_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[material_request_approvals] CHECK CONSTRAINT [FK_material_request_approvals_approver_id]
GO
ALTER TABLE [dbo].[material_request_approvals]  WITH CHECK ADD  CONSTRAINT [FK_material_request_approvals_request_id] FOREIGN KEY([request_id])
REFERENCES [dbo].[material_requests] ([request_id])
GO
ALTER TABLE [dbo].[material_request_approvals] CHECK CONSTRAINT [FK_material_request_approvals_request_id]
GO
ALTER TABLE [dbo].[material_request_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_request_lines_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[material_request_lines] CHECK CONSTRAINT [FK_material_request_lines_item_id]
GO
ALTER TABLE [dbo].[material_request_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_request_lines_request_id] FOREIGN KEY([request_id])
REFERENCES [dbo].[material_requests] ([request_id])
GO
ALTER TABLE [dbo].[material_request_lines] CHECK CONSTRAINT [FK_material_request_lines_request_id]
GO
ALTER TABLE [dbo].[material_request_lines]  WITH CHECK ADD  CONSTRAINT [FK_material_request_lines_uom_id] FOREIGN KEY([uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[material_request_lines] CHECK CONSTRAINT [FK_material_request_lines_uom_id]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [FK_material_requests_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [FK_material_requests_branch_id]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [FK_material_requests_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [FK_material_requests_department_id]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [FK_material_requests_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [FK_material_requests_priority_id]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [FK_material_requests_requester_id] FOREIGN KEY([requester_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [FK_material_requests_requester_id]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [FK_material_requests_source_warehouse_id] FOREIGN KEY([source_warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [FK_material_requests_source_warehouse_id]
GO
ALTER TABLE [dbo].[notification_recipients]  WITH CHECK ADD  CONSTRAINT [FK_notification_recipients_notification_id] FOREIGN KEY([notification_id])
REFERENCES [dbo].[notifications] ([notification_id])
GO
ALTER TABLE [dbo].[notification_recipients] CHECK CONSTRAINT [FK_notification_recipients_notification_id]
GO
ALTER TABLE [dbo].[notification_recipients]  WITH CHECK ADD  CONSTRAINT [FK_notification_recipients_user_id] FOREIGN KEY([user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[notification_recipients] CHECK CONSTRAINT [FK_notification_recipients_user_id]
GO
ALTER TABLE [dbo].[notifications]  WITH CHECK ADD  CONSTRAINT [FK_notifications_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[notifications] CHECK CONSTRAINT [FK_notifications_created_by]
GO
ALTER TABLE [dbo].[operational_task_assignments]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_assignments_assigned_by] FOREIGN KEY([assigned_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[operational_task_assignments] CHECK CONSTRAINT [FK_operational_task_assignments_assigned_by]
GO
ALTER TABLE [dbo].[operational_task_assignments]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_assignments_assignee_id] FOREIGN KEY([assignee_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[operational_task_assignments] CHECK CONSTRAINT [FK_operational_task_assignments_assignee_id]
GO
ALTER TABLE [dbo].[operational_task_assignments]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_assignments_task_id] FOREIGN KEY([task_id])
REFERENCES [dbo].[operational_tasks] ([task_id])
GO
ALTER TABLE [dbo].[operational_task_assignments] CHECK CONSTRAINT [FK_operational_task_assignments_task_id]
GO
ALTER TABLE [dbo].[operational_task_dependencies]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_dependencies_predecessor_task_id] FOREIGN KEY([predecessor_task_id])
REFERENCES [dbo].[operational_tasks] ([task_id])
GO
ALTER TABLE [dbo].[operational_task_dependencies] CHECK CONSTRAINT [FK_operational_task_dependencies_predecessor_task_id]
GO
ALTER TABLE [dbo].[operational_task_dependencies]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_dependencies_successor_task_id] FOREIGN KEY([successor_task_id])
REFERENCES [dbo].[operational_tasks] ([task_id])
GO
ALTER TABLE [dbo].[operational_task_dependencies] CHECK CONSTRAINT [FK_operational_task_dependencies_successor_task_id]
GO
ALTER TABLE [dbo].[operational_task_progress]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_progress_reported_by] FOREIGN KEY([reported_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[operational_task_progress] CHECK CONSTRAINT [FK_operational_task_progress_reported_by]
GO
ALTER TABLE [dbo].[operational_task_progress]  WITH CHECK ADD  CONSTRAINT [FK_operational_task_progress_task_id] FOREIGN KEY([task_id])
REFERENCES [dbo].[operational_tasks] ([task_id])
GO
ALTER TABLE [dbo].[operational_task_progress] CHECK CONSTRAINT [FK_operational_task_progress_task_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_area_id] FOREIGN KEY([area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_area_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_branch_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_category_id] FOREIGN KEY([category_id])
REFERENCES [dbo].[work_categories] ([category_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_category_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_created_by]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_department_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_parent_task_id] FOREIGN KEY([parent_task_id])
REFERENCES [dbo].[operational_tasks] ([task_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_parent_task_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_priority_id] FOREIGN KEY([priority_id])
REFERENCES [dbo].[reference_values] ([reference_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_priority_id]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [FK_operational_tasks_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [FK_operational_tasks_room_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_runs]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_runs_maintenance_work_id] FOREIGN KEY([maintenance_work_id])
REFERENCES [dbo].[maintenance_works] ([maintenance_work_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_runs] CHECK CONSTRAINT [FK_preventive_maintenance_runs_maintenance_work_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_runs]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_runs_performed_by] FOREIGN KEY([performed_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_runs] CHECK CONSTRAINT [FK_preventive_maintenance_runs_performed_by]
GO
ALTER TABLE [dbo].[preventive_maintenance_runs]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_runs_schedule_id] FOREIGN KEY([schedule_id])
REFERENCES [dbo].[preventive_maintenance_schedules] ([schedule_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_runs] CHECK CONSTRAINT [FK_preventive_maintenance_runs_schedule_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_schedules_area_id] FOREIGN KEY([area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] CHECK CONSTRAINT [FK_preventive_maintenance_schedules_area_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_schedules_asset_id] FOREIGN KEY([asset_id])
REFERENCES [dbo].[assets] ([asset_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] CHECK CONSTRAINT [FK_preventive_maintenance_schedules_asset_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_schedules_assigned_department_id] FOREIGN KEY([assigned_department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] CHECK CONSTRAINT [FK_preventive_maintenance_schedules_assigned_department_id]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_schedules_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] CHECK CONSTRAINT [FK_preventive_maintenance_schedules_created_by]
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules]  WITH CHECK ADD  CONSTRAINT [FK_preventive_maintenance_schedules_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[preventive_maintenance_schedules] CHECK CONSTRAINT [FK_preventive_maintenance_schedules_room_id]
GO
ALTER TABLE [dbo].[role_permissions]  WITH CHECK ADD  CONSTRAINT [FK_role_permissions_granted_by] FOREIGN KEY([granted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[role_permissions] CHECK CONSTRAINT [FK_role_permissions_granted_by]
GO
ALTER TABLE [dbo].[role_permissions]  WITH CHECK ADD  CONSTRAINT [FK_role_permissions_permission_id] FOREIGN KEY([permission_id])
REFERENCES [dbo].[permissions] ([permission_id])
GO
ALTER TABLE [dbo].[role_permissions] CHECK CONSTRAINT [FK_role_permissions_permission_id]
GO
ALTER TABLE [dbo].[role_permissions]  WITH CHECK ADD  CONSTRAINT [FK_role_permissions_role_id] FOREIGN KEY([role_id])
REFERENCES [dbo].[roles] ([role_id])
GO
ALTER TABLE [dbo].[role_permissions] CHECK CONSTRAINT [FK_role_permissions_role_id]
GO
ALTER TABLE [dbo].[room_blocks]  WITH CHECK ADD  CONSTRAINT [FK_room_blocks_blocked_by] FOREIGN KEY([blocked_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[room_blocks] CHECK CONSTRAINT [FK_room_blocks_blocked_by]
GO
ALTER TABLE [dbo].[room_blocks]  WITH CHECK ADD  CONSTRAINT [FK_room_blocks_incident_id] FOREIGN KEY([incident_id])
REFERENCES [dbo].[incidents] ([incident_id])
GO
ALTER TABLE [dbo].[room_blocks] CHECK CONSTRAINT [FK_room_blocks_incident_id]
GO
ALTER TABLE [dbo].[room_blocks]  WITH CHECK ADD  CONSTRAINT [FK_room_blocks_released_by] FOREIGN KEY([released_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[room_blocks] CHECK CONSTRAINT [FK_room_blocks_released_by]
GO
ALTER TABLE [dbo].[room_blocks]  WITH CHECK ADD  CONSTRAINT [FK_room_blocks_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[room_blocks] CHECK CONSTRAINT [FK_room_blocks_room_id]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [FK_room_inspections_cleaning_task_id] FOREIGN KEY([cleaning_task_id])
REFERENCES [dbo].[cleaning_tasks] ([cleaning_task_id])
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [FK_room_inspections_cleaning_task_id]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [FK_room_inspections_inspector_user_id] FOREIGN KEY([inspector_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [FK_room_inspections_inspector_user_id]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [FK_room_inspections_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [FK_room_inspections_room_id]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [FK_room_inspections_version_id] FOREIGN KEY([version_id])
REFERENCES [dbo].[inspection_checklist_versions] ([version_id])
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [FK_room_inspections_version_id]
GO
ALTER TABLE [dbo].[room_status_history]  WITH CHECK ADD  CONSTRAINT [FK_room_status_history_changed_by] FOREIGN KEY([changed_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[room_status_history] CHECK CONSTRAINT [FK_room_status_history_changed_by]
GO
ALTER TABLE [dbo].[room_status_history]  WITH CHECK ADD  CONSTRAINT [FK_room_status_history_room_id] FOREIGN KEY([room_id])
REFERENCES [dbo].[rooms] ([room_id])
GO
ALTER TABLE [dbo].[room_status_history] CHECK CONSTRAINT [FK_room_status_history_room_id]
GO
ALTER TABLE [dbo].[rooms]  WITH CHECK ADD  CONSTRAINT [FK_rooms_area_id] FOREIGN KEY([area_id])
REFERENCES [dbo].[areas] ([area_id])
GO
ALTER TABLE [dbo].[rooms] CHECK CONSTRAINT [FK_rooms_area_id]
GO
ALTER TABLE [dbo].[rooms]  WITH CHECK ADD  CONSTRAINT [FK_rooms_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[rooms] CHECK CONSTRAINT [FK_rooms_branch_id]
GO
ALTER TABLE [dbo].[rooms]  WITH CHECK ADD  CONSTRAINT [FK_rooms_floor_id] FOREIGN KEY([floor_id])
REFERENCES [dbo].[floors] ([floor_id])
GO
ALTER TABLE [dbo].[rooms] CHECK CONSTRAINT [FK_rooms_floor_id]
GO
ALTER TABLE [dbo].[rooms]  WITH CHECK ADD  CONSTRAINT [FK_rooms_room_type_id] FOREIGN KEY([room_type_id])
REFERENCES [dbo].[room_types] ([room_type_id])
GO
ALTER TABLE [dbo].[rooms] CHECK CONSTRAINT [FK_rooms_room_type_id]
GO
ALTER TABLE [dbo].[shift_assignments]  WITH CHECK ADD  CONSTRAINT [FK_shift_assignments_assigned_by] FOREIGN KEY([assigned_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[shift_assignments] CHECK CONSTRAINT [FK_shift_assignments_assigned_by]
GO
ALTER TABLE [dbo].[shift_assignments]  WITH CHECK ADD  CONSTRAINT [FK_shift_assignments_shift_id] FOREIGN KEY([shift_id])
REFERENCES [dbo].[work_shifts] ([shift_id])
GO
ALTER TABLE [dbo].[shift_assignments] CHECK CONSTRAINT [FK_shift_assignments_shift_id]
GO
ALTER TABLE [dbo].[shift_assignments]  WITH CHECK ADD  CONSTRAINT [FK_shift_assignments_user_id] FOREIGN KEY([user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[shift_assignments] CHECK CONSTRAINT [FK_shift_assignments_user_id]
GO
ALTER TABLE [dbo].[shift_attendance]  WITH CHECK ADD  CONSTRAINT [FK_shift_attendance_shift_assignment_id] FOREIGN KEY([shift_assignment_id])
REFERENCES [dbo].[shift_assignments] ([shift_assignment_id])
GO
ALTER TABLE [dbo].[shift_attendance] CHECK CONSTRAINT [FK_shift_attendance_shift_assignment_id]
GO
ALTER TABLE [dbo].[shift_attendance]  WITH CHECK ADD  CONSTRAINT [FK_shift_attendance_verified_by] FOREIGN KEY([verified_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[shift_attendance] CHECK CONSTRAINT [FK_shift_attendance_verified_by]
GO
ALTER TABLE [dbo].[shift_templates]  WITH CHECK ADD  CONSTRAINT [FK_shift_templates_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[shift_templates] CHECK CONSTRAINT [FK_shift_templates_branch_id]
GO
ALTER TABLE [dbo].[stock_document_lines]  WITH CHECK ADD  CONSTRAINT [FK_stock_document_lines_document_id] FOREIGN KEY([document_id])
REFERENCES [dbo].[stock_documents] ([document_id])
GO
ALTER TABLE [dbo].[stock_document_lines] CHECK CONSTRAINT [FK_stock_document_lines_document_id]
GO
ALTER TABLE [dbo].[stock_document_lines]  WITH CHECK ADD  CONSTRAINT [FK_stock_document_lines_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[stock_document_lines] CHECK CONSTRAINT [FK_stock_document_lines_item_id]
GO
ALTER TABLE [dbo].[stock_document_lines]  WITH CHECK ADD  CONSTRAINT [FK_stock_document_lines_uom_id] FOREIGN KEY([uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[stock_document_lines] CHECK CONSTRAINT [FK_stock_document_lines_uom_id]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_approved_by] FOREIGN KEY([approved_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_approved_by]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_created_by]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_destination_warehouse_id] FOREIGN KEY([destination_warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_destination_warehouse_id]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_posted_by] FOREIGN KEY([posted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_posted_by]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_reversal_of_document_id] FOREIGN KEY([reversal_of_document_id])
REFERENCES [dbo].[stock_documents] ([document_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_reversal_of_document_id]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [FK_stock_documents_source_warehouse_id] FOREIGN KEY([source_warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [FK_stock_documents_source_warehouse_id]
GO
ALTER TABLE [dbo].[stock_movements]  WITH CHECK ADD  CONSTRAINT [FK_stock_movements_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stock_movements] CHECK CONSTRAINT [FK_stock_movements_created_by]
GO
ALTER TABLE [dbo].[stock_movements]  WITH CHECK ADD  CONSTRAINT [FK_stock_movements_document_line_id] FOREIGN KEY([document_line_id])
REFERENCES [dbo].[stock_document_lines] ([line_id])
GO
ALTER TABLE [dbo].[stock_movements] CHECK CONSTRAINT [FK_stock_movements_document_line_id]
GO
ALTER TABLE [dbo].[stock_movements]  WITH CHECK ADD  CONSTRAINT [FK_stock_movements_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[stock_movements] CHECK CONSTRAINT [FK_stock_movements_item_id]
GO
ALTER TABLE [dbo].[stock_movements]  WITH CHECK ADD  CONSTRAINT [FK_stock_movements_warehouse_id] FOREIGN KEY([warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[stock_movements] CHECK CONSTRAINT [FK_stock_movements_warehouse_id]
GO
ALTER TABLE [dbo].[stock_thresholds]  WITH CHECK ADD  CONSTRAINT [FK_stock_thresholds_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[stock_thresholds] CHECK CONSTRAINT [FK_stock_thresholds_item_id]
GO
ALTER TABLE [dbo].[stock_thresholds]  WITH CHECK ADD  CONSTRAINT [FK_stock_thresholds_warehouse_id] FOREIGN KEY([warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[stock_thresholds] CHECK CONSTRAINT [FK_stock_thresholds_warehouse_id]
GO
ALTER TABLE [dbo].[stocktake_lines]  WITH CHECK ADD  CONSTRAINT [FK_stocktake_lines_counted_by] FOREIGN KEY([counted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stocktake_lines] CHECK CONSTRAINT [FK_stocktake_lines_counted_by]
GO
ALTER TABLE [dbo].[stocktake_lines]  WITH CHECK ADD  CONSTRAINT [FK_stocktake_lines_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[stocktake_lines] CHECK CONSTRAINT [FK_stocktake_lines_item_id]
GO
ALTER TABLE [dbo].[stocktake_lines]  WITH CHECK ADD  CONSTRAINT [FK_stocktake_lines_stocktake_id] FOREIGN KEY([stocktake_id])
REFERENCES [dbo].[stocktakes] ([stocktake_id])
GO
ALTER TABLE [dbo].[stocktake_lines] CHECK CONSTRAINT [FK_stocktake_lines_stocktake_id]
GO
ALTER TABLE [dbo].[stocktakes]  WITH CHECK ADD  CONSTRAINT [FK_stocktakes_adjustment_document_id] FOREIGN KEY([adjustment_document_id])
REFERENCES [dbo].[stock_documents] ([document_id])
GO
ALTER TABLE [dbo].[stocktakes] CHECK CONSTRAINT [FK_stocktakes_adjustment_document_id]
GO
ALTER TABLE [dbo].[stocktakes]  WITH CHECK ADD  CONSTRAINT [FK_stocktakes_approved_by] FOREIGN KEY([approved_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stocktakes] CHECK CONSTRAINT [FK_stocktakes_approved_by]
GO
ALTER TABLE [dbo].[stocktakes]  WITH CHECK ADD  CONSTRAINT [FK_stocktakes_counted_by] FOREIGN KEY([counted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[stocktakes] CHECK CONSTRAINT [FK_stocktakes_counted_by]
GO
ALTER TABLE [dbo].[stocktakes]  WITH CHECK ADD  CONSTRAINT [FK_stocktakes_warehouse_id] FOREIGN KEY([warehouse_id])
REFERENCES [dbo].[warehouses] ([warehouse_id])
GO
ALTER TABLE [dbo].[stocktakes] CHECK CONSTRAINT [FK_stocktakes_warehouse_id]
GO
ALTER TABLE [dbo].[system_settings]  WITH CHECK ADD  CONSTRAINT [FK_system_settings_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[system_settings] CHECK CONSTRAINT [FK_system_settings_branch_id]
GO
ALTER TABLE [dbo].[system_settings]  WITH CHECK ADD  CONSTRAINT [FK_system_settings_updated_by] FOREIGN KEY([updated_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[system_settings] CHECK CONSTRAINT [FK_system_settings_updated_by]
GO
ALTER TABLE [dbo].[uom_conversions]  WITH CHECK ADD  CONSTRAINT [FK_uom_conversions_from_uom_id] FOREIGN KEY([from_uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[uom_conversions] CHECK CONSTRAINT [FK_uom_conversions_from_uom_id]
GO
ALTER TABLE [dbo].[uom_conversions]  WITH CHECK ADD  CONSTRAINT [FK_uom_conversions_item_id] FOREIGN KEY([item_id])
REFERENCES [dbo].[inventory_items] ([item_id])
GO
ALTER TABLE [dbo].[uom_conversions] CHECK CONSTRAINT [FK_uom_conversions_item_id]
GO
ALTER TABLE [dbo].[uom_conversions]  WITH CHECK ADD  CONSTRAINT [FK_uom_conversions_to_uom_id] FOREIGN KEY([to_uom_id])
REFERENCES [dbo].[units_of_measure] ([uom_id])
GO
ALTER TABLE [dbo].[uom_conversions] CHECK CONSTRAINT [FK_uom_conversions_to_uom_id]
GO
ALTER TABLE [dbo].[user_branch_access]  WITH CHECK ADD  CONSTRAINT [FK_user_branch_access_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[user_branch_access] CHECK CONSTRAINT [FK_user_branch_access_branch_id]
GO
ALTER TABLE [dbo].[user_branch_access]  WITH CHECK ADD  CONSTRAINT [FK_user_branch_access_granted_by] FOREIGN KEY([granted_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[user_branch_access] CHECK CONSTRAINT [FK_user_branch_access_granted_by]
GO
ALTER TABLE [dbo].[user_branch_access]  WITH CHECK ADD  CONSTRAINT [FK_user_branch_access_user_id] FOREIGN KEY([user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[user_branch_access] CHECK CONSTRAINT [FK_user_branch_access_user_id]
GO
ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_assigned_by] FOREIGN KEY([assigned_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_assigned_by]
GO
ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_branch_id]
GO
ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_role_id] FOREIGN KEY([role_id])
REFERENCES [dbo].[roles] ([role_id])
GO
ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_role_id]
GO
ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_user_id] FOREIGN KEY([user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_user_id]
GO
ALTER TABLE [dbo].[user_sessions]  WITH CHECK ADD  CONSTRAINT [FK_user_sessions_user_id] FOREIGN KEY([user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[user_sessions] CHECK CONSTRAINT [FK_user_sessions_user_id]
GO
ALTER TABLE [dbo].[users]  WITH CHECK ADD  CONSTRAINT [FK_users_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[users] CHECK CONSTRAINT [FK_users_department_id]
GO
ALTER TABLE [dbo].[users]  WITH CHECK ADD  CONSTRAINT [FK_users_manager_user_id] FOREIGN KEY([manager_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[users] CHECK CONSTRAINT [FK_users_manager_user_id]
GO
ALTER TABLE [dbo].[users]  WITH CHECK ADD  CONSTRAINT [FK_users_primary_branch_id] FOREIGN KEY([primary_branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[users] CHECK CONSTRAINT [FK_users_primary_branch_id]
GO
ALTER TABLE [dbo].[warehouses]  WITH CHECK ADD  CONSTRAINT [FK_warehouses_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[warehouses] CHECK CONSTRAINT [FK_warehouses_branch_id]
GO
ALTER TABLE [dbo].[warehouses]  WITH CHECK ADD  CONSTRAINT [FK_warehouses_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[warehouses] CHECK CONSTRAINT [FK_warehouses_department_id]
GO
ALTER TABLE [dbo].[warehouses]  WITH CHECK ADD  CONSTRAINT [FK_warehouses_manager_user_id] FOREIGN KEY([manager_user_id])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[warehouses] CHECK CONSTRAINT [FK_warehouses_manager_user_id]
GO
ALTER TABLE [dbo].[work_categories]  WITH CHECK ADD  CONSTRAINT [FK_work_categories_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[work_categories] CHECK CONSTRAINT [FK_work_categories_department_id]
GO
ALTER TABLE [dbo].[work_shifts]  WITH CHECK ADD  CONSTRAINT [FK_work_shifts_branch_id] FOREIGN KEY([branch_id])
REFERENCES [dbo].[branches] ([branch_id])
GO
ALTER TABLE [dbo].[work_shifts] CHECK CONSTRAINT [FK_work_shifts_branch_id]
GO
ALTER TABLE [dbo].[work_shifts]  WITH CHECK ADD  CONSTRAINT [FK_work_shifts_created_by] FOREIGN KEY([created_by])
REFERENCES [dbo].[users] ([user_id])
GO
ALTER TABLE [dbo].[work_shifts] CHECK CONSTRAINT [FK_work_shifts_created_by]
GO
ALTER TABLE [dbo].[work_shifts]  WITH CHECK ADD  CONSTRAINT [FK_work_shifts_department_id] FOREIGN KEY([department_id])
REFERENCES [dbo].[departments] ([department_id])
GO
ALTER TABLE [dbo].[work_shifts] CHECK CONSTRAINT [FK_work_shifts_department_id]
GO
ALTER TABLE [dbo].[work_shifts]  WITH CHECK ADD  CONSTRAINT [FK_work_shifts_shift_template_id] FOREIGN KEY([shift_template_id])
REFERENCES [dbo].[shift_templates] ([shift_template_id])
GO
ALTER TABLE [dbo].[work_shifts] CHECK CONSTRAINT [FK_work_shifts_shift_template_id]
GO
ALTER TABLE [dbo].[attachments]  WITH CHECK ADD  CONSTRAINT [CK_attachments_entity_type] CHECK  (([entity_type]='TASK' OR [entity_type]='REQUEST' OR [entity_type]='STOCK_DOCUMENT' OR [entity_type]='ASSET' OR [entity_type]='MAINTENANCE' OR [entity_type]='INCIDENT' OR [entity_type]='INSPECTION' OR [entity_type]='CLEANING_TASK' OR [entity_type]='ROOM'))
GO
ALTER TABLE [dbo].[attachments] CHECK CONSTRAINT [CK_attachments_entity_type]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [CK_audit_logs_action] CHECK  (([action]='LOGOUT' OR [action]='LOGIN' OR [action]='CANCEL' OR [action]='REJECT' OR [action]='APPROVE' OR [action]='DELETE' OR [action]='UPDATE' OR [action]='CREATE'))
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [CK_audit_logs_action]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [CK_audit_logs_changed_fields] CHECK  (([changed_fields] IS NULL OR isjson([changed_fields])=(1)))
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [CK_audit_logs_changed_fields]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [CK_audit_logs_new_values] CHECK  (([new_values] IS NULL OR isjson([new_values])=(1)))
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [CK_audit_logs_new_values]
GO
ALTER TABLE [dbo].[audit_logs]  WITH CHECK ADD  CONSTRAINT [CK_audit_logs_old_values] CHECK  (([old_values] IS NULL OR isjson([old_values])=(1)))
GO
ALTER TABLE [dbo].[audit_logs] CHECK CONSTRAINT [CK_audit_logs_old_values]
GO
ALTER TABLE [dbo].[cleaning_assignments]  WITH CHECK ADD  CONSTRAINT [CK_cleaning_assignments_status] CHECK  (([assignment_status]='CANCELLED' OR [assignment_status]='REASSIGNED' OR [assignment_status]='COMPLETED' OR [assignment_status]='ACTIVE'))
GO
ALTER TABLE [dbo].[cleaning_assignments] CHECK CONSTRAINT [CK_cleaning_assignments_status]
GO
ALTER TABLE [dbo].[cleaning_progress_logs]  WITH CHECK ADD  CONSTRAINT [CK_cleaning_progress_logs_percent] CHECK  (([progress_percent]>=(0) AND [progress_percent]<=(100)))
GO
ALTER TABLE [dbo].[cleaning_progress_logs] CHECK CONSTRAINT [CK_cleaning_progress_logs_percent]
GO
ALTER TABLE [dbo].[cleaning_progress_logs]  WITH CHECK ADD  CONSTRAINT [CK_cleaning_progress_logs_status] CHECK  (([status] IS NULL OR ([status]='COMPLETED' OR [status]='BLOCKED' OR [status]='IN_PROGRESS')))
GO
ALTER TABLE [dbo].[cleaning_progress_logs] CHECK CONSTRAINT [CK_cleaning_progress_logs_status]
GO
ALTER TABLE [dbo].[cleaning_reworks]  WITH CHECK ADD  CONSTRAINT [CK_cleaning_reworks_status] CHECK  (([status]='CANCELLED' OR [status]='COMPLETED' OR [status]='IN_PROGRESS' OR [status]='PENDING'))
GO
ALTER TABLE [dbo].[cleaning_reworks] CHECK CONSTRAINT [CK_cleaning_reworks_status]
GO
ALTER TABLE [dbo].[cleaning_tasks]  WITH CHECK ADD  CONSTRAINT [CK_cleaning_tasks_status] CHECK  (([status]='CANCELLED' OR [status]='COMPLETED' OR [status]='IN_PROGRESS' OR [status]='PENDING'))
GO
ALTER TABLE [dbo].[cleaning_tasks] CHECK CONSTRAINT [CK_cleaning_tasks_status]
GO
ALTER TABLE [dbo].[incident_events]  WITH CHECK ADD  CONSTRAINT [CK_incident_events_to_status] CHECK  (([to_status] IS NULL OR ([to_status]='CANCELLED' OR [to_status]='CLOSED' OR [to_status]='RESOLVED' OR [to_status]='IN_PROGRESS' OR [to_status]='OPEN')))
GO
ALTER TABLE [dbo].[incident_events] CHECK CONSTRAINT [CK_incident_events_to_status]
GO
ALTER TABLE [dbo].[incidents]  WITH CHECK ADD  CONSTRAINT [CK_incidents_status] CHECK  (([status]='CANCELLED' OR [status]='CLOSED' OR [status]='RESOLVED' OR [status]='IN_PROGRESS' OR [status]='OPEN'))
GO
ALTER TABLE [dbo].[incidents] CHECK CONSTRAINT [CK_incidents_status]
GO
ALTER TABLE [dbo].[inspection_defects]  WITH CHECK ADD  CONSTRAINT [CK_inspection_defects_status] CHECK  (([status]='CANCELLED' OR [status]='RESOLVED' OR [status]='IN_PROGRESS' OR [status]='OPEN'))
GO
ALTER TABLE [dbo].[inspection_defects] CHECK CONSTRAINT [CK_inspection_defects_status]
GO
ALTER TABLE [dbo].[inspection_results]  WITH CHECK ADD  CONSTRAINT [CK_inspection_results_outcome] CHECK  (([outcome]='NA' OR [outcome]='FAIL' OR [outcome]='PASS'))
GO
ALTER TABLE [dbo].[inspection_results] CHECK CONSTRAINT [CK_inspection_results_outcome]
GO
ALTER TABLE [dbo].[internal_request_assignments]  WITH CHECK ADD  CONSTRAINT [CK_internal_request_assignments_status] CHECK  (([status]='CANCELLED' OR [status]='REASSIGNED' OR [status]='COMPLETED' OR [status]='ACTIVE'))
GO
ALTER TABLE [dbo].[internal_request_assignments] CHECK CONSTRAINT [CK_internal_request_assignments_status]
GO
ALTER TABLE [dbo].[internal_request_feedback]  WITH CHECK ADD  CONSTRAINT [CK_internal_request_feedback_rating] CHECK  (([rating]>=(1) AND [rating]<=(5)))
GO
ALTER TABLE [dbo].[internal_request_feedback] CHECK CONSTRAINT [CK_internal_request_feedback_rating]
GO
ALTER TABLE [dbo].[internal_requests]  WITH CHECK ADD  CONSTRAINT [CK_internal_requests_status] CHECK  (([status]='CANCELLED' OR [status]='REJECTED' OR [status]='CLOSED' OR [status]='COMPLETED' OR [status]='IN_PROGRESS' OR [status]='SUBMITTED' OR [status]='DRAFT'))
GO
ALTER TABLE [dbo].[internal_requests] CHECK CONSTRAINT [CK_internal_requests_status]
GO
ALTER TABLE [dbo].[maintenance_work_logs]  WITH CHECK ADD  CONSTRAINT [CK_maintenance_work_logs_status] CHECK  (([status] IS NULL OR ([status]='COMPLETED' OR [status]='ON_HOLD' OR [status]='IN_PROGRESS')))
GO
ALTER TABLE [dbo].[maintenance_work_logs] CHECK CONSTRAINT [CK_maintenance_work_logs_status]
GO
ALTER TABLE [dbo].[maintenance_works]  WITH CHECK ADD  CONSTRAINT [CK_maintenance_works_status] CHECK  (([status]='CANCELLED' OR [status]='CLOSED' OR [status]='COMPLETED' OR [status]='ON_HOLD' OR [status]='IN_PROGRESS' OR [status]='PENDING'))
GO
ALTER TABLE [dbo].[maintenance_works] CHECK CONSTRAINT [CK_maintenance_works_status]
GO
ALTER TABLE [dbo].[material_fulfillments]  WITH CHECK ADD  CONSTRAINT [CK_material_fulfillments_status] CHECK  (([status]='CANCELLED' OR [status]='RECEIVED' OR [status]='ISSUED' OR [status]='PREPARING'))
GO
ALTER TABLE [dbo].[material_fulfillments] CHECK CONSTRAINT [CK_material_fulfillments_status]
GO
ALTER TABLE [dbo].[material_request_approvals]  WITH CHECK ADD  CONSTRAINT [CK_material_request_approvals_decision] CHECK  (([decision]='RETURNED' OR [decision]='REJECTED' OR [decision]='APPROVED'))
GO
ALTER TABLE [dbo].[material_request_approvals] CHECK CONSTRAINT [CK_material_request_approvals_decision]
GO
ALTER TABLE [dbo].[material_requests]  WITH CHECK ADD  CONSTRAINT [CK_material_requests_status] CHECK  (([status]='CANCELLED' OR [status]='REJECTED' OR [status]='COMPLETED' OR [status]='PARTIALLY_FULFILLED' OR [status]='APPROVED' OR [status]='RETURNED' OR [status]='SUBMITTED' OR [status]='DRAFT'))
GO
ALTER TABLE [dbo].[material_requests] CHECK CONSTRAINT [CK_material_requests_status]
GO
ALTER TABLE [dbo].[operational_task_assignments]  WITH CHECK ADD  CONSTRAINT [CK_operational_task_assignments_status] CHECK  (([assignment_status]='CANCELLED' OR [assignment_status]='REASSIGNED' OR [assignment_status]='COMPLETED' OR [assignment_status]='ACTIVE'))
GO
ALTER TABLE [dbo].[operational_task_assignments] CHECK CONSTRAINT [CK_operational_task_assignments_status]
GO
ALTER TABLE [dbo].[operational_task_progress]  WITH CHECK ADD  CONSTRAINT [CK_operational_task_progress_new] CHECK  (([new_status] IS NULL OR ([new_status]='CANCELLED' OR [new_status]='COMPLETED' OR [new_status]='BLOCKED' OR [new_status]='IN_PROGRESS' OR [new_status]='TODO')))
GO
ALTER TABLE [dbo].[operational_task_progress] CHECK CONSTRAINT [CK_operational_task_progress_new]
GO
ALTER TABLE [dbo].[operational_task_progress]  WITH CHECK ADD  CONSTRAINT [CK_operational_task_progress_old] CHECK  (([old_status] IS NULL OR ([old_status]='CANCELLED' OR [old_status]='COMPLETED' OR [old_status]='BLOCKED' OR [old_status]='IN_PROGRESS' OR [old_status]='TODO')))
GO
ALTER TABLE [dbo].[operational_task_progress] CHECK CONSTRAINT [CK_operational_task_progress_old]
GO
ALTER TABLE [dbo].[operational_task_progress]  WITH CHECK ADD  CONSTRAINT [CK_operational_task_progress_percent] CHECK  (([progress_percent]>=(0) AND [progress_percent]<=(100)))
GO
ALTER TABLE [dbo].[operational_task_progress] CHECK CONSTRAINT [CK_operational_task_progress_percent]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [CK_operational_tasks_progress] CHECK  (([progress_percent]>=(0) AND [progress_percent]<=(100)))
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [CK_operational_tasks_progress]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [CK_operational_tasks_source_type] CHECK  (([source_type] IS NULL OR ([source_type]='MANUAL' OR [source_type]='INTERNAL_REQUEST' OR [source_type]='MAINTENANCE' OR [source_type]='INSPECTION' OR [source_type]='CLEANING')))
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [CK_operational_tasks_source_type]
GO
ALTER TABLE [dbo].[operational_tasks]  WITH CHECK ADD  CONSTRAINT [CK_operational_tasks_status] CHECK  (([status]='CANCELLED' OR [status]='COMPLETED' OR [status]='BLOCKED' OR [status]='IN_PROGRESS' OR [status]='TODO'))
GO
ALTER TABLE [dbo].[operational_tasks] CHECK CONSTRAINT [CK_operational_tasks_status]
GO
ALTER TABLE [dbo].[role_permissions]  WITH CHECK ADD  CONSTRAINT [CK_role_permissions_data_scope] CHECK  (([data_scope]='ALL' OR [data_scope]='BRANCH' OR [data_scope]='DEPARTMENT' OR [data_scope]='OWN'))
GO
ALTER TABLE [dbo].[role_permissions] CHECK CONSTRAINT [CK_role_permissions_data_scope]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [CK_room_inspections_result] CHECK  (([result] IS NULL OR ([result]='CONDITIONAL' OR [result]='FAIL' OR [result]='PASS')))
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [CK_room_inspections_result]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [CK_room_inspections_status] CHECK  (([status]='CANCELLED' OR [status]='COMPLETED' OR [status]='IN_PROGRESS' OR [status]='PENDING'))
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [CK_room_inspections_status]
GO
ALTER TABLE [dbo].[room_inspections]  WITH CHECK ADD  CONSTRAINT [CK_room_inspections_status_result] CHECK  (([status]='COMPLETED' AND [result] IS NOT NULL OR [status]<>'COMPLETED' AND [result] IS NULL))
GO
ALTER TABLE [dbo].[room_inspections] CHECK CONSTRAINT [CK_room_inspections_status_result]
GO
ALTER TABLE [dbo].[room_status_history]  WITH CHECK ADD  CONSTRAINT [CK_room_status_history_from] CHECK  (([from_status] IS NULL OR ([from_status]='OUT_OF_SERVICE' OR [from_status]='READY' OR [from_status]='INSPECTION' OR [from_status]='CLEANING' OR [from_status]='DIRTY' OR [from_status]='OCCUPIED' OR [from_status]='AVAILABLE')))
GO
ALTER TABLE [dbo].[room_status_history] CHECK CONSTRAINT [CK_room_status_history_from]
GO
ALTER TABLE [dbo].[room_status_history]  WITH CHECK ADD  CONSTRAINT [CK_room_status_history_to] CHECK  (([to_status]='OUT_OF_SERVICE' OR [to_status]='READY' OR [to_status]='INSPECTION' OR [to_status]='CLEANING' OR [to_status]='DIRTY' OR [to_status]='OCCUPIED' OR [to_status]='AVAILABLE'))
GO
ALTER TABLE [dbo].[room_status_history] CHECK CONSTRAINT [CK_room_status_history_to]
GO
ALTER TABLE [dbo].[room_status_history]  WITH CHECK ADD  CONSTRAINT [CK_room_status_history_trigger_type] CHECK  (([trigger_type] IS NULL OR ([trigger_type]='MANUAL_CORRECTION' OR [trigger_type]='MAINTENANCE' OR [trigger_type]='ROOM_BLOCK' OR [trigger_type]='INSPECTION' OR [trigger_type]='CLEANING_TASK' OR [trigger_type]='CHECKOUT' OR [trigger_type]='CHECKIN' OR [trigger_type]='RELEASE' OR [trigger_type]='INIT')))
GO
ALTER TABLE [dbo].[room_status_history] CHECK CONSTRAINT [CK_room_status_history_trigger_type]
GO
ALTER TABLE [dbo].[rooms]  WITH CHECK ADD  CONSTRAINT [CK_rooms_operational_status] CHECK  (([operational_status]='OUT_OF_SERVICE' OR [operational_status]='READY' OR [operational_status]='INSPECTION' OR [operational_status]='CLEANING' OR [operational_status]='DIRTY' OR [operational_status]='OCCUPIED' OR [operational_status]='AVAILABLE'))
GO
ALTER TABLE [dbo].[rooms] CHECK CONSTRAINT [CK_rooms_operational_status]
GO
ALTER TABLE [dbo].[stock_documents]  WITH CHECK ADD  CONSTRAINT [CK_stock_documents_type] CHECK  (([document_type]='REVERSAL' OR [document_type]='ADJUSTMENT' OR [document_type]='TRANSFER' OR [document_type]='RETURN' OR [document_type]='ISSUE' OR [document_type]='RECEIPT'))
GO
ALTER TABLE [dbo].[stock_documents] CHECK CONSTRAINT [CK_stock_documents_type]
GO
ALTER TABLE [dbo].[users]  WITH CHECK ADD  CONSTRAINT [CK_users_failed_login_count] CHECK  (([failed_login_count]>=(0)))
GO
ALTER TABLE [dbo].[users] CHECK CONSTRAINT [CK_users_failed_login_count]
GO
ALTER TABLE [dbo].[users]  WITH CHECK ADD  CONSTRAINT [CK_users_manager_not_self] CHECK  (([manager_user_id] IS NULL OR [manager_user_id]<>[user_id]))
GO
ALTER TABLE [dbo].[users] CHECK CONSTRAINT [CK_users_manager_not_self]
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'ROOM, CLEANING_TASK, INSPECTION, INCIDENT, MAINTENANCE, ASSET, STOCK_DOCUMENT, REQUEST, TASK' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'attachments', @level2type=N'COLUMN',@level2name=N'entity_type'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'CREATE, UPDATE, DELETE, APPROVE, REJECT, CANCEL, LOGIN, LOGOUT' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'audit_logs', @level2type=N'COLUMN',@level2name=N'action'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'Valid range: 0-100' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'cleaning_progress_logs', @level2type=N'COLUMN',@level2name=N'progress_percent'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'PASS, FAIL, NA' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'inspection_results', @level2type=N'COLUMN',@level2name=N'outcome'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'Valid range: 1-5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'internal_request_feedback', @level2type=N'COLUMN',@level2name=N'rating'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'APPROVED, REJECTED, RETURNED' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'material_request_approvals', @level2type=N'COLUMN',@level2name=N'decision'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'CLEANING, INSPECTION, MAINTENANCE, INTERNAL_REQUEST, MANUAL' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'operational_tasks', @level2type=N'COLUMN',@level2name=N'source_type'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'PASS, FAIL, CONDITIONAL' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'room_inspections', @level2type=N'COLUMN',@level2name=N'result'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'AVAILABLE, OCCUPIED, DIRTY, CLEANING, INSPECTION, READY, OUT_OF_SERVICE' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'rooms', @level2type=N'COLUMN',@level2name=N'operational_status'
GO
EXEC sys.sp_addextendedproperty @name=N'Column_Description', @value=N'RECEIPT, ISSUE, RETURN, TRANSFER, ADJUSTMENT, REVERSAL' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'stock_documents', @level2type=N'COLUMN',@level2name=N'document_type'
GO

PRINT N'HotelOps schema installation completed. Run 03_HotelOps_RBAC_Catalog_Seed.sql next.';
GO
SET NOEXEC OFF;
GO
