import { useEffect, useState } from "react";
import { Users } from "lucide-react";
import { dateKey, type Row } from "./api";
import { useAuth } from "./auth";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { Badge, Dialog, Empty, Pager, State, useQuery } from "./ui";

export default function SchedulingStaff() {
  const { branch, profile } = useAuth();
  const [search, setSearch] = useState(""), [department, setDepartment] = useState(""), [working, setWorking] = useState("");
  const [page, setPage] = useState(1), [revision, refresh] = useState(0), [employee, setEmployee] = useState<Row | null>(null);
  const [date, setDate] = useState(dateKey());
  const query = useQuery<Row[]>(`workday/personnel?branchId=${branch}`, revision);
  const week = useQuery(employee ? `workday?branchId=${branch}&date=${date}` : null, revision);
  useEffect(() => { const timer = setInterval(() => refresh(n => n + 1), 30000); return () => clearInterval(timer); }, []);
  useEffect(() => { setEmployee(null); setPage(1); }, [branch]);
  const rows = (query.data || []).filter(u => `${u.fullName} ${u.employeeCode}`.toLocaleLowerCase("vi").includes(search.trim().toLocaleLowerCase("vi")))
    .filter(u => !department || u.department === department).filter(u => !working || u.working === (working === "yes"));
  const currentPage = Math.min(page, Math.max(1, Math.ceil(rows.length / 10)));
  const currentEmployee = (query.data || []).find(u => u.userId === employee?.userId);
  const assigned = (week.data?.assignments || []).filter((a: Row) => a.userId === employee?.userId && a.status !== "CANCELLED");
  const time = (minute: number) => `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`;
  const moveWeek = (days: number) => { const value = new Date(`${date}T12:00:00`); value.setDate(value.getDate() + days); setDate(dateKey(value)); };
  return <>
    <PageTitle><h1>Nhân sự</h1><p>Nhân viên và trạng thái ca thực tế trong chi nhánh</p></PageTitle>
    <PageToolbar actions={<a className="button-link" href="#workday">Mở lịch làm việc</a>}>
      <label className="search">Tìm nhân viên<input value={search} placeholder="Tên hoặc mã nhân viên" onChange={e => { setSearch(e.target.value); setPage(1); }}/></label>
      <label>Bộ phận<select value={department} onChange={e => { setDepartment(e.target.value); setPage(1); }}><option value="">Tất cả</option>{[...new Set((query.data || []).map(u => String(u.department)))].filter(Boolean).map(d => <option key={d}>{d}</option>)}</select></label>
      <label>Ca làm việc<select value={working} onChange={e => { setWorking(e.target.value); setPage(1); }}><option value="">Tất cả</option><option value="yes">Đang trong ca</option><option value="no">Không trong ca</option></select></label>
      <button onClick={() => { setSearch(""); setDepartment(""); setWorking(""); setPage(1); }}>Xóa bộ lọc</button>
    </PageToolbar>
    <State {...query} retry={() => refresh(n => n + 1)}>
      <div className="table-scroll"><table><thead><tr><th>Nhân viên</th><th>Bộ phận / Vai trò</th><th>Liên hệ</th><th>Thông tin chi tiết</th></tr></thead><tbody>
        {rows.slice((currentPage - 1) * 10, currentPage * 10).map(u => <tr key={u.userId} style={u.working ? { backgroundColor: "#e4f3e9" } : undefined}>
          <td><strong><Users size={16} aria-hidden="true"/> {u.fullName}</strong><small>{u.employeeCode}</small>{u.working && <Badge value="WORKING"/>}</td>
          <td>{u.department || "Chưa cập nhật"}<small>{u.roles.join(", ")}</small></td><td>{u.phone || "Chưa có số điện thoại"}<small>{u.email || "Chưa có email"}</small></td>
          <td><button onClick={() => { setEmployee(u); setDate(dateKey()); }}>Thông tin chi tiết</button></td>
        </tr>)}
      </tbody></table></div>
      {!rows.length && <Empty>Không có nhân sự phù hợp trong chi nhánh.</Empty>}
      <Pager page={currentPage} pageSize={10} total={rows.length} onPage={setPage}/>
    </State>
    {currentEmployee && <Dialog title={currentEmployee.fullName} close={() => setEmployee(null)}>
      <p>{currentEmployee.employeeCode} · {currentEmployee.roles.join(", ")} · {currentEmployee.department}</p>
      <p>{currentEmployee.phone || "Chưa có số điện thoại"} · {currentEmployee.email || "Chưa có email"}</p>
      <p>{profile.branches.find(b => b.branchId === branch)?.name} · {currentEmployee.working ? "Đang trong ca" : "Không trong ca"}</p>
      <h3>Lịch làm việc</h3><div className="toolbar"><button onClick={() => moveWeek(-7)}>Tuần trước</button><input type="date" value={date} onChange={e => setDate(e.target.value || dateKey())}/><button onClick={() => setDate(dateKey())}>Tuần này</button><button onClick={() => moveWeek(7)}>Tuần sau</button></div>
      <State {...week}>{assigned.length ? <div className="table-scroll"><table><thead><tr><th>Ngày</th><th>Ca / Giờ</th><th>Trạng thái</th></tr></thead><tbody>{assigned.map((a: Row) => { const shift = week.data?.shifts.find((s: Row) => s.shiftId === a.shiftId); return <tr key={a.assignmentId}><td>{a.workDate}</td><td>{shift?.name}<small>{shift && `${time(shift.startMinute)}–${time(shift.endMinute)}`}</small></td><td><Badge value={a.status}/></td></tr>; })}</tbody></table></div> : <Empty>Chưa có lịch trong tuần này.</Empty>}</State>
    </Dialog>}
  </>;
}
