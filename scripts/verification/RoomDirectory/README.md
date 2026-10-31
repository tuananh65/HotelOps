# Task60 — UC023 Room Directory internal read acceptance

Ngày 30/10/2026. **Internal list: implemented + SQL verified. Full UC023: PARTIAL.**
HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`, branch
`feature/BE1-database-integration`, upstream ahead6/behind0 theo local refs; không fetch.
Index không staged changes; bốn worktrees và 385 preexisting modified/untracked files được
copy/hash tại `obj/task60-preflight-20261030-044344/manifest.json`. Không sửa Task51–59.

## Contract được thực hiện và ranh giới

[UC023](../../../docs/requirements/srs_sections/02_use_case_specifications.md) §2.12 yêu cầu:
logged-in user có permission; list room number/floor/type/current operational status;
lọc branch/floor/area/type/status/active/keyword; sort floor rồi room number; empty matching list;
room detail/current reason/source/history ở bước5–6; không thay đổi dữ liệu.
[GBR015 / SYS-PAR11](../../../docs/requirements/srs_sections/05_requirement_appendix.md)
yêu cầu scope, pagination default20/max100 và record-ID tie-breaker.
[Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md) và
[Task20A decisions](../HotelStructureRead/IMPLEMENTATION_PLAN.md) vẫn chưa phê duyệt D3/D5/D7.
Không coi draft actor list là grant cho SYS_ADMIN/OWNER/STOREKEEPER.

Hai production files mới:

- [RoomDirectoryContracts.cs](../../../src/HotelOps.Api/DTOs/Rooms/RoomDirectoryContracts.cs):
  validated internal query + explicit nested response DTOs, không EF entity/navigation graph.
- [RoomDirectoryService.cs](../../../src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs):
  `ListAsync(SessionReference authenticatedSession, RoomDirectoryQuery query, CancellationToken)`.
  Ticket phải từ authenticated server context; không dùng request-supplied actor/claims/branch
  làm authority. Dùng actual `CurrentUserAccessor`, `SqlSessionService`, `PermissionService`
  và `EfPermissionDataSource`, tất cả phải cùng scoped DbContext khi composition.

Service chưa đăng ký DI và không có controller. Đây là chức năng nội bộ có authorization,
không phải public API DTO/route được duyệt. Không sửa Program/auth infrastructure/catalog,
DbContext/generated entities/schema, DEV2 source hoặc UC006 release gates.

| Behavior | Implementation / limit |
|---|---|
| Session / authorization | Fresh SQL hash-bound session phải khớp actor/UserId/SessionId và unrestricted; existing `ROOM_VIEW` Branch evaluation trước mọi room count/page. SQL session/permission checked lại trước publish. Không role-name bypass |
| Branch | Một required positive branch cho internal call; SQL Room.BranchId predicate. Floor/Area phải cùng branch và Area.FloorId null hoặc khớp room floor; malformed legacy links không lộ metadata branch khác. Không gán BranchId cho RoomType global |
| Filters | FloorId/AreaId/RoomTypeId/status/active kết hợp; keyword là literal RoomNumber substring, trim blank; không coi `%/_/[` là user LIKE pattern. Search limit200 là internal input bound, chưa chốt HTTP search contract |
| Status | Bảy raw stored codes theo CK_rooms_operational_status, exact casing; không chuyển thành display/lifecycle state, không tính occupancy hoặc current status reason |
| Inactive references | Room.IsActive tri-state; không ẩn room chỉ vì Floor/Area/RoomType inactive. Branch inactive vẫn bị existing evaluator deny |
| Sort | Internal deterministic order: numbered floors trước null, FloorNumber/FloorCode/FloorId, lexical RoomNumber theo SQL collation, RoomId. Không nhận dynamic SQL sort. Null/natural/collation HTTP decision D7 chưa được duyệt |
| Pagination | Page>=1, size1–100, default1/20; checked Int32 offset. LongCount sau scope/filters và page vượt cuối trả empty. Hai joined room queries độc lập số phòng, không N+1 |
| Consistency | Dedicated clean SQL context; service owns SERIALIZABLE tx. Existing User→Session locks trước grants/features; retained auth/count/page/joins, không feature→User acquisition mới. Không snapshot DB option hoặc schema patch mới |
| Read-only | AsNoTracking; không SaveChanges/session touch/audit cho reads. Không trả description, credential, user/session/device hoặc feature evidence. Exceptions/cancellation dispose tx; không tự retry |
| Errors nội bộ | Invalid input: ValidationException trước SQL. SessionInvalid/Forbidden/AuthorizationUnavailable không có Directory. Missing/inactive/unauthorized branch đều Forbidden, không query room count. Compatible nhưng không match filters: Success/empty. Query/provider faults propagate; CT cancellation thành OCE kể cả provider cancellation error |

Count/page consistency là trong **một request**; các pages đọc bằng request khác có thể thay đổi
khi writer commit, không hứa snapshot giữa nhiều request. SERIALIZABLE và existing session
UPDLOCK có thể chặn writers/reads cùng actor; transaction ngắn, bounded query/page, CT/command
timeout do caller cấu hình. SQL wait tests là bounded schedules, không proof mọi global writer
deadlock-free hay full UC006 integration acceptance.

## Actual SQL verification

[Harness](Program.cs), [project](RoomDirectory.Verification.csproj),
[accepted receipt](task60-sql-accepted.local.json): **117 checks PASS / 0 FAIL,
11 DMV-confirmed SQL lock waits**. Production service, session validator, accessor và EF grant
data source thực sự chạy; không fake authorization hoặc sửa canonical RBAC grants.

Trial: `HotelOps_Canonical_20261030_044852_22713f76_Fresh`.
Setup dùng unchanged UserDirectory/Prepare-Database.ps1 (canonical schema/reference/catalog
và approved existing patches), rồi unchanged guarded UserProfileUpdate/rowversion-forward.sql.
Harness kiểm tra exact trial name, expected SQL instance và Task17 marker trước fixture;
StartupSchemaValidator Compatible trước mọi acceptance. Không truy cập HotelOpsDB.

| Scenario | Executed evidence |
|---|---|
| Retrieval / empty | 29 valid scoped rooms; multiple floors/types/areas; physically empty authorized branch; unmatched search; no-match count/page |
| Projection / sorting | Nullable area, branch-level area, inactive parents, global types, raw statuses, floor-number ties/null-last/room order; malformed cross-branch/hierarchy FK rows excluded; deterministic page composition |
| Filters / bounds | Seven stored status codes, active/floor/area/type/combined filters; foreign/incompatible IDs no cross-branch data; literal wildcard search; size1/default20/max100, last/beyond-last and Int32 offset; 11 invalid-query cases execute zero SQL |
| Authorization | HK branch allow; DH branch-wide allow without department narrowing; other branch with different role cannot borrow ROOM_VIEW path; no-grant actor and SYS_ADMIN/master-data-only deny; missing/inactive branch deny before count; anonymous/ticket substitution/wrong hash denied |
| Read-only / N+1 | Exactly one room count + one joined page; no tracked entities/open tx after result; all mapped rows before/after reads equal, including sessions/audits/credentials/catalog |
| User-first writer wait (6) | Actual production reader waits on retained User lock while competing transaction commits account inactive/session revoked/assignment inactive/membership revoked; authoritative reread denies before room query. Cancellation and actual SQL1222 release tx without room queries |
| Read-first waits (3) | Pause production read after count, before page; independent room-status UPDATE, room INSERT or User-first grant revoker waits (DMV). Read returns consistent precommit count/page/authority; next request sees committed change |
| Room-first waits (2) | Uncommitted room status writer makes actual reader wait, then committed status read; cancellation during real room lock wait returns OCE and releases whole tx |
| Time / faults | Expire SQL session or assignment time after count; final gate withholds data. Interceptor faults actual authority/page commands; unavailable/no partial response and disposed tx; fresh normal read recovers |
| Preservation | All mapped rows after owned fixture cleanup equal pre-run snapshot; canonical catalog unchanged. Trial DB/artifacts retained; SQL identity/global rowversion counters may advance |

Room INSERT/status writes in these schedules are **test fixture SQL**, not certification of an
unimplemented room writer. Global ALL/OWN/unknown grants are covered by foundation regressions;
this SQL suite does not modify catalog to invent such ROOM_VIEW grants. No DEV2 race/full UC006
acceptance claimed.

First execution: `obj/task60-sql-baseline.log` retained. It reached the final page-fault case,
then harness expected unwrapped TimeoutException; EF wraps it in InvalidOperationException.
Harness now checks the exact injected inner timeout, still propagating unrelated failures.
No production workaround/retry/contract change. Full rerun passed with new accepted receipt.
Initial sandbox SQL connection failed SSPI before setup; guarded setup/tests succeeded under
the authorized Windows execution context. No schema workaround was used.

Reproduce only with a **new guarded canonical trial** and NEW receipt path:

```powershell
& scripts/verification/UserDirectory/Prepare-Database.ps1 -Database <fresh-trial-name>
# Apply unchanged guarded scripts/verification/UserProfileUpdate/rowversion-forward.sql
# to that same trial; never run setup/patch against HotelOpsDB.
dotnet build HotelOps.slnx
dotnet build scripts/verification/RoomDirectory/RoomDirectory.Verification.csproj
dotnet scripts/verification/RoomDirectory/bin/Debug/net10.0/RoomDirectory.Verification.dll <fresh-trial-name> <new-receipt.local.json>
```

The existing setup script's canonical instance guard remains unchanged. Receipt path cannot
already exist; suite budget6min, command20s, SQL wait observation6s, barrier15s; no infinite waits.
Snapshots stay in memory and never enter logs/receipt. Cleanup removes only owned trial fixture
rows in FK order; không xóa database/artifacts/worktrees.

## Build / regression / HTTP

- `dotnet build HotelOps.slnx`: **PASS**, 0 errors; NU1900 vulnerability-feed warnings do
  api.nuget.org unavailable (restore/build cùng cause). SQL harness build PASS; no C# warnings.
- Relevant targeted regression rebuilt + executed **không `--live`**:
  PermissionService43, CurrentUserAccessor14, Authorization61, ResourceAuthorization49 =
  **167 PASS**. Authorization HTTP suites dùng production handlers + session/data doubles;
  không phải HTTP Room Directory hoặc SQL business ownership certification.
- `dotnet test HotelOps.slnx`: exit0; solution chỉ chứa API project, không có discovered test
  project. Không gọi exit0 là automated unit tests PASS; executed verification là console harnesses.
- HTTP UC023: **NOT RUN / endpoint not registered**. Không cấp quyền bằng technical proposal.
- Whitespace/preservation **PASS**: `git diff --check` và cached check exit0;
  **385/385 preexisting SHA-256 giữ nguyên**, HEAD/index/bốn worktrees giữ nguyên, chỉ sáu
  files mới trong Room Directory scope. Git vẫn báo sáu LF→CRLF notices cho source có trước
  task; không rewrite chúng. Receipt/hashes: `obj/task60-validation.local.json`.

## Remaining blockers / next implementation

1. **D3:** approve raw SQL status list slice hay approved display/current-reason derivation.
   Các source task/inspection/block có thể cùng tồn tại; chưa chốt authoritative current source,
   reason fields và permission cho linked details/history. Không suy latest history = current reason.
2. **D5:** approve single-branch phase hay authorized All Branches; reconcile draft actor list
   với actual grants. ROOM_VIEW không được tự OR MASTER_DATA_MANAGE hoặc thêm Owner/Storekeeper.
3. **D7:** approve route/query/response/error/search fields/collation/natural-vs-lexical/null ordering,
   inactive defaults/filter behavior và full detail/history visibility. Internal query choices trên
   không thay thế approval cho public API.
4. Sau approval, P01b thêm controller và scoped DI theo Task60 authorization; cần giải thích shared
   DI impact, preserve resolver/security infrastructure và SQL+HTTPS authorization/session/scope
   acceptance. Không cần tạo role/permission/schema để mở read endpoint.

**Next task: P01b UC023 HTTP integration + approved detail/status/history slice**, dùng service
đã kiểm thử này, targeted real HTTPS/SQL verification; giữ PARTIAL tới khi toàn UC đạt DoD.
Không tiếp tục UC006 audit để thay cho Room Directory implementation.
