# Handoff FE03 / Router Owner / PL

READY_FOR_FE03=NO. Không bật UC027 → UC049.

Component: frontend/src/incidents/ReportIncidentRoute.tsx; import vào frontend/src/auth/AuthRouteHost.tsx của host đã được owner chọn:

```tsx
import ReportIncidentRoute from "../incidents/ReportIncidentRoute";
// Trong Routes hiện hữu, dưới AuthProvider/BrowserRouter hiện hữu:
<Route path="/incidents/new" element={<ReportIncidentRoute />} />
```

Đây là đề xuất chưa apply; fixture registration không phải integrated acceptance. Destination không redirect sau POST sang detail route chưa được duyệt và không yêu cầu INCIDENT_VIEW. Host cần SPA fallback cho direct-open/reload. Host auth reference d0a5a073b7921040f1e2ed726782ca97e0e30e61; Router Owner acceptance reference: NONE.

FE03 sau khi tất cả gate được duyệt mới dùng reportIncidentHref(branchId: string, roomId: string): string từ incidents/navigation.ts. Dùng string canonical room/branch IDs đã được server cấp, URLSearchParams; không tự cấp quyền và không fallback room/branch. Runtime chính destination xử lý lỗi như UC049_ROUTE_CONTRACT.json.

Source commit: NOT_PUBLISHED; starting 6aee2a0e692a7814f41c81224709cddfbe1e2ec4. Local implementation chưa commit: git add explicit source/test paths exit128, index.lock permission denied ngoài writable roots. Developer cần review CHANGED_FILES_SHA256.json rồi stage từng source/test file (không git add -A), commit source-only; PL chọn PR target trước publication. Không push/PR/merge/deploy trong task này.

Các gate và responsible owner đầy đủ: FINAL_READINESS.json. FE06/Security cần test SQL isolated được duyệt và chốt contract; Router Owner mount/test exact host; FE03/PL chấp thuận v1 context; publication cần actual accepted source SHA.

