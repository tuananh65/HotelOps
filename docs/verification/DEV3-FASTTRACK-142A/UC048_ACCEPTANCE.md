# UC048 — View Room Inspection History

COMPLETE_VERIFIED tại C74 trên disposable SQL và trusted HTTPS. Đối chiếu UC DETAIL048 v3 đã lưu và SRS5.10/GBR015/016/057. Không suy ra toàn bộ FE05 hoặc deployment đã hoàn tất.

| Acceptance | Bằng chứng trực tiếp C74 |
|---|---|
| Mở history | Canonical `/inspection-history` có chọn branch; link từ trang chính và deep link notification giữ nguyên. API admission trước khi mount workspace, current session/scope kiểm tra mỗi lần đọc. |
| Actors/scope | HK Supervisor và Operations Manager dùng INSPECTION_VIEW hiện có; foreign branch và revoked branch trả403. Không thêm permission hoặc suy diễn Department Head access. |
| Danh sách/filter | Newest inspection date + ID tie-break; room/date range/result/inspector/official cleaner/keyword và branch; browser page1/2 khác nhau, count giữ nguyên. Invalid page/size/result/reversed dates trả400. Empty MSG10 giữ filter và đổi filter để tiếp tục. |
| Người thực sự dọn | Immutable FE04 attribution + completion log + đúng assignment/room/branch. Cancelled historical assignee không được tính là cleaner. Detail phân biệt CompletedBy với assignment history. Capability thiếu: filter fail-closed, không suy đoán người từ assignment. |
| UC v3 counts | DefectCount là số defect thuộc inspection/đúng checklist version. ReinspectionCount là số record được binding chính thức trong cùng cleaning cycle, gồm lượt đã giao; không lấy tổng inspections trừ1. CycleFailedInspectionCount là số inspection COMPLETED/FAIL của cycle. Thiếu binding capability trảnull, UI “Chưa xác minh”. Browser/API kiểm tra số1 của fixture so với defect/detail và cycle đã Passed. Đây là các phạm vi riêng, không đồng nhất defect với failed inspection. |
| Chi tiết lịch sử | All criteria của version gốc, defects/photos, rework và inspection cùng cycle. Browser tải private evidence, chuyển sang reinspection Passed rồi reload deep link inspection Failed ban đầu. Lịch sử vẫn giữ sau khi pointer phòng chuyển sang một synthetic later cancelled cycle; fixture này không phải acceptance của Create Cleaning Cycle. |
| Read-only | Fingerprint SQL trước/sau history gồm inspection/results/rework, business audit/notification/room-history không đổi. Không có mutation endpoint mới; CSRF/strong ETag của luồng Reject/Rework/Reinspection tiếp tục được kiểm tra bởi harness gốc. |
| Runtime/cleanup | Final trial3 PASS: 22 parent records và 8 history browser flows/114 check records, ở cả phòng thường và effective-blocked. Exact instance/GUID/run, rehearsal rollback và finally cleanup; zero synthetic rows, original guards restored, 0 HTTPS listeners, 0 private files. |

[Final runtime](checkpoint74-runtime.json), [cleanup](checkpoint74-cleanup.json), [independent SQL cleanup](checkpoint74-cleanup-sql.txt). Trial1 PASS trước bổ sung counts/later-cycle; trial2 FAIL do synthetic task_no vượt40 ký tự, đã sửa fixture và cleanup; [earlier trials](checkpoint74-earlier-trials.json). Không nới constraint để sửa fixture.

Backend build0 warnings/errors; regression610 PASS/11 SKIP. Solution test exit0 không tự tính thêm coverage. Frontend history78 PASS, Auth/Router175 PASS ở integration run, hai test admission mở rộng rerun PASS, turnover consumer5 PASS; TypeScript/production build PASS. Existing Vite use-client/chunk768.68KB warnings được giữ. Không rerun npm ci vì lockfile không đổi.

Không đổi generated EF, schema production/shared, permission catalog hay TLS/authentication. Fixture chỉ dùng database disposable đã provision và các capability thử nghiệm được hoàn nguyên. Source runtime trên shared database chưa provision không được coi là deployment acceptance.

Reproduce từ repository root trên đúng máy/database disposable đã xác minh: build solution và ModelProbe, build frontend, rồi `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verification/Invoke-Dev3InspectionHistoryTrial.ps1`. Wrapper giống từng byte với script đã thực thi, yêu cầu port7142/7143 trống, kiểm tra GUID/run/empty fixture và tự tăng tên receipt để không ghi đè. Không chạy wrapper trên database khác hoặc dùng nó làm production seed.
