# DEV1 FS010 integration note

Authorized autonomous feature-branch changes under explicit FS010 task; no per-step DEV2/DEV3 approval gate inferred. Other worktrees untouched. No generated entity/mapping edits, no EF migrations, no main/develop merge.

| Path / symbol | Contract impact | Reconciliation |
| --- | --- | --- |
| src/HotelOps.Api/Program.cs AddDbContext | Adds CandidateCommitGuard.Instance once; unarmed transactions unaffected | DEV2: preserve scoped DbContext; avoid replacing options or double publisher |
| Services/HotelStructure/RoomTypeBatchService.cs BatchAsync | Optional preserveCommitErrors default false, UC029-only true | Preserve existing Serializable owner/locks; other modules retain default |
| Services/Notifications/CandidateCommitGuard.cs | EF commit callback persisted proof; sync armed commit refused | Direct provider/SQL COMMIT/external clients unsupported; never label universal |
| Services/Housekeeping/CleaningAssignmentCandidateService.cs | Reauth + original fingerprint durable-only recovery, 10s separate token | DEV2 compare accepted idempotency/result/error contract and cancellation semantics |
| Services/Notifications/ProvisionalCleaningNotification.cs AvailableAsync | Adds exact FS010 local trial tuple and marker; default OFF preserved | Accepted source still missing; no production activation |
| Services/Housekeeping/CleaningTaskCreationService.cs LifecycleInstalled | Recognizes FS010-V1 identical lifecycle proposal | DEV3 review current-cycle/readiness vs historical room history; no new lifecycle business state |
| scripts/database/release/fs010-candidate | Separate exact-target proposal copy; existing fs009 guard proc/trigger/index names retained for runtime compatibility | No shared DB deployment; schema/key/catalog ownership pending accepted DEV2 source |

Trial FS004/FS008/FS009 untouched. New FS010 only. Synthetic append is additive, no reset/reseed between repeat runs. Populated recovery refusal tested; no destructive recovery/drop executed. Test-only auth doubles exist only in verification executable, never API DI. Mandatory pending-publication staging remains atomic and source-read checks precede delivery.
