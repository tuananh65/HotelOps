# Source baseline

| Source | Verified SHA |
|---|---|
| origin/develop | 6aee2a0e692a7814f41c81224709cddfbe1e2ec4 |
| origin/feature/react-frontend | 9d220a94509e650464406a9b83099d24cb568220 |
| origin/handoff/codex-a-20261105 | 28f0a5e4f582bc56ba101b48915d33baadeb7b95 |
| origin/handoff/codex-b-20261105 | 21c06bf51bcbcf0c99e8c711302b71682590e41f |
| origin/main | 8f4059d7e8019d58df43e92012801c736699996b |

A source snapshot: e859006fcff91b1b913b18c27844a73809a18d8a. Final A handover adds metadata/evidence; its src tree is identical to that source snapshot. B handover contains no new production implementation.

Merge-base develop/A: 4a187320643d0c82f232c6b9dd5a667b8401c280. Merge-base develop/React: e0d99f306ed9f75677cacf1628f52905e2fd0f6f. The React branch also contains obsolete backend/bootstrap changes; only frontend/ was imported. A handover omits some newer develop graphs, so it was not merged wholesale.

Integration starts from pinned origin/develop. Existing develop source is retained; approved A source is composed additively. PUBLICATION.md pins the tested integration source commit after it exists. The final documentation commit SHA is verified externally with ls-remote to avoid self-referential hashes.
