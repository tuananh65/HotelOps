# Đối chiếu nguồn FE06 UC049

Worktree sạch ban đầu: 6aee2a0e692a7814f41c81224709cddfbe1e2ec4; đúng nhánh feature/dev2-fe06-uc049-integration025b. Ba Git object tham chiếu tồn tại. Không reset/merge/rebase/fetch; REMOTE_FRESHNESS=UNKNOWN.

Đã đọc ba report 024B tại repository gốc; không làm lại audit. Placement 2140522457e89b974cf2d65c64515ec15d733fa9 và FE03 6143218925fddb2e9927a5d053ae43041940ae41 không phải ancestor của baseline. Dùng chúng làm bằng chứng nguồn, không gán approval. Host auth d0a5a073b7921040f1e2ed726782ca97e0e30e61 chỉ là reference composition đã pin. Không có bằng chứng acceptance mới.

Sparse expansion thất bại exit128 vì sparse-checkout.lock ngoài writable roots. Khôi phục dependency byte-for-byte từ HEAD bằng git archive vào worktree; không sửa metadata sparse. Các lần thử pipeline PowerShell/tar lỗi đã được thay bằng archive file + tar --force-local. Dependency đã khôi phục; build cuối pass.

Đã inspect original candidates và tracked/untracked/hash trong SOURCE_PINS.json. Không copy cả cây. Chỉ adapt create workflow và authority/placement từ commit placement vào folder Incidents; exception filter từ original working source. Viết lại frontend FE06 nhỏ để dùng auth host đã pin, không lấy shell/router/services dirty từ repository gốc. Giữ tùy chọn legacy reporter thay vì JsonIgnore của local candidate để không âm thầm chấp nhận mismatch.

Status/triage và list/detail service khớp nguyên đoạn nguồn baseline sau chuẩn hóa newline: {"statusAndTriageUnchanged":true,"listAndDetailUnchanged":true,"scope":"normalized exact source comparison, NOT runtime workflow regression tests"}. Đây là kiểm tra source, không phải runtime regression.

Source/test tạo hoặc sửa (16):

- `src/HotelOps.Api/Controllers/IncidentsController.cs`
- `src/HotelOps.Api/DTOs/Incidents/CreateIncidentRequest.cs`
- `src/HotelOps.Api/Services/Incidents/IIncidentService.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentService.cs`
- `frontend/src/incidents/ReportIncidentRoute.tsx`
- `frontend/src/incidents/api.ts`
- `frontend/src/incidents/navigation.ts`
- `frontend/src/screens/maintenance/ReportIncidentScreen.tsx`
- `frontend/tests/incidents/uc049.test.cjs`
- `src/HotelOps.Api/Controllers/IncidentAccessFilterAttribute.cs`
- `src/HotelOps.Api/DTOs/Incidents/IncidentOptionsDto.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentCreateAuthority.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentPlacementGuard.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentService.Options.cs`
- `tests/HotelOps.Incident.UnitTests/HotelOps.Incident.UnitTests.csproj`
- `tests/HotelOps.Incident.UnitTests/Program.cs`

Danh sách report và hash đầy đủ trong CHANGED_FILES_SHA256.json. Không sửa generated entities, mappings, Program.cs, shared DI/security, SQL hay FE03/04/05. Git add chỉ explicit paths thất bại exit128 do index.lock; không tạo implementation commit.

