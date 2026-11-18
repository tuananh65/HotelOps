import {
  restoredFilterDraft,
  CollapsibleFilterPanel,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  InboxFailure,
  readInbox,
  markInboxRead,
  openInboxSource,
  type Inbox,
} from "./inboxApi";
export function NotificationCenter() {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    !state.session.mustChangePassword &&
    state.pending === null
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [query, setQuery] = useState("");
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState<number[]>([]);
  const [draft, setDraft] = useState({
    type: "",
    priority: "",
    search: "",
    unread: "",
    acknowledged: "",
    from: "",
    to: "",
  });
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Inbox;
    error?: string;
  } | null>(null);
  const [write, setWrite] = useState<{
    owner: AuthorityGeneration;
    busy: boolean;
    message?: string;
  } | null>(null);
  const pending = useRef<AbortController | null>(null);
  const key = `${query}:${revision}`;
  useEffect(() => setSelected([]), [owner, key]);
  useEffect(() => {
    setWrite(null);
    return () => {
      pending.current?.abort();
      pending.current = null;
    };
  }, [owner]);
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readInbox(query, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          key,
          error: "Không tải được thông báo. Tải lại để thử lại.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const appliedFilters =
    [...new URLSearchParams(query).entries()]
      .filter(([name]) => name !== "page")
      .map(([name, value]) => `${name}: ${value}`)
      .join("; ") || "Không có";
  const operation = write?.owner === owner ? write : null;
  async function openSource(id: number) {
    if (!owner || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setWrite({ owner, busy: true });
    try {
      const path = await openInboxSource(
        id,
        controller.signal,
        () => current.current === owner,
      );
      if (current.current === owner && !controller.signal.aborted)
        navigate(path);
    } catch (error) {
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message:
            error instanceof InboxFailure && [403, 404].includes(error.status)
              ? "MSG02: Nguồn chưa khả dụng hoặc bạn không còn quyền truy cập. Nội dung thông báo vẫn hiển thị."
              : "Nguồn hoặc xác nhận đọc chưa khả dụng. Tải lại để kiểm tra quyền.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (pending.current === controller) pending.current = null;
    }
  }
  async function mark(id: number | number[] | null, acknowledge = false) {
    if (!owner || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setWrite({ owner, busy: true });
    try {
      await markInboxRead(
        id,
        controller.signal,
        () => current.current === owner,
        acknowledge ? "acknowledge" : "read",
      );
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message: acknowledge
            ? "Đã xác nhận. Hồ sơ nguồn không thay đổi."
            : "Đã đánh dấu đã đọc. Hồ sơ nguồn không thay đổi.",
        });
        // Read/acknowledgment filters can remove the last row on this page.
        // Restart the filtered list so remaining matches stay reachable.
        setQuery((value) => {
          const parameters = new URLSearchParams(value);
          parameters.delete("page");
          return parameters.toString();
        });
        setRevision((n) => n + 1);
      }
    } catch (error) {
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message:
            "Chưa xác nhận trạng thái đã đọc. Tải lại trước khi thử lại.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (pending.current === controller) pending.current = null;
    }
  }
  return (
    <main aria-label="Trung tâm thông báo">
      <h1>Trung tâm thông báo</h1>
      <nav>
        <Link to="/dashboard">Tổng quan</Link>{" "}
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </nav>
      {owner && (
        <>
          <form
            aria-label="Bộ lọc thông báo"
            onSubmit={(e) => {
              e.preventDefault();
              const p = new URLSearchParams();
              Object.entries(draft).forEach(([k, v]) => {
                if (v.trim()) p.set(k, v.trim());
              });
              setQuery(p.toString());
              setRevision((n) => n + 1);
            }}
          >
            <div className="ds-quick-search">
              <label>
                Tìm thông báo
                <input
                  type="search"
                  maxLength={200}
                  value={draft.search}
                  onChange={(e) =>
                    setDraft((current) => ({
                      ...current,
                      search: e.target.value,
                    }))
                  }
                />
              </label>
              <button>Áp dụng bộ lọc</button>
            </div>
            <CollapsibleFilterPanel
              onOpen={() =>
                setDraft((v) => ({
                  ...restoredFilterDraft(v, query, {}),
                  search: v.search,
                }))
              }
              onClose={() =>
                setDraft((v) => ({
                  ...restoredFilterDraft(v, query, {}),
                  search: v.search,
                }))
              }
              activeCount={
                [...new URLSearchParams(query)].filter(
                  ([k, v]) => !["page", "pageSize"].includes(k) && !!v,
                ).length
              }
            >
              {(["type", "priority", "from", "to"] as const).map((name) => (
                <label key={name}>
                  {
                    {
                      type: "Loại thông báo",
                      priority: "Độ ưu tiên",
                      search: "Tìm thông báo",
                      from: "Từ ngày",
                      to: "Đến ngày",
                    }[name]
                  }
                  <input
                    type={name === "from" || name === "to" ? "date" : "text"}
                    value={draft[name]}
                    onChange={(e) =>
                      setDraft((d) => ({ ...d, [name]: e.target.value }))
                    }
                  />
                </label>
              ))}
              <label>
                Trạng thái đã đọc
                <select
                  aria-label="Trạng thái đã đọc"
                  value={draft.unread}
                  onChange={(e) =>
                    setDraft((d) => ({ ...d, unread: e.target.value }))
                  }
                >
                  <option value="">Tất cả</option>
                  <option value="true">Chưa đọc</option>
                  <option value="false">Đã đọc</option>
                </select>
              </label>

              <button
                type="button"
                onClick={() => {
                  setDraft({
                    type: "",
                    priority: "",
                    search: "",
                    unread: "",
                    acknowledged: "",
                    from: "",
                    to: "",
                  });
                  setQuery("");
                  setRevision((n) => n + 1);
                }}
              >
                Xóa bộ lọc
              </button>
              <label>
                Trạng thái xác nhận đã đọc
                <select
                  value={draft.acknowledged}
                  onChange={(e) =>
                    setDraft((d) => ({ ...d, acknowledged: e.target.value }))
                  }
                >
                  <option value="">Tất cả</option>
                  <option value="false">Chờ xác nhận đã đọc</option>
                  <option value="true">Đã xác nhận</option>
                </select>
              </label>
            </CollapsibleFilterPanel>
          </form>
          <button onClick={() => setRevision((n) => n + 1)}>Tải lại</button>
          <p aria-label="Bộ lọc thông báo đã áp dụng">
            Bộ lọc đã áp dụng: {appliedFilters}
          </p>
          {data?.acknowledgmentAvailable && (
            <button
              disabled={
                !!operation?.busy ||
                !data.items.some(
                  (r) =>
                    r.requiresAcknowledgment &&
                    !r.acknowledgedAt &&
                    selected.includes(r.id),
                )
              }
              onClick={() =>
                void mark(
                  data.items
                    .filter(
                      (r) =>
                        r.requiresAcknowledgment &&
                        !r.acknowledgedAt &&
                        selected.includes(r.id),
                    )
                    .map((r) => r.id),
                  true,
                )
              }
            >
              Xác nhận đã nhận đã chọn
            </button>
          )}
          <button
            disabled={
              !data ||
              !!operation?.busy ||
              !data.items.some((r) => !r.readAt && selected.includes(r.id))
            }
            onClick={() =>
              void mark(
                data!.items
                  .filter((r) => !r.readAt && selected.includes(r.id))
                  .map((r) => r.id),
              )
            }
          >
            Đánh dấu mục đã chọn đã đọc
          </button>
          <button
            disabled={!data || !!operation?.busy || data.unreadCount === 0}
            onClick={() => void mark(null)}
          >
            Đánh dấu tất cả đã đọc
          </button>
          {operation?.message && <p role="status">{operation.message}</p>}
          {visible?.error && <p role="alert">{visible.error}</p>}
          {!visible && <p role="status">Đang tải thông báo…</p>}
          {data && (
            <>
              <p role="status" aria-label="Số lượng thông báo">
                Chưa đọc: {data.unreadCount}. Matching: {data.totalCount}.
                Snapshot: {data.snapshot}
              </p>
              {!data.acknowledgmentAvailable && (
                <p>Chưa hỗ trợ xác nhận cảnh báo nghiêm trọng.</p>
              )}
              {data.items.length === 0 ? (
                <p role="status">
                  Không có thông báo phù hợp (MSG10). Đổi hoặc xóa bộ lọc.
                </p>
              ) : (
                <ul>
                  {data.items.map((row) => (
                    <li
                      key={row.id}
                      data-awaiting-acknowledgment={
                        !!row.requiresAcknowledgment && !row.acknowledgedAt
                      }
                      style={
                        row.requiresAcknowledgment && !row.acknowledgedAt
                          ? {
                              borderInlineStart: "4px solid currentColor",
                              paddingInlineStart: "1rem",
                            }
                          : undefined
                      }
                    >
                      <label>
                        <input
                          type="checkbox"
                          checked={selected.includes(row.id)}
                          disabled={
                            (!!row.readAt &&
                              !(
                                row.requiresAcknowledgment &&
                                !row.acknowledgedAt
                              )) ||
                            !!operation?.busy
                          }
                          onChange={(e) =>
                            setSelected((ids) =>
                              e.target.checked
                                ? [...ids, row.id]
                                : ids.filter((id) => id !== row.id),
                            )
                          }
                        />
                        Chọn thông báo {row.id}
                      </label>
                      <h2>{row.title}</h2>
                      <button
                        disabled={!!operation?.busy}
                        onClick={() => void openSource(row.id)}
                      >
                        Mở nguồn
                      </button>
                      <p>{row.message}</p>
                      {data.acknowledgmentAvailable &&
                        row.requiresAcknowledgment &&
                        !row.acknowledgedAt && (
                          <button
                            disabled={!!operation?.busy}
                            onClick={() => void mark([row.id], true)}
                          >
                            Xác nhận đã nhận thông báo {row.id}
                          </button>
                        )}
                      {row.requiresAcknowledgment && !row.acknowledgedAt && (
                        <strong>Chờ xác nhận đã đọc</strong>
                      )}
                      {row.acknowledgedAt && (
                        <p>
                          Đã xác nhận: <time>{row.acknowledgedAt}</time>
                        </p>
                      )}
                      <p>
                        {displayCode(row.type)} · {row.priority} ·{" "}
                        <time>{row.createdAt}</time> ·{" "}
                        {row.readAt ? "Đã đọc" : "Chưa đọc"}
                      </p>
                      <button
                        disabled={!!row.readAt || !!operation?.busy}
                        onClick={() => void mark(row.id)}
                      >
                        Đánh dấu thông báo {row.id} là đã đọc
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <button
                disabled={data.page <= 1}
                onClick={() => {
                  const p = new URLSearchParams(query);
                  p.set("page", String(data.page - 1));
                  setQuery(p.toString());
                }}
              >
                Trước
              </button>
              <button
                disabled={data.page * data.pageSize >= data.totalCount}
                onClick={() => {
                  const p = new URLSearchParams(query);
                  p.set("page", String(data.page + 1));
                  setQuery(p.toString());
                }}
              >
                Tiếp
              </button>
            </>
          )}
        </>
      )}
    </main>
  );
}
