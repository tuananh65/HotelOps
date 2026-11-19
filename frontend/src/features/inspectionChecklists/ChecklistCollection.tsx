import { SearchInput, searchSuggestions } from "../../components/SearchInput";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { CollapsibleFilterPanel } from "../../components/workspace-interactions";
import { displayCode } from "../../i18n/vi";
import { useEffect, useState } from "react";
import { createChecklistApi } from "./api";
import {
  collectionQuery,
  defaultCollectionQuery,
  type CollectionPage,
  type VersionStatus,
} from "./collection";
import { positiveId } from "./model";
const api = createChecklistApi();
export function ChecklistCollection({
  branch,
  refresh,
  blocked,
  onOpen,
  onDenied,
  onAuthorized,
}: {
  branch: number;
  refresh: number;
  blocked: boolean;
  onOpen: (id: number) => void;
  onDenied: (message: string) => void;
  onAuthorized: () => void;
}) {
  const [query, setQuery] = useState(defaultCollectionQuery);
  const [search, setSearch] = useState("");
  const [active, setActive] = useState("");
  const [status, setStatus] = useState("");
  const [type, setType] = useState("");
  const [size, setSize] = useState("20");
  const [page, setPage] = useState<CollectionPage | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setBusy(true);
    setError("");
    setPage(null);
    api
      .list(branch, query, controller.signal)
      .then((value) => {
        if (controller.signal.aborted) return;
        setPage(value);
        onAuthorized();
      })
      .catch((e) => {
        if (controller.signal.aborted) return;
        setPage(null);
        if ([401, 403].includes(e.status)) {
          setError("");
          onDenied(e.message);
        } else setError(e.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => controller.abort();
  }, [branch, query, refresh, reload, onDenied, onAuthorized]);
  function restoreAdvanced() {
    setActive(
      query.isActive === null || query.isActive === undefined
        ? ""
        : String(query.isActive),
    );
    setStatus(query.status ?? "");
    setType(query.roomTypeId?.toString() ?? "");
    setSize(String(query.pageSize));
  }
  function apply(e: React.FormEvent) {
    e.preventDefault();
    try {
      setQuery(
        collectionQuery({
          page: 1,
          pageSize: Number(size),
          search,
          isActive: active === "" ? null : active === "true",
          status: status === "" ? null : (status as VersionStatus),
          roomTypeId: type.trim() ? positiveId(type.trim()) : null,
        }),
      );
      setFormError("");
    } catch (e) {
      setFormError((e as Error).message);
    }
  }
  const visible = page && !blocked && !busy ? page : null;
  const pages = visible
    ? Math.max(1, Math.ceil(visible.total / query.pageSize))
    : 1;
  return (
    <section aria-label="Danh sách checklist" aria-busy={busy}>
      <div className="fe05-actions">
        <h2>Danh sách checklist</h2>
        <button
          type="button"
          disabled={busy}
          onClick={() => setReload((n) => n + 1)}
        >
          Tải lại danh sách
        </button>
      </div>
      <form onSubmit={apply} aria-label="Bộ lọc checklist">
        <div className="ds-quick-search">
          <label>
            Tìm mã hoặc tên
            <SearchInput suggestions={searchSuggestions(visible)}
              maxLength={200}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <button type="submit">Tìm kiếm</button>
        </div>
        <CollapsibleFilterPanel
          activeCount={
            [query.isActive, query.status, query.roomTypeId].filter(
              (v) => v !== null && v !== undefined,
            ).length
          }
          onOpen={restoreAdvanced}
          onClose={restoreAdvanced}
        >
          <div className="fe05-filters">
            <label>
              Hoạt động
              <SearchableSelect
                value={active}
                onChange={(e) => setActive(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="true">Đang hoạt động</option>
                <option value="false">Ngừng hoạt động</option>
              </SearchableSelect>
            </label>
            <label>
              Trạng thái phiên bản
              <SearchableSelect
                value={status}
                onChange={(e) => setStatus(e.target.value)}
              >
                <option value="">Tất cả</option>
                {["DRAFT", "PUBLISHED", "RETIRED"].map((v) => (
                  <option key={v} value={v}>
                    {displayCode(v)}
                  </option>
                ))}
              </SearchableSelect>
            </label>
            <label>
              ID loại phòng
              <input
                inputMode="numeric"
                value={type}
                onChange={(e) => setType(e.target.value)}
              />
            </label>
            <label>
              Số dòng mỗi trang
              <SearchableSelect value={size} onChange={(e) => setSize(e.target.value)}>
                {[20, 50, 100].map((v) => (
                  <option key={v} value={v}>
                    {displayCode(v)}
                  </option>
                ))}
              </SearchableSelect>
            </label>
          </div>
          <p>Trạng thái khớp bất kỳ phiên bản đã lưu, kể cả lịch sử.</p>
          <button type="submit">Áp dụng bộ lọc</button>
          <button
            type="button"
            onClick={() => {
              setSearch("");
              setActive("");
              setStatus("");
              setType("");
              setSize("20");
              setFormError("");
              setQuery(defaultCollectionQuery());
            }}
          >
            Xóa bộ lọc
          </button>
          {formError && <p role="alert">{userMessage(formError)}</p>}
        </CollapsibleFilterPanel>
      </form>
      {busy && <p role="status">Đang tải danh sách checklist…</p>}
      {error && <p role="alert">{userMessage(error)}</p>}
      {visible && (
        <>
          <p role="status">
            Tổng cộng {visible.total} danh sách kiểm tra · Trang {query.page}/
            {pages}
          </p>
          {visible.items.length ? (
            <div className="fe05-table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Mã</th>
                    <th>Tên</th>
                    <th>Hoạt động</th>
                    <th>Loại phòng</th>
                    <th>Các phiên bản</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.items.map((item) => (
                    <tr key={item.id}>
                      <td>{item.code}</td>
                      <td>{item.name}</td>
                      <td>{item.active ? "Có" : "Không"}</td>
                      <td>{item.roomTypeIds.join(", ")}</td>
                      <td>
                        {item.versions
                          .map(
                            (v) =>
                              `${v.number}: ${v.status}${v.usedByInspection ? " (đã sử dụng)" : ""}`,
                          )
                          .join(", ")}
                      </td>
                      <td>
                        <button type="button" onClick={() => onOpen(item.id)}>
                          Mở {item.code}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p>Không có checklist phù hợp trên trang này.</p>
          )}
          <nav aria-label="Phân trang checklist">
            <button
              type="button"
              disabled={query.page <= 1}
              onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
            >
              Trang trước
            </button>
            <button
              type="button"
              disabled={query.page >= pages}
              onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
            >
              Trang sau
            </button>
          </nav>
        </>
      )}
    </section>
  );
}
