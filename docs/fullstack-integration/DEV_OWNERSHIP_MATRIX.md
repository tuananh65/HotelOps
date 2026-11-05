# Ownership

| Owner | Scope |
|---|---|
| DEV1 | FE03 Room Operations; FE04 Housekeeping; FE05 Inspection; FE11 Operational Tasks; FE13 Internal Requests |
| DEV3 | FE01 Authentication/User/Role/Permission; FE02 Hotel Structure; FE12 Work-Time/Workday |
| DEV2 | FE06/07/08/09/10/14/15 unchanged |
| COMMON | DI/pipeline, DbContext/schema/entities, auth/session contracts, lifecycle/notification/source-read/transaction participants |

Explicit FS-001 ownership overrides the historical two-developer assignment in AGENTS.md, which is not edited for the whole team. TurnoverHistory belongs to DEV1 despite its HotelStructure folder. Room master data FE02 belongs to DEV3; room lifecycle FE03 belongs to DEV1. UC113 workload/time needs DEV1+DEV3 coordination. Notification contracts belong to DEV2/shared review.

SOURCE_FILE_OWNERSHIP.csv is a review aid, not a permission matrix or blanket shared ownership approval. Full SRS catalog has144 IDs; the old A ledger tracks75 of them.
