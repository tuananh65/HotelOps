# UC129 / UC130 notification session commit guard

Status: source security increment. No new source-complete or COMPLETE_VERIFIED UC. This security increment does not change completion counters. The separate approved UC018 closure brings the current total to 52/144 source-complete; 27/144 QA_VERIFIED remains historical.

Evidence and fix:
- NotificationCenterService authenticated at the start of ListAsync, AcknowledgeAsync and MarkReadCoreAsync. Serializable row locks protect data from concurrent writes but do not prevent elapsed wall-clock session expiry while a query waits.
- All three operations now revalidate the same user with current clock time immediately before commit. Failed validation throws inside the existing transaction, so read-marker/acknowledgment writes cannot commit through this path after a failed final check.
- Existing session predicate is extracted without weakening identity, revocation, active account, temporary-password restriction or absolute-lifetime rules. No permission, grant, schema, notification recipient or retention change.
- SRS UC129 explicitly lists type, period, priority and read/ack status filters. GBR-157 scopes the inbox to addressed recipients and rechecks source permission on opening. Branch/department FILTER UI is not a UC129 requirement; earlier matrix wording was unsupported and is corrected. Existing source authorization remains mandatory.

Actual validation: dotnet build HotelOps.slnx --no-restore PASS (0 warnings/errors). NotificationSessionPolicyTests + NotificationRetentionTests: 18 PASS. Tests cover expiry boundary, account/session restrictions, SQL translation and retention rules; they do NOT prove live SQL rollback or HTTP behavior. dotnet test HotelOps.slnx exits 0 without discovering tests and is not counted as coverage. Frontend unchanged; no new frontend build/test claim.

Remaining: verify final-gate rollback using a controlled expiring session while recipient SQL is blocked, list snapshot/empty-filter behavior and each emitted source-family adapter through authenticated HTTPS on the verified disposable database. No SQL was mutated or runtime acceptance executed in this increment. Source producers/physical cleanup remain separately tracked in UC124-128/JOB06.

Changed files: NotificationCenterService.cs; new NotificationSessionPolicy.cs and NotificationSessionPolicyTests.cs; this receipt and UC129 matrix entries.
