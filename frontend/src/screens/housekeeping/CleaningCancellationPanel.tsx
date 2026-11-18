import { useEffect, useRef, useState } from "react";
import { ApiError } from "../../api/dev1/client";
import { Link } from "react-router-dom";
import {
  cancelCleaning,
  cancellationCommand,
  readCancellation,
  type CancellationOptions,
} from "../../api/dev1/cleaningCancellation";

export default function CleaningCancellationPanel({
  branch,
  task,
  onCancelled,
}: {
  branch: string;
  task: string;
  onCancelled: () => void;
}) {
  const mounted = useRef(false);
  const [options, setOptions] = useState<CancellationOptions | null>(null);
  const [confirmed, setConfirmed] = useState(false),
    [reason, setReason] = useState("");
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [done, setDone] = useState(false);
  const [reload, setReload] = useState(0);
  const pending = useRef<{
    key: string;
    reason: string;
    options: CancellationOptions;
  } | null>(null);
  useEffect(() => {
    mounted.current = true;
    const abort = new AbortController();
    setOptions(null);
    setDone(false);
    setMessage("");
    pending.current = null;
    setConfirmed(false);
    setReason("");
    readCancellation(branch, task, abort.signal)
      .then((d) => {
        if (!abort.signal.aborted) setOptions(d);
      })
      .catch((e) => {
        if (!abort.signal.aborted && e instanceof ApiError && e.status !== 403)
          setMessage("Chưa thể xác minh quyền hoặc điều kiện hủy.");
      });
    return () => { mounted.current = false; abort.abort(); };
  }, [branch, task, reload]);
  if (!options && !message) return null;
  const retry = pending.current;
  let valid = false;
  try {
    if (options) {
      cancellationCommand(options, confirmed, reason);
      valid = true;
    }
  } catch {
    /* form incomplete */
  }
  async function submit() {
    if (busy || done || !options || (!retry && !valid)) return;
    const command = pending.current ?? {
      key: crypto.randomUUID(),
      reason: reason.trim(),
      options,
    };
    pending.current = command;
    setBusy(true);
    setMessage("");
    try {
      await cancelCleaning(command.options, true, command.reason, command.key);
      if (!mounted.current) return;
      setDone(true);
      pending.current = null;
      onCancelled();
    } catch (error) {
      if (!mounted.current) return;
      if (
        error instanceof ApiError &&
        [400, 401, 403, 404, 409, 412, 428].includes(error.status)
      ) {
        pending.current = null;
        setOptions(null);
        setMessage(
          "Yêu cầu bị từ chối hoặc dữ liệu đã thay đổi. Tải lại chi tiết trước khi thử lại.",
        );
      } else
        setMessage(
          "Chưa xác nhận kết quả. Thử lại cùng yêu cầu để kiểm tra; không sửa lý do.",
        );
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  return (
    <section aria-label="Hủy công việc vệ sinh">
      <h2>Hủy công việc vệ sinh</h2>
      {message && <p role="status">{message}</p>}
      {!options && message && !busy && !pending.current && (
        <button type="button" onClick={() => setReload(v => v + 1)}>Tải lại điều kiện hủy</button>
      )}
      {options?.capability === "UNAVAILABLE" && (
        options.blocker === "TASK_ALREADY_STARTED" || options.blocker === "TASK_HAS_EXECUTION_HISTORY" ? (
          <p role="status">MSG24: Công việc đã bắt đầu hoặc có lịch sử thực hiện nên không thể hủy. Bạn có thể xem phương án phân công lại.{" "}
            <Link to={`/cleaning/tasks/${encodeURIComponent(task)}?branchId=${encodeURIComponent(branch)}&execution=1`}>
              Phân công lại
            </Link>
          </p>
        ) : <p>Chưa đủ điều kiện hủy công việc.</p>
      )}
      {options?.capability === "PROVISIONAL_V1" && !done && (
        <>
          <label>
            Lý do hủy
            <textarea
              aria-label="Lý do hủy"
              minLength={10}
              maxLength={500}
              aria-describedby="cleaning-cancellation-reason-policy"
              value={reason}
              disabled={busy || !!retry}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          <p id="cleaning-cancellation-reason-policy">Lý do: 10–500 ký tự, không tính khoảng trắng đầu và cuối.</p>
          <p>MSG33: Xác nhận hủy công việc vệ sinh này?</p>
          <label>
            <input
              type="checkbox"
              checked={confirmed}
              disabled={busy || !!retry}
              onChange={(e) => setConfirmed(e.target.checked)}
            />
            Xác nhận hủy công việc
          </label>
          <button disabled={busy || (!valid && !retry)} onClick={submit}>
            {retry ? "Kiểm tra lại yêu cầu hủy" : "Hủy công việc"}
          </button>
        </>
      )}
    </section>
  );
}
