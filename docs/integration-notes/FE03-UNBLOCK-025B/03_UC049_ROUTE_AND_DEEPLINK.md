# Route và deep-link — PROPOSED

ROUTE_COMPONENT_READY=true; ROUTER_OWNER_APPROVED=false; INTEGRATED_ROUTE_REGISTERED=false; PRODUCTION_ROUTE_ENABLED=false. Component có thật và fixture route test pass; route tích hợp PENDING_ROUTER_OWNER.

reportIncidentHref(branchId: string, roomId: string): string tại frontend/src/incidents/navigation.ts. Href /incidents/new?branchId={branchId}&roomId={roomId}; URLSearchParams sau khi kiểm tra canonical positive decimal, <=Number.MAX_SAFE_INTEGER. Hai tham số bắt buộc cho builder FE03. Destination direct-open cho phép thiếu cả hai, không tự chọn branch; branch-only cho phép chọn source; room-only fail closed. Duplicate keys, rỗng, dấu, leading zero, decimal/exponent, zero/negative/unsafe đều bị từ chối trước GET. Unknown query keys bị bỏ qua và không cấp quyền.

Invalid context không fallback hoặc đổi URL. Options(roomId,branchId) xác minh authoritative current room;400 source unknown/inactive/stale/cross-branch,403 branch không được phép; form không render khi lỗi preflight. Context room đã chọn được khóa trong form; refresh/back/forward remount và reload. POST kiểm tra lại độc lập. Không có room rowversion/If-Match mới: stale ở đây là current source không còn hợp lệ, không hứa phát hiện mọi thay đổi lịch sử.

Không tạo BrowserRouter/AuthProvider mới. Phụ thuộc host auth interface được kiểm tra đúng pin d0a5a073b7921040f1e2ed726782ca97e0e30e61; chưa chứng minh tương thích mọi nhánh frontend khác.
