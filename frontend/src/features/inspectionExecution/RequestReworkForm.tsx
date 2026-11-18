import { useEffect, useRef, useState } from "react";
import {
  createInspectionApi,
  InspectionApiError,
  type ReworkOptions,
} from "./api";

export function RequestReworkForm({
  branch,
  id,
  onDenied,
  onSessionInvalid,
}: {
  branch: number;
  id: number;
  onDenied: (status: 401 | 403) => void;
  onSessionInvalid?: () => void;
}) {
  const [options, setOptions] = useState<ReworkOptions | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [assignee, setAssignee] = useState("");
  const [due, setDue] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [receipt, setReceipt] = useState<{
    id: number;
    notification: string;
  } | null>(null);
  const [reload, setReload] = useState(0);
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  function failed(e: unknown) {
    if (
      e instanceof InspectionApiError &&
      (e.status === 401 || e.status === 403)
    )
      onDenied(e.status);
    setError(
      e instanceof InspectionApiError && e.uncertain
        ? "Chưa xác định kết quả ghi. Tải lại lựa chọn để xác minh; không tự gửi lại."
        : e instanceof Error
          ? e.message
          : "Làm lại chưa khả dụng",
    );
    setOptions(null);
  }
  useEffect(() => {
    const scope = new AbortController();
    lifetime.current = scope;
    setOptions(null);
    setReceipt(null);
    setError("");
    setAssignee("");
    setBusy(true);
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    void api
      .reworkOptions(branch, id)
      .then((value) => {
        if (scope.signal.aborted) return;
        setOptions(value);
        setSelected(value.defects.map((d) => d.id));
      })
      .catch((e) => {
        if (!scope.signal.aborted) failed(e);
      })
      .finally(() => {
        if (!scope.signal.aborted) setBusy(false);
      });
    return () => {
      scope.abort();
      if (lifetime.current === scope) lifetime.current = null;
    };
    // Identity is keyed by the parent. Callback changes cannot replay requests.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, id, reload]);
  async function save() {
    const scope = lifetime.current;
    if (
      !scope ||
      scope.signal.aborted ||
      busy ||
      writing.current ||
      !options ||
      receipt
    )
      return;
    writing.current = true;
    setBusy(true);
    setError("");
    try {
      const api = createInspectionApi({
        signal: scope.signal,
        onSessionInvalid,
      });
      const result = await api.requestRework(
        branch,
        id,
        options,
        Number(assignee),
        selected,
        due,
        reason,
      );
      if (!scope.signal.aborted) setReceipt(result);
    } catch (e) {
      if (!scope.signal.aborted) failed(e);
    } finally {
      writing.current = false;
      if (!scope.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Yêu cầu vệ sinh lại">
      <h3>Yêu cầu vệ sinh lại</h3>
      {error && <p role="alert">{error}</p>}
      {busy && <p role="status">Đang tải hoặc lưu kết quả làm lại...</p>}
      <button
        type="button"
        disabled={busy}
        onClick={() => setReload((v) => v + 1)}
      >
        Tải lại lựa chọn làm lại
      </button>
      {receipt ? (
        <p role="status">
          Làm lại #{receipt.id} đang chờ tiếp nhận.{" "}
          {receipt.notification === "PENDING_DELIVERY"
            ? "Thông báo cho người được giao đang chờ gửi."
            : receipt.notification === "DELIVERED"
              ? "Đã thông báo cho người được giao."
              : "Chưa xác nhận thông báo đã đến người được giao."}
        </p>
      ) : options?.existingReworkId ? (
        <p role="alert">
          MSG03: Mở làm lại #{options.existingReworkId} đã tồn tại.
        </p>
      ) : (
        options && (
          <fieldset disabled={busy}>
            <legend>Lỗi, người được giao và thời hạn</legend>
            {options.defects.map((d) => (
              <label key={d.id}>
                <input
                  type="checkbox"
                  checked={selected.includes(d.id)}
                  onChange={(e) =>
                    setSelected((ids) =>
                      e.target.checked
                        ? [...ids, d.id]
                        : ids.filter((v) => v !== d.id),
                    )
                  }
                />
                {d.description}
              </label>
            ))}
            <label>
              Nhân viên buồng phòng
              <select
                value={assignee}
                onChange={(e) => setAssignee(e.target.value)}
              >
                <option value="">Chọn nhân viên buồng phòng đủ điều kiện</option>
                {options.assignees.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                    {a.originalCleaner ? "(nhân viên vệ sinh ban đầu)" : ""}
                  </option>
                ))}
              </select>
            </label>
            {!options.assignees.length && (
              <p role="status">
                Hiện không có nhân viên buồng phòng đủ điều kiện.
              </p>
            )}
            <label>
              Thời hạn làm lại
              <input
                type="datetime-local"
                value={due}
                onChange={(e) => setDue(e.target.value)}
              />
            </label>
            <label>
              Lý do làm lại
              <input
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button
              type="button"
              disabled={!assignee || !selected.length || !due || !reason.trim()}
              onClick={() => void save()}
            >
              Lưu yêu cầu làm lại
            </button>
          </fieldset>
        )
      )}
    </section>
  );
}
