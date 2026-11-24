# HotelOps Project 2 — Docker, CI và AWS

Phạm vi: đóng gói bản P0 schema 3.0.0, kiểm thử Linux, chuẩn bị triển khai lên `hotelops.ap.vn`. Không tạo EC2, RDS, DNS hoặc thay dữ liệu Production cục bộ trong công việc này. Tài liệu vận hành đặt tại `docs/deployment/` theo quy tắc repository, thay vị trí `deployment/README.md` trong đề xuất chat.

## 1. Kiến trúc và điều kiện

Nginx phục vụ frontend tĩnh và TLS trên 80/443, chuyển `/api/` và `/health` đến API HTTP nội bộ 8080. SQL Server không publish cổng ra host. Volume `hotelops_sql-data` giữ database; `hotelops_evidence` giữ tệp minh chứng. Không dùng Vite preview làm server public. Frontend dùng URL tương đối `/api/v2`, không chứa địa chỉ máy phát triển.

Máy chủ cần Linux x86-64, Docker Engine + Compose v2, Bash, Node.js 22, curl, OpenSSL, flock, AWS CLI v2 và SSM Agent. Chọn máy đủ bộ nhớ cho SQL Server và ứng dụng; không tự tạo tài nguyên tính phí từ workflow. SQL Express mặc định; cấu hình edition/license phù hợp trước khi dùng thực tế. Developer edition chỉ dành cho CI/test.

Backend chỉ tin proxy `172.30.40.2`, giới hạn một hop. Nếu subnet Docker trùng mạng hiện có, sửa subnet/IP của Compose và `Compact__KnownProxies__0` đồng bộ. Không bật chế độ tin mọi proxy. Đọc [hướng dẫn proxy của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

## 2. Cấu hình máy chủ một lần

1. Cài các công cụ trên, tạo `/opt/hotelops/releases` và `/opt/hotelops/shared` (thư mục shared quyền 0700, chủ sở hữu tài khoản vận hành). Chạy thao tác triển khai bằng tài khoản có quyền Docker; SSM mặc định chạy root.
2. Chép `.env.example` thành `/opt/hotelops/shared/production.env`, quyền 0600. Điền registry ECR, SHA đầy đủ 40 ký tự, mật khẩu SA và mật khẩu application khác nhau, thư mục chứng chỉ/backup/state. Đây là file Bash-compatible của quản trị viên, không phải đầu vào từ web. Không commit file thật; không chạy `set -x` hoặc in `docker compose config` chứa mật khẩu.
3. `MSSQL_SA_PASSWORD` chỉ dùng cho quản trị/backup. API đọc connection string từ file secret. `provision-app-login.sh` tạo SQL login riêng với datareader/datawriter/execute/view definition, không cấp sysadmin/db_owner và không đổi role nghiệp vụ. Script từ chối ghi đè login hoặc file connection hiện hữu.
4. Với deployment đã có login/application user từ backup, DBA cần đối chiếu SID và sửa mapping rõ ràng; không tự xóa account. File connection đặt trong thư mục root-only, quyền đọc cho mount container như script hướng dẫn. Connection string mẫu: `Server=db;Database=HotelOpsCompactDB;User ID=hotelops_app;Password=<private>;Encrypt=True;TrustServerCertificate=True`. SQL dùng chứng chỉ self-signed trong network Docker nội bộ; không áp dụng tùy chọn này cho SQL public.
5. Cấu hình `BACKUP_S3_URI=s3://<private-backup-bucket>/hotelops/` để sao lưu ngoài máy. Bật encryption, block public access, versioning/lifecycle theo chính sách lưu trữ. Sao lưu volume evidence riêng cùng database; không chỉ sao lưu file `.bak`.

## 3. DNS và HTTPS

- Record A của `hotelops.ap.vn` trỏ Elastic IP của EC2. Security Group mở 80/443; SSH nếu cần phải giới hạn nguồn, SSM không cần mở SSH. Không mở 1433 hoặc 8080.
- Trước khi chạy frontend lần đầu, cấp chứng chỉ bằng Certbot standalone khi cổng 80 còn trống: `sudo certbot certonly --standalone -d hotelops.ap.vn`. Đây là thao tác hạ tầng thực tế, chỉ chạy sau khi DNS đã đúng.
- Sau khi Nginx hoạt động, cấu hình gia hạn Certbot dùng webroot `/var/www/certbot` và reload Nginx khi gia hạn thành công. Mount toàn bộ `/etc/letsencrypt` để các symlink trong `live/` trỏ được đến `archive/`.
- Kiểm tra `certbot renew --dry-run` trên máy chủ. Chứng chỉ giả chỉ dùng trong CI; Production healthcheck không bỏ qua xác thực TLS.

## 4. Database: khôi phục dữ liệu hiện có, không chạy lại seed demo

Các script `07_V10Roles.sql` đến `10_StaffingV4.sql` là chuyển đổi dữ liệu lịch sử có điều kiện riêng; chúng không phải bộ migration chạy tự động mỗi lần deploy.

Đường khởi tạo máy chủ mới là dùng backup 3.0.0 đã VERIFYONLY và đã thử restore từ Production cục bộ. Chuyển backup bằng kênh riêng, không Git/S3 public. Không chạy song song hai máy chủ cùng ghi dữ liệu sau khi chuyển hệ thống.

```bash
export ENV_FILE=/opt/hotelops/shared/production.env
cd /opt/hotelops/releases/<commit-sha>
bash deployment/init-database.sh --restore /opt/hotelops/shared/import/verified-3.0.0.bak
bash deployment/provision-app-login.sh
bash deployment/deploy.sh <commit-sha>
```

`init-database.sh` chỉ khôi phục vào database chưa tồn tại, VERIFYONLY trước restore, ánh xạ file sang đường dẫn Linux, không có `WITH REPLACE`. Nếu backup có logical filename ngoài định dạng được hỗ trợ, dừng để DBA xem xét. Nếu restore thành công nhưng schema không phù hợp, giữ nguyên database và dừng API, không xóa/khôi phục lặp.

`--schema-only` chỉ cho database `HotelOpsCompact*Test`: dựng cấu trúc và metadata role/permission P0 để kiểm tra container; không tạo người dùng, chi nhánh, phòng hoặc tồn kho. Không dùng đường CI để thay dữ liệu khách sạn. Bootstrap tài khoản mới không nằm trong đường restore Production này.

## 5. Migration riêng khi cần

Không chạy migration trong `deploy.sh`. Với database đã 3.0.0, cập nhật image không cần chạy lại P0. Nếu cần nâng baseline 2.1.0 đã có các patch prerequisite được duyệt:

1. Tạo bản sao từ backup, thử migration và nghiệm thu trên bản sao TEST. Không dùng database dùng chung để thử.
2. Dừng các API/writer, chạy `migrate-database.sh --p0` để lấy digest. Trên TEST script chạy sau backup; trên primary script chỉ in digest và từ chối áp dụng khi chưa truyền `--apply-reviewed <digest>`.
3. Sau khi đối chiếu bundle/script của cùng release đã thử, chạy `bash deployment/migrate-database.sh --p0 --apply-reviewed <digest>`. Script kiểm tra target, phiên bản, API đã dừng, sao lưu và VERIFYONLY, chạy trong transaction, kiểm tra bảo toàn tài khoản/mật khẩu/sổ kho/lịch sử và các constraint.
4. Target database làm digest khác nhau; so sánh script source cùng commit và duy nhất khác tên target. Digest là xác nhận rõ bundle primary, không phải chứng nhận tự động rằng đã kiểm thử. Người vận hành phải lưu bằng chứng rehearsal. Không đổi guard TEST trong file SQL gốc.
5. Khi thất bại, transaction rollback và không tự khởi động phiên bản không tương thích. Không vô hiệu hóa constraint để vượt lỗi.

```bash
bash deployment/backup-database.sh
```

Backup có checksum, VERIFYONLY, bản sao trên host và SHA256; có thể đẩy lên S3 nếu đã cấu hình. VERIFYONLY không thay thế một lần restore thử. Thiết lập lịch backup ngoài các lần deploy và định kỳ thử phục hồi.

## 6. CI và tự triển khai

`ci.yml` chạy khi push lên nhánh feature hiện tại/develop/main và khi tạo PR: .NET build/test, frontend build + các kiểm thử Compact, kiểm thử generator, Bash/ShellCheck, sau đó build hai image và chạy SQL Server + API + Nginx trên Linux. SQL/CA/password trong CI độc lập, tạm thời. Các test Project 1 không thuộc job Compact. Những integration test xUnit có sẵn bị skip vẫn phải được đọc là skip, không phải pass.

Sau khi CI đạt, reusable workflow `deploy.yml` chỉ chạy nếu repository variable `DEPLOY_ENABLED=true`, event là push đúng nhánh `feature/project2-role-alignment-20261123`. Không tự deploy PR/fork. Workflow dùng đúng SHA đã kiểm thử, push hai image gắn SHA vào ECR, tải release scripts lên S3 và dùng SSM chạy trên EC2. `production` environment có thể đặt reviewer theo chính sách nhóm.

Các repository variables cần đặt:

| Biến | Ý nghĩa |
|---|---|
| `DEPLOY_ENABLED` | Chỉ đặt `true` sau khi bootstrap, DNS/TLS và backup đạt |
| `AWS_REGION` | Region của EC2/ECR |
| `AWS_DEPLOY_ROLE_ARN` | Role GitHub OIDC được phép assume |
| `ECR_REGISTRY` | `<account>.dkr.ecr.<region>.amazonaws.com` |
| `RELEASE_BUCKET` | Bucket riêng chứa release bundle |
| `EC2_INSTANCE_ID` | Instance chạy HotelOps |

Tạo ECR repositories `hotelops-api` và `hotelops-frontend`, bật immutable tags. CI không giữ AWS Access Key dài hạn. Theo [GitHub OIDC cho AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws), trust policy phải giới hạn `aud=sts.amazonaws.com` và đúng repository/environment `production`; kiểm tra dạng subject thực tế của repository. Role deploy chỉ cần push ECR đúng hai repository, PutObject đúng prefix releases, SendCommand vào đúng EC2/document AWS-RunShellScript và đọc kết quả. Instance profile chỉ cần SSM, pull ECR, đọc release prefix và ghi backup prefix khi bật backup S3. Không cấp quyền quản trị AWS chung.

AWS deploy không tự provision tài nguyên, cấp chứng chỉ hay tạo database. Bật `DEPLOY_ENABLED` quá sớm sẽ bị health/schema check từ chối. Khi GitHub Actions không chạy hoặc thiếu runner, không ghi nhận CI/container đã đạt.

## 7. Rollback và giới hạn

`deploy.sh` giữ khóa chống deploy đồng thời, kiểm tra schema, pull image trước thay dịch vụ, backup trước rollout, kiểm tra HTTPS/frontend/API. Chỉ ghi state `current` khi healthcheck đạt. Khi thất bại, thử trả về image của release trước nếu schema vẫn tương thích; trả exit code lỗi để Actions không báo success. Khôi phục application images không khôi phục database.

```bash
bash deployment/rollback.sh
```

Rollback thủ công dùng release đã ghi ở `previous`, tạo backup và kiểm tra lại. Với migration phá tương thích hoặc dữ liệu mới sau cutover, cần kế hoạch phục hồi riêng; không tự restore database cũ và làm mất giao dịch mới. Không dùng `docker compose down -v` trên máy thật.

Nguồn kỹ thuật thêm: [SQL Server container và persistence](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker?view=sql-server-ver17). Trạng thái nghiệm thu thực tế được ghi trong báo cáo lượt triển khai; tài liệu này mô tả cách vận hành, không tuyên bố AWS đã chạy.
