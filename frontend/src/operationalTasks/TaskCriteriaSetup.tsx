import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { initializeTaskProgress, TaskProgressError } from "./progress";

export function TaskCriteriaSetup({
  branch,
  task,
  owner,
  onInitialized,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onInitialized: () => void;
}) {
  const { actions } = useAuth();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([{ label: "", required: true }]);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const active = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  useEffect(() => () => active.current?.abort(), []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (inFlight.current || locked || saved) return;
    inFlight.current = true;
    setBusy(true);
    setError("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await initializeTaskProgress(
        branch,
        task,
        items,
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setSaved(true);
        onInitialized();
      }
    } catch (e) {
      if (controller.signal.aborted) return;
      if (e instanceof TaskProgressError && e.status === 401)
        actions.invalidateSession(owner);
      setLocked(true);
      setError(
        e instanceof TaskProgressError && e.status === 403
          ? "Bạn không có quyền định nghĩa tiêu chí cho công việc này."
          : e instanceof TaskProgressError && e.status === 409
            ? "Tiêu chí đã tồn tại hoặc công việc chưa bắt đầu. Tiêu chí hiện có không bị thay thế."
            : "Chưa xác nhận thiết lập. Tải lại công việc trước khi thử lại; yêu cầu chưa được gửi lại.",
      );
    } finally {
      inFlight.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Thiết lập tiêu chí công việc">
      {!open ? (
        <button onClick={() => setOpen(true)}>
          Thiết lập tiêu chí hoàn tất
        </button>
      ) : (
        <>
          <h3>Thiết lập tiêu chí hoàn tất</h3>
          <p>
            Dành cho công việc đã bắt đầu nhưng chưa có tiêu chí. Tiêu chí cố định sau khi thiết lập. Quyền quản lý công việc được kiểm tra khi lưu.
          </p>
          {error && <p role="alert">{error}</p>}
          {saved ? (
            <p role="status">Đã khởi tạo tiêu chí hoàn tất.</p>
          ) : (
            <form onSubmit={submit}>
              <fieldset disabled={busy || locked}>
                <legend>Định nghĩa tiêu chí</legend>
                {items.map((item, index) => (
                  <div key={index}>
                    <label>
                      Tiêu chí {index + 1}
                      <input
                        required
                        maxLength={250}
                        value={item.label}
                        onChange={(e) =>
                          setItems((rows) =>
                            rows.map((r, n) =>
                              n === index ? { ...r, label: e.target.value } : r,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        checked={item.required}
                        onChange={(e) =>
                          setItems((rows) =>
                            rows.map((r, n) =>
                              n === index
                                ? { ...r, required: e.target.checked }
                                : r,
                            ),
                          )
                        }
                      />
                      Tiêu chí bắt buộc {index + 1}
                    </label>
                    <button
                      type="button"
                      disabled={items.length === 1}
                      onClick={() =>
                        setItems((rows) => rows.filter((_, n) => n !== index))
                      }
                    >
                      Xóa tiêu chí {index + 1}
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  disabled={items.length >= 100}
                  onClick={() =>
                    setItems((rows) => [...rows, { label: "", required: true }])
                  }
                >
                  Thêm tiêu chí
                </button>
                <label>
                  Lý do thiết lập
                  <input
                    required
                    minLength={10}
                    maxLength={500}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button>Khởi tạo tiêu chí</button>
              </fieldset>
            </form>
          )}
        </>
      )}
    </section>
  );
}
