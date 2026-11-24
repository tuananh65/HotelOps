import { useState } from "react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import "./housekeeping-history.css";
import "./housekeeping-workspace.css";

export default function HousekeepingHistory({personal=false}:{personal?:boolean}) {
  const { branch } = useAuth();
  const [day,setDay]=useState(""), [period,setPeriod]=useState("day"), [page,setPage]=useState(1),
    [search,setSearch]=useState(""), [status,setStatus]=useState(""), [sort,setSort]=useState("latest"),
    [filters,setFilters]=useState(false), [revision,bump]=useState(0);
  const query=useQuery(`work/housekeeping-history?branchId=${branch}&mine=${personal}&page=${page}&period=${period}&sort=${sort}&search=${encodeURIComponent(search)}${day?`&date=${day}`:""}${status?`&status=${status}`:""}`,revision);
  const shownDay=day||query.data?.date||"";
  function move(offset:number){const d=new Date(`${shownDay}T12:00:00Z`);if(Number.isNaN(d.getTime()))return;d.setUTCDate(d.getUTCDate()+offset*(period==="week"?7:1));const next=d.toISOString().slice(0,10);if(next<"2000-01-01"||next>"2100-12-31")return;setDay(next);setPage(1);}
  function reset(){setDay("");setPeriod("day");setSearch("");setStatus("");setSort("latest");setPage(1);bump(n=>n+1);}
  return <div className="housekeeping-history">
    <PageTitle><h1>Lịch sử vệ sinh</h1><p>Công việc dọn phòng và dọn lại theo thời điểm gửi hoàn thành.</p></PageTitle>
    <div className="housekeeping-filter-panel">
      <label>Tìm công việc, phòng hoặc nhân viên<input type="search" value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}} /></label>
      <button aria-expanded={filters} onClick={()=>setFilters(!filters)}>Bộ lọc</button>
    </div>
    {filters&&<div className="housekeeping-filter-panel">
      <label>Xem theo<select value={period} onChange={e=>{setPeriod(e.target.value);setPage(1);}}><option value="day">Ngày</option><option value="week">Tuần</option></select></label>
      <button disabled={!shownDay} onClick={()=>move(-1)}>Trước</button>
      <label>Ngày hoàn thành<input type="date" value={shownDay} min="2000-01-01" max="2100-12-31" onChange={e=>{setDay(e.target.value);setPage(1);}}/></label>
      <button disabled={!shownDay} onClick={()=>move(1)}>Sau</button>
      <label>Trạng thái<select value={status} onChange={e=>{setStatus(e.target.value);setPage(1);}}><option value="">Tất cả</option><option value="COMPLETED">Hoàn thành</option><option value="PENDING_INSPECTION">Chờ kiểm tra</option><option value="PENDING_REINSPECTION">Chờ kiểm tra lại</option><option value="REWORK_REQUIRED">Cần làm lại</option></select></label>
      <label>Sắp xếp<select value={sort} onChange={e=>{setSort(e.target.value);setPage(1);}}><option value="latest">Mới nhất</option><option value="oldest">Cũ nhất</option><option value="title">Tên công việc</option></select></label>
      <button onClick={reset}>Đặt lại</button>
    </div>}
    <State {...query} retry={()=>bump(n=>n+1)}>
      <p>{query.data?.date} {period==="week"?`– ${query.data?.endDate}`:""} · {query.data?.timezone} · {query.data?.total||0} công việc</p>
      <div className="table-scroll"><table><thead><tr><th>Công việc</th><th>Phòng</th><th>Người hoàn thành</th><th>Thời điểm hoàn thành</th><th>Trạng thái</th></tr></thead>
        <tbody>{query.data?.items.map((r:Row)=><tr key={r.workId}><td data-label="Công việc"><a href={`#${personal?"cleaning":"work"}?kind=${r.kind}&id=${r.workId}`}>{r.title}</a></td><td data-label="Phòng">{r.room||"—"}</td><td data-label="Người hoàn thành">{r.performer||"—"}</td><td data-label="Thời điểm hoàn thành">{timestamp(r.completedAt)}</td><td data-label="Trạng thái"><Badge value={r.status}/>{r.completedWithIncident&&<small>Hoàn thành vệ sinh có sự cố</small>}</td></tr>)}</tbody>
      </table></div>
      {!query.data?.items.length&&<Empty>Không có công việc gửi hoàn thành trong khoảng thời gian đã chọn.</Empty>}
      <Pager page={page} pageSize={25} total={query.data?.total||0} onPage={setPage}/>
    </State>
  </div>;
}
