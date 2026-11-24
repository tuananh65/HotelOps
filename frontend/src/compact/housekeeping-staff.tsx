import { useState } from "react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import StatusLight from "./status-light";

export default function HousekeepingStaff() {
  const { branch, can } = useAuth();
  const [date, setDate] = useState(""),
    [search, setSearch] = useState(""),
    [sort, setSort] = useState("name"),
    [page, setPage] = useState(1),
    [revision, bump] = useState(0);
  const query = useQuery(
    `work/housekeepers?branchId=${branch}${date ? `&date=${date}` : ""}`,
    revision,
  );
  const rows: Row[] = (query.data?.items || [])
    .filter((r: Row) =>
      `${r.fullName} ${r.employeeCode}`
        .toLocaleLowerCase()
        .includes(search.toLocaleLowerCase()),
    )
    .sort((a: Row, b: Row) =>
      sort === "name"
        ? a.fullName.localeCompare(b.fullName, "vi")
        : b[sort] - a[sort],
    );
  const current = Math.min(page, Math.max(1, Math.ceil(rows.length / 25)));
  const time = (n: number) =>
    `${String(Math.floor(n / 60)).padStart(2, "0")}:${String(n % 60).padStart(2, "0")}`;
  return (
    <>
      <PageTitle>
        <h1>Nhân viên & Ca trực</h1>
        <p>Nhân viên đủ điều kiện nhận việc vệ sinh trong chi nhánh</p>
      </PageTitle>
      <PageToolbar>
        <label className="search">
          Tìm nhân viên
          <input
            type="search"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Tên hoặc mã nhân viên"
          />
        </label>
        <label>
          Ngày ca
          <input
            type="date"
            value={date || query.data?.date || ""}
            onChange={(e) => {
              setDate(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label>
          Sắp xếp
          <select value={sort} onChange={(e) => setSort(e.target.value)}>
            <option value="name">Họ tên</option>
            <option value="open">Việc đang mở</option>
            <option value="completed">Đã hoàn thành</option>
          </select>
        </label>
        {can("ATTENDANCE_RECORD") && (
          <a className="button-link" href="#workday">
            Lịch cá nhân
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((x) => x + 1)}>
        <p>
          Ca ngày {query.data?.date} · {query.data?.timezone}. Số công việc là
          tổng các phân công hiện hành; trạng thái ca dựa trên giờ ghi nhận.
        </p>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th>Ca được phân</th>
                <th>Đang mở</th>
                <th>Đã hoàn thành</th>
                <th>Công việc hiện tại</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((current - 1) * 25, current * 25).map((r) => (
                <tr key={r.userId}>
                  <td>
                    <strong>
                      {r.fullName}{" "}
                      <StatusLight active={r.accountStatus === "ACTIVE"} />
                    </strong>
                    <small>{r.employeeCode}</small>
                  </td>
                  <td>
                    {r.shifts.length
                      ? r.shifts.map((s: Row, i: number) => (
                          <div key={i}>
                            {s.name} · {time(s.startMinute)}–{time(s.endMinute)}{" "}
                            <Badge value={s.status} />
                            <small>
                              {s.startedAt && `Vào ${timestamp(s.startedAt)}`}
                              {s.endedAt && ` · Ra ${timestamp(s.endedAt)}`}
                            </small>
                          </div>
                        ))
                      : "Chưa có ca"}
                  </td>
                  <td>{r.open}</td>
                  <td>{r.completed}</td>
                  <td>
                    {r.busy
                      ? "Đang thực hiện công việc"
                      : "Không có phiên công việc đang chạy"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && <Empty />}
        <Pager
          page={current}
          total={rows.length}
          pageSize={25}
          onPage={setPage}
        />
      </State>
    </>
  );
}
