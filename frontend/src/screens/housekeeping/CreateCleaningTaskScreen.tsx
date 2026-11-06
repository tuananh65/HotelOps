import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  cleaningOptions,
  createCleaning,
  createError,
  type CreateOptions,
} from "../../api/dev1/cleaningCreate";
import { PageHeader } from "../../ui";
export default function CreateCleaningTaskScreen() {
  const [params, setParams] = useSearchParams(),
    nav = useNavigate();
  const branch = params.get("branchId") || "",
    room = params.get("roomId") || "",
    key = branch + ":" + room;
  const [result, setResult] = useState<{
      key: string;
      data: CreateOptions;
    } | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [retry, setRetry] = useState(0),
    [submitted, setSubmitted] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError("");
    setSubmitted(false);
    if (branch && room)
      cleaningOptions(branch, room, abort.signal)
        .then((data) => {
          if (!abort.signal.aborted) setResult({ key, data });
        })
        .catch((e) => {
          if (!abort.signal.aborted) setError(createError(e));
        });
    return () => abort.abort();
  }, [branch, room, key, retry]);
  const data = result?.key === key ? result.data : null;
  function load(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setParams({
      branchId: String(f.get("branchId") || ""),
      roomId: String(f.get("roomId") || ""),
    });
    setRetry((v) => v + 1);
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data || busy || submitted) return;
    const f = new FormData(e.currentTarget);
    setBusy(true);
    setError("");
    setSubmitted(true);
    try {
      const d = await createCleaning(branch, {
        roomId: data.roomId,
        cleaningTypeId: Number(f.get("cleaningTypeId")),
        priorityId: Number(f.get("priorityId")),
        dueAt: String(f.get("dueAt") || ""),
        instruction: String(f.get("instruction") || "").trim() || null,
      });
      nav(`/cleaning/tasks/${d.id}?branchId=${branch}&monitor=1&kind=cleaning`);
    } catch (e) {
      setError(createError(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageHeader id={25} />
      <form className="filter-bar" onSubmit={load}>
        <label>
          Chi nhánh (ID)
          <input name="branchId" required defaultValue={branch} />
        </label>
        <label>
          Phòng (ID)
          <input name="roomId" required defaultValue={room} />
        </label>
        <button disabled={busy}>Tải cấu hình phòng</button>
      </form>
      {error && <p role="alert">{error}</p>}
      {!data && (
        <p>Chọn phòng và chi nhánh hợp lệ để tải cấu hình từ máy chủ.</p>
      )}
      {data && (
        <section className="panel form-panel">
          <h2>
            {data.roomNumber} · Chi nhánh {branch}
          </h2>
          <p>
            {data.cleaningItemCount} hạng mục xem trước; snapshot được cố định
            khi bắt đầu vệ sinh. Trạng thái phòng legacy không bị ghi đè.
          </p>
          <form onSubmit={submit}>
            <label>
              Loại vệ sinh
              <select aria-label="Loại vệ sinh" name="cleaningTypeId" required>
                {data.cleaningTypes.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Ưu tiên
              <select aria-label="Ưu tiên" name="priorityId" required>
                {data.priorities.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Hạn UTC (ISO 8601)
              <input name="dueAt" required placeholder="YYYY-MM-DDTHH:mm:ssZ" />
            </label>
            <label>
              Hướng dẫn
              <input name="instruction" maxLength={4000} />
            </label>
            <button
              className="primary"
              disabled={
                busy ||
                submitted ||
                data.cleaningItemCount < 1 ||
                data.cleaningItemCount > 100
              }
            >
              {busy ? "Đang tạo…" : "Tạo công việc"}
            </button>
          </form>
          {submitted && !busy && (
            <button
              onClick={() => {
                setSubmitted(false);
                setRetry((v) => v + 1);
              }}
            >
              Tải lại cấu hình sau khi kiểm tra Monitor
            </button>
          )}
        </section>
      )}
      <Link to={`/cleaning/tasks?branchId=${encodeURIComponent(branch)}`}>
        Về Cleaning Monitor
      </Link>
    </>
  );
}
