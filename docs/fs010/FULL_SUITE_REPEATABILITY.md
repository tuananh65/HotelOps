# Full suite repeatability

| Full run | Result | Evidence |
| --- | --- | --- |
| run01 | 0/12, TLS trust before API request | LIVE_RUN_01_TLS_FAILED.json |
| run02 | 11/12; staging fault expected500, received503 after controlled durable recovery | run02_LIVE_RESULTS.json |
| run03 | 12/12 uninterrupted PASS | run03_LIVE_RESULTS.json |
| run04 | 12/12 uninterrupted PASS; repeat | run04_LIVE_RESULTS.json |

Run03 and run04 used retained FS010 SQL data, no reset/reseed or per-case data repair. Additive unique synthetic room capacity was appended before each full run as needed; earlier rows retained. Exact publication IDs prevent cross-case dispatch consumption. Original GUID/If-Match preserved for replay; unique new commands and unused rooms isolate fixtures. Fault enable/disable is deliberate setup/finally, not data repair to force PASS.

Node public CA now configured by Run-LiveSuite.ps1; TLS verification stays enabled. No arbitrary timeout increase: child timeout remains 60000ms, retries zero, single-worker SQL suite. Helper timing sample in run04: 72 calls, max 12828ms, nonzero exit 0, ETIMEDOUT 0. Original FS009 terminal-helper timeout was not reproduced on isolated FS010; exact historical root cause is NOT proven. Build attempts during live binaries are explicitly prohibited in the environment guide after observed file-lock errors.

No focused rerun is combined into a full PASS. Actual restart/default-off receipts are separate tests, not extra cases added to the 12-case full runs. Interception47/47 is separate from REAL_BROWSER_E2E/LIVE_API_SQL.
