import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { auditChanges, auditValue, recordHistoryQuery } from "./auditHistory";
import type { AuditRow } from "./auditApi";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  AuditFailure,
  readAudit,
  type AuditPage as Page,
  type AuditDetail,
} from "./auditApi";

export function AuditPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [draft, setDraft] = useState({
    branchId: "",
    actorId: "",
    entityType: "",
    entityId: "",
    action: "",
    search: "",
    from: "",
    to: "",
  });
  const [query, setQuery] = useState("page=1&pageSize=25");
  const [id, setId] = useState<number | null>(null);
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Page | AuditDetail;
    error?: string;
  } | null>(null);
  const key = `${query}:${id}:${retry}`;
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readAudit(query, id, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const status = error instanceof AuditFailure ? error.status : 0;
        setResult({
          owner,
          key,
          error:
            status === 403
              ? "Bạn không có quyền xem các nhật ký này."
              : status === 404
                ? "Bản ghi nhật ký này chưa khả dụng."
                : status === 400
                  ? "Kiểm tra bộ lọc đã chọn."
                  : "Dữ liệu nhật ký chưa khả dụng.",
        });
        if (status === 401) {
          actions.invalidateSession(owner);
        }
      });
    return () => controller.abort();
  }, [owner, key, query, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const list = data && "items" in data ? data : null;
  const detail = data && "entry" in data ? data : null;
  const changes = detail ? auditChanges(detail.before, detail.after) : [];
  function history(entry: AuditRow) {
    const next = recordHistoryQuery(entry, query);
    const params = new URLSearchParams(next);
    setDraft({
      branchId: params.get("branchId") ?? "",
      actorId: "",
      entityType: entry.entityType,
      entityId: entry.entityId,
      action: "",
      search: "",
      from: "",
      to: "",
    });
    setId(null);
    setQuery(next);
    setRetry((n) => n + 1);
  }
  function apply(event: FormEvent) {
    event.preventDefault();
    const params = new URLSearchParams({ page: "1", pageSize: "25" });
    for (const [name, value] of Object.entries(draft))
      if (value.trim()) params.set(name, value.trim());
    setId(null);
    setQuery(params.toString());
    setRetry((n) => n + 1);
  }
  function page(next: number) {
    const params = new URLSearchParams(query);
    params.set("page", String(next));
    setQuery(params.toString());
  }
  return (
    <main aria-label="Lịch sử thay đổi">
      <h1>Lịch sử thay đổi</h1>
      <Link to="/dashboard">Không gian làm việc</Link>
      {!owner ? (
        <p role="status">Dữ liệu nhật ký được ẩn trong khi kiểm tra phiên đăng nhập.</p>
      ) : (
        <>
          <form onSubmit={apply} aria-label="Bộ lọc lịch sử thay đổi">
            {Object.entries(draft).map(([name, value]) => (
              <label key={name}>
                {
                  (
                    {
                      branchId: "Mã chi nhánh",
                      actorId: "Mã người thực hiện",
                      entityType: "Loại đối tượng",
                      entityId: "Mã hồ sơ",
                      action: "Thao tác",
                      search: "Tìm kiếm",
                      from: "từ (UTC)",
                      to: "Đến (UTC)",
                    } as Record<string, string>
                  )[name]
                }
                <input
                  type={name === "from" || name === "to" ? "date" : "text"}
                  value={value}
                  maxLength={100}
                  onChange={(e) =>
                    setDraft((d) => ({ ...d, [name]: e.target.value }))
                  }
                />
              </label>
            ))}
            <button type="submit">Áp dụng bộ lọc</button>
          </form>
          {visible?.error && <p role="alert">{visible.error}</p>}
          <p aria-label="Bộ lọc nhật ký đã áp dụng">
            Bộ lọc đã áp dụng:{" "}
            {[...new URLSearchParams(query)]
              .filter(([k]) => k !== "page" && k !== "pageSize")
              .map(([k, v]) => `${k}=${v}`)
              .join("; ") || "Tất cả nhật ký được phép xem"}
          </p>
          {!visible && <p role="status">Đang tải lịch sử thay đổi…</p>}
          <button
            onClick={() => {
              setRetry((n) => n + 1);
            }}
          >
            Tải lại
          </button>
          {list && (
            <>
              <p>{list.totalCount} bản ghi</p>
              {list.items.length === 0 ? (
                <p role="status">
                  MSG10: Không có nhật ký phù hợp. Đổi bộ lọc để tiếp tục.
                </p>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>Thời gian (UTC)</th>
                      <th>Người thực hiện</th>
                      <th>Thao tác</th>
                      <th>Hồ sơ</th>
                      <th>Tóm tắt / thay đổi đã ghi nhận</th>
                      <th>Chi tiết</th>
                    </tr>
                  </thead>
                  <tbody>
                    {list.items.map((row) => (
                      <tr key={row.id}>
                        <td>{row.occurredAt}</td>
                        <td>{row.actor ?? "Hệ thống"}</td>
                        <td>{row.action}</td>
                        <td>
                          {row.entityType} / {row.entityId}
                        </td>
                        <td>
                          {row.action}:{" "}
                          {row.changedFields?.length
                            ? row.changedFields.join(", ")
                            : "Tên trường chưa được ghi nhận hoặc không được hiển thị"}
                        </td>
                        <td>
                          <button onClick={() => setId(row.id)}>
                            Xem bản ghi {row.id}
                          </button>
                          <button onClick={() => history(row)}>
                            Xem lịch sử hồ sơ {row.id}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <button
                disabled={list.page <= 1}
                onClick={() => page(list.page - 1)}
              >
                Trước
              </button>
              <span>Trang {list.page}</span>
              <button
                disabled={list.page * list.pageSize >= list.totalCount}
                onClick={() => page(list.page + 1)}
              >
                Tiếp
              </button>
            </>
          )}
          {id !== null && (
            <button onClick={() => setId(null)}>Quay lại danh sách</button>
          )}
          {detail && (
            <section aria-label="Bản ghi thay đổi">
              <h2>
                {detail.entry.action}: {detail.entry.entityType} /{" "}
                {detail.entry.entityId}
              </h2>
              <p>
                {detail.entry.occurredAt} — {detail.entry.actor ?? "Hệ thống"}
              </p>
              <p>Trường nhạy cảm và chưa hỗ trợ được ẩn.</p>
              <p>{detail.provenance}</p>
              {detail.retention && <section aria-label="Lưu giữ bằng chứng">
                <h3>Lưu giữ bằng chứng</h3>
                <dl>
                  <dt>Trạng thái lưu trữ</dt><dd>{detail.retention.archiveStatus}</dd>
                  <dt>Lưu trữ lúc (UTC)</dt><dd>{detail.retention.archivedAt ?? "Chưa lưu trữ"}</dd>
                  <dt>Thời hạn lưu giữ tối thiểu đến (UTC)</dt><dd>{detail.retention.retentionUntil ?? "Không áp dụng"}</dd>
                  <dt>Người lưu trữ</dt><dd>{detail.retention.archivedBy ?? "Không áp dụng"}</dd>
                  <dt>Lưu giữ theo yêu cầu pháp lý</dt><dd>{detail.retention.legalHold
                    ? detail.retention.legalHoldUntil ? `Đến ${detail.retention.legalHoldUntil} (UTC)` : "Không thời hạn"
                    : "Chưa có lệnh giữ bổ sung được ghi nhận"}</dd>
                  <dt>Điều kiện xóa dữ liệu</dt><dd>{detail.retention.purgeEligibility}: {detail.retention.reason}</dd>
                </dl>
                <p>Đến hạn lưu giữ không tự cho phép xóa. Nhật ký và biên nhận vẫn được giữ.</p>
              </section>}
              <button onClick={() => history(detail.entry)}>
                Xem lịch sử hồ sơ này
              </button>
              <h3>Các trường đã thay đổi</h3>
              {changes.length === 0 ? (
                <p>
                  Không có thay đổi có thể đối chiếu. Không tái dựng giá trị thiếu hoặc bị ẩn.
                </p>
              ) : (
                <table aria-label="Thay đổi các trường">
                  <thead>
                    <tr>
                      <th>Trường</th>
                      <th>Trước</th>
                      <th>Sau</th>
                    </tr>
                  </thead>
                  <tbody>
                    {changes.map((change) => (
                      <tr key={change.field}>
                        <td>{change.field}</td>
                        <td>
                          {auditValue(change.before, change.beforeRecorded)}
                        </td>
                        <td>
                          {auditValue(change.after, change.afterRecorded)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <h3>Trước</h3>
              <pre>{JSON.stringify(detail.before, null, 2)}</pre>
              <h3>Sau</h3>
              <pre>{JSON.stringify(detail.after, null, 2)}</pre>
            </section>
          )}
        </>
      )}
    </main>
  );
}
