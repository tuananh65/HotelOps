# [HOTELOPS DEV2] FE04-NOTIF-OWNER-DECISION-007

D03=READY_FOR_OWNER_APPROVAL.56/56 entries reviewed individually in D03_PERMISSION_MATRIX_56.json with original ordinal/pointer/content, principal, direct/inherited qualification, consumer, native access, conditional chaining, DENY, proposal and exact source lines/hash. Original has no entry IDs/reasons; null retains that fact rather than fabricating them.43 original allows,13 original DENYs.

Native EXECUTE ordinals24/26 are required. Native module static dbo chaining can cover its own underlying reads/writes when actual ownership/module scope is accepted; it cannot authorize separate C# queries. No table is classified chain-only because proven C# reads also need independent access, or relevance is explicitly unresolved. Auth/session/user/role tables are not read by native proc: their direct reads belong to application authorization. Employment state is on users; no extra employee permission is invented.

22 direct application SELECTs retained separately from native execution. room_types ordinal13 held for exact-path confirmation. Six object metadata rights held for scoped observer approval. Ten Delivery/Inbox rights separated, including ordinal52 next_attempt_at: SetRead performs a self-update to obtain rowversion and needs it. Two unproven claim fields53/54 omitted. All13 DENYs retained. Effective deployed rights/owners/signing/cross-database exceptions NOT_OBSERVED.

D03_NarrowedPermissions.review.sql begins unconditional THROW and all proposed statements are comments, with no GO. No permission execution. Run offline: node scripts/verification/NotifOwner007/review-permissions.cjs <exact-FE04-git-archive-directory>. D03_PERMISSION_NARROWING.json contains all original-vs-proposed rows and unresolved exceptions. Source proof includes native body extraction excluding its broad historical installer. Do not run that installer, widen schema SELECT or change fs013_app WITHOUT LOGIN.

|Ordinal|Object / action|Classification|Decision|
|---|---|---|---|
|1|dbo.users SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|2|dbo.user_sessions SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|3|dbo.user_branch_access SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|4|dbo.user_roles SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|5|dbo.roles SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|6|dbo.role_permissions SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|7|dbo.permissions SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|8|dbo.branches SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|9|dbo.departments SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|10|dbo.rooms SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|11|dbo.floors SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|12|dbo.areas SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|13|dbo.room_types SELECT|UNRESOLVED_OWNER_DECISION|HOLD_UNTIL_NATIVE_CALL_PATH_CONFIRMED|
|14|dbo.shift_assignments SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|15|dbo.work_shifts SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|16|dbo.shift_templates SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|17|dbo.cleaning_tasks SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|18|dbo.cleaning_assignments SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|19|dbo.room_cleaning_cycle_state SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|20|dbo.audit_logs SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|21|dbo.notification_operation_reservations SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|22|dbo.notification_publications SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|23|dbo.notification_delivery_intents SELECT|NOTIFICATION_RUNTIME_REQUIRED|RETAIN_APP_READ_SEPARATE_FROM_EXECUTE_ONLY|
|24|dbo.fs013_assign_with_evidence EXECUTE|NATIVE_UC029_REQUIRED|RETAIN_SCOPED_EXECUTE|
|25|dbo.fs013_assign_with_evidence VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|26|dbo.fs009_verify_assignment_guard EXECUTE|NATIVE_UC029_REQUIRED|RETAIN_SCOPED_EXECUTE|
|27|dbo.fs009_verify_assignment_guard VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|28|dbo.notification_operation_reservations INSERT|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|29|dbo.notification_operation_reservations DELETE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|30|dbo.notification_operation_reservations UPDATE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|31|dbo.notification_operation_reservations VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|32|dbo.notification_publications INSERT|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|33|dbo.notification_publications DELETE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|34|dbo.notification_publications UPDATE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|35|dbo.notification_publications VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|36|dbo.notification_delivery_intents INSERT|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|37|dbo.notification_delivery_intents DELETE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|38|dbo.notification_delivery_intents VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|39|dbo.cleaning_assignments INSERT|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|40|dbo.cleaning_assignments DELETE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|41|dbo.cleaning_assignments UPDATE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|42|dbo.cleaning_assignments VIEW DEFINITION|UNRESOLVED_OWNER_DECISION|SEPARATE_SCOPED_METADATA_OBSERVER_APPROVAL|
|43|dbo.audit_logs UPDATE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|44|dbo.audit_logs DELETE|NATIVE_UC029_REQUIRED|RETAIN_DENY|
|45|dbo.notifications SELECT|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|46|dbo.notifications INSERT|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|47|dbo.notification_recipients SELECT|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|48|dbo.notification_recipients INSERT|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|49|dbo.notification_recipients UPDATE(read_at)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|50|dbo.notification_delivery_intents UPDATE(status)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|51|dbo.notification_delivery_intents UPDATE(attempt_count)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|52|dbo.notification_delivery_intents UPDATE(next_attempt_at)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|53|dbo.notification_delivery_intents UPDATE(claim_id)|REDUNDANT_OR_OVERBROAD|OMIT_UNPROVEN_COLUMN_GRANT|
|54|dbo.notification_delivery_intents UPDATE(claimed_until)|REDUNDANT_OR_OVERBROAD|OMIT_UNPROVEN_COLUMN_GRANT|
|55|dbo.notification_delivery_intents UPDATE(failure_code)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
|56|dbo.notification_delivery_intents UPDATE(inbox_notification_id)|DELIVERY_INBOX_ONLY|EXCLUDE_FROM_NATIVE_PROFILE_REQUIRE_SEPARATE_APPROVAL|
