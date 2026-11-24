# Bằng chứng thực thi P0 — 24/11/2026

Tổng hợp 28/28 nhóm E2E, 1349 assertions trong các lần chạy được chọn. Đây là bằng chứng cộng dồn sau sửa lỗi, không phải một lần chạy lại từ database trống. API TEST port 5774; database `HotelOpsCompactWorkdayTestP0261124`.

| Nhóm | Kết quả | Assertions | Artifact trong artifacts/p0-workflow |
|---|---|---:|---|
| E2E-01 — Role migration, eight actors, membership, sessions and history | VERIFIED | 41 | runtime-migration.json |
| E2E-02 — Eight fill items and independent branch configuration | VERIFIED | 11 | runtime-results.json |
| E2E-03 — Start shift allocation and idempotent retry | VERIFIED | 102 | runtime-start-retry.json |
| E2E-04 — Short stock start preserves attendance without partial issue | VERIFIED | 14 | runtime-results.json |
| E2E-05 — Cleaning reservation, execution, consumption and inspection | VERIFIED | 42 | runtime-integration-2.json |
| E2E-06 — Insufficient staff supply rejects assignment | VERIFIED | 8 | runtime-integration-2.json |
| E2E-07 — Concurrent reservations cannot exceed available supply | VERIFIED | 54 | runtime-rooms-race-4.json |
| E2E-08 — Reassignment and cancellation release reservations atomically | VERIFIED | 23 | runtime-integration-2.json |
| E2E-09 — Supervisor rejects refill with required reason | VERIFIED | 8 | runtime-requests-3.json |
| E2E-10 — Supervisor confirms and storekeeper fulfills refill | VERIFIED | 19 | runtime-requests-3.json |
| E2E-11 — Storekeeper rejects shortage without stock movement | VERIFIED | 16 | runtime-requests-3.json |
| E2E-12 — Concurrent stock issues cannot oversell | VERIFIED | 23 | runtime-requests-3.json |
| E2E-13 — Manual end returns unused supplies exactly once | VERIFIED | 42 | runtime-integration-2.json |
| E2E-14 — Unfinished work, reservations or refill prevent shift closure | VERIFIED | 20 | runtime-requests-3.json |
| E2E-15 — Low stock threshold, deduplication and recovery | VERIFIED | 34 | runtime-requests-3.json |
| E2E-16 — OM replenishment approval precedes actual goods receipt | VERIFIED | 18 | runtime-requests-3.json |
| E2E-17 — Assigned durable item missing, replacement and inspection | VERIFIED | 85 | runtime-rooms-race-4.json |
| E2E-18 — Durable rework preserves history without duplicate issue | VERIFIED | 97 | runtime-rooms-race-4.json |
| E2E-19 — Durable shortage cannot mark remediation complete | VERIFIED | 40 | runtime-rooms-race-4.json |
| E2E-20 — HK damage report through external maintenance supervision | VERIFIED | 112 | runtime-rooms-race-4.json |
| E2E-21 — Reception damage report preserves occupancy | VERIFIED | 117 | runtime-rooms-race-4.json |
| E2E-22 — Maintenance rework retains all attempts | VERIFIED | 147 | runtime-rooms-race-4.json |
| E2E-23 — False issue rejection creates no remedial stock movement | VERIFIED | 30 | runtime-rooms-race-4.json |
| E2E-24 — Multiple room blockers independently prevent readiness | VERIFIED | 147 | runtime-rooms-race-4.json |
| E2E-25 — Unified request routing, timeline, parent-child and persistence | VERIFIED | 18 | runtime-rooms-race-4.json |
| E2E-26 — OM ordinary request read-only; replenishment exception | VERIFIED | 9 | runtime-requests-3.json |
| E2E-27 — Eight-role authorization and cross-branch isolation | VERIFIED | 70 | runtime-integration-2.json |
| E2E-28 — Inventory reconciliation in both branches | VERIFIED | 2 | runtime-rooms-race-4.json |

## Bổ sung runtime

- runtime-handler-supplement.json: E2E-25, VERIFIED, 44 assertions.
- runtime-legacy-supplement.json: E2E-14, VERIFIED, 56 assertions.
- runtime-asset-operations.json: A-OM-OPERATIONAL-SELF-REVIEW, VERIFIED, 33 assertions.
- runtime-asset-operations.json: A-ASSET-CREATE-UPDATE-MOVE-HANDOVER, VERIFIED, 36 assertions.
- runtime-report-snapshot.json: E2E-28, VERIFIED, 42 assertions.

## Tính toàn vẹn bằng chứng

Các JSON lưu HTTP status, actor alias, route và thời gian, không lưu token. File private bị giới hạn ACL và không thuộc Git. Hash để đối chiếu snapshot artifact:

| Artifact | SHA-256 |
|---|---|
| runtime-results.json | 6e328e791fbc101c69e823aa8a03d65a392929ed67756f656d23806e50118de9 |
| runtime-start-retry.json | 5575e85bc82cd0a0ce2bba46ad6d3a357ff6cbd8fb46fda8d52d5a96538212ff |
| runtime-integration-2.json | 73fa4d07a29942b0341777d2057968e69f9ed7dfccc68788275cce2a042dbaf5 |
| runtime-migration.json | 3cddcd21b8aa44beb53e1e100430ee98478f2b2033c3418c00e30f628a2b48dc |
| runtime-requests-3.json | a6751843c1cfd1a1c7cf9bbbf2ecbdcee73d2dac2d8007fe0c3e367baa4b81f7 |
| runtime-rooms-race-4.json | b6b526d4c51a73deb02e716b3a2fa6ba3da894993d5d7b499c6073e5aa1a7ebf |
| runtime-handler-supplement.json | bd936d1c266702daf7a83f090b0e3f0da1830257c9b089fad50de65893540ef2 |
| runtime-legacy-supplement.json | c9e9087138f766255e010c891bc880a7ffa2631fdd8d54b97b335fa490fd9c6e |
| runtime-asset-operations.json | 9dc81a4af6a34068fd8ffd0322aa9f1381f521debf71678bbf74058d21da222b |
| runtime-report-snapshot.json | 39fadb5cf7ffe378bbc03343910825170ea87e9710e5b71dc26a426fdcd4b67c |

## Giới hạn

- Three existing xUnit integration tests are skipped.
- Browser route/layout audit is not interactive coverage of every form.
- Synthetic legacy fixture simulates pre-migration dates; it is not original hotel history.
- No Production cutover, deployment, commit or push.
