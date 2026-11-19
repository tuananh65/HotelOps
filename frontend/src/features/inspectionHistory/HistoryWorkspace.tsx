import { SearchInput, searchSuggestions } from "../../components/SearchInput";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import {
  restoredFilterDraft,
  CollapsibleFilterPanel,
} from "../../components/workspace-interactions";
import { HistoryDetails } from "./HistoryDetails";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { useWorkspacePermissions } from "../../screens/housekeeping/useWorkspacePermissions";
import {
  ReadDrawer,
  operationalLabel,
  recordedTime,
  hotelInputToUtc,
  utcToHotelInput,
} from "../../screens/housekeeping/OperationalReadView";
import { useEffect, useRef, useState } from "react";
import { createHistoryApi, HistoryError } from "./api";
import {
  filters,
  positiveId,
  type Detail,
  type Evidence,
  type Filters,
  type HistoryPage,
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
  const { state } = useAuth();
  const access = useWorkspacePermissions(branch);
  const canExecute = (row: Detail["inspection"]) =>
    access.allows("INSPECTION_PERFORM") &&
    String(row.inspectorId) === state.session?.user.id &&
    ["PENDING", "ASSIGNED", "IN_PROGRESS"].includes(row.status);
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
        from: hotelInputToUtc(form.from),
        to: hotelInputToUtc(form.to),
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
    <section className="ops-history" aria-label="Lịch sử kiểm tra phòng">
      <CollapsibleFilterPanel
        onOpen={() =>
          setForm((v) => ({
            ...restoredFilterDraft(v, query, { pageSize: "20" }),
            from: utcToHotelInput(query.from),
            to: utcToHotelInput(query.to),
          }))
        }
        onClose={() =>
          setForm((v) => ({
            ...restoredFilterDraft(v, query, { pageSize: "20" }),
            from: utcToHotelInput(query.from),
            to: utcToHotelInput(query.to),
          }))
        }
        activeCount={
          Object.entries(query).filter(
            ([k, v]) => !["page", "pageSize"].includes(k) && !!v,
          ).length
        }
      >
        <form onSubmit={apply} aria-label="Bộ lọc lịch sử">
          {(["from", "to"] as const).map((key) => (
            <label key={key}>
              {key === "from"
                ? "Từ ngày, giờ (Việt Nam)"
                : "Đến ngày, giờ (Việt Nam)"}
              <input
                type="datetime-local"
                value={form[key]}
                onChange={(e) => setForm({ ...form, [key]: e.target.value })}
              />
            </label>
          ))}
          <label>
            Kết quả
            <SearchableSelect
              aria-label="Kết quả"
              value={form.result}
              onChange={(e) => setForm({ ...form, result: e.target.value })}
            >
              <option value="">Tất cả</option>
              {["PASS", "FAIL", "CONDITIONAL"].map((value) => (
                <option key={value} value={value}>
                  {operationalLabel(value)}
                </option>
              ))}
            </SearchableSelect>
          </label>
          <label>
            Tìm kiếm
            <SearchInput suggestions={searchSuggestions(page)}
              maxLength={150}
              placeholder="Số phòng hoặc mã kiểm tra"
              value={form.search}
              onChange={(e) => setForm({ ...form, search: e.target.value })}
            />
          </label>
          <label>
            Số dòng
            <SearchableSelect
              aria-label="Số dòng"
              value={form.pageSize}
              onChange={(e) => setForm({ ...form, pageSize: e.target.value })}
            >
              {[1, 10, 20, 50, 100].map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </SearchableSelect>
          </label>
          <button type="submit">Áp dụng bộ lọc</button>
        </form>
      </CollapsibleFilterPanel>
      <button onClick={() => setReload((n) => n + 1)}>Tải lại lịch sử</button>
      {error && <p role="alert">{userMessage(error)}</p>}
      {notice && <p role="status">{userMessage(notice)}</p>}
      {listBusy && <p role="status">Đang tải danh sách…</p>}
      {page && (
        <>
          <p>
            {page.total} bản ghi · Trang {page.filters.page}
          </p>
          {!page.items.length ? (
            <p role="status">
              Không có lịch sử khớp bộ lọc. Thử thay đổi thời gian hoặc kết quả.
            </p>
          ) : (
            <div className="ops-table-wrap">
              <table className="ops-history-table">
                <caption>Lịch sử kiểm tra phòng</caption>
                <thead>
                  <tr>
                    <th>Mã kiểm tra</th>
                    <th>Phòng</th>
                    <th>Người kiểm tra</th>
                    <th>Thời điểm</th>
                    <th>Kết quả</th>
                    <th>Khắc phục</th>
                  </tr>
                </thead>
                <tbody>
                  {page.items.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <button onClick={() => open(row.id)}>
                          {row.number}
                        </button>
                        {canExecute(row) && (
                          <Link
                            to={`/inspections/${branch}/${row.id}/execution`}
                          >
                            Thực hiện kiểm tra
                          </Link>
                        )}
                      </td>
                      <td>{row.roomNumber}</td>
                      <td>{row.inspectorName}</td>
                      <td>{recordedTime(row.at)}</td>
                      <td>
                        {row.result
                          ? operationalLabel(row.result)
                          : "Chưa kết luận"}
                        <small>{operationalLabel(row.status)}</small>
                      </td>
                      <td>
                        {row.defectCount == null
                          ? "Chưa xác minh lỗi"
                          : row.defectCount + " lỗi"}
                        <small>{row.reworkCount} yêu cầu làm lại</small>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
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
        <ReadDrawer
          title="Chi tiết lịch sử kiểm tra"
          onClose={() => setSelected(null)}
        >
          {error && <p role="alert">{userMessage(error)}</p>}
          {notice && <p role="status">{userMessage(notice)}</p>}
          {canExecute(detail.inspection) && (
            <Link
              to={`/inspections/${branch}/${detail.inspection.id}/execution`}
            >
              Thực hiện kiểm tra
            </Link>
          )}
          <HistoryDetails
            detail={detail}
            open={open}
            saveEvidence={saveEvidence}
            downloadBusy={downloadBusy}
          />
        </ReadDrawer>
      )}
    </section>
  );
}
