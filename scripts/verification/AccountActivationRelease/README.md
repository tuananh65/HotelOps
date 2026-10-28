# Task37 — controlled Account Activation release

Packages the Task35 activation primitive and Task36 `PATCH /api/users/{userId}/activate`, their reproducible verification and the DEV2 deactivation handoff. The user explicitly authorizes one controlled commit named `feat(users): implement secure account activation` after gates pass. No push, merge, PR, amend, rebase, branch/worktree operation or HotelOpsDB access is authorized/performed.

Baseline: feature/BE1-database-integration at `1a969e3731d12d430a250e219caa1e1fbfeb52b9`, upstream same, index empty, Program DI diff only among tracked files. Feature source/docs/harness were already untracked; unrelated runtime artifacts are retained. AGENTS.md, historical SRS UC006/GBR005/007/008/012/026/027/028, change notes, matrix proposals and explicit Task35–36 approval were reviewed. Explicit approved UC006 scope takes priority over the historical draft; no pending permission is invented.

- [Exact32-file allowlist](FILE_MANIFEST.md)
- [Security and transaction review](SECURITY_REVIEW.md)
- [Executed verification](VERIFICATION_RESULTS.md)
- [Ready-to-send DEV2 message](DEV2_HANDOFF_MESSAGE.md)
- [Remaining limitations](REMAINING_RISKS.md)

Current contract and commands: [Task36 API](../UserAccountActivation/API_CONTRACT.md), [harness reproduction](../UserAccountActivation/README.md). Task35 documentation is retained as the decision/primitive history; its HTTP-blocked statements are historical and superseded only for activation. Deactivation remains BLOCKED. No status/deactivate mutation endpoint is registered; a valid direct primitive INACTIVE request is also refused.

The scope is activation only: no session/credential/role/assignment reset and no DEV2 implementation. Shared Program change is limited to two scoped registrations already authorized for Task36; no foundation behavior/schema changes. Backend still depends on the previously approved users.row_version patch. Git commit is not production migration/deployment approval.

SQL/HTTPS tests use fresh owned disposable trials with name/server/database/ownership/schema guards. JSON receipts, generated certificates, bin/obj and index-export build snapshots remain local artifacts. Source and Markdown are scanned and staged only by the explicit manifest, with cached whitespace/file/blob checks. The resulting commit hash and file list are verified using Git after commit; they are not embedded through an amend. DEV2 handoff is prepared only, never sent automatically and never represented as accepted.
