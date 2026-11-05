# Security boundaries

Approved target only: local machine/SQLEXPRESS/exact database/DEV1_FS004 marker are checked. Setup validates 18 source hashes. Historical PASS never authorizes other databases.

SQL TrustServerCertificate applies only to this local Windows-authenticated SQL Express setup. HTTPS remains validated. Existing cert key stays in Windows store; public PEM only; no trust or SQL privilege modifications.

Cookie/session/CSRF, permissions, branch scope, assignee and ETags use unchanged backend code. Live tests use no interception, fake principals or TLS-ignore. Interception regression is separate. Five recovery controllers remain inactive.

DPAPI ciphertext, metadata, logs, certificates, evidence and process receipts are ignored. Live traces/video/screenshots are off. Committed evidence contains statuses/counts/booleans and approved target only. Never print credential IPC.

No production schema edits, generated EF edits, migrations, DEV2 source changes, shared-auth changes, pending proposals or baseline merges. Reset/drop/replacement DB/broader access requires separate approval.
