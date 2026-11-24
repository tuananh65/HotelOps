---
title: Supervisor cleaning request rooms
date: 2026-11-23
summary: Show pending rooms before work exists and link creation to staff assignment.
---

# Supervisor cleaning request rooms

## Outcome
Fixed the missing supervisor intake: the old queue listed existing cleaning work only. Added a scoped eligible-room API and a default pending-room tab. The create modal selects only vacant, active, unblocked pending-cleaning rooms without open housekeeping work. Prefills room metadata and opens existing staff assignment after creation. Unassigned requests remain available if assignment is postponed.

## Validation
Real isolated HK API fixture includes checkout-to-intake, permission/branch denial, stale selection, blocked/ready/occupied exclusions, duplicate prevention and full staff cleaning/rework/inspection workflow. Backend build passed with zero warnings/errors after stopping the task-owned API that initially locked the DLL. .NET 28 passed, 3 conditional skips. Shared compact UX passed. Browser test mock was corrected to return the real related-work array shape and use the existing confirmation button.

## Boundaries
No schema/grant/primary business changes. RoomRowVersion is optional for existing callers, used by the new intake to reject stale selection within the existing serializable transaction. Primary API restarted with worker disabled. No automatic commit or push. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
