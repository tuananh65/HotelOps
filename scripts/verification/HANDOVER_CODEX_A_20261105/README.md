# Codex A — bàn giao cuối, 05/11/2026

Đây là snapshot bảo toàn source và WIP, không phải production release. Chủ dự án đã cho phép commit chọn lọc và push nhánh `handoff/codex-a-20261105`. Không thực hiện UC mới. Snapshot current và lịch sử phải được đọc riêng; historical PASS không tái nghiệm thu current runtime.

## 1. Tổng quan dự án và kiến trúc

HotelOps quản lý vận hành khách sạn theo branch: nhân sự/quyền truy cập, cấu trúc khách sạn, phòng, housekeeping/inspection, task/work-time/internal requests, incident/maintenance, asset/inventory/material/replenishment, notification/reporting/audit. Backend duy nhất `src/HotelOps.Api`, ASP.NET Core Web API .NET10, EF Core10, SQL Server Database First. Controllers xử lý HTTP; DTO độc lập entity; services xử lý nghiệp vụ/transaction. Không tách Application/Domain/Infrastructure. `Program.cs` chứa DI và pipeline; `Data/` chứa DbContext; `Entities/` là schema-generated baseline. Không expose EF entities trong responses.

Source SDK: `global.json`; dependencies: `src/HotelOps.Api/HotelOps.Api.csproj`; solution chỉ có API. Tests DEV1 được chạy riêng bằng project/runner Task87. Console verification runners không phải framework cases. DEV2 console/integration suites giữ ownership DEV2.

## 2. Phân công mới và cách đọc source

| Owner mới | Phạm vi feature theo chỉ đạo hiện tại | Source chính / giới hạn |
|---|---|---|
| DEV1 | FE-03/04/05/11/13 | CleaningMonitor, TurnoverHistory (dù nằm trong Services/HotelStructure), Housekeeping DTOs; historical Housekeeping/Inspection/Task/InternalRequest writers trong cf8fb02 phải review trước restore |
| DEV3 | FE-01/02/12 | Authentication/Security/Users; BranchAdministration, RoomTypeDirectory, cấu trúc hotel; WorkTime/Workdays; UC013 trước đây Codex B làm, phải nhận bàn giao riêng |
| DEV2 | FE-06/07/08/09/10/14/15 | Incident/Maintenance/Assets/Inventory/MaterialRequests/Replenishments/Notification/Reporting/Audit, không nhận source của họ là Codex A |
| SHARED | Cross-feature interfaces/infrastructure | DbContext/entities/schema/DI, auth integration, shared DTOs, transaction participants, lifecycle coordinator, source-read adapters, notification contract; thống nhất owner và exact diff trước sửa |

`SRS_ALL_UC_OWNERSHIP.json` đối chiếu toàn bộ catalog SRS (144 IDs). `UC_LEDGER.json/.csv` giữ đầy đủ 75 UC thuộc ledger Codex A cũ, có feature/owner mới, title/classification/weight/credited points/evidence. Feature lấy từ bảng UC ở `docs/requirements/srs_sections/01_overall_requirements.md`, không suy từ tên UC hoặc thư mục C#. UC chứa nhiều feature có thể là SHARED. Bảng classification source ở `SOURCE_OWNERSHIP.csv` là định hướng review, không thay permission matrix.

AGENTS.md cũ vẫn ghi hai BE và phân công cũ; yêu cầu bàn giao hiện tại có ưu tiên cao hơn. Chưa sửa AGENTS thay cả nhóm. DEV3 cần chốt/update hướng dẫn repository với team.

## 3. Database First và schema contracts

Ưu tiên: yêu cầu explicit → approved changes → SRS_CHANGE_NOTES → SRS draft → schema/mapping/current source. Đọc `docs/database/` và `scripts/database/release/`. Schema có sẵn; không migrations/generated entity edits nếu chưa được duyệt. Local `ConnectionStrings:HotelOpsDb` dùng User Secrets; không lấy SQL instance trong receipts làm shared config. Không kết nối protected SQL trong bàn giao.

WorkTime additive entities/mapping và `ConfigureWorkTime(modelBuilder)` đã có trong current source, với approval/evidence Task77 A1. SQL trial install không chứng minh DB chung đã triển khai. RoomTypeDirectory/TurnoverHistory dùng schema guards/unmapped read projection để giữ shared EF model. Branch rowversion đọc bằng unmapped SQL projection; generated Branch không được thêm RowVersion để ép test chạy. Schema rowversion/unique-code prerequisites cần đối chiếu trial-specific Task86 receipts. Script prepare/DDL là lịch sử gated, không auto-run.

Mọi query async + CancellationToken, read-only AsNoTracking khi phù hợp. Multi-table business+audit+participant cùng transaction do caller sở hữu. Không chạy test EF InMemory để chứng nhận SQL constraints/rowversion/isolation.

## 4. Authentication, Cookie Session, RBAC

FE01 hiện có auth, credential lockout/reset, temporary-password restrictions, SQL session create/validate/touch/revoke, cookie events, antiforgery, error handling, current identity và permission policies/resources. Xem `Security/`, `Services/Authentication/`, `Services/Users/`, cùng verification folders đã commit trong parent history.

`HotelOpsCookieTicket` dùng scheme HotelOpsCookie, host cookie, ticket/session binding; CurrentUserAccessor yêu cầu request-local validated SessionSnapshot. Actor ID/role/client branch không tự cấp quyền. PermissionService đánh giá từng grant path ALL/BRANCH/OWN và verified authoritative resource branch/ownership. Role-name SYS_ADMIN không bypass. Account active, employment eligibility, responsibility và source-read permission là các kiểm tra riêng. Department Head quản lý phạm vi branch, không phải chỉ Department.ManagerUserId.

Fallback authenticated policy đã có trong local current source; health readiness và Development OpenAPI explicit anonymous; template WeatherForecast bị xóa có approval trong FallbackAuthorization/README.md. Explicit endpoint policies vẫn cần đúng quyền riêng. SqlSessionPolicy.AbsoluteDeadline giữ UTC kind khi datetime2 materialize unspecified (Task79). Không suy fallback authenticated-only là đầy đủ business authorization.

Reception/Operation Manager/Department Head revised permissions và room transitions vẫn cần matrix được phê duyệt. Không tạo permission codes/grants dựa vào bản draft.

## 5. UC ledger và trạng thái hiện tại

Credited historical baseline **26/75 UC, 60/256 điểm =23.4375%**, không cộng thêm điểm từ recovery/regression/handover. Complete/partial/blocked từng UC nằm trong full ledger. `COMPLETE_VERIFIED[_V1]` phản ánh evidence đã credited; source có ở Git lịch sử không đồng nghĩa route chạy trong current checkout.

| UC theo ledger/SRS | Owner mới | Trạng thái / current source |
|---|---|---|
| UC012 Update Branch Information | DEV3 / FE02 | PARTIAL; Create/Read/Update runtime hiện có, conditional rowversion, audit/transactions; full Deactivate fail-closed và còn GBR033 dependency |
| UC013 View Branch Structure | DEV3 / FE02 | Historical credited; Codex B được giao riêng trước đổi tổ chức. Không trộn current B WIP. Service/controller historical trong cf8fb02; nhận handover B và exact DI diff riêng |
| UC037 Monitor Cleaning Tasks | DEV1 / FE04 | PARTIAL; GET list/detail/timeline page hiện có; Cancel/lifecycle/mandatory notification chưa đầy đủ |
| UC038 View Room Turnover History | DEV1 / FE04 | Credited COMPLETE_VERIFIED_V1; recovery Task85B có runtime hiện tại, scoped list/detail/private evidence; xem current tests vs historical SQL |
| UC138 Manage Operational Workday and Shift Records | DEV3 / FE12 | NOT_IMPLEMENTED full public workflow; ShiftAssignmentConflictReader có read component; mapping/rowversion/notification gates còn |
| UC139 Record Workday Start and End | DEV3 / FE12 | BLOCKED_EXTERNAL; scheduled own B1.1 source có, B1.2 unscheduled/Pause All + DEV2 participant chưa integrated |
| UC142 View Room Types List | DEV3 / FE02 | Credited COMPLETE_VERIFIED_V1; recovery Task84 current list/detail read-only, legacy global-only ownership giữ nguyên |

Các writer UC029/041/046 và nhiều FE02/housekeeping/checklist paths từng có trong snapshot cf8fb02 nhưng không có current runtime graph. UC044/045 không được coi là implemented từ inspection finish PENDING_CONCLUSION. Không bulk restore RoomTypeCreationService partial graph hoặc generated mappings để làm xuất hiện route.

## 6. Lịch sử Task80 → mới nhất

| Task | Kết quả có thời điểm | Giới hạn |
|---|---|---|
| 80 | DEV2 producer/writer handoff; UC138 conflict reader 81 checks, time math422 lịch sử | Không full UC138, không notification producer integration |
| 81 | Readiness/C1 decision package | C1-A/D reviewable; B/E NO_GO, C blocked; không nghiệp vụ mới |
| 82 | UC037 timeline pagination; historical167 HTTPS/SQL +14 boundary checks | UC037 vẫn PARTIAL, Cancel thiếu |
| 83 | Recovery selection audit/reconciliation | BLOCKED before implementation; UC038/142 đã credited, không cộng lại |
| 84 | UC142 restored; approved DI; historical131 HTTPS/SQL assertions | Trial-specific acceptance, không shared deployment |
| 85 Phase A | DEV2 Notification/P0/B12 inventory/handoff | Source/design/reported evidence khác runtime accepted |
| 85 Phase B | UC038 restored; historical146 HTTPS/SQL +26 field-oracle checks | Earlier failed harness receipts giữ lại, không cộng trùng |
| 86 | UC012 Create/Update restored + trial-specific concurrency/audit evidence | UC012 PARTIAL do Deactivate. Historical failed harness83+1,37+1; later51+0; SQL oracle13; không giấu execution history |
| 87 | xUnit framework foundation, final91 Passed | No SQL/HTTPS framework integration; SQL acceptance backlog NOT_RUN_SQL_GATE |
| COORD-FINAL-01 _02 | Shared decisions/contracts/source evidence | Design accepted không tự phê duyệt executable DDL/EF/DI/guards |
| UC013 readonly handoff _4 | A xuất evidence/provenance cho B | Không phải B implementation acceptance; ownership nay chuyển DEV3 theo yêu cầu |

Các báo cáo Task75–79 và approval records được bảo toàn để giải thích fallback, A1, B1.1, UTC expiry và B1.2 proposals. Current numbered task mới nhất là87; không bắt đầu88.

## 7. Notification và DEV2

Nguồn quyết định: `Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104_02/`. A01 interface ACCEPTED ở mức quyết định; exact source chưa được chứng nhận trong checkout A. A02 structural DEV2-reported, không runtime acceptance. B01 architecture ba bảng reservation/publication/delivery_intents (chỉ delivery_intents là retry queue); executable B01 DDL, B02 EF, C01 guard/DI REVISE.

Caller-owned transaction phải gồm business+audit+operation completion+mandatory publication/intents. Persist intent thất bại trước commit rollback business; delivery thất bại sau commit giữ business và durable retry. Stable Idempotency-Key GUID, UNIQUE(actor_user_id,operation_key), canonical versioned fingerprint, authorized replay/original result. Publication immutable, recipients source revision cố định; retry không lấy latest assignment để đổi recipient. DELIVERED cần durable inbox evidence. JOB03 cadence5 phút, exponential backoff cap60 phút; terminal reasons không dùng cho transient policy/DB outage. Không tự backfill/purge reservations.

DEV2 tự làm internal FE06–10/14/15 theo contract; shared exact diffs phải review/approve. Notification không cấp quyền đọc source. Không để owner mới đoán namespace/signature hoặc áp B12 như interface đã có.

## 8. Room lifecycle và transaction boundaries

Ba chiều Occupancy / Housekeeping Readiness / Effective RoomBlock độc lập. Check-in cần Active+Vacant+Ready+không effective block; Ready phải có passed current inspection. ExpectedReleaseAt không tự release; incident chỉ release block của chính nó. Một unreleased block mỗi(RoomId,IncidentId), một manual unreleased/room theo design.

Current Room entity vẫn legacy OperationalStatus, thiếu persisted dimension/current cycle contracts. EF default AVAILABLE khác SQL release DIRTY; unique unreleased room block chỉ room_id chặt hơn design cardinality. Exact migration/coordinator/history/source-read/DI chưa approved. Không đổi schema hoặc suy AVAILABLE=Vacant+Ready. Coordinator/participants không tự commit; global total lock order chưa source-verified.

UC012 branch-active writer protocol phải phối hợp DEV2 create/open/reopen writers cùng retained locks/transaction. Deactivate không hard-delete/cancel work để vượt blocker. Cả hai chiều Create→Deactivate và Deactivate→Create cần deterministic SQL tests; SQL1205 là FAIL. Current branch deactivation giữ fail-closed.

## 9. Workday / UC139 / UC138

Task77 A1: Workday/WorkInterval, filtered uniqueness WORKING day và OPEN interval/user; IWorkTimeTransactionParticipant/concrete dùng cùng scoped instance. Runtime kinds CLEANING/REWORK/TASK, không tự nhận MAINTENANCE/PM dù SQL vocabulary rộng hơn. B1.1 own Start/Current/Own-Shifts/End được giữ. ATTENDANCE_RECORD ALL/BRANCH không cấp cross-user Workday manipulation.

B1.2 chưa applied: unscheduled reason>=10, một primary eligible Department Head của branch, không manager fallback/broadcast; recipient cần actual cross-user source-read permission. Pause All atomic pause valid source+close interval+history/audit+End; một item lỗi rollback tất cả. DEV2 sở hữu maintenance/PM participant; không PM PAUSED invented, không ShiftAttendance thay WorkInterval.

UC138 read conflict component không là Manage Shift workflow. C1 mappings/rowversion, participant/source-read, mandatory notification approval vẫn cần exact diff. Xem Task78/79/80/81 approval scopes; không tái dùng trial permissions cũ.

## 10. Source provenance và artifacts

`WORKTREE_INVENTORY.json`: actual branch/HEAD/index/status/tracked/untracked hashes của mọi worktree, chỉ đọc worktrees ngoài A. `GIT_AUDIT.json`: upstream, six commits ahead cached upstream, all refs. Six commits gồm DEV2 merges vốn có trong ancestry, không claim tác giả Codex A. Remote target được ls-remote trước push và xác minh exact SHA sau push.

Historical important DEV1-A snapshot `cf8fb020362f8f64192e7f2f14a10547cebe5b91` đã ở remote `integration/dev1-fe-handoff`; frontend integration ref `FE+BE/integration/dev1-fe-handoff` ở `0006f471c190eaf3536cc022dfb63efbf3db60fa`. Giữ refs đã có, không overwrite hoặc cherry-pick vào current. Root HEAD ban đầu `4a187320643d0c82f232c6b9dd5a667b8401c280`.

`PHYSICAL_SOURCE_ARTIFACT_INVENTORY.json` bao gồm text source/evidence cả ignored logs ngoài build dirs. `PRESERVATION_MANIFEST.json` ghi exact selective paths/hashes/actions. `SCRATCH_SOURCE_INVENTORY.json` và content-addressed archive bảo toàn source/WIP Codex A trong root obj backups, tách khỏi runtime. Copy có nhãn archival/unapproved, không chạy tự động. Other-owner worktrees chỉ metadata/source hashes, không commit thay owner. Binary zip duplicates/build outputs/SQL data dumps/private credentials không được commit. SQL release/fixture scripts không phải database dumps; chúng cần guard/approval mới để execute.

Private local SQL host được thay bằng REDACTED_LOCAL_SQL_HOST trong bản sanitized archive khi cần. Original vẫn giữ nguyên local và không staged; byte hashes chứng minh provenance. Sanitized evidence không phải exact original receipt, không được coi checksum original giống bản công bố. Scripts archived dạng .txt cần deliberate local configuration trước tái chạy. Credentials generated runtime không phải committed plaintext.

## 11. Kiểm thử hiện tại và lịch sử

Current handover: `dotnet build HotelOps.slnx` PASS,0 errors/2 NU1900; `dotnet test HotelOps.slnx` exit0/no framework cases trong solution; project xUnit explicit91 discovered/91 Passed/0 Failed/0 Skipped, TRX retained. Production fallback93 checks PASS, giải thích baseline199=93+49+43+14. Task87 đã chạy default fallback61 nên report167, không phải production regression. Không sửa receipt Task87 lịch sử.

Task87 regression evidence giữ lại UC03714, UC03826, UC14219, UC01239, ResourceAuthorization49, PermissionService43, CurrentUserAccessor14 và hardening21 PASS. Các lượt này là earlier session/current source evidence, không nhận thành vừa chạy lại nếu không có log handover mới. Historical Task84/85/86 SQL/HTTPS không chạy lại trong handover, không protected DB access. Rowversion/concurrency/duplicate/audit rollback/query result scope cần fresh approved SQL trial cho framework suite. No line coverage collector configured. Current build không chứng minh schema deployment.

## 12. Shared dependencies / approvals / next actions

Shared: Program DI/resolver wrappers; DbContext/entities/release schema; auth fallback/cookies/current-user/session locks; global admin & eligibility locks; permission catalog/policy/resource adapters; branch active/ownership; Room lifecycle; Workday participant and source-read; Notification publication/transaction/idempotency; common DTO/API conventions. Owner mới cần review owner trước sửa.

Pending approvals/decisions: revised role/permission matrix; notification executable B01/B02/C01; lifecycle schema/default/block-index compatibility/coordinator; B1.2 unscheduled/Pause All/cross-user read; DEV2 maintenance/PM participant; UC138 C1 mapping/rowversion/notification; UC012 GBR033 writers/terminal PM predicates/races; UC013 Codex B exact diff integration; new disposable SQL trials. Không blanket approval từ architecture/historical trial receipts.

Ưu tiên DEV3: nhận Codex B riêng; đối chiếu ledger vs current routes; giữ cf8fb02 để phục hồi có chọn lọc; chốt auth/branch/workday shared contracts với DEV1/DEV2; update AGENTS ownership; chạy build/framework trên checkout handoff; request new SQL gate nếu tiếp tục. DEV1 nhận housekeeping/inspection histories/notification dependencies; DEV2 tiếp tục phần cũ. Không nhận thêm UC credit cho handover. Không tự merge main/develop hoặc triển khai DB.


## Final audit additions

Codex B independently published/committed its own handoff at21c06bf51bcbcf0c99e8c711302b71682590e41f on handoff/codex-b-20261105 while A was auditing. WORKTREE_INVENTORY captures that committed state. A did not change B branch/index/source or adopt its implementation. The task11 rehearsal still has103 staged changes, inventoried only; its owner must preserve/publish them independently if needed.

Full catalog144 ownership: DEV1=37, DEV3=31, DEV2=67, SHARED=9. These are UC allocation counts, not completion counts. Historical ledger75: DEV1=37, DEV3=31, SHARED=7;26 credited complete/60points unchanged. Exact UC138 title is Manage Operational Workday and Shift Records (FE12); UC139 is Record Workday Start and End (FE12).

All222 text entries of5 ZIPs are preserved in package-archive; ZIP_INVENTORY records original hashes.2561 ignored scratch candidates are preserved as476 unique text blobs;861 DEV2 reference-copy paths inventory-only. Neither archive is runtime.42-byte historical PDF fixture preserved as base64/hash. No database dump included.

Selective index contains sanitized versions at original paths as well as archived text copies; local originals stay untouched. On clone these historical verification files compile with a redacted placeholder SQL host and must be deliberately configured before any approved trial execution. Inherited historical checksum claims refer to original bytes, not sanitized export. Git index blob IDs in STAGED_FILES.json are authoritative for exported content; CRLF normalization during Git add can differ from original filesystem SHA256.

Whitespace validation: `git diff --cached --check` FAILED on historical reference/evidence/archival text and trailing blank lines. `git diff --cached --check -- src tests` reports only two new blank EOFs in Task87 test files; production C# change hunks have no reported whitespace issues. No historical evidence was rewritten solely to silence whitespace warnings. Git add also warned about LF→CRLF normalization. These are known preservation limitations, not build/test failures.

## Supplementary ignored-evidence preservation

Final source-preservation audit additionally inventoried4382 ignored historical text evidence files in root obj.3689 archival candidates are preserved as1597 unique redacted text blobs in ignored-evidence-archive;390 byte-identical items reference already published content;3 other-owner references and300 database-export/build-metadata items remain inventory-only. No database data dump was newly packaged. Additional schema/review script audit374 paths:77 already preserved,297 instances retained as26 unique archival scripts in additional-scratch-archive, never executed. Original hashes/paths and historical-only labels are in IGNORED_EVIDENCE_INVENTORY.json and ADDITIONAL_SCRATCH_INVENTORY.json. Final supplementary credential/private-host scan found0 paths. Database fixtures/metadata exports and build logs are classified in inventory; confidential raw/local files remain on disk.

Source snapshot e859006fcff91b1b913b18c27844a73809a18d8a was pushed to handoff/codex-a-20261105 and remote SHA verified. A subsequent evidence-only commit adds this final audit and PUBLICATION_RECEIPT. It does not change src/, tests/, credited progress or runtime behavior. Final branch HEAD is reported in the completion response and can be read directly on GitHub.

## Publication restriction — resolved by explicit local-retention decision

Status: HANDOVER_COMPLETE within the clarified scope. User explicitly chose “Giữ local, bàn giao inventory/hash” after review rejection. Source snapshot e859006fcff91b1b913b18c27844a73809a18d8a was already pushed and verified. Automatic approval review rejected the later bulk publish of1597 ignored-evidence text archives and26 additional schema/review script archives: Codex A ownership and sensitivity of the full historical set were not sufficiently established. No workaround or indirect publication of that content was attempted.

The supplementary archive contents remain LOCAL ONLY in ignored-evidence-archive/ and additional-scratch-archive/. Published supplementary inventories contain paths/hashes/classification only; these are not the contents. An explicit decision was requested; the user chose LOCAL retention and inventory/hash handover, so no further archive upload is authorized or pending. Earlier sections describing these archives describe local preservation, not GitHub availability. The476 source backup blobs, expanded ZIP text, current source/tests/Task75-87 evidence in the original e859006f snapshot remain published.

The next evidence-only commit is deliberately limited to receipt, corrected handover reports and path/hash inventories. It excludes all newly blocked archive payloads and their SQL/text contents. Historical SQL integration remains NOT RUN in this handover. Partial/blocked UC classifications and60points are unchanged.

Final user decision: “Giữ local, bàn giao inventory/hash”. The local-only archives and their supplementary preparation scripts are intentionally excluded from publication. This resolves the rejected bulk-egress step by reducing scope; it does not approve or indirectly publish the archive contents. Handover is complete when the metadata/receipt commit is pushed and final remote SHA verified.
