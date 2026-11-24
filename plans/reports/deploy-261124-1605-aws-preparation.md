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

Docker không được cài trên máy Windows hiện tại; kiểm chứng container/Linux đã thực hiện trên GitHub Actions. AWS chưa được cấp cấu hình/hạ tầng trong lượt này, nên không tuyên bố hotelops.ap.vn đã online. Full A+B acceptance P0 vẫn PARTIAL theo báo cáo trước; publish code không thay đổi kết luận đó.

Không đưa thư mục công cụ `.agents`, `.agentkit`, `.codex`, prototype ngoài phạm vi, artifacts, backup, chứng chỉ/private key hoặc credentials TEST vào commit. Các literal mật khẩu trong script fixture cũ được xác nhận chỉ dùng database TEST có guard; template deployment chỉ có placeholder/biến môi trường.

Đã push P0, deployment, logo và các script runtime verification lên nhánh được yêu cầu. Secret scan của các nhóm stage không phát hiện credential thực. Các workbook/docx dữ liệu nhân viên được giữ local, không đưa lên Git. ShellCheck và actionlint đều đạt. Lần tải actionlint đầu bị timeout; lần tải tiếp thành công và kiểm tra đạt.

CI build/test và Docker smoke ban đầu đã PASS tại https://github.com/tuananh65/HotelOps/actions/runs/45367133842. Một push cũ bị từ chối non-fast-forward vì commit mới hơn trong cùng lượt đã lên remote; không force push hoặc ghi đè commit. Remote đã được đối chiếu lại.

Lượt mở rộng https://github.com/tuananh65/HotelOps/actions/runs/45367673278 đã restore SQL thành công nhưng thất bại khi script CI ghi lại file connection đã chmod 444. Đã sửa script chỉ mở quyền ghi cho owner lúc chuyển database, rồi đặt lại chỉ đọc trước khởi động API. Lượt CI tiếp theo chứa lỗi đã biết được hủy để nhường runner cho bản sửa; không ghi PASS cho lượt lỗi.

Kết quả cuối VERIFIED: https://github.com/tuananh65/HotelOps/actions/runs/45368592341, source commit `4313be40534035bc5d6d1f9c1fce2d71d7c5c638`, hoàn tất 09:51:54 UTC ngày 24/11/2026. `build-test` SUCCESS; `docker-smoke` SUCCESS, gồm backup/restore SQL vào database TEST khác và khởi động API/HTTPS frontend với database đã phục hồi. `deploy` SKIPPED đúng điều kiện chưa bật AWS. Commit chốt báo cáo chỉ thay đổi `plans/**`, nằm trong paths-ignore của CI.

GitHub Actions có annotation deprecation Node.js 20 của các action v4 đã pin SHA; runner đã chạy chúng bằng Node.js 24 và các job thành công. Đây là cảnh báo cần theo dõi khi nâng action, không phải lỗi build/test. Các process Production/API 43404, frontend 7680 (launcher 10388), TEST API 15136 và frontend 48136 vẫn được giữ; watcher CI đã kết thúc với exit code 0. Không khởi động thêm server lâu dài trong lượt triển khai này.

Log container ghi cảnh báo ASP.NET Data Protection về key trong filesystem không bền vững/không cấu hình XML encryptor. Compact hiện xác thực bằng opaque session trong SQL, không dựa trên cookie Data Protection. Không tuyên bố đã triển khai lưu trữ/encryption key dùng chung cho các tính năng Data Protection khác. AWS OIDC/ECR/SSM, SQL application-login provisioning và rollback khi rollout lỗi chưa được thực thi trên hạ tầng AWS thật.
