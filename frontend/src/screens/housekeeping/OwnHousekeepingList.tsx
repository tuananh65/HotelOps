import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getJson } from "../../api/dev1/client";
import { LoadingIndicator } from "../../components/LoadingIndicator";
import { useRoomWorkspace, roomLabel } from "./RoomWorkspace";
type Item = {
  id: number;
  branchId: number;
  roomNumber: string;
  taskNumber: string;
  recordedStatus: string;
  assignmentId?: number;
};
export default function OwnHousekeepingList({
  scope,
  kind,
}: {
  scope: ReturnType<typeof useRoomWorkspace>;
  kind?: "rework";
}) {
  const [params, setParams] = useSearchParams();
  const branch = scope.branch?.id,
    owner = scope.owner,
    before = params.get("before") ?? "";
  const key = JSON.stringify([branch, before, owner, kind]);
  const [retry, setRetry] = useState(0),
    [result, setResult] = useState<{
      key: string;
      items?: Item[];
      next?: number | null;
      error?: string;
    } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    if (branch && owner && scope.branch?.canReadOwn)
      void getJson(
        `/branches/${branch}/my-${kind === "rework" ? "rework" : "cleaning"}-assignments${before ? `?before=${encodeURIComponent(before)}` : ""}`,
        abort.signal,
      )
        .then((v) => {
          const d = v as { items: Item[]; nextCursor: number | null };
          if (
            !d ||
            !Array.isArray(d.items) ||
            d.items.some(
              (r) =>
                r.branchId !== branch ||
                !Number.isSafeInteger(r.id) ||
                r.id <= 0 ||
                [r.roomNumber, r.taskNumber, r.recordedStatus].some(
                  (x) => typeof x !== "string",
                ) ||
                (!kind &&
                  (!Number.isSafeInteger(r.assignmentId) ||
                    r.assignmentId! <= 0)),
            ) ||
            !(
              d.nextCursor === null ||
              (Number.isSafeInteger(d.nextCursor) && d.nextCursor > 0)
            )
          )
            throw new Error();
          if (!abort.signal.aborted)
            setResult({ key, items: d.items, next: d.nextCursor });
        })
        .catch(() => {
          if (!abort.signal.aborted)
            setResult({
              key,
              error: "Chưa tải được công việc được giao. Vui lòng thử lại.",
            });
        });
    return () => abort.abort();
  }, [branch, owner, before, kind, key, retry, scope.branch?.canReadOwn]);
  const visible = result?.key === key ? result : null;
  const page = (cursor?: number) => {
    const next = new URLSearchParams(params);
    next.delete("before");
    if (cursor) next.set("before", String(cursor));
    setParams(next);
  };
  if (!scope.branch?.canReadOwn)
    return (
      <p className="room-empty">
        Bạn chưa có quyền xem công việc cá nhân tại chi nhánh này.
      </p>
    );
  return (
    <section>
      <p className="room-note">
        Chỉ hiển thị các công việc {kind ? "làm lại " : ""}được giao cho bạn.
        Chọn mã công việc để xem chi tiết.
      </p>
      <button onClick={() => setRetry((v) => v + 1)}>Tải lại công việc</button>
      {visible?.error ? (
        <p role="alert">{visible.error}</p>
      ) : !visible?.items ? (
        <LoadingIndicator label="Đang tải công việc" />
      ) : !visible.items.length ? (
        <p className="room-empty">
          Bạn chưa có công việc {kind ? "làm lại " : ""}được giao trong danh
          sách này.
        </p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Công việc</th>
                <th>Phòng</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {visible.items.map((r) => (
                <tr key={r.assignmentId ?? r.id}>
                  <td>
                    <Link
                      to={
                        kind
                          ? `/rework/${r.id}?branchId=${branch}`
                          : `/cleaning/tasks/${r.id}?branchId=${branch}&assignmentId=${r.assignmentId}`
                      }
                    >
                      {r.taskNumber}
                      {kind ? ` · Làm lại ${r.id}` : ""}
                    </Link>
                  </td>
                  <td>{r.roomNumber}</td>
                  <td>{roomLabel(r.recordedStatus)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="room-pagination">
        {before && <button onClick={() => page()}>Về đầu danh sách</button>}
        {visible?.next && (
          <button onClick={() => page(visible.next!)}>
            Xem thêm công việc
          </button>
        )}
      </div>
    </section>
  );
}
