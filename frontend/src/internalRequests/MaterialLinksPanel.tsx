import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { Link } from "react-router-dom";

interface Item {
  materialRequestId: number;
  code: string;
  purpose: string;
  state: string;
  mandatory: boolean;
  completionSatisfied: boolean | null;
  completionUnavailableReason: string | null;
}
interface View {
  requestId: number;
  branchId: number;
  version: string;
  items: Item[];
  total: number;
  page: number;
  candidates: boolean;
  canLink: boolean;
}
class Failure extends Error {
  constructor(readonly status: number) {
    super("Vật tư liên quan chưa khả dụng");
  }
}
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
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

export function InternalRequestMaterialLinksPanel({
  id,
  onLinked,
}: {
  id: number;
  onLinked?: () => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [candidates, setCandidates] = useState(false);
  const [selected, setSelected] = useState("");
  const [mandatory, setMandatory] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    view?: View;
    error?: string;
  } | null>(null);
  const key = `${id}:${candidates}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setLoaded(null);
    setSelected("");
    setMandatory("");
    setBusy(false);
    setMessage("");
    sending.current = false;
    if (owner)
      void json(
        `/api/internal-requests/${id}/linked-materials?candidates=${candidates}&page=${page}`,
        abort.signal,
      )
        .then((raw) => {
          const view = raw as View;
          if (
            !view ||
            view.requestId !== id ||
            !positive(view.branchId) ||
            view.page !== page ||
            !version(view.version) ||
            view.candidates !== candidates ||
            typeof view.canLink !== "boolean" ||
            (candidates && !view.canLink) ||
            !Number.isSafeInteger(view.total) ||
            view.total < 0 ||
            !Array.isArray(view.items) ||
            view.items.length > 25 ||
            view.items.length > Math.max(0, view.total - (page - 1) * 25) ||
            view.items.some(
              (item) =>
                !item ||
                !positive(item.materialRequestId) ||
                typeof item.code !== "string" ||
                typeof item.purpose !== "string" ||
                typeof item.state !== "string" ||
                typeof item.mandatory !== "boolean" ||
                !(
                  item.completionSatisfied === null ||
                  typeof item.completionSatisfied === "boolean"
                ) ||
                !(
                  item.completionUnavailableReason === null ||
                  typeof item.completionUnavailableReason === "string"
                ) ||
                item.mandatory !== (item.completionSatisfied !== null) ||
                (item.completionSatisfied === true &&
                  item.completionUnavailableReason !== null),
            ) ||
            new Set(view.items.map((item) => item.materialRequestId)).size !==
              view.items.length
          )
            throw new Error("Phản hồi vật tư liên quan không hợp lệ");
          if (!abort.signal.aborted) setLoaded({ owner, key, view });
        })
        .catch((error) => {
          if (abort.signal.aborted) return;
          setLoaded({
            owner,
            key,
            error: "Chưa có vật tư liên quan. Tải lại quyền hiện tại.",
          });
          if (error instanceof Failure && error.status === 401)
            actions.invalidateSession(owner!);
        });
    return () => abort.abort();
  }, [id, owner, candidates, page, revision, key, actions]);
  if (!owner) return null;
  const visible = loaded?.owner === owner && loaded.key === key ? loaded : null;
  async function link() {
    const abort = controller.current;
    const view = visible?.view;
    if (
      !owner ||
      !view?.canLink ||
      !candidates ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    const materialRequestId = Number(selected);
    if (
      !view.items.some(
        (item) => item.materialRequestId === materialRequestId,
      ) ||
      !["required", "optional"].includes(mandatory)
    ) {
      setMessage("Chọn yêu cầu vật tư và điều kiện hoàn tất.");
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
        typeof csrf?.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const raw = (await json(
        `/api/internal-requests/${id}/linked-materials`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            materialRequestId,
            mandatory: mandatory === "required",
          }),
        },
      )) as {
        requestId?: unknown;
        materialRequestId?: unknown;
        mandatory?: unknown;
        version?: unknown;
        message?: unknown;
      };
      if (
        !raw ||
        raw.requestId !== id ||
        raw.materialRequestId !== materialRequestId ||
        raw.mandatory !== (mandatory === "required") ||
        !version(raw.version) ||
        raw.message !== "MSG08"
      )
        throw new Error("Chưa xác nhận liên kết");
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          "MSG08: Đã liên kết yêu cầu vật tư. Tải lại vật tư liên quan.",
        );
        onLinked?.();
      }
    } catch (error) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          error instanceof Failure && error.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi liên kết."
            : "Chưa xác nhận liên kết. Tải lại quyền và trạng thái trước khi thử lại.",
        );
        if (error instanceof Failure && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  return (
    <section aria-label="Yêu cầu vật tư liên quan" aria-busy={busy || !visible}>
      <h3>Yêu cầu vật tư liên quan</h3>
      <p>
        Chỉ liệt kê vật tư trong quyền MR_VIEW hiện tại. Danh sách trống không
        xác nhận có thể đóng yêu cầu.
      </p>
      <button disabled={busy} onClick={() => setRevision((value) => value + 1)}>
        Tải lại vật tư liên quan
      </button>
      {message && <p role="status">{userMessage(message)}</p>}
      {candidates && (
        <button
          disabled={busy}
          onClick={() => {
            setCandidates(false);
            setPage(1);
          }}
        >
          Xem vật tư liên quan
        </button>
      )}
      {!candidates && visible?.view?.canLink && (
        <button
          disabled={busy}
          onClick={() => {
            setCandidates(true);
            setPage(1);
          }}
        >
          Liên kết yêu cầu vật tư hiện có
        </button>
      )}
      {visible?.view?.canLink && (
        <Link
          to={`/operations/material-requests?action=A004A-043&branchId=${visible.view.branchId}&internalRequestId=${id}&sourceVersion=${encodeURIComponent(visible.view.version)}`}
        >
          Tạo yêu cầu vật tư liên quan
        </Link>
      )}
      {visible?.error && <p role="status">{userMessage(visible.error)}</p>}
      {visible?.view && (
        <>
          {visible.view.items.length === 0 && (
            <p>
              Không có mục được phép xem {candidates ? "open" : "linked"} vật tư
              trên trang này.
            </p>
          )}
          <ul>
            {visible.view.items.map((item) => (
              <li key={item.materialRequestId}>
                <Link
                  to={`/operations/material-requests/${item.materialRequestId}`}
                >
                  Mở yêu cầu vật tư
                </Link>{" "}
                <strong>{item.code}</strong>: {item.purpose} —{" "}
                {displayCode(item.state)}
                {!candidates && (
                  <p>
                    {!item.mandatory
                      ? "Không bắt buộc để hoàn tất"
                      : item.completionSatisfied
                        ? "Vật tư bắt buộc: đã xác nhận bằng chứng hoàn tất"
                        : "Vật tư bắt buộc: chưa xác nhận hoàn tất. Công việc còn lại hoặc thiếu bằng chứng đang chặn đóng."}
                  </p>
                )}
              </li>
            ))}
          </ul>
          <button
            disabled={busy || page <= 1}
            onClick={() => setPage((value) => value - 1)}
          >
            Vật tư trước
          </button>
          <span> Trang {page} </span>
          <button
            disabled={busy || page * 25 >= visible.view.total}
            onClick={() => setPage((value) => value + 1)}
          >
            Vật tư tiếp theo
          </button>
          {candidates && visible.view.canLink && (
            <ActionDialog title="Liên kết yêu cầu vật tư" busy={busy}>
              <label>
                Mở yêu cầu vật tư
                <SearchableSelect
                  value={selected}
                  disabled={busy}
                  onChange={(event) => setSelected(event.target.value)}
                >
                  <option value="">Chọn yêu cầu vật tư</option>
                  {visible.view.items.map((item) => (
                    <option
                      key={item.materialRequestId}
                      value={item.materialRequestId}
                    >
                      {item.code}: {displayCode(item.purpose)}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Điều kiện hoàn tất
                <SearchableSelect
                  value={mandatory}
                  disabled={busy}
                  onChange={(event) => setMandatory(event.target.value)}
                >
                  <option value="">Chọn điều kiện</option>
                  <option value="required">Bắt buộc để hoàn tất</option>
                  <option value="optional">Không bắt buộc</option>
                </SearchableSelect>
              </label>
              <button disabled={busy} onClick={() => void link()}>
                Lưu liên kết vật tư
              </button>
            </ActionDialog>
          )}
        </>
      )}
    </section>
  );
}
