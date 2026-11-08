import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { PageHeader } from "../../ui";
import {
  listTurnovers,
  readTurnover,
  turnoverError,
  type TurnoverPage,
  type TurnoverDetail,
  type Coverage,
} from "../../api/dev1/turnoverHistory";
function CoverageNote({ value }: { value: Coverage }) {
  return (
    <p role="note">
      Coverage: Cleaning {value.cleaning} · Inspection {value.inspection} ·
      Rework {value.rework} · Readiness {value.readiness} · RoomHistory{" "}
      {value.roomStatusHistory}. Kết quả Inspection chưa accepted; không suy
      diễn PASS/FAIL.
    </p>
  );
}
export default function RoomTurnoverHistoryScreen() {
  const [params, setParams] = useSearchParams(),
    [retry, setRetry] = useState(0);
  const key = params.toString(),
    room = params.get("roomId") || params.get("room") || "",
    cycle = params.get("cycleId") || "";
  const [result, setResult] = useState<{
      key: string;
      page?: TurnoverPage;
      detail?: TurnoverDetail;
    } | null>(null),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError("");
    setLoading(!!room);
    if (room)
      (cycle
        ? readTurnover(
            room,
            cycle,
            new URLSearchParams(key),
            abort.signal,
          ).then((detail) => ({ key, detail }))
        : listTurnovers(room, new URLSearchParams(key), abort.signal).then(
            (page) => ({ key, page }),
          )
      )
        .then((d) => {
          if (!abort.signal.aborted) setResult(d);
        })
        .catch((e) => {
          if (!abort.signal.aborted) setError(turnoverError(e));
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    return () => abort.abort();
  }, [room, cycle, key, retry]);
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = new URLSearchParams();
    new FormData(e.currentTarget).forEach((v, k) => {
      if (String(v).trim()) next.set(k, String(v).trim());
    });
    next.set("page", "1");
    setParams(next);
    setRetry((v) => v + 1);
  }
  function href(id?: number) {
    const next = new URLSearchParams(params);
    next.delete("room");
    next.set("roomId", room);
    next.delete("timelinePage");
    if (id) next.set("cycleId", String(id));
    else next.delete("cycleId");
    return `/rooms/turnover?${next}`;
  }
  function pageChange(name: string, value: number) {
    const next = new URLSearchParams(params);
    next.set(name, String(value));
    setParams(next);
  }
  const data = result?.key === key ? result : null;
  const p = data?.page,
    d = data?.detail;
  return (
    <>
      <PageHeader id={30} />
      <form key={key} className="filter-bar" onSubmit={submit}>
        <label>
          Chi nhánh (ID)
          <input
            name="branchId"
            inputMode="numeric"
            defaultValue={params.get("branchId") || ""}
          />
        </label>
        <label>
          Phòng (ID)
          <input
            name="roomId"
            inputMode="numeric"
            required
            defaultValue={room}
          />
        </label>
        <label>
          Từ UTC
          <input
            name="from"
            placeholder="2026-11-01T00:00:00Z"
            defaultValue={params.get("from") || ""}
          />
        </label>
        <label>
          Đến UTC
          <input
            name="to"
            placeholder="2026-11-07T23:59:59Z"
            defaultValue={params.get("to") || ""}
          />
        </label>
        <label>
          Kết quả Inspection
          <select
            aria-label="Kết quả Inspection"
            name="result"
            defaultValue={params.get("result") || ""}
          >
            <option value="">Tất cả (coverage hiện có)</option>
            <option value="PASS">PASS — cần nguồn accepted</option>
            <option value="FAIL">FAIL — cần nguồn accepted</option>
          </select>
        </label>
        <label>
          Số dòng
          <select name="pageSize" defaultValue={params.get("pageSize") || "20"}>
            {[10, 20, 50, 100].map((n) => (
              <option key={n}>{n}</option>
            ))}
          </select>
        </label>
        <button>Tải lịch sử</button>
      </form>
      {!room && <p>Chọn ID phòng để xem lịch sử từ máy chủ.</p>}
      {loading && <p role="status">Đang tải lịch sử…</p>}
      {error && (
        <p role="alert">
          {error}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {p && (
        <>
          <CoverageNote value={p.coverage} />
          <p>
            {p.total} cycles · phòng {p.roomId} · chi nhánh {p.branchId}
          </p>
          {!p.items.length ? (
            <p>Không có cycle phù hợp bộ lọc.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    {[
                      "Cycle",
                      "Loại",
                      "Trạng thái",
                      "Hiện tại",
                      "Nhân viên",
                      "Bắt đầu UTC",
                      "Kết thúc UTC",
                      "Rework",
                      "Lý do hủy",
                    ].map((x) => (
                      <th key={x}>{x}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {p.items.map((c) => (
                    <tr key={c.id}>
                      <td>
                        <Link to={href(c.id)}>{c.number}</Link>
                      </td>
                      <td>{c.cleaningType}</td>
                      <td>{c.workflowState ?? c.recordedStatus}</td>
                      <td>
                        {c.isCurrentCycle === null
                          ? "Chưa xác định"
                          : c.isCurrentCycle
                            ? "Có"
                            : "Lịch sử"}
                      </td>
                      <td>
                        {c.assignees.map((a) => a.name).join(", ") ||
                          "Chưa phân công"}
                      </td>
                      <td>{c.startUtc || "—"}</td>
                      <td>{c.endUtc || "—"}</td>
                      <td>{c.reworkCount ?? "Chưa xác định"}</td>
                      <td>{c.cancellationReason || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <button
            disabled={p.filters.page <= 1}
            onClick={() => pageChange("page", p.filters.page - 1)}
          >
            Trang trước
          </button>{" "}
          Trang {p.filters.page}{" "}
          <button
            disabled={p.filters.page * p.filters.pageSize >= p.total}
            onClick={() => pageChange("page", p.filters.page + 1)}
          >
            Trang sau
          </button>
        </>
      )}
      {d && (
        <>
          <h2>
            {d.cycle.number} · Phòng {d.cycle.roomNumber}
          </h2>
          <Link to={href()}>Danh sách cycles</Link>
          <CoverageNote value={d.coverage} />
          <p>
            {d.cycle.isCurrentCycle === null
              ? "Current cycle chưa xác định"
              : d.cycle.isCurrentCycle
                ? "Current cycle"
                : "Historical cycle"}{" "}
            · {d.cycle.workflowState ?? d.cycle.recordedStatus}
          </p>
          <h2>Timeline ({d.timelineTotal})</h2>
          {!d.timeline.length && <p>Không có sự kiện trên trang này.</p>}
          <ol>
            {d.timeline.map((e) => (
              <li key={`${e.sourceType}:${e.sourceId}:${e.event}`}>
                <time>{e.atUtc}</time> · {e.sourceType} #{e.sourceId} ·{" "}
                {e.event} · {e.state} · {e.note} · Actor {e.actorId ?? "—"}
              </li>
            ))}
          </ol>
          <button
            disabled={d.timelinePage <= 1}
            onClick={() => pageChange("timelinePage", d.timelinePage - 1)}
          >
            Timeline trước
          </button>{" "}
          Trang {d.timelinePage}{" "}
          <button
            disabled={d.timelinePage * d.timelinePageSize >= d.timelineTotal}
            onClick={() => pageChange("timelinePage", d.timelinePage + 1)}
          >
            Timeline sau
          </button>
          <h2>Inspection metadata (chưa accepted)</h2>
          <ul>
            {d.inspections.map((i) => (
              <li key={i.id}>
                Inspection {i.id} · Checklist version {i.versionNumber} (ID{" "}
                {i.versionId}) · {i.recordedStatus} · {i.atUtc} · Result
                unavailable
              </li>
            ))}
          </ul>
          <h2>Rework đã ghi nhận</h2>
          <ul>
            {d.reworks.map((w) => (
              <li key={w.id}>
                Rework {w.id} · Inspection {w.inspectionId} ·{" "}
                {w.workflowState ?? w.recordedStatus} · {w.requestedAtUtc} ·{" "}
                {w.note}
              </li>
            ))}
          </ul>
          <h2>Evidence</h2>
          <ul>
            {d.evidence.map((e) => (
              <li key={e.id}>
                <a href={e.downloadPath} target="_blank" rel="noreferrer">
                  {e.name}
                </a>{" "}
                · {e.entityType} #{e.entityId}
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}
