# [HOTELOPS DEV2] FE04-NOTIF-OWNER-DECISION-007

**D03 — SecurityOwner + NotificationOwner + DEV1 FS013**

Accept exact object/column profiles in D03_PERMISSION_NARROWING.json and exact SQL. Preserve13 DENYs, hold room_types and scoped metadata, omit unproven claim_id/claimed_until. Confirm SQL session side effects and static dbo ownership chain/module hashes.

**D05 — SecurityOwner + SchemaOwner + RuntimeOwner; DEV1 minimal accepted composition adapter**

Implement/accept INotifOwner007RuntimeAuthority on existing caller connection/transaction only: actual sys.database_recovery_status.database_guid, SID/restricted effective rights, source revision/current command authority, catalog/index/trigger/module hashes, current central exclusive lease and revocation before/after observation. No alternate connection/elevation. Wire optional hook explicitly in isolated acceptance composition only.

**D08 — RuntimeOwner + all five existing FE04 approving owners**

Issue fresh signed successor lease with independent HardExpiry, new/retired lease IDs and epoch, actual login/user SID mapping, accepted final composed source/script/policy/catalog/module hashes, explicit approval reference and permitted uses. Supply OS-protected independent owner trust resolver and real central lease validator; no configuration self-issuance.

**NATIVE_TARGET — DEV1 FS013 + SchemaOwner**

Native source 10_SqlWriteBoundary.sql hardcodes HotelOps_Canonical_20261106_f130001_Fresh. Accept exact separately reviewed ENVNOTIF target module definition/hash or establish authoritative target decision. No retargeting or installer execution performed.

**FE04_V1_ADAPTER — DEV1 acceptance/runtime adapter owner**

Existing Fe04NotificationApproval V1 remains tied to Writer004A HardExpiry. Accept a minimal version-aware successor adapter; keep old contract immutable. No full FE04 domain recomposition requested.

No real lease/SID/fixture/observer authority supplied. No broad grant, credential in Git, installer, SQL lease acquisition or producer/worker/retry requested. Only exact owner decisions and minimal accepted adapter wiring are needed; no new full FE04 source composition work.
