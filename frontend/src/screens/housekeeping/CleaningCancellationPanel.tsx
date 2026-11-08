import { useEffect, useRef, useState } from "react";
import { ApiError } from "../../api/dev1/client";
import {
  cancelCleaning,
  cancellationCommand,
  readCancellation,
  type CancellationOptions,
} from "../../api/dev1/cleaningCancellation";

export default function CleaningCancellationPanel({
  branch,
  task,
}: {
  branch: string;
  task: string;
}) {
  const [options, setOptions] = useState<CancellationOptions | null>(null);
  const [confirmed, setConfirmed] = useState(false),
    [reason, setReason] = useState("");
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [done, setDone] = useState(false);
  const pending = useRef<{
    key: string;
    reason: string;
    options: CancellationOptions;
  } | null>(null);
  useEffect(() => {
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
    return () => abort.abort();
  }, [branch, task]);
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
      setDone(true);
      pending.current = null;
      setMessage("Đã hủy công việc. Tải lại chi tiết để xem lịch sử mới.");
    } catch (error) {
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
      setBusy(false);
    }
  }
  return (
    <section aria-label="Hủy công việc vệ sinh">
      <h2>Hủy công việc vệ sinh</h2>
      {message && <p role="status">{message}</p>}
      {options?.capability === "UNAVAILABLE" && (
        <p>Chưa đủ điều kiện hủy công việc.</p>
      )}
      {options?.capability === "PROVISIONAL_V1" && !done && (
        <>
          <label>
            Lý do hủy
            <textarea
              aria-label="Lý do hủy"
              maxLength={500}
              value={reason}
              disabled={busy || !!retry}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
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
