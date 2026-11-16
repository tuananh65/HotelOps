# UC037 cancellation delivery/disclosure final authority

Source fix; PARTIAL_IMPLEMENTATION. No completion increment.

A serializable transaction prevents concurrent row updates but cannot stop a role/grant expiry timestamp from passing. Cancellation delivery previously checked recipient eligibility only before inserting inbox records. It now rechecks eligibility using a fresh clock immediately before commit. On denial it throws a dedicated internal exception, disposes/rolls back the delivery transaction, clears tracking, then marks only a still-PENDING intent TERMINAL with RECIPIENT_OR_SOURCE_DENIED. A concurrently completed intent is not overwritten. No committed notification/recipient from the rejected transaction survives.

Own cancellation receipt previously revalidated the session before return but did not revalidate recipient permission with the new time. It now rereads the proven receipt and current recipient access after session validation and requires identical immutable receipt values before commit/disclosure. Assignment identity and approved historical own-receipt scope remain unchanged; no full cancelled-task access is granted.

Validation: dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors. Focused CleaningCancellationNotificationTests + CleaningCancellationTests + CleaningMonitorActorTests: 35 PASS, 0 failed/skipped, including grant valid at admission but denied exactly at expiry, wrong branch and revoked role. These are source/component checks, not a SQL rollback proof for the new delivery branch. Frontend unchanged; no new frontend build claimed. No schema, shared database, grant, deployment or worker configuration changes.

Human QA must run real cancellation, delivery with expiry between admission/commit, assert inbox rollback + terminal intent, confirm retry/idempotency and receipt denial after expiry, and exercise UI/HTTPS. Previous recovery SQL component evidence remains separate; do not mark COMPLETE_VERIFIED from these checks.
