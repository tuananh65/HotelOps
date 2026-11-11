-- OWNER007 REVIEW ONLY: all rights below are comments, never executable.
THROW 51990, 'Owner-approved exact permission installer required; this is review-only', 1;
-- No GO batches. No DENY removal, schema grants, login/user creation or execution authority.

-- Original ordinal 1; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- SQL session eligibility and employment/account state in users; no separate employee table in these 56 entries. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.users TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 2; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Real session lookup/revocation validation, not native procedure authorization. Session creation/logout UPDATE rights are outside this proposal. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.user_sessions TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 3; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Actor/recipient branch membership read; ordinary procedure ownership chain cannot cover this EF query. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.user_branch_access TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 4; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Permission assignment path and actor/recipient role eligibility. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.user_roles TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 5; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Role navigation participates in generated recipient permission SQL. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.roles TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 6; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Recipient CLEANING_VIEW grant path. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.role_permissions TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 7; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Permission navigation emitted in recipient query. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.permissions TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 8; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Direct branch active-state lock/read and source context. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.branches TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 9; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Assignee eligibility traverses department and branch, outside native proc. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.departments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 10; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Persisted assignment source/room branch and graph audit consistency. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.rooms TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 11; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Persisted room/floor activity and branch consistency. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.floors TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 12; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Optional room area must belong to same branch/floor and be active. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.areas TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 13; APP_AUTH_SOURCE_READ; HOLD_UNTIL_NATIVE_CALL_PATH_CONFIRMED
-- Room-type eligibility/planning navigation; exact native acceptance path must confirm this extra read before grant. Required direct C# read must not be removed just because module-internal reads can chain.

-- Original ordinal 14; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Housekeeper current shift eligibility before native mutation. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.shift_assignments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 15; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Shift navigation for current branch/department and planned time interval. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.work_shifts TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 16; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Shift template navigation checks active and branch. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.shift_templates TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 17; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Actual caller source/revision/current workflow verification, independent of native proc reads. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.cleaning_tasks TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 18; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Persisted assignment source/current assignee and replay verification. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.cleaning_assignments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 19; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Current persisted room cleaning cycle in source verification. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.room_cleaning_cycle_state TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 20; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Graph source audit evidence read, while native proc creates audit. No grant for direct audit inserts proposed. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.audit_logs TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 21; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Operation immutable fingerprint/result/completed replay and range lock. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.notification_operation_reservations TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 22; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Single existing operation publication and immutable payload/source revision. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.notification_publications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 23; APP_AUTH_SOURCE_READ; RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY
-- Native receipt mandatory recipient/channel cardinality; SELECT not solely delivery. Required direct C# read must not be removed just because module-internal reads can chain.
-- GRANT SELECT ON OBJECT::dbo.notification_delivery_intents TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 24; NATIVE_EXECUTION; RETAIN_SCOPED_EXECUTE
-- Object-scoped EXECUTE; underlying table reads do not imply direct SELECT.
-- GRANT EXECUTE ON OBJECT::dbo.fs013_assign_with_evidence TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 25; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 26; NATIVE_EXECUTION; RETAIN_SCOPED_EXECUTE
-- Object-scoped EXECUTE; underlying table reads do not imply direct SELECT.
-- GRANT EXECUTE ON OBJECT::dbo.fs009_verify_assignment_guard TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 27; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 28; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY INSERT ON OBJECT::dbo.notification_operation_reservations TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 29; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY DELETE ON OBJECT::dbo.notification_operation_reservations TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 30; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY UPDATE ON OBJECT::dbo.notification_operation_reservations TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 31; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 32; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY INSERT ON OBJECT::dbo.notification_publications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 33; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY DELETE ON OBJECT::dbo.notification_publications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 34; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY UPDATE ON OBJECT::dbo.notification_publications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 35; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 36; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY INSERT ON OBJECT::dbo.notification_delivery_intents TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 37; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY DELETE ON OBJECT::dbo.notification_delivery_intents TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 38; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 39; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY INSERT ON OBJECT::dbo.cleaning_assignments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 40; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY DELETE ON OBJECT::dbo.cleaning_assignments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 41; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY UPDATE ON OBJECT::dbo.cleaning_assignments TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 42; PREFLIGHT_METADATA; SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL
-- Object metadata visibility proposal is not proof that restricted actual-GUID/principal/TLS/catalog observation will succeed. Owner must accept exact observer and minimal metadata visibility, no server-wide elevation.

-- Original ordinal 43; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY UPDATE ON OBJECT::dbo.audit_logs TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 44; ALL_PROFILES_PROTECTIVE_DENY; RETAIN_DENY
-- Preserve direct caller DML prohibition. Static same-owner dbo module may perform its owned DML; no DENY removal or direct EF fallback.
-- DENY DELETE ON OBJECT::dbo.audit_logs TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 45; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT SELECT ON OBJECT::dbo.notifications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 46; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT INSERT ON OBJECT::dbo.notifications TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 47; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT SELECT ON OBJECT::dbo.notification_recipients TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 48; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT INSERT ON OBJECT::dbo.notification_recipients TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 49; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_recipients ([read_at]) TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 50; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_delivery_intents ([status]) TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 51; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_delivery_intents ([attempt_count]) TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 52; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_delivery_intents ([next_attempt_at]) TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 53; OPTIONAL_DELIVERY_INBOX; OMIT_UNPROVEN_COLUMN_GRANT
-- No write to this column in pinned CleaningNotificationRuntime. Future claims/retry require distinct reviewed implementation and rights.

-- Original ordinal 54; OPTIONAL_DELIVERY_INBOX; OMIT_UNPROVEN_COLUMN_GRANT
-- No write to this column in pinned CleaningNotificationRuntime. Future claims/retry require distinct reviewed implementation and rights.

-- Original ordinal 55; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_delivery_intents ([failure_code]) TO [HotelOps_DEV2_FullStack_Runtime];

-- Original ordinal 56; OPTIONAL_DELIVERY_INBOX; EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL
-- Only explicit contained delivery or Inbox capability may use this right. No worker, producer or retry activation.
-- GRANT UPDATE ON OBJECT::dbo.notification_delivery_intents ([inbox_notification_id]) TO [HotelOps_DEV2_FullStack_Runtime];
