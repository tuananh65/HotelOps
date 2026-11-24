import { useState } from "react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import PageToolbar from "./page-toolbar";
import PageTitle from "./page-title";
import "./housekeeping-history.css";

export default function HousekeepingHistory() {
  const { branch } = useAuth();
  const [day, setDay] = useState(""), [page, setPage] = useState(1), [search, setSearch] = useState(""), [revision, bump] = useState(0);
  const query = useQuery(`work/housekeeping-history?branchId=${branch}&page=${page}&search=${encodeURIComponent(search)}${day ? `&date=${day}` : ""}`, revision);
  const shownDay = day || query.data?.date || "";
  function move(offset: number) { const d = new Date(`${shownDay}T12:00:00Z`); if(Number.isNaN(d.getTime()))return;d.setUTCDate(d.getUTCDate() + offset);const next=d.toISOString().slice(0,10);if(next<"2000-01-01"||next>"2100-12-31")return;setDay(next);setPage(1); }
  return <div className="housekeeping-history">
    <PageTitle><h1>Lịch sử vệ sinh</h1>
    <p>Công việc dọn phòng và làm lại theo ngày gửi hoàn thành tại chi nhánh.</p></PageTitle>
    <PageToolbar>
      <button disabled={!shownDay||shownDay==="2000-01-01"} onClick={() => move(-1)}>Ngày trước</button>
      <label>Ngày hoàn thành<input type="date" value={shownDay} min="2000-01-01" max="2100-12-31" onChange={e => { setDay(e.target.value); setPage(1); }} /></label>
      <button disabled={!shownDay||shownDay==="2100-12-31"} onClick={() => move(1)}>Ngày sau</button>
      <button onClick={() => { setDay(""); setPage(1); bump(n => n + 1); }}>Hôm nay</button>
      <label>Tìm công việc / phòng<input type="search" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
    </PageToolbar>
    <State {...query} retry={() => bump(n => n + 1)}>
      <p className="history-day">Ngày {shownDay.split("-").reverse().join("/")} · {query.data?.total || 0} công việc gửi hoàn thành</p>
      <div className="table-scroll"><table><thead><tr><th>Công việc</th><th>Phòng</th><th>Người hoàn thành</th><th>Thời điểm hoàn thành</th><th>Trạng thái</th></tr></thead>
        <tbody>{query.data?.items.map((r: Row) => <tr key={r.workId}><td data-label="Công việc"><a href={`#work?kind=${r.kind}&id=${r.workId}`}>{r.title}</a></td><td data-label="Phòng">{r.room||"—"}</td><td data-label="Người hoàn thành">{r.performer || "—"}</td><td data-label="Thời điểm hoàn thành">{timestamp(r.completedAt)}</td><td data-label="Trạng thái"><Badge value={r.status} /></td></tr>)}</tbody>
      </table></div>
      {!query.data?.items.length && <Empty>Không có công việc gửi hoàn thành trong ngày này.</Empty>}
      <Pager page={page} pageSize={25} total={query.data?.total || 0} onPage={setPage} />
    </State>
  </div>;
}
