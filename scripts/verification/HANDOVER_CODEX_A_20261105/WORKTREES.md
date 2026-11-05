# Worktree provenance

Metadata/source hashes read only outside Codex A root. No staging/commit performed in another worktree.

| Path | Branch | HEAD | Status entries | Staged entries |
|---|---|---|---:|---:|
| D:/SEP490/Projects/HotelOps | feature/BE1-database-integration | 4a187320643d0c82f232c6b9dd5a667b8401c280 | 958 | 0 |
| D:/SEP490/Projects/HotelOps/obj/task11-dev2-rehearsal | integration/task11-dev1-dev2-rehearsal | 3688ba9959984deb24f179bd41a4820df1ecfebd | 103 | 103 |
| D:/SEP490/Projects/HotelOps/obj/task12-pr7-resolution | integration/task12-pr7-conflict-resolution | d6ef057bb0980b1763b1c4870a25316f35d9add9 | 0 | 0 |
| D:/SEP490/Projects/HotelOps-AI-Loop | ai/hotelops-loop-test | 8e0b67303c4c72b982ba3ca92d84cff654cf5b5d | 0 | 0 |
| D:/SEP490/Projects/HotelOps-Codex-B | handoff/codex-b-20261105 | 21c06bf51bcbcf0c99e8c711302b71682590e41f | 0 | 0 |

Root ahead6 relative to cached upstream, containing DEV2 merges; authorship is not claimed. task11 rehearsal has103 staged entries owned by that worktree; never adopted. Codex B was observed changing from ai/codex-b at4a18732 to handoff/codex-b-20261105 at21c06bf during parallel owner activity; read-only inventory captured the latter. This is not a Codex A change.
Historical Codex A snapshot cf8fb020362f8f64192e7f2f14a10547cebe5b91 is already published on integration/dev1-fe-handoff. No cherry-pick/bulk merge into current runtime.
Other worktree source and WIP are inventoried, not republished under Codex A ownership.
