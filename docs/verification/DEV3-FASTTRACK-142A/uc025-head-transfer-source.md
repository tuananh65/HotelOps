# UC025 atomic Department Head transfer ? source delivery

Status: IMPLEMENTED_PENDING_QA. New source-complete UC: UC025. New COMPLETE_VERIFIED UCs: 0.
Decision: Project Leader approved atomic designation + explicit mapping; independent grants/mappings and existing workflow ownership remain unchanged.

Implementation:
- Canonical /structure/departments update uses existing confirmation, HTTPS cookie/session, CSRF, If-Match, MASTER_DATA_MANAGE and a current SYS_ADMIN role.
- Existing Serializable BatchAsync transaction retains user, branch/catalog and department locks. Department update, explicit mapping transition and two before/after audits commit together; failure rolls back.
- New Head requires active employment/account, branch access, current DEPT_MANAGER, department membership, branch IR_PROCESS and MR_APPROVE_L1; warehouse-owning departments also require MR_APPROVE_FALLBACK. No role/grant is created. Existing material workflows still enforce independent approver/requester separation.
- Provenance values distinguish DESIGNATION, INDEPENDENT, BOTH and LEGACY_UNKNOWN. Removing designation revokes only DESIGNATION; BOTH becomes INDEPENDENT. Other departments are untouched. Active unknown provenance and future-dated target mappings reject transfer with actionable UI errors.
- Additive installer adds mapping provenance and department rowversion. ETag incorporates the physical version when installed. Existing generated entities and DbContext are unchanged.
- New internal-request notification selection and new material approval cycles require the current designation AND existing explicit mapping/current business grants. Existing request, cycle and handover ownership is not rewritten; later mutations still reauthorize.

Validation actually executed:
- dotnet build HotelOps.slnx --no-restore: PASS, 0 warnings/errors.
- npm run build (tsc + Vite): PASS. Existing React Router use-client warning and >500 KB bundle warning remain (~1001.76 KB JS).
- Canonical browser tests UC024/025: 2 PASS, mocked API; not HTTPS/SQL acceptance. First attempt from repository root failed module resolution; rerun from frontend passed.
- Hotel API transport tests: 91 PASS, including 9 explicit transfer rejection codes without replay or detail disclosure.
- InternalRequestIntakeTests: 2 PASS (focused validation, not SQL).
- dotnet test HotelOps.slnx --no-restore exited 0 without discovering tests; not counted as test coverage.

Runtime / Human QA:
- DepartmentHeadTransferV1.sql is PREPARED, NOT EXECUTED. Exact disposable instance/database/GUID + recent checksum backup guards, transactional trial rollback by default. No shared/production mutation.
- Run installer twice in guarded disposable SQL; verify rollback and compatibility. Existing active LEGACY_UNKNOWN rows require evidence-backed classification by an authorized administrator; never infer source from ManagerUserId.
- Exercise real transfer, retained independent authority, another-department mappings, null Head, inactive/wrong branch/department, missing/revoked grants, unauthorized actor, future mapping, stale version, concurrent mapping/Head changes, induced audit failure/rollback, new routing and unchanged open ownership.
- Post-commit schema rollback preserves provenance: restore the verified backup to a NEW disposable database. Do not drop provenance after real transfers.
- No runtime correctness or COMPLETE_VERIFIED claim until these checks pass.

Changed paths are the UC025 controller/service + DepartmentHeadTransfer helper, internal-request intake, material candidate/cycle admission, hotel frontend transport/form/messages, focused frontend tests, installer and these receipt/matrix documents.
