# PR25-FINAL-COORD-001 — Source pins
Ngày thực hiện: 2026-11-11 (Asia/Saigon).
WORKTREE_READY: đúng đường dẫn yêu cầu; nhánh feature/dev2-dev3-master001-responsibility-readers.
FEATURE_HEAD_BEFORE=2140522457e89b974cf2d65c64515ec15d733fa9
FEATURE_TREE_BEFORE=33ae51a1dbc228dc0338768ecbe9cd959dec0939
INTEGRATION_HEAD_BEFORE_CACHED=8fd30f2f12e9dae102de2826e0be20e99e6a14ad
LIVE_REMOTE_FEATURE_SHA=2140522457e89b974cf2d65c64515ec15d733fa9 (GitHub fetch_pr)
LIVE_REMOTE_INTEGRATION_SHA=8fd30f2f12e9dae102de2826e0be20e99e6a14ad (GitHub fetch_commit, symbolic integration/fullstack-base)
REMOTE_FRESHNESS=CONNECTOR_OBSERVED_NOT_GIT_FETCHED
INTEGRATION_PARENT_SYNCED=NOT_SYNCED
Merge/commit/push: NOT_RUN.

Working tree ban đầu sạch. Remote origin=https://github.com/tuananh65/HotelOps.git.
Git common directory ở ngoài writable root: C:/Users/Admin/Documents/GitHub/HotelOps/.git.
Sparse paths: docs/fullstack-integration, docs/integration-notes, scripts, src, tests.
Không mở rộng sparse checkout; không tác động worktree khác.

git fetch origin exit 1: không ghi được FETCH_HEAD (Permission denied).
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers refs/heads/integration/fullstack-base exit 128: không kết nối github.com:443.
Quyền phiên hiện tại không cho escalation. Không thử lách sandbox qua GitHub write API.

GitHub PR #25 trả về OPEN, merged=false, mergeable=true; head SHA đúng.
Tuy nhiên base_sha=3ab51777ca7b15af5336eb42aa442666a4264687 khác integration symbolic lookup.
Mergeability hiện hành với integration mới: UNVERIFIED; không dùng metadata này làm chứng cứ combined source.
Chỉ dẫn PL đã đọc trực tiếp qua connector: https://github.com/tuananh65/HotelOps/pull/25#issuecomment-6773183595.
