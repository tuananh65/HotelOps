import { displayCode } from "../i18n/vi";
import { useEffect, useState, type ComponentProps } from "react";
import { useAuth } from "../auth/AuthProvider";
import { decodeHistory, HistoryFailure, readHistory, type HistoryPage } from "../internalRequests/historyApi";
import { InternalRequestTaskCreationPanel } from "../internalRequests/TaskCreationPanel";
import { TaskCreatePanel } from "./TaskCreatePanel";

// Source selection is a read only lookup. The context endpoint independently
// proves current request processing and target creation authority before prefill.
export function TaskCreationHost(props: ComponentProps<typeof TaskCreatePanel>) {
  const { actions } = useAuth();
  const [linked, setLinked] = useState(false); const [status, setStatus] = useState("IN_PROGRESS");
  const [search, setSearch] = useState(""); const [page, setPage] = useState(1); const [selected, setSelected] = useState("");
  const [error, setError] = useState("");
  const [loaded, setLoaded] = useState<{ owner: typeof props.owner; key: string; data: HistoryPage } | null>(null);
  const key = `${props.branch}:${status}:${search}:${page}:${linked}`;
  useEffect(() => {
    const abort = new AbortController(); setLoaded(null); setSelected(""); setError("");
    if (linked) void readHistory(`?${new URLSearchParams({ branchId: String(props.branch), status, search, page: String(page), pageSize: "25" })}`, abort.signal)
      .then(decodeHistory).then(data => {
        if (data.page !== page || data.pageSize !== 25 || data.items.some(r => r.branchId !== props.branch || r.status !== status)) throw new Error("Trang nguồn không hợp lệ");
        if (!abort.signal.aborted) setLoaded({ owner: props.owner, key, data });
      }).catch(e => { if (!abort.signal.aborted) { setError("Chưa có nguồn yêu cầu. Cần quyền xem yêu cầu hiện tại."); if (e instanceof HistoryFailure && e.status === 401) actions.invalidateSession(props.owner); } });
    return () => abort.abort();
  }, [props.branch, props.owner, linked, status, search, page, key, actions]);
  const data = loaded?.owner === props.owner && loaded.key === key ? loaded.data : null;
  const request = data?.items.find(r => String(r.id) === selected);
  return <section aria-label="Nguồn công việc">
    <label><input type="checkbox" checked={linked} onChange={e => setLinked(e.target.checked)} />Dùng yêu cầu nội bộ làm nguồn</label>
    {!linked ? <TaskCreatePanel {...props} /> : <>
      {error && <p role="status">{error}</p>}
      <label>Trạng thái yêu cầu nguồn<select aria-label="Trạng thái yêu cầu nguồn" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}>
        <option value="IN_PROGRESS">Đang thực hiện</option><option value="ACCEPTED">Đã tiếp nhận</option></select></label>
      <label>Tìm yêu cầu nguồn<input aria-label="Tìm yêu cầu nguồn" maxLength={200} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
      {data && <><label>Yêu cầu nội bộ nguồn<select aria-label="Yêu cầu nội bộ nguồn" value={selected} onChange={e => setSelected(e.target.value)}>
        <option value="">Chọn yêu cầu</option>{data.items.map(r => <option key={r.id} value={r.id}>{displayCode(r.number)}: {displayCode(r.subject)}</option>)}</select></label>
        <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Yêu cầu nguồn trước</button><span> Trang {page} </span>
        <button disabled={page * 25 >= data.totalCount} onClick={() => setPage(p => p + 1)}>Yêu cầu nguồn tiếp theo</button>
        {request && <InternalRequestTaskCreationPanel key={`${props.owner}:${request.id}`} id={request.id} branch={props.branch} onCreated={props.onCreated} />}
      </>}
    </>}
  </section>;
}
