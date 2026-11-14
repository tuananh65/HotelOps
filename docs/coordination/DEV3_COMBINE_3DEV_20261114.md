# DEV3 source publication — current receipt

Source branch: DEV/pushcodetocombine3dev. Shared target base: d2e23411c396ec0b5219b4f6c15e3e2775ef9764.

PR121 was externally merged with the original 19-path source slice (head 6a9837c). The user subsequently authorized **branch push only, no replacement PR**. This increment is not part of merged PR121. No target merge is performed here.

Historical integration b85743a source, four local UC010/UC006/UC016 increments, UC004 b9ed872 composition, UC012 fixture R3 and the disabled D5 classifier are now collected. The 19 PR117 paths and 131 PR120 paths are preserved exactly. All current FE04/FE05 target changes are retained. See [exact manifest](../fullstack-integration/DEV3_SOURCE_PUBLICATION/MANIFEST.json) and [test receipts](../fullstack-integration/DEV3_SOURCE_PUBLICATION/TEST_RECEIPTS.json).

Canonical main/Auth/Router remains unchanged. Legacy API-backed route compositions use explicitly named standalone components and are unmounted. Synthetic prototype screens/dependencies are preserved under frontend/prototypes/dev3, outside production src and not imported by the production entry. They are not implemented backend business workflows or permission evidence.

RoomTypeCreationService preserves DEV1 notification injection and adds the historical optional DEV2 dependency reader. Program retains notification interceptors and DEV2 registrations while adding missing DEV3 readers/WorkTime dependencies. Scheduled participant changes are limited to the historical assignment-claim/date checks; no FE04 start-interval lock hierarchy is replaced. Source-compatible registration does not grant runtime acceptance.

Remaining integration blocker: historical HotelOps.Dev3Unblock.NoSql expects pre-004C placement authority and reaches writer-order probes without current DEV2 service authority. Current target correctly throws Dev2AccessDenied before SQL. That failing receipt is preserved; no authority weakening, false lock-order PASS or cross-owner writer change. Owner-compatible test admission is still needed.

PR51 paired active Branch RowVersion cutover is preserved as an exact held patch, not applied. Current active Branch ETag remains unchanged. UC012 R3 artifacts remain DESIGN_ONLY, execution NOT_GRANTED; exact raw hashes are protected from checkout EOL conversion. D5 provider allowlist remains empty.

UC004 source and unchanged handoff/manifest are included; immutable archive was not modified or published. ENV acknowledgement, owned disposable target/principal/lease/TLS, exact fixture/schema/fault approvals and authorized runtime trial remain missing. Historical handoffs are source provenance, not current execution approvals.

UC006/010/016/022/026 and FE12 release fuses remain active. UC007 Remove/Primary/role-bearing Transfer, UC008 Update revision, UC022 status/history mapping, FE12 Notification/Pause All writer coordination and UC140 storage/workflow remain owner-contract blocked. No UC COMPLETE_VERIFIED delta.

READY_FOR_SHARED_INTEGRATION_MERGE=NO pending source review and DEV2-compatible offline verification; SQL/HTTPS, seed, production activation and target merge NOT_RUN.
