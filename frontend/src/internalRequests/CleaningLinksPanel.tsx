import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

interface Item {
  taskId: number;
  code: string;
  title: string;
  state: string | null;
  mandatory: boolean;
  path: string;
}
interface View {
  requestId: number;
  branchId: number;
  version: string;
  canLink: boolean;
  items: Item[];
  total: number;
  page: number;
  candidates: boolean;
}
const validVersion = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
class Failure extends Error {
  constructor(readonly status: number) {
    super("Công việc liên quan chưa khả dụng");
  }
}
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!response.ok) throw new Failure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}
export function InternalRequestCleaningLinksPanel({
  id,
  onLinked,
}: {
  id: number;
  onLinked?: () => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [candidates, setCandidates] = useState(false);
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    view: View;
  } | null>(null);
  const [selected, setSelected] = useState("");
  const [mandatory, setMandatory] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const key = `${id}:${candidates}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setLoaded(null);
    setSelected("");
    setMandatory("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    if (owner)
      void json(
        `/api/internal-requests/${id}/linked-cleaning?candidates=${candidates}&page=${page}`,
        abort.signal,
      )
        .then((raw) => {
          const v = raw as View;
          if (
            !v ||
            v.requestId !== id ||
            !Number.isSafeInteger(v.branchId) ||
            v.branchId <= 0 ||
            !validVersion(v.version) ||
            typeof v.canLink !== "boolean" ||
            v.candidates !== candidates ||
            v.page !== page ||
            !Number.isSafeInteger(v.total) ||
            v.total < 0 ||
            !Array.isArray(v.items) ||
            v.items.length > 25 ||
            v.items.length > v.total ||
            v.items.some(
              (t) =>
                !Number.isSafeInteger(t.taskId) ||
                t.taskId <= 0 ||
                typeof t.code !== "string" ||
                typeof t.title !== "string" ||
                !(t.state === null || typeof t.state === "string") ||
                typeof t.mandatory !== "boolean" ||
                typeof t.path !== "string" ||
                !new RegExp(
                  `^/cleaning/tasks/${t.taskId}\\?branchId=${v.branchId}&(?:execution=1|monitor=1&kind=cleaning)$`,
                ).test(t.path),
            )
          )
            throw new Error("Phản hồi công việc liên quan không hợp lệ");
          if (!abort.signal.aborted) setLoaded({ owner, key, view: v });
        })
        .catch((e) => {
          if (!abort.signal.aborted) {
            setMessage("Chưa có công việc liên quan. Tải lại quyền hiện tại.");
            if (e instanceof Failure && e.status === 401)
              actions.invalidateSession(owner!);
          }
        });
    return () => abort.abort();
  }, [id, owner, candidates, page, revision, key, actions]);
  const view =
    owner && loaded?.owner === owner && loaded.key === key ? loaded.view : null;
  async function link() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canLink ||
      !candidates ||
      !abort ||
      abort.signal.aborted ||
      sending.current
    )
      return;
    const taskId = Number(selected);
    if (
      !view.items.some((t) => t.taskId === taskId) ||
      !["required", "optional"].includes(mandatory)
    ) {
      setMessage(
        "Chọn công việc và xác định có bắt buộc để hoàn tất hay không.",
      );
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
        `/api/internal-requests/${id}/linked-cleaning`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({ taskId, mandatory: mandatory === "required" }),
        },
      )) as {
        requestId?: unknown;
        taskId?: unknown;
        mandatory?: unknown;
        version?: unknown;
      };
      if (
        raw.requestId !== id ||
        raw.taskId !== taskId ||
        raw.mandatory !== (mandatory === "required") ||
        !validVersion(raw.version)
      )
        throw new Error("Chưa xác nhận liên kết");
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          "MSG08: Đã liên kết công việc vệ sinh. Mở danh sách công việc liên quan để xem.",
        );
        onLinked?.();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại công việc liên quan trước khi lưu."
            : "Chưa xác nhận liên kết. Tải lại trước khi thử lại.",
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
    <section aria-label="Công việc vệ sinh liên quan">
      <h3>Công việc vệ sinh liên quan</h3>
      <p>Chỉ liệt kê công việc bạn được xem. Liên kết không đóng yêu cầu.</p>
      {message && <p role="status">{userMessage(message)}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại công việc liên quan
      </button>
      {candidates && (
        <button
          disabled={busy}
          onClick={() => {
            setCandidates(false);
            setPage(1);
          }}
        >
          Xem công việc liên quan
        </button>
      )}
      {!candidates && view?.canLink && (
        <button
          disabled={busy}
          onClick={() => {
            setCandidates(true);
            setPage(1);
          }}
        >
          Liên kết công việc vệ sinh hiện có
        </button>
      )}
      {!candidates && view?.canLink && (
        <Link
          to={`/cleaning/tasks/new?branchId=${view.branchId}&requestId=${id}`}
        >
          Tạo công việc vệ sinh mới
        </Link>
      )}
      {view && (
        <>
          {view.items.length === 0 && (
            <p>
              Không có mục được phép xem {candidates ? "open" : "linked"} công
              việc trên trang này.
            </p>
          )}
          <ul>
            {view.items.map((t) => (
              <li key={t.taskId}>
                <Link to={t.path}>
                  {t.code}: {t.title}
                </Link>{" "}
                — {displayCode(t.state ?? "Trạng thái chưa khả dụng")}
                {!candidates && (
                  <span>
                    {" "}
                    — {t.mandatory ? "Bắt buộc để hoàn tất" : "Không bắt buộc"}
                  </span>
                )}
              </li>
            ))}
          </ul>
          <button
            disabled={busy || page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Công việc trước
          </button>
          <span> Trang {page} </span>
          <button
            disabled={busy || page * 25 >= view.total}
            onClick={() => setPage((p) => p + 1)}
          >
            Công việc tiếp theo
          </button>
          {candidates && view.canLink && (
            <ActionDialog title="Liên kết công việc vệ sinh" busy={busy}>
              <div aria-busy={busy}>
                <label>
                  Mở công việc vệ sinh
                  <SearchableSelect
                    aria-label="Mở công việc vệ sinh"
                    disabled={busy}
                    value={selected}
                    onChange={(e) => setSelected(e.target.value)}
                  >
                    <option value="">Chọn công việc</option>
                    {view.items.map((t) => (
                      <option key={t.taskId} value={t.taskId}>
                        {t.code}: {t.title}
                      </option>
                    ))}
                  </SearchableSelect>
                </label>
                <label>
                  Điều kiện hoàn tất
                  <SearchableSelect
                    aria-label="Điều kiện hoàn tất"
                    disabled={busy}
                    value={mandatory}
                    onChange={(e) => setMandatory(e.target.value)}
                  >
                    <option value="">Chọn điều kiện</option>
                    <option value="required">Bắt buộc để hoàn tất</option>
                    <option value="optional">Không bắt buộc</option>
                  </SearchableSelect>
                </label>
                <button
                  data-dialog-action
                  disabled={busy}
                  onClick={() => void link()}
                >
                  Lưu liên kết vệ sinh
                </button>
              </div>
            </ActionDialog>
          )}
        </>
      )}
    </section>
  );
}
