import { useEffect, useState } from "react";
import { useAuth } from "./auth";
import { type Row, timestamp } from "./api";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import RoomIssues from "./room-issues";
import Work from "./work";
import "./housekeeping-workspace.css";

const tabs = [["all", "Tất cả"], ["waiting", "Chờ tiếp nhận"], ["doing", "Đang xử lý"], ["inspection", "Chờ kiểm tra"], ["done", "Lịch sử"]];
export default function MaintenanceWorkspace() {
  const { branch } = useAuth();
  const [queue, setQueue] = useState(location.hash.startsWith("#in-progress") ? "doing" : "all");
  const [search, setSearch] = useState(""), [page, setPage] = useState(1), [revision, bump] = useState(0);
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const [selected, setSelected] = useState<Row | null>(() => Number(params.get("id")) ? { workId: Number(params.get("id")), kind: params.get("kind") || "INCIDENT" } : null);
  const query = useQuery(`work/maintenance-inbox?branchId=${branch}&queue=${queue}&page=${page}&search=${encodeURIComponent(search)}`, revision);
  useEffect(() => { setPage(1); }, [branch, search, queue]);
  function open(row: Row) {
    setSelected(row);
    history.replaceState(null, "", `#work?kind=${row.kind}&id=${row.workId}`);
  }
  function close() {
    setSelected(null); bump(n => n + 1);
    history.replaceState(null, "", "#work");
  }
  return <>
    <PageTitle><h1>Công việc bảo trì</h1><p>Tiếp nhận sự cố, giám sát sửa chữa và theo dõi kiểm tra trong cùng một nơi</p></PageTitle>
    <div className="tabs housekeeping-tabs" role="group" aria-label="Trạng thái bảo trì">
      {tabs.map(([key, name]) => <button key={key} aria-pressed={queue === key} onClick={() => setQueue(key)}>{name} <b>{query.data?.counts?.filter((c: Row) => key === "all" ? c.queue !== "done" : c.queue === key).reduce((n: number, c: Row) => n + c.count, 0) ?? "—"}</b></button>)}
    </div>
    <div className="housekeeping-filter-panel"><label>Tìm sự cố / phòng<input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Tên sự cố hoặc số phòng…" /></label><button onClick={() => { setSearch(""); setQueue("all"); setPage(1); }}>Đặt lại</button><button onClick={() => bump(n => n + 1)}>Tải lại</button></div>
    <State {...query} retry={() => bump(n => n + 1)}>
      <div className="table-scroll"><table><thead><tr><th>Công việc / sự cố</th><th>Phòng</th><th>Trạng thái</th><th>Cập nhật</th><th>Thao tác</th></tr></thead><tbody>
        {query.data?.items?.map((r: Row) => <tr key={r.workId}><td>{r.title}</td><td>{r.room || "—"}</td><td><Badge value={r.queue === "inspection" ? "PENDING_INSPECTION" : tabs.find(t => t[0] === r.queue)?.[1] || r.status} /></td><td>{timestamp(r.updatedAt)}</td><td><button onClick={() => open(r)}>{r.queue === "waiting" ? "Tiếp nhận / xem" : "Xem chi tiết"}</button></td></tr>)}
      </tbody></table></div>
      {!query.data?.items?.length && <Empty>Không có công việc bảo trì phù hợp.</Empty>}
      <Pager page={page} total={query.data?.total || 0} onPage={setPage} />
    </State>
    {selected && (selected.kind === "INCIDENT" ? <RoomIssues key={selected.workId} detailOnly selectedId={selected.workId} onClose={close} onChanged={()=>bump(n=>n+1)} /> : <Work key={selected.workId} onlyKind="MAINTENANCE" technicalStaff detailOnly onCloseDetail={close} />)}
  </>;
}
