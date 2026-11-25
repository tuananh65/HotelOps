# Đối chiếu Report 3, Report 4 và Report 5 — Compact V2

Cập nhật 25/11/2026. Đây là bảng đối chiếu và danh sách phần còn thiếu theo Master mục 40, không phải báo cáo nghiệm thu hoặc mẫu báo cáo mới của trường.

## Nguồn và cấu trúc đã kiểm tra

| Report | Nguồn thực tế đã tìm thấy | Kết luận về định dạng |
|---|---|---|
| Report 3 — Software Requirement Specification | `docs/requirements/HotelOps_SRS_DRAFT.docx`, bản chuyển đổi `HotelOps_SRS_DRAFT.md`, `srs_sections/` và `SRS_README.md` | Markdown ghi rõ là bản chuyển đổi Report 3. Đã kiểm tra cấu trúc: I Overall Requirements, II Use Case Specifications, III Functional Requirements, IV Non-Functional Requirements, V Requirement Appendix. UC có bảng HTML chứa UC ID and Name, Primary/Secondary Actors, Trigger, Preconditions, Postconditions, Normal/Alternative Flows. |
| Report 4 — Software Design Description | Chưa tìm thấy tài liệu mang tên/vai trò Report 4 trong các tệp nguồn dự án đã kiểm tra. Có `DESIGN.md`, `docs/architecture/`, tài liệu database và thiết kế V2. | `DESIGN.md` là thiết kế giao diện HotelOps Cacao, không phải bằng chứng về mẫu Software Design Description của trường. Chưa kiểm chứng mẫu Report 4; không tự thay thế bằng một mẫu suy đoán. |
| Report 5 — Testing | Chưa tìm thấy tài liệu mang tên/vai trò Report 5 trong các tệp nguồn dự án đã kiểm tra. Có `docs/testing/`, `docs/verification/`, E2E results và artifacts V2. | Các implementation/test reports từng module không tự trở thành Report 5 chính thức. Chưa kiểm chứng cấu trúc mẫu Report 5. |

Phạm vi tìm kiếm: tệp ở gốc repo; tên tài liệu Report/report, SRS, DESIGN và DOCX trong `docs`; nội dung Report 4/5/Software Design Description trong `docs/architecture` và `docs/testing`. Không kết luận rằng tài liệu ngoài workspace không tồn tại. Chưa chỉnh định dạng Word hoặc sơ đồ ảnh trong SRS; bản Markdown chỉ dùng đối chiếu nội dung. `SRS_README.md` ghi 59 hình trích xuất từ Word, chưa có bằng chứng đã cập nhật các hình đó cho V2.

Nguồn hành vi: [Master](OWNER_REQUEST.md), [D01–D12](DECISION_AND_BLOCKER_REGISTER.md), các quyết định UI/nghiệp vụ được Owner xác nhận sau đó tại [Owner UX delta](OWNER_UX_DELTA_20261125.md). SRS draft mô tả baseline; source/database xác định implementation, không thay quyết định nghiệp vụ. Username-only thay chỉ dẫn mã nhân viên/email cũ. Giữ đội bảo trì; gộp DH vào OM; OM chỉ đọc yêu cầu liên bộ phận thông thường, còn duyệt điều chuyển/ghi giảm theo workflow riêng.

## Report 3 — nội dung đối chiếu

| Mục thực tế / yêu cầu Master | Tài liệu V2 tương ứng | Trạng thái và phần còn thiếu |
|---|---|---|
| I: Actor List, mô tả actor và Permission Matrix | [Role before/after](ROLE_PERMISSION_BEFORE_AFTER.md), [role recovery](ROLE_MIGRATION_RECOVERY_REPORT.md), [UC mapping](UC_CHANGE_MAPPING.md) | Đã ghi trách nhiệm mới và giữ actor lịch sử để so sánh. Cần đồng bộ toàn bảng quyền, actor descriptions và Use Case Diagram trong bản Report 3 được quản lý chính thức. Không suy ra mọi actor cũ tự có quyền mới. |
| II: UC Specifications | [UC mapping](UC_CHANGE_MAPPING.md) | 100 UC lịch sử có ID, tên và Primary Actors lấy trực tiếp từ bảng SRS; 5 dòng không đánh số cho chức năng mới/bị gỡ/blocker. Phân loại đúng tập RETAINED/MODIFIED/MERGED/REMOVED/RESTORED/NEW/BLOCKED. Chưa viết lại toàn bộ Normal/Alternative Flows, pre/postconditions của 144 UC. |
| III: Functional Requirements; request workflows | [Business impact](BUSINESS_CHANGE_IMPACT_MATRIX.md), [API register](API_CHANGE_REGISTER.md), [frontend register](FRONTEND_CHANGE_REGISTER.md) | Có delta implementation; cần truy vết từng FR gốc đến UC và acceptance case. Cấu hình người nhận theo role; không biến OM thành người xử lý request thường. |
| V: Business Rules, state transitions, housekeeping | [Inventory reconciliation](INVENTORY_RECONCILIATION.md), [Owner UX](OWNER_UX_DELTA_20261125.md), [E2E results](E2E_TEST_RESULTS.md) | Giữ chỗ tối đa rồi giảm theo thiếu thực tế; chỉ tiêu hao thực bổ sung. Ca hết giờ không xóa custody; dọn lại không tự thành bảo trì. Cần cập nhật từng rule/state catalog, không chỉ mô tả giao diện. |
| Inventory classification và FIFO/FEFO | [Classification](INVENTORY_CLASSIFICATION_MATRIX.md), [FIFO/FEFO](FIFO_FEFO_DESIGN.md), [reusable lifecycle](REUSABLE_ITEM_LIFECYCLE.md) | Phân biệt tiêu hao, tái sử dụng và tài sản; tồn hỏng còn thuộc sở hữu đến khi ghi giảm được duyệt và commit. Legacy thiếu hạn dùng/vị trí thực tế tiếp tục chờ đối soát. |
| Maintenance và transfer | [Maintenance](MAINTENANCE_WORKFLOW_V2.md), [transfer](INTER_BRANCH_TRANSFER_WORKFLOW.md) | Giữ Maintenance Staff, nghiệm thu kỹ thuật độc lập rồi HK kiểm tra; nhận kho theo thực nhận, thiếu giữ chờ đối soát. Legacy room 104 và các sửa quantity/context mới chưa được chứng nhận bằng lượt runtime cũ. |
| IV: NFR, account security, AI và SignalR | [Account security](ACCOUNT_INVITATION_SECURITY.md), [AI](AI_INTEGRATION_DESIGN.md), [SignalR](SIGNALR_ARCHITECTURE.md) | Có hợp đồng và kiểm thử trong phạm vi fixture. Chưa chứng minh đầy đủ hiệu năng/SLA, mọi chuỗi business-triggered realtime hoặc provider/email thật. |

Không sửa lại toàn bộ số UC. Forgot Password đã nằm trong Alternative Flows của UC-001, nên không cấp một số mới chỉ vì thay UI. Invitation mở rộng UC-004; các quy trình chưa có UC riêng được ghi rõ không đánh số. Phân loại REMOVED cho trang xem vật tư nhân viên không có nghĩa xóa ledger hoặc luồng cấp/hoàn.

## Report 4 — tài liệu kỹ thuật có thể tái sử dụng

| Phần Master yêu cầu | Nguồn implementation/tài liệu hiện có | Phần cần hoàn thiện khi đối chiếu bản Report 4 thực tế |
|---|---|---|
| System Architecture, External Systems | API Compact trong một project .NET; [AI](AI_INTEGRATION_DESIGN.md), [SignalR](SIGNALR_ARCHITECTURE.md), [account security](ACCOUNT_INVITATION_SECURITY.md) | Sơ đồ kiến trúc thống nhất, ranh giới SMTP/provider, phiên đăng nhập và realtime. Không tạo thêm tầng/project để khớp một mẫu suy đoán. |
| Database ERD, Data Dictionary, Inventory Model | [Schema delta](DATABASE_SCHEMA_DELTA.md), [FIFO/FEFO](FIFO_FEFO_DESIGN.md), [reusable](REUSABLE_ITEM_LIFECYCLE.md), `docs/database/` | Cập nhật ERD/dictionary theo đúng schema đã kiểm trên TEST; đối chiếu schema vận hành trước release. TEST 57 bảng không được mô tả thành baseline backup 49 bảng. |
| Class, Sequence và Activity Diagrams | [Maintenance](MAINTENANCE_WORKFLOW_V2.md), [transfer](INTER_BRANCH_TRANSFER_WORKFLOW.md), [account security](ACCOUNT_INVITATION_SECURITY.md) | Chưa có bằng chứng mọi diagram đã chỉnh theo source cuối. Cần các nhánh fail/retry/rollback, ca hết giờ, technical review rồi HK, chuyển kho thiếu và đổi mật khẩu lần đầu. |
| API Design, Authentication Flow | [API register](API_CHANGE_REGISTER.md), [frontend register](FRONTEND_CHANGE_REGISTER.md), [UC mapping](UC_CHANGE_MAPPING.md) | Đối chiếu contract/validation/status/authorization/version của từng endpoint thay đổi. Quantity guard, legacy recovery và phân trang mới phải gắn kiểm chứng mới. |
| Transaction Boundaries | [Reconciliation](INVENTORY_RECONCILIATION.md), [transfer](INTER_BRANCH_TRANSFER_WORKFLOW.md), [FIFO/FEFO](FIFO_FEFO_DESIGN.md), [SignalR](SIGNALR_ARCHITECTURE.md) | Mô tả rõ commit ledger/custody/reservation, idempotency/version, thông báo sau commit; không dùng test happy path thay sơ đồ rollback/concurrency. |

## Report 5 — bằng chứng kiểm thử và giới hạn

| Nội dung yêu cầu | Nguồn hiện có | Cách sử dụng đúng |
|---|---|---|
| Test Plan, Test Cases, Expected Result | Master mục 37, [E2E results](E2E_TEST_RESULTS.md), active plan | Giữ 48 E2E và 8 demo làm acceptance riêng. Số checks trong runner không thay thế số E2E đã hoàn tất. |
| Test Data | Metadata fixture và artifact trong `artifacts/restructure-v2/`; [reconciliation](INVENTORY_RECONCILIATION.md) | Database TEST riêng; backup CHECKSUM/VERIFYONLY trước ghi; actor/catalog tổng hợp và cleanup có bằng chứng. Không coi fixture là dữ liệu kho thực hoặc cho phép sửa dữ liệu vận hành. |
| Actual Result và E2E Evidence | [UC mapping: evidence references](UC_CHANGE_MAPPING.md#evidence-references), [E2E results](E2E_TEST_RESULTS.md) | Login/recovery 19 checks, hết ca/settlement 20, cấu hình/báo cáo 15, maintenance 17, inventory/reusable 43, transfer/advisory 14, SignalR 15 hub/API + 5 browser có phạm vi riêng. Không cộng thành tổng nghiệm thu. |
| Defect Log | Artifact lỗi được giữ cùng log, worker reports trong `plans/reports/` | Phân biệt lỗi sản phẩm với lỗi selector/fixture/quota; liên kết lỗi gốc, sửa và lượt rerun. Chưa có defect register hợp nhất cho mọi acceptance case. |
| Regression Results | Log build/test được dẫn trong final report và E2E results | Chỉ ghi kết quả của phiên đã chạy; 3 SKIP không phải PASS. Build/test cũ không tự chứng nhận source vừa thay đổi. Tài liệu này không chạy build hoặc test sản phẩm. |
| External integration | AI/email design và D07 | `BLOCKED_EXTERNAL_CONFIGURATION` cho AI provider và email thật. SMTP sink không chứng minh thư đến hộp thư thực; SignalR fixture SQL chưa chứng minh mọi business UI trigger. |

## Việc còn lại trước khi công bố đồng bộ Report 3/4/5

1. Tích hợp bảng đối chiếu vào đúng bản báo cáo học thuật đang được quản lý; bản Report 4/5 chưa được xác định trong phạm vi tìm kiếm nêu trên. Không cần chặn implementation hoặc test độc lập vì thiếu hai mẫu này.
2. Đồng bộ actor/permission matrix, từng FR/BR và các flow bị thay đổi của SRS; kiểm chéo với API/source cuối thay vì thay tên role hàng loạt.
3. Cập nhật và kiểm tra sơ đồ kiến trúc, ERD, class/sequence/activity; giữ định dạng hiện có khi có nguồn thật.
4. Hoàn thành các bước còn thiếu của 48 E2E, 8 demo, defect register và regression; bổ sung bằng chứng mới cho quantity/creator/legacy/paging đang được xử lý.
5. Giữ các chênh lệch inventory/asset legacy chờ kiểm kê và external runtime chưa cấu hình ở trạng thái đúng. Chưa ký nghiệm thu tổng thể hoặc tuyên bố Report 3/4/5 hoàn tất.
