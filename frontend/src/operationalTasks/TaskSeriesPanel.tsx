import { userMessage } from "../i18n/userMessage";
import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  readSeries,
  stopSeries,
  reactivateSeries,
  type SeriesPage,
  type TaskSeries,
} from "./series";

export function TaskSeriesPanel({
  branch,
  owner,
  seriesId,
  taskId,
}: {
  branch: number;
  owner: AuthorityGeneration;
  seriesId?: number;
  taskId?: number;
}) {
  const { actions } = useAuth();
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<SeriesPage | null>(null);
  const [selected, setSelected] = useState<TaskSeries | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [hidden, setHidden] = useState(false);
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setData(null);
    setSelected(null);
    setConfirmed(false);
    setReason("");
    setError("");
    setHidden(false);
    setUncertain(false);
    void readSeries(branch, page, controller.signal, seriesId)
      .then((result) => {
        if (!controller.signal.aborted) setData(result);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (
          error instanceof TaskProgressError &&
          [403, 503].includes(error.status)
        )
          setHidden(true);
        else setError("Không tải được công việc định kỳ. Tải lại để thử tiếp.");
      });
    return () => controller.abort();
  }, [branch, owner, actions, page, revision, seriesId]);
  useEffect(() => () => active.current?.abort(), []);
  async function stop() {
    if (!selected || !confirmed || saving.current || busy || uncertain) return;
    saving.current = true;
    setBusy(true);
    setError("");
    setMessage("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await (selected.isActive ? stopSeries : reactivateSeries)(
        branch,
        selected,
        reason.trim() || null,
        controller.signal,
        taskId,
      );
      if (!controller.signal.aborted) {
        setMessage(
          selected.isActive
            ? "MSG08: Đã dừng chuỗi. Công việc đã tạo giữ nguyên."
            : "MSG08: Đã kích hoạt lại chuỗi, không tạo bù lần bị bỏ qua.",
        );
        setRevision((n) => n + 1);
      }
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof TaskProgressError && error.status === 401)
        actions.invalidateSession(owner);
      setUncertain(true);
      setError(
        "Chưa xác nhận dừng hoặc chuỗi đã đổi. Tải lại chuỗi trước mọi thao tác tiếp theo.",
      );
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <section aria-label="Chuỗi công việc định kỳ">
      <h2>Chuỗi công việc định kỳ</h2>
      <button disabled={busy} onClick={() => setRevision((n) => n + 1)}>
        Tải lại chuỗi định kỳ
      </button>
      {message && <p role="status">{userMessage(message)}</p>}
      {error && <p role="alert">{userMessage(error)}</p>}
      {data && (
        <>
          <p>{data.totalCount} Chuỗi định kỳ</p>
          <ul>
            {data.items.map((series) => (
              <li key={series.seriesId}>
                <strong>{series.title}</strong> —{" "}
                {series.isActive ? "Đang hoạt động" : "Ngừng hoạt động"};{" "}
                {series.rule}; Người thực hiện: {series.defaultAssigneeName};
                công việc đã tạo: {series.occurrenceCount}.
                <p>
                  Thời hạn đầu tiên: {series.firstDueAt}; Thời hạn tiếp theo:{" "}
                  {series.nextDueAt ?? "Lịch đã hết lần thực hiện"}; Ngày kết
                  thúc: {series.endDate ?? "Chưa có ngày kết thúc"}.
                </p>
                {(series.isActive || series.nextDueAt !== null) && (
                  <button
                    disabled={busy || uncertain}
                    onClick={() => {
                      setSelected(series);
                      setConfirmed(false);
                      setReason("");
                    }}
                  >
                    {series.isActive ? "Dừng" : "Kích hoạt lại"} Chuỗi định kỳ:{" "}
                    {series.title}
                  </button>
                )}
              </li>
            ))}
          </ul>
          <nav aria-label="Phân trang chuỗi định kỳ">
            <button
              disabled={busy || page === 1}
              onClick={() => setPage((n) => n - 1)}
            >
              Chuỗi định kỳ trước
            </button>
            <span>Trang chuỗi định kỳ {page}</span>
            <button
              disabled={busy || page * data.pageSize >= data.totalCount}
              onClick={() => setPage((n) => n + 1)}
            >
              Chuỗi định kỳ tiếp theo
            </button>
          </nav>
        </>
      )}
      {selected && (
        <ActionDialog
          open
          title="Thay đổi chuỗi công việc"
          onClose={() => setSelected(null)}
          busy={busy}
          locked={uncertain}
          error={message}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void stop();
            }}
          >
            <fieldset disabled={busy || uncertain}>
              <legend>
                {selected.isActive ? "Dừng" : "Kích hoạt lại"} {selected.title}
              </legend>
              <p>
                Công việc và lịch sử hiện có giữ nguyên. Kích hoạt lại cần quyền
                nguồn, yêu cầu liên quan đang mở và người thực hiện hợp lệ;
                không tạo bù kỳ đã bỏ qua.
              </p>
              <label>
                Lý do thay đổi
                <textarea
                  maxLength={4000}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <label>
                <input
                  type="checkbox"
                  required
                  checked={confirmed}
                  onChange={(e) => setConfirmed(e.target.checked)}
                />
                Xác nhận áp dụng thay đổi cho các lần tạo trong tương lai
              </label>
              <button
                data-dialog-action
                disabled={!confirmed || (!selected.isActive && !reason.trim())}
              >
                Xác nhận {selected.isActive ? "stop" : "reactivation"}
              </button>
              <button type="button" onClick={() => setSelected(null)}>
                Hủy thay đổi
              </button>
            </fieldset>
          </form>
        </ActionDialog>
      )}
    </section>
  );
}
