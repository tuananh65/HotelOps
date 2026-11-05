# Task75 — DEV2 dependency handoff (local proposal, not integrated)

Owner: DEV2 notification/delivery and its business writers; DEV1 cleaning/inspection producers. This file is prepared locally only. No message was sent externally and no DEV2 source was modified.

UC029/041/046 stay PARTIAL. UC044/045 stay NOT_IMPLEMENTED. The read-only UC037 subset does not close these dependencies.

## Notification adapter contract to agree

Required producer facts: stable operation/idempotency key, source type/id/revision, actor, branch, recipient, occurrence UTC, safe summary and source action reference. Do not expose credentials, session keys, private file paths or unrestricted evidence URLs.

The adapter must accept the authoritative committed recipient/source facts, not arbitrary client recipient/branch values. Existing SQL-backed source authorization remains mandatory when the recipient opens an action; receiving a notification does not grant access.

DEV2 must define the actual adapter API, storage ownership and delivery status vocabulary. DEV1 will not introduce a competing queue/outbox or new notification status/catalog values. Persist publication intent atomically with business/audit, or approve another reliable protocol that prevents committed assignments with silently lost notifications. An in-memory callback after commit is insufficient.

## Delivery, retry and idempotency

- Delivery means a durable in-app notification readable by its authorized recipient, with actionable source linkage; method return, enqueue or audit alone is not delivery.
- Business request retry and consumer retry must not create duplicate notifications. Agree source revision/recipient/operation key uniqueness and retention.
- Define retryable/permanent errors, bounded backoff, restart recovery, retry exhaustion and owner-operated recovery. No retry count or backoff values are invented here.
- A delivery outage must preserve the business assignment and durable retry intent. A rolled-back business transaction must leave no deliverable orphan event.
- Recipient deactivation/branch change or source cancellation requires an explicit handling rule; do not silently widen authorization or redirect to a different recipient.

Required real acceptance: transport/storage failure with durable retry, worker restart, repeated producer/consumer invocation, no duplicate delivery, rollback without orphan notification, recipient/branch isolation, source permission recheck and actionable authorized navigation. Use disposable SQL only. No fake delivered flags or success-only test doubles count toward UC completion.

## Room lifecycle / effective blocks

Before UC044/045 or UC037 Cancel, agree exact stored versus projected states and the current cleaning/inspection/rework cycle. Define an effective RoomBlock, block interval boundaries and Ready/Blocked precedence under concurrent Incident/Maintenance/RoomBlock writers.

Review the concrete retained lock/read order across DEV1 and DEV2 producers. Required races include block creation/removal while approving an inspection, competing approve/reject, stale checklist/result/defect/rework revisions and session/grant revocation. Whole transaction must preserve cleaning/inspection/room/audit/publication consistency. A Ready result must never override an effective block.

No Reception permission revision, database mutation, DEV2 writer fix or UC006/UC016 gate change is authorized by this proposal. Any required shared schema or writer change needs a separately reviewed patch and owner approval.
