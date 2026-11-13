import { ChecklistCollection } from "./ChecklistCollection";
import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  ChecklistApiError,
  createChecklistApi,
  type LoadedChecklist,
} from "./api";
import { editableCriteria, lifecycle, positiveId, reasonInput } from "./model";
import { ChecklistForm } from "./ChecklistForm";
import "./checklists.css";
const api = createChecklistApi();
export function ChecklistWorkspace({ initialId }: { initialId?: string }) {
  const [search, setSearch] = useSearchParams();
  const branch = search.get("branchId") || "";
  const [branchText, setBranchText] = useState(branch);
  const [error, setError] = useState("");
  let branchId: number | null = null;
  try {
    branchId = positiveId(branch);
  } catch {
    /* Await explicit branch selection. */
  }
  return (
    <section className="fe05-checklists">
      <h1>Checklist kiểm tra phòng</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          try {
            positiveId(branchText);
            const next = new URLSearchParams(search);
            next.set("branchId", branchText);
            setSearch(next);
            setError("");
          } catch (e) {
            setError((e as Error).message);
          }
        }}
      >
        <label>
          Mã chi nhánh
          <input
            required
            inputMode="numeric"
            value={branchText}
            onChange={(e) => setBranchText(e.target.value)}
          />
        </label>
        <button>Chọn chi nhánh</button>
      </form>
      {error && <p role="alert">{error}</p>}
      {branchId ? (
        <BranchWorkspace
          key={`${branchId}:${initialId || ""}`}
          branch={branchId}
          initialId={initialId}
        />
      ) : (
        <p>Chọn chi nhánh để mở hoặc tạo checklist.</p>
      )}
    </section>
  );
}
function BranchWorkspace({
  branch,
  initialId,
}: {
  branch: number;
  initialId?: string;
}) {
  const [idText, setIdText] = useState(initialId || "");
  const [selected, setSelected] = useState<number | null>(() => {
    try {
      return initialId ? positiveId(initialId) : null;
    } catch {
      return null;
    }
  });
  const [detailKey, setDetailKey] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const [denied, setDenied] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState("");
  const authorize = useCallback(() => {
    setDenied(false);
    setError("");
  }, []);
  const deny = useCallback((message: string) => {
    setError(message);
    setDenied(true);
    setCreating(false);
    setSelected(null);
  }, []);
  function open(id: number) {
    setSelected(id);
    setDetailKey((n) => n + 1);
    setIdText(String(id));
    setError("");
  }
  function changed() {
    setRefresh((n) => n + 1);
  }
  return (
    <>
      <div className="fe05-actions">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            try {
              open(positiveId(idText));
            } catch (e) {
              setError((e as Error).message);
            }
          }}
        >
          <label>
            ID checklist
            <input
              required
              inputMode="numeric"
              value={idText}
              onChange={(e) => setIdText(e.target.value)}
            />
          </label>
          <button>Mở checklist</button>
        </form>
        <button
          type="button"
          disabled={denied}
          onClick={() => {
            setCreating(true);
            setError("");
          }}
        >
          Tạo checklist
        </button>
      </div>
      {error && <p role="alert">{error}</p>}
      <ChecklistCollection
        branch={branch}
        refresh={refresh}
        blocked={denied}
        onOpen={open}
        onDenied={deny}
        onAuthorized={authorize}
      />
      {creating && (
        <ChecklistForm
          branch={branch}
          onBlocked={(e) => {
            if ([401, 403].includes(e.status)) deny(e.message);
          }}
          onCancel={() => setCreating(false)}
          onDone={(result) => {
            setCreating(false);
            open(result.value.id);
            changed();
          }}
        />
      )}
      {selected && (
        <ChecklistDetail
          key={`${selected}:${detailKey}`}
          branch={branch}
          id={selected}
          onLoaded={authorize}
          onDenied={deny}
          onMutated={changed}
        />
      )}
    </>
  );
}
function ChecklistDetail({
  branch,
  id,
  onLoaded,
  onDenied,
  onMutated,
}: {
  branch: number;
  id: number;
  onLoaded: (value: LoadedChecklist) => void;
  onDenied: (message: string) => void;
  onMutated: () => void;
}) {
  const [loaded, setLoaded] = useState<LoadedChecklist | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [reload, setReload] = useState(0);
  const [blocked, setBlocked] = useState(false);
  const [editing, setEditing] = useState(false);
  const [action, setAction] = useState<"publish" | "retire" | null>(null);
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setLoaded(null);
    setBusy(true);
    setEditing(false);
    setAction(null);
    setError("");
    setBlocked(false);
    api
      .read(branch, id, abort.signal)
      .then((result) => {
        setLoaded(result);
        onLoaded(result);
      })
      .catch((e) => {
        if (e.name !== "AbortError") {
          setError(e.message);
          if ([401, 403].includes(e.status)) onDenied(e.message);
        }
      })
      .finally(() => {
        if (!abort.signal.aborted) setBusy(false);
      });
    return () => abort.abort();
    // Parent callbacks update access state; identity/reload, not callback references, drive reads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, id, reload]);
  function accept(result: LoadedChecklist) {
    setLoaded(result);
    onLoaded(result);
    onMutated();
    setEditing(false);
    setAction(null);
    setReason("");
    setConfirmed(false);
    setNotice("Đã lưu thay đổi trên server.");
  }
  async function transition(e: React.FormEvent) {
    e.preventDefault();
    if (!loaded || !action || !confirmed) return;
    setError("");
    try {
      const clean = reasonInput(reason);
      setBusy(true);
      accept(
        await api.transition(branch, id, loaded.etag, action, clean, confirmed),
      );
    } catch (e) {
      setError((e as Error).message);
      if (e instanceof ChecklistApiError) {
        if (e.uncertain || [401, 403, 409, 412, 428].includes(e.status))
          setBlocked(true);
        if ([401, 403].includes(e.status)) {
          setLoaded(null);
          onDenied(e.message);
        }
      }
    } finally {
      setBusy(false);
    }
  }
  const rules = loaded && lifecycle(loaded.value);
  return (
    <section aria-label="Chi tiết checklist">
      <div className="fe05-actions">
        <h2>Chi tiết checklist #{id}</h2>
        <button
          type="button"
          disabled={busy}
          onClick={() => {
            setNotice("");
            setReload((n) => n + 1);
          }}
        >
          Tải lại checklist
        </button>
      </div>
      {busy && <p role="status">Đang xử lý…</p>}
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {loaded && rules && (
        <>
          <h3>
            {loaded.value.code} — {loaded.value.name}
          </h3>
          <p>{loaded.value.description}</p>
          <p>Loại phòng áp dụng: {loaded.value.roomTypeIds.join(", ")}</p>
          <p>
            Mã, tên và loại phòng áp dụng được giữ nguyên trong thao tác cập
            nhật tiêu chí.
          </p>
          {[...loaded.value.versions]
            .sort((a, b) => b.number - a.number)
            .map((v) => (
              <article key={v.id}>
                <h3>
                  Phiên bản {v.number} — {v.status}
                </h3>
                <p>
                  {v.usedByInspection
                    ? "Đã được Inspection sử dụng — dữ liệu lịch sử chỉ đọc."
                    : "Chưa có Inspection sử dụng phiên bản này."}
                </p>
                <p>
                  Hiệu lực:{" "}
                  {v.effectiveAt
                    ? new Date(v.effectiveAt).toLocaleString("vi-VN")
                    : "Chưa công bố"}
                </p>
                <ol>
                  {v.criteria.map((c) => (
                    <li key={c.id}>
                      <strong>{c.text}</strong>{" "}
                      {c.mandatory ? "(Bắt buộc)" : "(Tùy chọn)"}
                      {c.group && <p>Nhóm: {c.group}</p>}
                      {c.passCondition && (
                        <p>Điều kiện đạt: {c.passCondition}</p>
                      )}
                      {c.description && <p>{c.description}</p>}
                    </li>
                  ))}
                </ol>
              </article>
            ))}
          {!loaded.value.versions.length && <p>Checklist chưa có phiên bản.</p>}
          <div className="fe05-actions">
            <button
              disabled={busy || blocked || !rules.canEdit}
              onClick={() => setEditing(true)}
            >
              {rules.draft ? "Chỉnh sửa Draft" : "Tạo Draft kế tiếp"}
            </button>
            <button
              disabled={busy || blocked || !rules.canPublish}
              onClick={() => {
                setAction("publish");
                setReason("");
                setConfirmed(false);
              }}
            >
              Công bố Draft
            </button>
            <button
              disabled={busy || blocked || !rules.canRetire}
              onClick={() => {
                setAction("retire");
                setReason("");
                setConfirmed(false);
              }}
            >
              Ngừng sử dụng bản công bố
            </button>
          </div>
          {editing && (
            <ChecklistForm
              key={loaded.etag}
              branch={branch}
              initial={{
                id,
                etag: loaded.etag,
                criteria: editableCriteria(loaded.value),
              }}
              disabled={busy || blocked}
              onBlocked={(e) => {
                setBlocked(true);
                if ([401, 403].includes(e.status)) onDenied(e.message);
              }}
              onDone={accept}
              onCancel={() => {
                setEditing(false);
                setReload((n) => n + 1);
              }}
            />
          )}
          {action && (
            <form onSubmit={transition} aria-label="Xác nhận phiên bản">
              <h3>
                {action === "publish"
                  ? "Công bố Draft"
                  : "Ngừng sử dụng bản công bố"}
              </h3>
              <fieldset disabled={busy || blocked}>
                <label>
                  Lý do
                  <textarea
                    required
                    maxLength={500}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label className="fe05-inline">
                  <input
                    type="checkbox"
                    checked={confirmed}
                    onChange={(e) => setConfirmed(e.target.checked)}
                  />
                  Tôi xác nhận thao tác
                </label>
                <button disabled={!confirmed} type="submit">
                  Xác nhận
                </button>
              </fieldset>
              <button
                type="button"
                disabled={busy}
                onClick={() => setAction(null)}
              >
                Hủy thao tác
              </button>
            </form>
          )}
        </>
      )}
    </section>
  );
}
