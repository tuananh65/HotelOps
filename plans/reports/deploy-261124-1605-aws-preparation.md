# Chuẩn bị Docker/CI/AWS và push P0

Yêu cầu: xử lý đề xuất trong chat chia sẻ, đưa cả thay đổi P0 hiện tại và bộ triển khai lên `feature/project2-role-alignment-20261123`. User đã xác nhận phạm vi push. Không tạo tài nguyên AWS, không thay DNS hoặc redeploy Production cục bộ trong lượt này.

## Đã triển khai

- Hai Dockerfile, Compose với SQL/evidence volume bền vững, SQL/API không public port, Nginx TLS cho hotelops.ap.vn và API cùng origin.
- Backend nhận forwarded scheme/client IP chỉ từ proxy cấu hình; kiểm thử chấp nhận đúng proxy và từ chối header từ nguồn không tin cậy.
- Script backup có checksum/VERIFYONLY/SHA256 và tùy chọn S3; restore từ chối database có sẵn; provision SQL application login riêng; migration P0 độc lập, backup trước thay đổi và bảo toàn credentials/ledger/history; deploy/rollback giữ khóa và kiểm tra schema/health.
- CI build/test .NET + Compact frontend, kiểm tra generator/shell, SQL/container smoke riêng. AWS reusable workflow chỉ bật qua biến cấu hình, dùng OIDC/ECR/S3/SSM, commit SHA cố định, không chạy từ PR/fork.
- Hướng dẫn tại `docs/deployment/aws-docker.md`. Không replay script demo 07–10 trên máy chủ.

## Bằng chứng local

- `dotnet build HotelOps.slnx`: Passed, 0 warnings/errors.
- `dotnet test HotelOps.slnx --no-build --no-restore`: 101 passed, 3 existing integration tests skipped.
- Frontend build: Passed; Vite cảnh báo output ngoài project không tự xóa, đây là thư mục artifact riêng để giữ Production đang chạy.
- `npm run test:compact:ci`: 9 passed.
- Deployment generator tests: 4 passed; Bash syntax passed.
- SQL fresh TEST `HotelOpsCompactDeploymentTest261124`: backup trước schema, cài 3.0.0 thành công; chứng cứ `artifacts/deployment-schema-verification.json`.
- Đã sửa lỗi namespace proxy và prerequisites metadata/trùng permission phát hiện trong thử nghiệm SQL. Các lần SQL lỗi rollback, không sửa primary.
- Lệnh npm test mặc định ban đầu kéo cả test Project 1 nên đã dừng đúng runner và đổi CI sang suite Compact. Không tuyên bố toàn bộ legacy frontend test passed.

## Giới hạn và xuất bản

Docker không được cài trên máy Windows hiện tại; kiểm chứng container/Linux sẽ dựa trên job GitHub Actions sau push. AWS chưa được cấp cấu hình/hạ tầng trong lượt này, nên không tuyên bố hotelops.ap.vn đã online. Full A+B acceptance P0 vẫn PARTIAL theo báo cáo trước; publish code không thay đổi kết luận đó.

Không đưa thư mục công cụ `.agents`, `.agentkit`, `.codex`, prototype ngoài phạm vi, artifacts, backup, chứng chỉ/private key hoặc credentials TEST vào commit. Các literal mật khẩu trong script fixture cũ được xác nhận chỉ dùng database TEST có guard; template deployment chỉ có placeholder/biến môi trường.

Kết quả commit/push và CI sẽ được bổ sung sau khi nhận bằng chứng từ GitHub.
