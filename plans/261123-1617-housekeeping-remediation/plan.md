# Dọn phòng theo thời hạn và khắc phục sự cố phòng

Đã chốt: 10–30 phút từ lúc giao việc; sau khắc phục kiểm tra đạt trả về Cần dọn dẹp lại; chỉ đồ đã gán vào phòng. Checklist giấy. Giữ dữ liệu/grants hiện có.

- [ ] Kiểm tra schema, permission và luồng liên bộ phận; ghi rõ gap.
- [ ] Thời hạn server, audit quá hạn một lần/lượt giao; phân công lại không lý do.
- [ ] Nhân viên bắt đầu → hoàn thành; không minh chứng/sự cố/bàn giao sau nhận; mobile không tràn.
- [ ] Báo hỏng/thiếu theo phòng, cấu hình trưởng bộ phận, duyệt và chuyển khắc phục, kiểm tra lại.
- [ ] Build, unit/API fixture riêng, browser mobile, review và báo cáo.

Production cũ API48468/frontend31292 vẫn phục vụ; không dừng khi đang phát triển. Delegate explore read-only đang kiểm tra luồng sự cố; không có writing delegate.

23/11 16:45: người dùng duyệt đúng ba grant BRANCH DEPT_MANAGER trong ROOM_REMEDIATION_PROPOSAL.md; kiểm thử đầy đủ trước khi sao lưu/áp dụng primary. Không hỏi lại quyền này.

23/11 tiếp tục: hoàn tất remediation trước Admin; Admin mới khảo sát chưa sửa. Build 0 warning, unit 66 passed/3 skipped. Browser remediation/deadline/paper inspection passed. Fixture e22d6cf1d539 passed to returned inventory; test fixture omitted required independent return approval and got409, needs fixture correction (not bypass). Review fixes include current-cycle proof, returns, rejected MR, shared room block and premature unblock guard. Primary grants/deploy unchanged. Pending user decision: one Head per branch leaves Head-created MR without independent approver. Do not auto-change this rule. Release publish succeeded with transient MSB3026 OFL.txt copy retry; hashes checked separately. Hook denied dist copy; use direct Vite artifact output rather than changing ignore rules.

23/11 18:00: user approved warehouse direct acceptance for Head-approved remediation. Implement narrow approval exception in existing document trigger + check constraint, with immutable authorization event, no new grant. Fixture first attempt failed schema gate because extra trigger; moved guard into existing document trigger, preserving exact count and posted-document protection. Build 0 warnings; 66 unit passed/3 skipped. Primary unchanged. Process45664 already exited; active production48468/31292 and preview9072 retained.

23/11 18:21: 69 API groups passed on fixture54f9c926ca5b; 66 unit passed/3 skipped; three browser suites passed. Fresh primary backup verified18:20, exact three grants + narrow approval exception applied, user/room/other-role-grant snapshots unchanged. Production API46692/frontend launcher19284, preview5082 stopped. Await production browser check before starting Admin.

COMPLETED 18:22: Production HTTPS5080 browser passed with trusted TLS, new bundle200, anonymous API401, no runtime errors. Begin queued Admin task now. Report plans/reports/implementation-261123-1821-housekeeping-remediation.md.
