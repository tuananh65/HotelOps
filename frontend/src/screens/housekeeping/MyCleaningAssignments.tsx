import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { getJson, ApiError } from "../../api/dev1/client";
import { positiveId } from "../../api/dev1/rooms";
interface Item { id: number; assignmentId: number; branchId: number; roomNumber: string; taskNumber: string; recordedStatus: string }
interface Page { items: Item[]; nextCursor: number | null }
export default function MyCleaningAssignments() {
  const [params, setParams] = useSearchParams();
  const branch = params.get("branchId") || "", before = params.get("before") || "";
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const key = JSON.stringify([branch, before, owner]);
  const [result, setResult] = useState<{key: string; data: Page} | null>(null);
  const [error, setError] = useState<{key: string; message: string} | null>(null);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const abort = new AbortController(); setResult(null); setError(null);
    if (branch && owner !== null) void (async () => {
      const b = positiveId(branch), cursor = before ? positiveId(before) : null;
      const d = await getJson('/branches/' + b + '/my-cleaning-assignments' + (cursor ? '?before=' + cursor : ''), abort.signal) as Page;
      const ids = new Set<number>();
      if (!d || !Array.isArray(d.items) || d.items.length > 50
          || !(d.nextCursor === null || Number.isSafeInteger(d.nextCursor) && d.nextCursor > 0 && (!cursor || d.nextCursor < cursor)))
        throw new ApiError(502, "INVALID_ASSIGNMENT_LIST");
      for (const item of d.items) {
        if (!item || item.branchId !== b || !Number.isSafeInteger(item.id) || item.id <= 0
            || !Number.isSafeInteger(item.assignmentId) || item.assignmentId <= 0 || ids.has(item.assignmentId)
            || (cursor !== null && item.assignmentId >= cursor)
            || ![item.roomNumber, item.taskNumber, item.recordedStatus].every(x => typeof x === "string"))
          throw new ApiError(502, "INVALID_ASSIGNMENT_LIST");
        ids.add(item.assignmentId);
      }
      if (!abort.signal.aborted) setResult({key, data: d});
    })().catch(e => { if (!abort.signal.aborted) setError({key, message: e instanceof ApiError && e.status === 403
      ? "Current CLEANING_VIEW and branch access are required." : "Assignments could not be loaded. Check your session, branch and connection."}); });
    return () => abort.abort();
  }, [branch, before, owner, key, retry]);
  const data = owner !== null && result?.key === key ? result.data : null;
  return <section className="panel"><h1>My cleaning assignments</h1>
    <Link to={'/cleaning/tasks?branchId=' + encodeURIComponent(branch)}>Supervisor monitor</Link>
    <form onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); setParams({view: 'mine', branchId: String(f.get('branchId') || '')}); }}>
      <label>Branch <input name="branchId" required defaultValue={branch} key={branch} inputMode="numeric" /></label><button>Load</button>
    </form>
    <button onClick={() => setRetry(v => v + 1)}>Refresh</button>
    {error?.key === key && <p role="alert">{error.message}</p>}
    {branch && owner !== null && !data && error?.key !== key && <p role="status">Loading assignments?</p>}
    {data && <><ul>{data.items.map(item => <li key={item.assignmentId}>
      <Link to={'/cleaning/tasks/' + item.id + '?branchId=' + branch + '&assignmentId=' + item.assignmentId}>{item.taskNumber} ? Room {item.roomNumber}</Link> ? {item.recordedStatus}
    </li>)}</ul>{data.items.length === 0 && <p>No current assignments on this page.</p>}
      {before && <button onClick={() => setParams({view: 'mine', branchId: branch})}>Newest</button>}
      {data.nextCursor && <button onClick={() => setParams({view: 'mine', branchId: branch, before: String(data.nextCursor)})}>Older assignments</button>}
    </>}
  </section>;
}
