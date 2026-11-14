import { useEffect, useRef, useState } from "react";
import { createHistoryApi, HistoryError } from "./api";
import {
  filters,
  positiveId,
  type Detail,
  type Evidence,
  type Filters,
  type HistoryPage,
  type Inspection,
} from "./model";

export function HistoryWorkspace({
  branch,
  initialId,
  onDenied,
}: {
  branch: number;
  initialId?: number;
  onDenied: (status: 401 | 403) => void;
}) {
  const lifetime = useRef<AbortController | null>(null);
  const download = useRef<AbortController | null>(null);
  const urls = useRef(new Set<string>());
  const [query, setQuery] = useState<Filters>({ page: 1, pageSize: 20 });
  const [form, setForm] = useState({
    roomId: "",
    inspectorId: "",
    housekeeperId: "",
    from: "",
    to: "",
    result: "",
    search: "",
    pageSize: "20",
  });
  const [page, setPage] = useState<HistoryPage | null>(null),
    [detail, setDetail] = useState<Detail | null>(null);
  const [selected, setSelected] = useState<number | null>(initialId ?? null);
  const [listBusy, setListBusy] = useState(true),
    [detailBusy, setDetailBusy] = useState(false),
    [downloadBusy, setDownloadBusy] = useState(false);
  const [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [reload, setReload] = useState(0);
  useEffect(() => {
    const scope = new AbortController();
    lifetime.current = scope;
    return () => {
      scope.abort();
      download.current?.abort();
      for (const url of urls.current) URL.revokeObjectURL(url);
      urls.current.clear();
    };
  }, []);
  function fail(e: unknown) {
    if (e instanceof HistoryError && (e.status === 401 || e.status === 403)) {
      onDenied(e.status);
      return;
    }
    setError(e instanceof Error ? e.message : "Không đọc được lịch sử.");
  }
  useEffect(() => {
    const scope = lifetime.current!;
    const request = new AbortController();
    const api = createHistoryApi(branch, scope.signal);
    setPage(null);
    setListBusy(true);
    setError("");
    api
      .list(query, request.signal)
      .then((value) => {
        if (!request.signal.aborted && !scope.signal.aborted) setPage(value);
      })
      .catch((e) => {
        if (!request.signal.aborted && !scope.signal.aborted) fail(e);
      })
      .finally(() => {
        if (!request.signal.aborted && !scope.signal.aborted)
          setListBusy(false);
      });
    return () => request.abort();
    // State is owned by the keyed authorized subtree, not by callback identity.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, query, reload]);
  useEffect(() => {
    setDetail(null);
    setDetailBusy(false);
    if (selected === null) return;
    const scope = lifetime.current!;
    const request = new AbortController();
    setDetailBusy(true);
    createHistoryApi(branch, scope.signal)
      .detail(selected, request.signal)
      .then((value) => {
        if (!request.signal.aborted && !scope.signal.aborted) setDetail(value);
      })
      .catch((e) => {
        if (!request.signal.aborted && !scope.signal.aborted) fail(e);
      })
      .finally(() => {
        if (!request.signal.aborted && !scope.signal.aborted)
          setDetailBusy(false);
      });
    return () => request.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, selected, reload]);
  useEffect(
    () => () => {
      download.current?.abort();
    },
    [selected],
  );
  function open(id: number) {
    download.current?.abort();
    setDownloadBusy(false);
    setNotice("");
    setError("");
    setDetail(null);
    if (id === selected) setReload((n) => n + 1);
    else setSelected(id);
  }
  function apply(event: React.FormEvent) {
    event.preventDefault();
    try {
      const next = filters({
        page: 1,
        pageSize: Number(form.pageSize),
        from: form.from || null,
        to: form.to || null,
        result: form.result || null,
        search: form.search || null,
        roomId: form.roomId ? positiveId(form.roomId) : null,
        inspectorId: form.inspectorId ? positiveId(form.inspectorId) : null,
        housekeeperId: form.housekeeperId
          ? positiveId(form.housekeeperId)
          : null,
      });
      download.current?.abort();
      setDownloadBusy(false);
      setSelected(null);
      setDetail(null);
      setNotice("");
      setQuery(next);
      setError("");
    } catch (e) {
      setError((e as Error).message);
    }
  }
  async function saveEvidence(evidence: Evidence) {
    if (!detail || downloadBusy) return;
    const scope = lifetime.current!;
    const request = new AbortController();
    download.current = request;
    setDownloadBusy(true);
    setError("");
    setNotice("");
    try {
      const file = await createHistoryApi(branch, scope.signal).download(
        detail.inspection.id,
        evidence,
        request.signal,
      );
      if (scope.signal.aborted || request.signal.aborted) return;
      const url = URL.createObjectURL(file.blob);
      urls.current.add(url);
      const link = document.createElement("a");
      link.href = url;
      link.download = file.name;
      try {
        document.body.appendChild(link);
        link.click();
      } finally {
        link.remove();
      }
      setTimeout(() => {
        URL.revokeObjectURL(url);
        urls.current.delete(url);
      }, 1000);
      setNotice("Đã gửi tệp tới chức năng tải xuống của trình duyệt.");
    } catch (e) {
      if (!scope.signal.aborted && !request.signal.aborted) fail(e);
    } finally {
      if (download.current === request) download.current = null;
      if (!scope.signal.aborted && !request.signal.aborted)
        setDownloadBusy(false);
    }
  }
  return (
    <section aria-label="Lịch sử kiểm tra phòng">
      <h1>Lịch sử kiểm tra phòng</h1>
      <p>Chi nhánh #{branch}</p>
      <form onSubmit={apply} aria-label="Bộ lọc lịch sử">
        {(["roomId", "inspectorId", "housekeeperId"] as const).map((key) => (
          <label key={key}>
            {
              {
                roomId: "ID phòng",
                inspectorId: "ID người kiểm tra",
                housekeeperId: "ID nhân viên dọn phòng",
              }[key]
            }
            <input
              inputMode="numeric"
              value={form[key]}
              onChange={(e) => setForm({ ...form, [key]: e.target.value })}
            />
          </label>
        ))}
        {(["from", "to"] as const).map((key) => (
          <label key={key}>
            {key === "from" ? "Từ (UTC)" : "Đến (UTC)"}
            <input
              placeholder="2026-11-13T00:00:00Z"
              value={form[key]}
              onChange={(e) => setForm({ ...form, [key]: e.target.value })}
            />
          </label>
        ))}
        <label>
          Kết quả
          <select
            aria-label="Kết quả"
            value={form.result}
            onChange={(e) => setForm({ ...form, result: e.target.value })}
          >
            <option value="">Tất cả</option>
            {["PASS", "FAIL", "CONDITIONAL"].map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        <label>
          Tìm kiếm
          <input
            maxLength={150}
            value={form.search}
            onChange={(e) => setForm({ ...form, search: e.target.value })}
          />
        </label>
        <label>
          Số dòng
          <select
            aria-label="Số dòng"
            value={form.pageSize}
            onChange={(e) => setForm({ ...form, pageSize: e.target.value })}
          >
            {[10, 20, 50, 100].map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        <button type="submit">Áp dụng bộ lọc</button>
      </form>
      <button onClick={() => setReload((n) => n + 1)}>Tải lại lịch sử</button>
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {listBusy && <p role="status">Đang tải danh sách…</p>}
      {page && (
        <>
          <p>
            {page.total} bản ghi · Trang {page.filters.page}
          </p>
          {!page.items.length ? (
            <p>Không có lịch sử khớp bộ lọc đã áp dụng.</p>
          ) : (
            <table>
              <caption>Lịch sử Inspection</caption>
              <thead>
                <tr>
                  <th>Inspection</th>
                  <th>Phòng</th>
                  <th>Thời điểm ghi nhận</th>
                  <th>Người kiểm tra</th>
                  <th>Checklist lịch sử</th>
                  <th>Trạng thái / Kết quả</th>
                  <th>Cleaning task</th>
                  <th>Rework (API)</th>
                </tr>
              </thead>
              <tbody>
                {page.items.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <button onClick={() => open(row.id)}>{row.number}</button>
                    </td>
                    <td>
                      {row.roomNumber} (#{row.roomId})
                    </td>
                    <td>{row.at}</td>
                    <td>
                      {row.inspectorName} (#{row.inspectorId})
                    </td>
                    <td>
                      #{row.versionId} · v{row.versionNumber}
                    </td>
                    <td>
                      {row.status} / {row.result ?? "Chưa kết luận"}
                    </td>
                    <td>{row.cleaningTaskId ?? "—"}</td>
                    <td>{row.reworkCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <button
            disabled={listBusy || query.page <= 1}
            onClick={() => setQuery({ ...query, page: query.page - 1 })}
          >
            Trang trước
          </button>
          <button
            disabled={listBusy || query.page * query.pageSize >= page.total}
            onClick={() => setQuery({ ...query, page: query.page + 1 })}
          >
            Trang sau
          </button>
        </>
      )}
      {detailBusy && <p role="status">Đang tải chi tiết…</p>}
      {detail && (
        <section aria-label="Chi tiết lịch sử">
          <h2>{detail.inspection.number}</h2>
          <InspectionSummary row={detail.inspection} />
          <p>{detail.inspection.summary}</p>
          <h3>Nhân viên liên quan</h3>
          {detail.housekeepers.map((a) => (
            <p key={a.id}>
              {a.name} (#{a.userId}) · {a.status} · {a.assignedAt} —{" "}
              {a.endedAt ?? "Chưa kết thúc"} · {a.reason}
            </p>
          ))}
          <h3>Tiêu chí của phiên bản lịch sử</h3>
          {detail.criteria.map((c) => (
            <article key={c.itemId}>
              <h4>
                #{c.itemId} {c.criterion}
              </h4>
              <p>
                {c.group} · {c.mandatory ? "Bắt buộc" : "Tùy chọn"} ·{" "}
                {c.outcome ?? "Chưa ghi nhận"}
              </p>
              <p>
                {c.description} {c.passCondition}
              </p>
              <p>
                {c.note} {c.at}
              </p>
              <p>Evidence IDs: {c.evidenceIds?.join(", ") || "—"}</p>
            </article>
          ))}
          <h3>Defects</h3>
          {!detail.defects.length && <p>Không có defect được trả về.</p>}
          {detail.defects.map((d) => (
            <article key={d.id}>
              <h4>
                Defect #{d.id}: {d.description}
              </h4>
              <p>
                Criterion #{d.itemId ?? "—"} · {d.status} · Severity #
                {d.severityId ?? "—"} · {d.location}
              </p>
              <p>
                {d.correctiveAction} {d.resolutionNote} {d.resolvedAt}
              </p>
              <p>Evidence IDs: {d.evidenceIds?.join(", ") || "—"}</p>
            </article>
          ))}
          <h3>Reworks</h3>
          {!detail.reworks.length && <p>Không có rework được trả về.</p>}
          {detail.reworks.map((r) => (
            <article key={r.id}>
              <h4>Rework #{r.id}</h4>
              <p>
                Inspection #{r.inspectionId} · Cleaning task #{r.cleaningTaskId}{" "}
                · Assignee #{r.assigneeId ?? "—"} · {r.status}
              </p>
              <p>
                {r.reason} {r.note}
              </p>
              <p>
                {r.requestedAt} — {r.completedAt ?? "Chưa hoàn tất"}
              </p>
            </article>
          ))}
          <h3>Inspection cùng cleaning cycle</h3>
          {detail.cycleInspections.map((row) => (
            <article key={row.id}>
              <InspectionSummary row={row} />
              <button onClick={() => open(row.id)}>
                Xem lịch sử #{row.id}
              </button>
            </article>
          ))}
          <h3>Evidence</h3>
          {!detail.evidence.length && <p>Không có evidence được trả về.</p>}
          {detail.evidence.map((e) => (
            <article key={e.id}>
              <p>
                {e.name} · {e.mimeType ?? "Chưa có MIME"} ·{" "}
                {e.bytes ?? "Chưa có kích thước"} bytes · {e.uploadedAt}
              </p>
              <button
                disabled={downloadBusy}
                onClick={() => void saveEvidence(e)}
              >
                Tải evidence #{e.id}
              </button>
            </article>
          ))}
        </section>
      )}
    </section>
  );
}
function InspectionSummary({ row }: { row: Inspection }) {
  return (
    <p>
      Inspection #{row.id} · Phòng {row.roomNumber} (#{row.roomId}) · Chi nhánh
      #{row.branchId} · Cleaning cycle/task #{row.cleaningTaskId ?? "—"} ·
      Checklist #{row.versionId} v{row.versionNumber} · {row.inspectorName} (#
      {row.inspectorId}) · {row.status} / {row.result ?? "Chưa kết luận"} ·{" "}
      {row.at}
    </p>
  );
}
