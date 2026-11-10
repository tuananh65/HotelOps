# [HOTELOPS DEV2] FE04-NOTIF-OWNER-DECISION-007

Existing fs013_assign_with_evidence, fs009_verify_assignment_guard and operation/publication/intent graph are unchanged. No second graph, publication reservation, fingerprint, completed/result_json ownership or domain command change. Same caller transaction remains mandatory; no independent commit or rollback and no direct EF common-target staging fallback. Recipient/source authorization and activation gates remain in place.

Native implementation at FE04 pin: scripts/database/release/fs013-candidate/10_SqlWriteBoundary.sql. Guard:07_ExecutableCandidate.sql. Extracted procedure bodies use static dbo references, no dynamic SQL/EXECUTE AS found. Actual ownership chain, deployed hashes, signing and effective DENY behavior remain owner/runtime evidence. CandidateCommitGuard directly executes fs009, so its direct EXECUTE remains necessary even though fs013 also invokes it.

Important blocker: native procedure body hardcodes HotelOps_Canonical_20261106_f130001_Fresh while FE04 package proposes HotelOps_ENVNOTIF001_Trial_20261109. No native source was retargeted. Schema/native owner must accept exact target-bound deployed module/hash before runtime. Historical installer contains broad grants; excluded from narrowed proposal.

Consumer compatibility:64 discovered xUnit tests PASS on exact FE04 tree plus four DEV2 files. All1727 other FE04 files remain byte-identical, including approval/domain/SQL source. Hook is deliberately unregistered; positive live native/delivery acceptance remains NOT_RUN.
