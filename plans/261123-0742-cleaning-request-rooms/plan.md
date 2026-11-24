# Supervisor cleaning request rooms

- [x] Audit: current queue lists existing CLEANING work only; checkout rooms without work are absent. Preserve creation/assignment lifecycle and compact UX.
- [x] Add scoped, paged eligible-room reads: pending cleaning, active, vacant, unblocked, no open housekeeping work. Protect creation against stale room selection without changing existing callers.
- [x] Show rooms in supervisor queue and create picker; prefill selected room and open existing staff assignment after successful creation. Keep existing task queues and permissions.
- [x] Verify real API on backed-up isolated fixture, browser at desktop/mobile, frontend/backend builds and relevant tests; document outcome. No primary test writes or automatic publication.

Evidence: fixture HotelOpsCompactApiTestd2f06f5ba0c2, 33/33 groups; .NET 28 passed / 3 conditional skips; frontend build/typecheck passed; 5 workflow units; shared compact UX suites including 48 navbar route/viewport checks passed; new responsive browser test at 1440/768/375 passed with mocked APIs. Details in plans/reports/implementation-261123-0742-cleaning-request-rooms.md.
