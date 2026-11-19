import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface RouteView {
  requestId: number;
  status: string;
  version: string;
  departments: { departmentId: number; name: string }[];
}
class Failure extends Error {
  constructor(readonly status: number) {
    super("Chuyển bộ phận chưa khả dụng");
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const r = await fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!r.ok) throw new Failure(r.status);
  if (!r.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return r.json();
}
export function InternalRequestRoutePanel({
  id,
  onRouted,
}: {
  id: number;
  onRouted: () => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [result, setResult] = useState<{
    owner: typeof owner;
    id: number;
    view: RouteView;
  } | null>(null);
  const [revision, setRevision] = useState(0);
  const [department, setDepartment] = useState("");
  const [note, setNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setResult(null);
    setDepartment("");
    setNote("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    if (owner)
      void json(`/api/internal-requests/${id}/route`, abort.signal)
        .then((raw) => {
          const v = raw as RouteView;
          if (
            !v ||
            v.requestId !== id ||
            typeof v.status !== "string" ||
            !version(v.version) ||
            !Array.isArray(v.departments) ||
            v.departments.some(
              (d) =>
                !Number.isSafeInteger(d.departmentId) ||
                d.departmentId <= 0 ||
                typeof d.name !== "string",
            )
          )
            throw new Error("Phản hồi điều hướng không hợp lệ");
          if (!abort.signal.aborted) setResult({ owner, id, view: v });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          setMessage("Chưa thể chuyển bộ phận. Tải lại quyền hiện tại.");
          if (e instanceof Failure && e.status === 401)
            actions.invalidateSession(owner!);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function routeRequest() {
    const abort = controller.current;
    if (
      !owner ||
      !view ||
      !abort ||
      abort.signal.aborted ||
      sending.current ||
      view.status !== "PENDING_ACCEPTANCE"
    )
      return;
    const selected = view.departments.find(
      (d) => d.departmentId === Number(department),
    );
    if (!selected || note.trim().length < 10) {
      setMessage("Chọn bộ phận đích và nhập lý do ít nhất 10 ký tự.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const raw = (await json(
        `/api/internal-requests/${id}/route`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            departmentId: selected.departmentId,
            reason: note.trim(),
          }),
        },
      )) as { requestId?: unknown; departmentId?: unknown; version?: unknown };
      if (
        raw.requestId !== id ||
        raw.departmentId !== selected.departmentId ||
        !version(raw.version)
      )
        throw new Error("Chưa xác nhận điều hướng");
      if (!abort.signal.aborted) {
        setResult(null);
        setDepartment("");
        setNote("");
        onRouted();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại bộ phận trước khi chuyển."
            : "Chưa xác nhận điều hướng. Tải lại trước khi thử lại.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner) return null;
  return (
    <ActionDialog title="Chuyển yêu cầu" busy={busy}>
      <section aria-label="Chuyển yêu cầu">
        <h3>Chuyển sang bộ phận khác</h3>
        {message && <p role="status">{userMessage(message)}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại bộ phận
        </button>
        {view &&
          (view.status === "PENDING_ACCEPTANCE" ? (
            <div aria-busy={busy}>
              <label>
                Bộ phận đích
                <SearchableSelect
                  aria-label="Bộ phận đích"
                  disabled={busy}
                  value={department}
                  onChange={(e) => setDepartment(e.target.value)}
                >
                  <option value="">Chọn bộ phận</option>
                  {view.departments.map((d) => (
                    <option key={d.departmentId} value={d.departmentId}>
                      {d.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Lý do chuyển bộ phận
                <input
                  disabled={busy}
                  maxLength={4000}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                />
              </label>
              <button
                disabled={busy || !department}
                onClick={() => void routeRequest()}
              >
                Chuyển yêu cầu
              </button>
            </div>
          ) : (
            <p>Chỉ yêu cầu đang chờ tiếp nhận mới được chuyển bộ phận.</p>
          ))}
      </section>
    </ActionDialog>
  );
}
