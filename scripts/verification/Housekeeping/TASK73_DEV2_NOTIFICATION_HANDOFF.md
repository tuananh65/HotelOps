# Task73 — notification delivery/retry handoff (proposal, not integrated)

Owner of delivery/notification module: DEV2. No DEV2 source or notification tables were
changed by Task73. No message sent to another person/system. This file is the local handoff.

UC029 Assign Cleaning Task, UC041 Assign Room Inspection and UC046 Request Cleaning Rework
require actual in-app notification and durable retry under GBR010/UC124. Their independent
business writes/audit exist; API response truthfully reports NOT_INTEGRATED. An audit entry
or internal enqueue is not evidence of delivery. These UCs earn no verified points yet.

Proposed producer facts for DEV2 review (no event names or queue states committed as a
shared contract): stable event/idempotency key, business operation/source type and ID,
source revision, actor, branch, recipient user ID, occurrence UTC, safe summary and the
authorized source action path. No passwords, cookie/session credentials, raw storage keys,
Base64 evidence or unrestricted public URLs. Proposed recipients are the committed assignee/
inspector; they must not be recomputed from arbitrary client input or stale UI data.

Required agreement before completion:

1. Producer/outbox ownership and durable storage atomic with business change, or another
   explicitly approved reliable publication protocol. Notification failure keeps the
   business assignment and queues retry; silently losing the event is not acceptable.
2. DEV2 adapter channel/status semantics, retry/backoff/dead-letter/recovery ownership,
   stable idempotency and no duplicate messages on request retries or consumer restarts.
3. Delivery/source authorization: branch/recipient eligibility, source-scoped action URL,
   read ownership and no notification allowing a forbidden cross-branch resource.
4. Real disposable SQL/HTTPS proof of successful delivery, temporary delivery failure,
   durable retry after restart, eventual delivery exactly once per business event,
   revoked/inactive recipient handling, malformed event rejection, audit/business rollback
   publishing nothing, and observable unresolved failure without claiming delivery.

Do not implement the DEV2 consumer or mutate its shared schema as part of this handoff.
After approval/integration, rerun the three notification-dependent UC acceptance suites
and update classifications individually. UC042/043 remain independently verified.
