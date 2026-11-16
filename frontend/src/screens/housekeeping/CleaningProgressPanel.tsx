import { useEffect, useRef, useState, type FormEvent } from "react";
import { ApiError } from "../../api/dev1/client";
import {
  readCleaningProgress,
  saveCleaningProgress,
  progressError,
  uploadFile,
  type ProgressSnapshot,
} from "../../api/dev1/cleaningProgress";
import { PageHeader } from "../../ui";

export default function CleaningProgressPanel({
  branch,
  task,
}: {
  branch: string;
  task: string;
}) {
  const [current, setCurrent] = useState<ProgressSnapshot | null>(null);
  const [percent, setPercent] = useState(0);
  const [items, setItems] = useState<
    { id: number; done: boolean; note: string | null }[]
  >([]);
  const [note, setNote] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [locked, setLocked] = useState(false);
  const [error, setError] = useState("");
  const [percentError, setPercentError] = useState("");
  const [fileError, setFileError] = useState("");
  const [success, setSuccess] = useState("");
  const [reload, setReload] = useState(0);
  const active = useRef<AbortController | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  function show(snapshot: ProgressSnapshot) {
    setCurrent(snapshot);
    setPercent(snapshot.data.percent);
    setItems(
      snapshot.data.items.map((item) => ({
        id: item.id,
        done: item.done === true,
        note: item.note,
      })),
    );
    setNote("");
    setFiles([]);
    setPercentError("");
    setFileError("");
    if (fileInput.current) fileInput.current.value = "";
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setLoading(true);
    setCurrent(null);
    setError("");
    setSuccess("");
    setLocked(false);
    readCleaningProgress(branch, task, controller.signal)
      .then((snapshot) => {
        if (!controller.signal.aborted) show(snapshot);
      })
      .catch((reason) => {
        if (!controller.signal.aborted) setError(progressError(reason));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, reload]);
  async function save(event: FormEvent) {
    event.preventDefault();
    if (!current || saving || locked) return;
    if (!Number.isInteger(percent) || percent < 0 || percent > 100) {
      setPercentError("MSG03: Tiến độ phải là số nguyên từ 0 đến 100.");
      return;
    }
    setPercentError("");
    setFileError("");
    const controller = new AbortController();
    active.current = controller;
    setSaving(true);
    setError("");
    setSuccess("");
    let saved = false;
    try {
      const photos = await (async () => {
        try {
          if (files.length > 5) throw new Error("INVALID_EVIDENCE");
          return await Promise.all(files.map(uploadFile));
        } catch (reason) {
          if (!controller.signal.aborted)
            setFileError("MSG23: Chọn tối đa 5 tệp JPG/PNG/PDF hợp lệ, tối đa 10 MiB mỗi tệp.");
          throw reason;
        }
      })();
      controller.signal.throwIfAborted();
      const updated = await saveCleaningProgress(
        branch,
        task,
        current,
        { percent, items, note: note.trim() || null, photos },
        controller.signal,
      );
      saved = true;
      if (controller.signal.aborted) return;
      show(updated);
      setSuccess(
        "Đã lưu tiến độ. Công việc vẫn đang thực hiện, kể cả khi đạt 100%.",
      );
      const refreshed = await readCleaningProgress(
        branch,
        task,
        controller.signal,
      );
      if (!controller.signal.aborted) show(refreshed);
    } catch (reason) {
      if (controller.signal.aborted) return;
      if (saved) {
        setError(
          "Đã lưu thành công nhưng chưa tải lại được dữ liệu. Tải lại trước khi lưu tiếp.",
        );
        setLocked(true);
      } else {
        setError(progressError(reason));
        if (
          reason instanceof ApiError &&
          (reason.status !== 400 || reason.code === "CSRF_VALIDATION_FAILED")
        )
          setLocked(true);
      }
    } finally {
      if (!controller.signal.aborted) setSaving(false);
    }
  }
  return (
    <>
      <PageHeader id={26} />
      <p>
        Tiến độ vệ sinh · Chi nhánh {branch} · Công việc {task}. Lưu tiến độ
        không hoàn tất công việc.
      </p>
      {loading && <p role="status">Đang tải tiến độ…</p>}
      {error && <p role="alert">{error}</p>}
      {success && <p role="status">{success}</p>}
      {!loading && (
        <button
          disabled={saving}
          onClick={() => setReload((value) => value + 1)}
        >
          Tải lại (bỏ bản sửa)
        </button>
      )}
      {current && !loading && (
        <form onSubmit={save} noValidate>
          <fieldset disabled={saving || locked}>
            <legend>Cập nhật tiến độ</legend>
            <label>
              Tiến độ (%)
              <input
                aria-label="Tiến độ (%)"
                aria-invalid={!!percentError}
                aria-describedby={percentError ? "cleaning-percent-error" : undefined}
                type="number"
                required
                min={0}
                max={100}
                step={1}
                value={Number.isNaN(percent) ? "" : percent}
                onChange={(event) =>
                  setPercent(
                    event.target.value === ""
                      ? NaN
                      : Number(event.target.value),
                  )
                }
              />
            </label>
            {percentError && <p id="cleaning-percent-error" role="alert">{percentError}</p>}
            {items.length === 0 && <p>Chưa có hạng mục vệ sinh.</p>}
            {current.data.items.map((item, index) => (
              <div key={item.id}>
                <label>
                  <input
                    type="checkbox"
                    checked={items[index]?.done ?? false}
                    onChange={(event) =>
                      setItems((values) =>
                        values.map((value) =>
                          value.id === item.id
                            ? { ...value, done: event.target.checked }
                            : value,
                        ),
                      )
                    }
                  />
                  {item.label}
                  {item.required ? " (bắt buộc)" : ""}
                </label>
                <label>
                  Ghi chú: {item.label}
                  <input
                    maxLength={1000}
                    value={items[index]?.note ?? ""}
                    onChange={(event) =>
                      setItems((values) =>
                        values.map((value) =>
                          value.id === item.id
                            ? { ...value, note: event.target.value || null }
                            : value,
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
                onChange={(event) => setNote(event.target.value)}
              />
            </label>
            <label>
              Tệp đính kèm (tối đa 5)
              <input
                ref={fileInput}
                aria-invalid={!!fileError}
                aria-describedby={fileError ? "cleaning-file-error" : undefined}
                type="file"
                multiple
                accept=".jpg,.jpeg,.png,.pdf"
                onChange={(event) =>
                  setFiles(Array.from(event.target.files || []))
                }
              />
            </label>
            {fileError && <p id="cleaning-file-error" role="alert">{fileError}</p>}
            <p>JPG/PNG/PDF, tối đa 10 MiB mỗi tệp. Ghi chú dùng một dòng.</p>
            <button type="submit">
              {saving ? "Đang lưu…" : "Lưu tiến độ"}
            </button>
          </fieldset>
          <p>
            {current.data.timeline.length} bản ghi tiến độ ·{" "}
            {current.data.evidence.length} tệp đã lưu.
          </p>
          <h3>Tiến độ đã ghi nhận</h3>
          <ol>
            {current.data.timeline.map((entry) => (
              <li key={entry.id}>
                <time>{entry.at}</time> · {entry.percent}% · {entry.note || "—"}
              </li>
            ))}
          </ol>
          <h3>Tệp đã lưu</h3>
          <ul>
            {current.data.evidence.map((file) => (
              <li key={file.id}>
                <a href={file.downloadPath} download>{file.name}</a>
              </li>
            ))}
          </ul>
        </form>
      )}
    </>
  );
}
