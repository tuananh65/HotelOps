import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { uploadFile } from "../api/dev1/cleaningProgress";
import { submitTask } from "./submission";
import {
  readTaskProgress,
  enterTask,
  declineTask,
  resumeTask,
  pauseTask,
  readPauseReasons,
  type PauseReason,
  saveTaskProgress,
  TaskProgressError,
  type TaskProgressSnapshot,
  type Criterion,
} from "./progress";

// Parent remounts this editor for each admitted authority and task identity.
export function TaskProgressPanel({
  branch,
  task,
  owner,
  onDeclined,
  onChanged,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onDeclined?: () => void;
  onChanged?: (message: string) => void;
}) {
  const { actions } = useAuth();
  const [current, setCurrent] = useState<TaskProgressSnapshot | null>(null);
  const [reasons, setReasons] = useState<PauseReason[]>([]);
  const [reasonId, setReasonId] = useState(0);
  const [pauseNote, setPauseNote] = useState("");
  const [percent, setPercent] = useState(0);
  const [items, setItems] = useState<Criterion[]>([]);
  const [note, setNote] = useState("");
  const [declineReason, setDeclineReason] = useState("");
  const [completing, setCompleting] = useState(false);
  const [resultNote, setResultNote] = useState("");
  const [handoverNote, setHandoverNote] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(true);
  const [locked, setLocked] = useState(true);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  const active = useRef<AbortController | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const saving = useRef(false);
  function show(value: TaskProgressSnapshot) {
    setCurrent(value);
    setPercent(value.data.percent);
    setItems(value.data.items);
    setNote("");
    setDeclineReason("");
    setCompleting(false);
    setResultNote("");
    setHandoverNote("");
    setFiles([]);
    if (fileInput.current) fileInput.current.value = "";
  }
  function failure(reason: unknown) {
    if (reason instanceof TaskProgressError && reason.status === 401)
      actions.invalidateSession(owner);
    setError(
      reason instanceof TaskProgressError && reason.status === 403
        ? "Chỉ người thực hiện đang được giao và có quyền mới được cập nhật công việc này."
        : reason instanceof TaskProgressError && reason.status === 412
          ? "Công việc đã đổi. Tải lại và kiểm tra trước khi lưu tiếp."
          : "Chưa xác nhận tiến độ. Tải lại trước khi lưu tiếp.",
    );
    setLocked(true);
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setLocked(true);
    setCurrent(null);
    setError("");
    setMessage("");
    void readTaskProgress(branch, task, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          show(value);
          setLocked(false);
        }
      })
      .catch((reason) => {
        if (!controller.signal.aborted) failure(reason);
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, reload, actions]);
  async function save(event: FormEvent) {
    event.preventDefault();
    if (
      !current ||
      !["IN_PROGRESS", "PAUSED"].includes(current.data.state) ||
      busy ||
      locked ||
      saving.current
    )
      return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    let evidencePrepared = false;
    try {
      if (files.length > 5) throw new Error("Vượt số lượng tệp cho phép");
      const photos = await Promise.all(files.map(uploadFile));
      controller.signal.throwIfAborted();
      evidencePrepared = true;
      const updated = await saveTaskProgress(
        branch,
        task,
        current,
        {
          percent,
          note: note.trim() || null,
          items: items
            .filter((i) => i.outcome !== null)
            .map((i) => ({
              id: i.id,
              outcome: i.outcome!,
              note: i.note?.trim() || null,
            })),
          photos,
        },
        controller.signal,
      );
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          "Đã lưu tiến độ. Đạt 100% không tự hoàn tất công việc.",
        );
        onChanged?.("Đã lưu tiến độ. Đạt 100% không tự hoàn tất công việc.");
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        if (!evidencePrepared || reason instanceof TaskProgressError && reason.status === 400 && reason.messageCode === "MSG23")
          setError("MSG23: Chọn tối đa 5 tệp PNG, JPEG hoặc PDF có thể đọc, mỗi tệp tối đa 10 MiB rồi lưu lại. Chỉnh sửa được giữ nguyên.");
        else failure(reason);
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function decline() {
    if (
      !current ||
      current.data.state !== "ASSIGNED" ||
      busy ||
      locked ||
      saving.current
    )
      return;
    if (!declineReason.trim()) {
      setError("Vui lòng nhập lý do từ chối nhận việc.");
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await declineTask(
        branch,
        task,
        current,
        declineReason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setCurrent(null);
        setItems([]);
        setDeclineReason("");
        setLocked(true);
        setMessage("MSG19: Đã từ chối công việc và trả về chờ phân công.");
        onDeclined?.();
      }
    } catch (reason) {
      if (!controller.signal.aborted) failure(reason);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function pause(loadOnly = false) {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (loadOnly) {
        const values = await readPauseReasons(branch, task, controller.signal);
        if (!controller.signal.aborted) setReasons(values);
      } else {
        const selected = reasons.find((r) => r.id === reasonId);
        if (!selected || (selected.code === "OTHER" && !pauseNote.trim())) {
          setError("Chọn lý do tạm dừng; lựa chọn Khác cần ghi chú.");
          return;
        }
        const value = await pauseTask(
          branch,
          task,
          current,
          controller.signal,
          reasonId,
          pauseNote,
        );
        if (!controller.signal.aborted) {
          setCurrent(value);
          setReasonId(0);
          setPauseNote("");
          setMessage("Đã tạm dừng công việc và kết thúc khoảng làm việc.");
          onChanged?.("Đã tạm dừng công việc và kết thúc khoảng làm việc.");
        }
      }
    } catch (reason) {
      if (!controller.signal.aborted) failure(reason);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function resume() {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const updated = await resumeTask(
        branch,
        task,
        current,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setCurrent(updated);
        setMessage("Đã tiếp tục công việc. Bắt đầu ghi nhận thời gian làm việc.");
        onChanged?.("Đã tiếp tục công việc. Bắt đầu ghi nhận thời gian làm việc.");
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        failure(reason);
        if (reason instanceof TaskProgressError && reason.status === 409)
          setError(
            "Không tiếp tục được công việc. Cần ngày làm việc đang hoạt động và không có khoảng khác đang mở. Tải lại trạng thái.",
          );
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function enter(action: "accept" | "start") {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const updated = await enterTask(
        branch,
        task,
        current,
        action,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          action === "accept"
            ? "MSG08: Đã tiếp nhận công việc."
            : "Đã bắt đầu công việc. Đang ghi nhận thời gian làm việc.",
        );
        onChanged?.(action === "accept" ? "MSG08: Đã tiếp nhận công việc. Bắt đầu khi sẵn sàng." : "Đã bắt đầu công việc. Đang ghi nhận thời gian làm việc.");
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        failure(reason);
        if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG30"
        )
          setError(
            "MSG30: Bắt đầu ngày làm việc trước khi bắt đầu công việc này. Tải lại để thử tiếp.",
          );
        else if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG31"
        )
          setError(
            "MSG31: Tạm dừng hoặc hoàn tất công việc hiện tại trước khi bắt đầu công việc này. Tải lại để thử tiếp.",
          );
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function complete() {
    if (
      !current ||
      current.data.state !== "IN_PROGRESS" ||
      busy ||
      locked ||
      saving.current
    )
      return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    let evidencePrepared = false;
    try {
      if (files.length > 5) throw new Error("Vượt số lượng tệp bằng chứng cho phép");
      const photos = await Promise.all(files.map(uploadFile));
      controller.signal.throwIfAborted();
      evidencePrepared = true;
      await submitTask(
        branch,
        task,
        current,
        {
          items: items
            .filter((i) => i.outcome !== null)
            .map((i) => ({
              id: i.id,
              outcome: i.outcome!,
              note: i.note?.trim() || null,
            })),
          resultNote: resultNote.trim() || null,
          handoverNote: handoverNote.trim() || null,
          photos,
        },
        controller.signal,
      );
      if (!controller.signal.aborted && onChanged) {
        setLocked(true);
        setFiles([]);
        onChanged("MSG09: Đã nộp công việc để xác minh và kết thúc khoảng làm việc.");
        return;
      }
      const updated = await readTaskProgress(branch, task, controller.signal);
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          "MSG09: Đã nộp công việc để xác minh và kết thúc khoảng làm việc.",
        );
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        if (!evidencePrepared)
          setError("MSG23: Chọn tối đa 5 tệp PNG, JPEG hoặc PDF có thể đọc, mỗi tệp tối đa 10 MiB rồi gửi lại. Chỉnh sửa được giữ nguyên.");
        else if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG03"
        )
          setError(
            `MSG03: Ghi kết quả cho các tiêu chí bắt buộc: ${reason.missingCriteria.join(", ")}.`,
          );
        else if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG23"
        )
          setError(
            "MSG23: Bằng chứng phải là PNG, JPEG hoặc PDF, tối đa 10 MB mỗi tệp.",
          );
        else failure(reason);
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Cập nhật tiến độ công việc">
      <h3>Cập nhật tiến độ</h3>
      <button disabled={busy} onClick={() => setReload((n) => n + 1)}>
        Tải lại tiến độ (bỏ chỉnh sửa)
      </button>
      {busy && <p role="status">Đang tải hoặc lưu tiến độ…</p>}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {current?.data.state === "PENDING_VERIFICATION" && (
        <p>Đang chờ trưởng bộ phận xác minh. Tiến độ đã khóa.</p>
      )}
      {current?.data.state === "IN_PROGRESS" && (
        <section aria-label="Hoàn tất công việc">
          <button disabled={busy || locked} onClick={() => setCompleting(true)}>
            Hoàn tất công việc
          </button>
          {completing && (
            <>
              <p>
                Kiểm tra kết quả tiêu chí và bằng chứng bên dưới trước khi gửi.
              </p>
              <label>
                Ghi chú kết quả
                <textarea
                  maxLength={4000}
                  disabled={busy || locked}
                  value={resultNote}
                  onChange={(e) => setResultNote(e.target.value)}
                />
              </label>
              <label>
                Ghi chú bàn giao
                <textarea
                  maxLength={4000}
                  disabled={busy || locked}
                  value={handoverNote}
                  onChange={(e) => setHandoverNote(e.target.value)}
                />
              </label>
              <button disabled={busy || locked} onClick={() => void complete()}>
                Gửi hoàn tất
              </button>
            </>
          )}
        </section>
      )}
      {current?.data.state === "ASSIGNED" && (
        <section aria-label="Kết quả phân công">
          <button
            disabled={busy || locked}
            onClick={() => void enter("accept")}
          >
            Tiếp nhận công việc
          </button>
          <label>
            Lý do từ chối nhận việc
            <textarea
              maxLength={4000}
              disabled={busy || locked}
              value={declineReason}
              onChange={(e) => setDeclineReason(e.target.value)}
            />
          </label>
          <button
            disabled={busy || locked || !declineReason.trim()}
            onClick={() => void decline()}
          >
            Từ chối nhận công việc
          </button>
        </section>
      )}
      {current?.data.state === "ACCEPTED" && (
        <button disabled={busy || locked} onClick={() => void enter("start")}>
          Bắt đầu công việc
        </button>
      )}
      {current?.data.state === "IN_PROGRESS" && (
        <section aria-label="Thao tác tạm dừng công việc">
          <button disabled={busy || locked} onClick={() => void pause(true)}>
            Tải lý do tạm dừng
          </button>
          <label>
            Lý do tạm dừng
            <select
              aria-label="Lý do tạm dừng"
              disabled={busy || locked}
              value={reasonId}
              onChange={(e) => setReasonId(Number(e.target.value))}
            >
              <option value={0}>Chọn lý do</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Ghi chú tạm dừng
            <textarea
              maxLength={1000}
              disabled={busy || locked}
              value={pauseNote}
              onChange={(e) => setPauseNote(e.target.value)}
            />
          </label>
          <p>Lựa chọn Khác cần ghi chú, tối đa 1000 ký tự.</p>
          <button
            disabled={busy || locked || !reasonId}
            onClick={() => void pause()}
          >
            Tạm dừng công việc
          </button>
        </section>
      )}
      {current?.data.state === "PAUSED" && (
        <button disabled={busy || locked} onClick={() => void resume()}>
          Tiếp tục công việc
        </button>
      )}
      {current && (
        <section aria-label="Bằng chứng công việc đã lưu">
          <h4>Bằng chứng đã lưu</h4>
          {current.data.evidence.length === 0 ? (
            <p>Chưa tải bằng chứng lên.</p>
          ) : (
            <ul>
              {current.data.evidence.map((file) => (
                <li key={file.id}>
                  <a href={file.downloadPath} download={file.name}>
                    {file.name}
                  </a>
                  {file.bytes !== null && <> — {file.bytes} bytes</>}
                  <time dateTime={file.uploadedAt}> — {file.uploadedAt}</time>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
      {current && (
        <form onSubmit={save}>
          <p>Trạng thái thực hiện công việc: {displayCode(current.data.state)}</p>
          <fieldset
            disabled={
              busy ||
              locked ||
              !["IN_PROGRESS", "PAUSED"].includes(current.data.state)
            }
          >
            <legend>Tiến độ và tiêu chí hoàn tất</legend>
            <label>
              Tiến độ (%)
              <input
                type="number"
                required
                min={0}
                max={100}
                step={1}
                value={Number.isNaN(percent) ? "" : percent}
                onChange={(e) =>
                  setPercent(
                    e.target.value === "" ? NaN : Number(e.target.value),
                  )
                }
              />
            </label>
            {items.length === 0 && (
              <p>Chưa cấu hình tiêu chí hoàn tất.</p>
            )}
            {items.map((item, index) => (
              <div key={item.id}>
                <label>
                  {item.label}
                  {item.required ? "(bắt buộc để hoàn tất)" : ""}
                  <select
                    aria-label={`Kết quả tiêu chí: ${item.label}`}
                    value={item.outcome ?? ""}
                    onChange={(e) =>
                      setItems((rows) =>
                        rows.map((r, n) =>
                          n === index
                            ? {
                                ...r,
                                outcome: e.target.value as "PASS" | "FAIL",
                              }
                            : r,
                        ),
                      )
                    }
                  >
                    <option value="" disabled>
                      Chưa ghi nhận
                    </option>
                    <option value="PASS">Đạt</option>
                    <option value="FAIL">Không đạt</option>
                  </select>
                </label>
                <label>
                  Ghi chú tiêu chí: {item.label}
                  <input
                    maxLength={1000}
                    disabled={item.outcome === null}
                    value={item.note ?? ""}
                    onChange={(e) =>
                      setItems((rows) =>
                        rows.map((r, n) =>
                          n === index ? { ...r, note: e.target.value } : r,
                        ),
                      )
                    }
                  />
                </label>
              </div>
            ))}
            <label>
              Ghi chú tiến độ
              <input
                maxLength={4000}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </label>
            <label>
              Tệp bằng chứng (tối đa 5 tệp PNG/JPEG/PDF, 10 MB mỗi tệp)
              <input
                ref={fileInput}
                type="file"
                multiple
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
              />
            </label>
            <button>Lưu tiến độ</button>
          </fieldset>
        </form>
      )}
    </section>
  );
}
