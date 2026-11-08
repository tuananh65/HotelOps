import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, EvidenceList, Results, useOperation } from "./shared";
import {
  parseId,
  type Detail,
  type Filters,
  type HistoryPage as Page,
} from "./api";

export function HistoryPage() {
  const { run, busy, error, setError } = useOperation();
  const [page, setPage] = useState<Page | null>(null);
  const [filters, setFilters] = useState<Filters>({ page: 1, pageSize: 20 });
  const [form, setForm] = useState({
    branchId: "",
    roomId: "",
    inspectorId: "",
    housekeeperId: "",
    search: "",
    result: "",
    from: "",
    to: "",
    pageSize: "20",
  });
  const load = (f: Filters) => {
    setPage(null);
    return run(
      (s) => api.history(f, s),
      (p) => {
        setPage(p);
        setFilters(f);
      },
    );
  };
  useEffect(() => {
    let alive = true;
    queueMicrotask(() => {
      if (alive) void load(filters);
    });
    return () => {
      alive = false;
    };
  }, []);
  return (
    <main>
      <h1>Room Inspection History</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          try {
            const f: Filters = { page: 1, pageSize: Number(form.pageSize) };
            for (const k of [
              "branchId",
              "roomId",
              "inspectorId",
              "housekeeperId",
            ] as const)
              if (form[k]) f[k] = parseId(form[k]);
            if (form.search) f.search = form.search;
            if (form.result) f.result = form.result;
            for (const k of ["from", "to"] as const)
              if (form[k]) f[k] = new Date(form[k]).toISOString();
            void load(f);
          } catch {
            setError("Enter valid IDs and date range.");
          }
        }}
      >
        {(["branchId", "roomId", "inspectorId", "housekeeperId"] as const).map(
          (k) => (
            <label key={k}>
              {k}
              <input
                inputMode="numeric"
                value={form[k]}
                onChange={(e) => setForm({ ...form, [k]: e.target.value })}
              />
            </label>
          ),
        )}
        <label>
          Search
          <input
            maxLength={150}
            value={form.search}
            onChange={(e) => setForm({ ...form, search: e.target.value })}
          />
        </label>
        <label>
          Result
          <select
            aria-label="Result"
            value={form.result}
            onChange={(e) => setForm({ ...form, result: e.target.value })}
          >
            <option value="">All results</option>
            {["PASS", "FAIL", "CONDITIONAL"].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </label>
        {(["from", "to"] as const).map((k) => (
          <label key={k}>
            {k} (local time)
            <input
              type="datetime-local"
              value={form[k]}
              onChange={(e) => setForm({ ...form, [k]: e.target.value })}
            />
          </label>
        ))}
        <label>
          Page size
          <select
            value={form.pageSize}
            onChange={(e) => setForm({ ...form, pageSize: e.target.value })}
          >
            {[10, 20, 50, 100].map((n) => (
              <option key={n}>{n}</option>
            ))}
          </select>
        </label>
        <button disabled={busy}>Search history</button>
      </form>
      <button disabled={busy} onClick={() => void load(filters)}>
        Refresh history
      </button>
      {busy && <p role="status">Loading history…</p>}
      {error && <p role="alert">{error}</p>}
      {page && (
        <>
          <p>
            {page.total} inspections · Page {page.filters.page}
          </p>
          {!page.items.length ? (
            <p>No inspections match these filters.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Inspection</th>
                  <th>Room / Branch</th>
                  <th>Version</th>
                  <th>Inspector</th>
                  <th>Status / Result</th>
                  <th>Recorded at</th>
                </tr>
              </thead>
              <tbody>
                {page.items.map((i) => (
                  <tr key={i.id}>
                    <td>
                      <Link to={`/inspections/history/${i.branchId}/${i.id}`}>
                        {i.number}
                      </Link>
                    </td>
                    <td>
                      {i.roomNumber} / #{i.branchId}
                    </td>
                    <td>
                      {i.versionNumber} (#{i.versionId})
                    </td>
                    <td>{i.inspectorName}</td>
                    <td>
                      {i.status} / {i.result ?? "Pending"}
                    </td>
                    <td>{i.at}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <button
            disabled={busy || page.filters.page <= 1}
            onClick={() =>
              void load({ ...filters, page: page.filters.page - 1 })
            }
          >
            Previous page
          </button>
          <button
            disabled={
              busy || page.filters.page * page.filters.pageSize >= page.total
            }
            onClick={() =>
              void load({ ...filters, page: page.filters.page + 1 })
            }
          >
            Next page
          </button>
        </>
      )}
    </main>
  );
}
export function HistoryDetailPage() {
  const p = useParams();
  return (
    <HistoryDetail
      key={`${p.branch}/${p.id}`}
      branchText={p.branch ?? ""}
      idText={p.id ?? ""}
    />
  );
}
function HistoryDetail({
  branchText,
  idText,
}: {
  branchText: string;
  idText: string;
}) {
  const { run, busy, error } = useOperation();
  const [detail, setDetail] = useState<Detail | null>(null);
  let branch = 0,
    id = 0;
  try {
    branch = parseId(branchText);
    id = parseId(idText);
  } catch {
    /* Invalid route sends no request. */
  }
  const load = () => {
    setDetail(null);
    void run((s) => api.detail(branch, id, s), setDetail);
  };
  useEffect(() => {
    let alive = true;
    queueMicrotask(() => {
      if (alive && branch && id) load();
    });
    return () => {
      alive = false;
    };
  }, []);
  if (!branch || !id)
    return (
      <main>
        <h1>Invalid history identity</h1>
      </main>
    );
  return (
    <main>
      <h1>Inspection History #{id}</h1>
      <Link to="/inspections/history">Back to history</Link>
      <button disabled={busy} onClick={load}>
        Reload history detail
      </button>
      {busy && <p role="status">Loading detail…</p>}
      {error && <p role="alert">{error}</p>}
      {detail && (
        <>
          <h2>{detail.inspection.number}</h2>
          <p>
            Room {detail.inspection.roomNumber} · Branch #
            {detail.inspection.branchId} · Version{" "}
            {detail.inspection.versionNumber} (#{detail.inspection.versionId})
          </p>
          <p>
            Inspector {detail.inspection.inspectorName} ·{" "}
            {detail.inspection.status} ·{" "}
            {detail.inspection.result ?? "No conclusion"} ·{" "}
            {detail.inspection.at}
          </p>
          <p>{detail.inspection.summary}</p>
          <Link to={`/inspections/${branch}/${id}/execution`}>
            Open execution (assigned inspector only)
          </Link>
          <Results criteria={detail.criteria} defects={detail.defects} />
          <h2>Housekeepers</h2>
          {detail.housekeepers.map((a) => (
            <p key={a.id}>
              {a.name} · {a.status} · {a.assignedAt} – {a.endedAt ?? "Open"} ·{" "}
              {a.reason}
            </p>
          ))}
          <h2>Reworks</h2>
          {detail.reworks.map((r) => (
            <p key={r.id}>
              #{r.id} · {r.status} · {r.reason} · {r.requestedAt} ·{" "}
              {r.completedAt} · {r.note}
            </p>
          ))}
          <h2>Cycle inspections</h2>
          {detail.cycleInspections.map((i) => (
            <p key={i.id}>
              <Link to={`/inspections/history/${i.branchId}/${i.id}`}>
                {i.number}
              </Link>{" "}
              · {i.status} · {i.result}
            </p>
          ))}
          <EvidenceList items={detail.evidence} branch={branch} id={id} />
        </>
      )}
    </main>
  );
}
