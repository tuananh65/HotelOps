# [HotelOps DEV2 → DEV1 FE04 / Security Owner / Runtime Owner]
# FE04-NOTIF-OWNER-DECISION-007

D03=READY_FOR_OWNER_APPROVAL; D05=READY_FOR_OWNER_APPROVAL; D08=READY_FOR_OWNER_APPROVAL. Runtime/permissions/new lease NOT_ACCEPTED.

D05: CleaningAssignmentStore.EnsureAsync and CleaningNotificationRuntime.RequireClean actually call optional hook in CleaningCommonTargetAdmission.cs. Actual GUID must come from sys.database_recovery_status.database_guid on same already-open connection. Historical guard intact; missing/stale/untrusted metadata, principal/SID, catalog/source/transaction or current central lease rejects. No default production adapter/registration.

D08: Writer004A original bytes/HardExpiry intact. Distinct WRITER004B_OWNER007_V1 verifies five independently pinned owner RSA-PSS signatures and real current central lease through explicit interfaces. RuntimeOwner must issue all owner-required values in D08_SUCCESSOR_CONTRACT.json: new/retired ID, timing/HardExpiry/epoch, approval, real SID, final source/script/policy/catalog/modules hashes, target/GUID/use. Existing FE04 V1 adapter still closes on old expiry; minimal version-aware accepted wiring required.

D03:56/56 original rows reviewed. Native EXECUTE2; protective DENY13; application SELECT22; unresolved room_types1 plus metadata6; Delivery/Inbox-only10; omit unproven claims2. Ownership chaining only covers module-internal operations when real owners match, not direct C# auth/source reads. No identity reads in native proc. Exact review SQL and original/proposed diff supplied; no rights executed.

Source pins DEV2 d60022b487b5b59e84d6551a0ee5162c9fd32164; FE04 54d75a634005afb8d42704f761a820ed16b1b9d4; implementation 4733f6318be79d3deb07fde0fa19b325fde9ea6c; branch feature/dev2-fe04-notif-owner007; SOURCE_PUBLISHED=false; remote HEAD UNKNOWN. Eight implementation paths in source commit, required reports follow locally. Exact file hashes in CHANGED_FILES_SHA256.json. Build0 warnings/errors; Cleaning74 + Owner00755 + Writer14 + FS01323 + composed FE0464 PASS. D03 coverage56, not SQL acceptance. Failed initial helper receipt retained.

Owner blockers: scoped real observer/current source authority; independently accepted trust and new central lease; exact permission policy; native source canonical-target mismatch; minimal V1 successor adapter. No full new FE04 composition requested.

SQL_CONNECTIONS=0; SQL_MUTATIONS_EXECUTED=false; permission execution=false; producer/worker/retry=false; merge/deploy=false.
