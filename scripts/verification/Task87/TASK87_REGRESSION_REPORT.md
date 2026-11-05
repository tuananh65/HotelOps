# TASK87 — Regression report

Tất cả 9 runners thực thi lại exit 0; không chạy --live, không kết nối SQL.

| Runner | Checks PASS | Loại |
|---|---:|---|
| Authorization | 61 | Loopback cookie/middleware HTTP + doubles |
| ResourceAuthorization | 49 | Loopback HTTP + production permission core/adapter + doubles |
| PermissionService | 43 | Core service fake data |
| CurrentUserAccessor | 14 | Request identity unit/DI |
| AuthorizationHardening | 21 | HTTP/guard doubles |
| UC037 Task82 timeline | 14 | Service boundary |
| UC038 Task85-B | 26 | DI/boundary/EF translation |
| UC142 Task84 | 19 | DI/boundary/static/EF translation |
| UC012 Task86 | 39 | DI/boundary/EF translation |

Core security hiện tại: 167 checks (61+49+43+14); hardening riêng 21. Không ghi 199 từ baseline thành kết quả mới: current Authorization runner tự báo 61, không phải 93. Đây là chênh lệch số lượng cần lưu ý khi so sánh receipt lịch sử; không có failing assertion trong các lượt hiện tại. Không suy ra 32 historical cases đã được chạy lại hoặc covered. Các checks này không cộng vào 91 framework cases.

Framework final: 91/91 PASS, TRX và JSON ở results-final. Lượt đầu 83/91 PASS và 8 fixture-binding failures giữ tại results/task87.trx; fixture corrected, production untouched. Initial compile failure cũng giữ log.

Build PASS (0 errors, 2 NU1900). Solution test exit 0/no cases vì solution chỉ có API; project test explicit discover 91 cases. Không claim SQL acceptance hoặc line coverage. Không production bugs được chứng minh. Source/artifact hashes và git diff --check PASS; postflight ghi rõ phạm vi bảo toàn.
