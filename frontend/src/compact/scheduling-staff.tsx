import { useState } from "react";
import { Users } from "lucide-react";
import { dateKey, type Row } from "./api";
import { useAuth } from "./auth";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { Badge, Dialog, Empty, Pager, State, useQuery } from "./ui";

export default function SchedulingStaff() {
  const { branch, profile } = useAuth();
  const [date, setDate] = useState(dateKey()),
    [shiftId, setShiftId] = useState(0),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1),
    [revision, refresh] = useState(0),
    [employee, setEmployee] = useState<Row | null>(null);
  const week = useQuery(`workday?branchId=${branch}&date=${date}`, revision);
  const shifts: Row[] = (week.data?.shifts || []).filter(
    (s: Row) => s.isActive,
  );
  const shift = shifts.find((s) => s.shiftId === shiftId) || shifts[0];
  const candidates = useQuery<Row[]>(
    shift ? `workday/candidates?shiftId=${shift.shiftId}&date=${date}` : null,
    revision,
  );
  const rows = (candidates.data || []).filter((u) =>
    `${u.fullName} ${u.employeeCode}`
      .toLocaleLowerCase("vi")
      .includes(search.trim().toLocaleLowerCase("vi")),
  );
  const currentPage = Math.min(page, Math.max(1, Math.ceil(rows.length / 10)));
  const assigned = (userId: number) =>
    (week.data?.assignments || []).filter(
      (a: Row) => a.userId === userId && a.status !== "CANCELLED",
    );
  const branchName = profile.branches.find((b) => b.branchId === branch)?.name;
  return (
    <>
      <PageTitle>
        <h1>Nhân sự</h1>
        <p>Ứng viên phục vụ phân ca trong chi nhánh</p>
      </PageTitle>
      <State {...week} retry={() => refresh((n) => n + 1)}>
        <PageToolbar
          actions={
            <button
              onClick={() => {
                location.hash = "workday";
              }}
            >
              Mở lịch làm việc
            </button>
          }
        >
          <label className="search">
            Ca cần bố trí
            <select
              value={shift?.shiftId || ""}
              disabled={!shifts.length}
              onChange={(e) => {
                setShiftId(Number(e.target.value));
                setPage(1);
                setEmployee(null);
              }}
            >
              {!shifts.length && <option value="">Chưa có ca hoạt động</option>}
              {shifts.map((s) => (
                <option key={s.shiftId} value={s.shiftId}>
                  {s.name} · {s.department}
                </option>
              ))}
            </select>
          </label>
          <label>
            Ngày phân ca
            <input
              type="date"
              value={date}
              onChange={(e) => {
                setDate(e.target.value || dateKey());
                setPage(1);
                setEmployee(null);
              }}
            />
          </label>
          <label className="search">
            Tìm nhân viên
            <input
              value={search}
              placeholder="Tên hoặc mã nhân viên"
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </label>
        </PageToolbar>
        <p className="hint">
          Danh sách ứng viên hợp lệ, không trùng giờ theo ca/ngày đã chọn. Sức
          chứa ca và quyền được kiểm tra lại khi phân công.
        </p>
        {!shift ? (
          <Empty>Chưa có ca hoạt động để tra cứu ứng viên.</Empty>
        ) : (
          <State {...candidates} retry={() => refresh((n) => n + 1)}>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Nhân viên</th>
                    <th>Vai trò / bộ phận</th>
                    <th>Số điện thoại</th>
                    <th>Lịch trong tuần</th>
                  </tr>
                </thead>
                <tbody>
                  {rows
                    .slice((currentPage - 1) * 10, currentPage * 10)
                    .map((u) => (
                      <tr key={u.userId}>
                        <td>
                          <strong>
                            <Users size={16} aria-hidden="true" /> {u.fullName}
                          </strong>
                          <small>{u.employeeCode}</small>
                        </td>
                        <td>
                          {shift.role}
                          <small>{shift.department}</small>
                        </td>
                        <td>{u.phone || "Chưa cập nhật"}</td>
                        <td>
                          <button onClick={() => setEmployee(u)}>
                            {assigned(u.userId).length} ca · Xem lịch
                          </button>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
            {!rows.length && (
              <Empty>
                Không có ứng viên phù hợp với ca/ngày và nội dung tìm kiếm.
              </Empty>
            )}
            <Pager
              page={currentPage}
              pageSize={10}
              total={rows.length}
              onPage={setPage}
            />
          </State>
        )}
        {employee && (
          <Dialog
            title={`Lịch tuần · ${employee.fullName}`}
            close={() => setEmployee(null)}
          >
            <p>
              Tuần bắt đầu {week.data?.weekStart} · {branchName}
            </p>
            {!assigned(employee.userId).length ? (
              <Empty>Nhân viên chưa được phân ca trong tuần này.</Empty>
            ) : (
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th>Ngày</th>
                      <th>Ca</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {assigned(employee.userId).map((a: Row) => (
                      <tr key={a.assignmentId}>
                        <td>{a.workDate}</td>
                        <td>
                          {shifts.find((s) => s.shiftId === a.shiftId)?.name ||
                            "Ca đã ngừng hoạt động"}
                        </td>
                        <td>
                          <Badge value={a.status} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Dialog>
        )}
      </State>
    </>
  );
}
