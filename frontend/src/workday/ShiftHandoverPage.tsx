import { referenceLabel } from "../i18n/referenceLabel";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Item = {
  kind: string;
  id: number;
  state: string;
  label: string;
  note: string | null;
};
type Receiver = {
  userId: number;
  name: string;
  assignmentId: number;
  start: string;
  end: string;
};
type Preview = {
  workdayId: number;
  branchId: number;
  departmentId: number;
  version: string;
  items: Item[];
  receivers: Receiver[];
};
type Receipt = {
  id: number;
  workdayId: number;
  branchId: number;
  departmentId: number;
  senderId: number;
  receiverId: number;
  previousId: number | null;
  state: string;
  version: string;
  submittedAt: string;
  decisionAt: string | null;
  reason: string | null;
  generalNote: string | null;
  items: Item[];
  restrictedItems: number;
  canDecide: boolean;
};
type Summary = {
  id: number;
  workdayId: number;
  senderId: number;
  receiverId: number;
  state: string;
  submittedAt: string;
};
function sourcePath(item: Item, branch: number): string | null {
  switch (item.kind) {
    case "TASK":
      return `/operational-tasks?branchId=${branch}&taskId=${item.id}`;
    case "CLEANING":
      return `/cleaning/tasks/${item.id}?branchId=${branch}`;
    case "REWORK":
      return `/cleaning/reworks/${item.id}?branchId=${branch}`;
    case "INCIDENT":
      return `/operations/incidents/${item.id}`;
    case "INTERNAL_REQUEST":
      return `/internal-requests?requestId=${item.id}`;
    case "ROOM":
      return `/rooms/${item.id}?branchId=${branch}`;
    default:
      return null;
  }
}
const validId = (id: number) => Number.isSafeInteger(id) && id > 0;
const identities = new WeakMap<object, number>();
let serial = 0;
function identityKey(identity: object) {
  if (!identities.has(identity)) identities.set(identity, ++serial);
  return identities.get(identity)!;
}

export function ShiftHandoverPage() {
  const { state, actions } = useAuth();
  const { id } = useParams();
  const [query] = useSearchParams();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, id, query.toString()],
  );
  const current = useRef(identity);
  current.current = identity;
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending !== null ||
    !state.session ||
    state.session.mustChangePassword
  )
    return (
      <p role="status">
        Xác minh phiên đăng nhập không bị hạn chế để dùng bàn giao ca.
      </p>
    );
  return (
    <Bound
      key={identityKey(identity)}
      id={Number(id)}
      branch={Number(query.get("branchId"))}
      day={Number(query.get("workdayId"))}
      previous={Number(query.get("previousId"))}
      user={Number(state.session.user.id)}
      current={() => current.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
function Bound({
  id,
  branch,
  day,
  previous,
  user,
  current,
  recover,
}: {
  id: number;
  branch: number;
  day: number;
  previous: number;
  user: number;
  current: () => boolean;
  recover: () => void;
}) {
  const [preview, setPreview] = useState<Preview | null>(null),
    [receipt, setReceipt] = useState<Receipt | null>(null);
  const [rows, setRows] = useState<Summary[]>([]),
    [page, setPage] = useState(1),
    [reload, setReload] = useState(0);
  const [receiver, setReceiver] = useState(""),
    [general, setGeneral] = useState(""),
    [reason, setReason] = useState("");
  const [notes, setNotes] = useState<Record<string, string>>({}),
    [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false),
    [confirmed, setConfirmed] = useState(false),
    [unknown, setUnknown] = useState(false);
  const requestKey = useRef(crypto.randomUUID()),
    pending = useRef(false),
    controller = useRef<AbortController | null>(null);
  async function read(url: string, signal: AbortSignal) {
    const response = await fetch(url, {
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
      headers: { Accept: "application/json" },
    });
    if (!response.ok) {
      if ((response.status === 401 || response.status === 403) && current())
        recover();
      throw new Error(`Quyền hoặc dữ liệu chưa khả dụng (${response.status}).`);
    }
    return response.json();
  }
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setBusy(true);
    setConfirmed(false);
    void (async () => {
      try {
        if (validId(id)) {
          const data = (await read(
            `/api/shift-handovers/${id}`,
            abort.signal,
          )) as Receipt;
          if (
            data.id !== id ||
            !Array.isArray(data.items) ||
            typeof data.version !== "string"
          )
            throw new Error("Phản hồi phiếu nhập không hợp lệ.");
          if (!abort.signal.aborted && current()) setReceipt(data);
        } else if (validId(day)) {
          const data = (await read(
            `/api/shift-handovers/preview/${day}`,
            abort.signal,
          )) as Preview;
          if (
            data.workdayId !== day ||
            !Array.isArray(data.items) ||
            !Array.isArray(data.receivers) ||
            typeof data.version !== "string"
          )
            throw new Error("Phản hồi bản chụp dữ liệu không hợp lệ.");
          if (!abort.signal.aborted && current()) {
            setPreview(data);
            setNotes({});
            setReceiver("");
            requestKey.current = crypto.randomUUID();
          }
        } else if (validId(branch)) {
          const data = (await read(
            `/api/shift-handovers?branchId=${branch}&page=${page}`,
            abort.signal,
          )) as Summary[];
          if (!Array.isArray(data))
            throw new Error("Danh sách bàn giao không hợp lệ.");
          if (!abort.signal.aborted && current()) setRows(data);
        } else
          setMessage(
            "Mở Bàn giao ca từ ngày làm việc hiện tại hoặc chọn chi nhánh.",
          );
      } catch (error) {
        if (!abort.signal.aborted && current())
          setMessage(
            error instanceof Error ? error.message : "Đã đọc chưa khả dụng.",
          );
      } finally {
        if (!abort.signal.aborted && current()) setBusy(false);
      }
    })();
    return () => abort.abort();
  }, [reload, page]);
  async function save(acknowledge?: boolean) {
    if (busy || pending.current || unknown || !confirmed || !controller.current)
      return;
    const signal = controller.current.signal;
    pending.current = true;
    setBusy(true);
    let dispatched = false;
    try {
      const csrf = (await read("/api/auth/csrf", signal)) as {
        headerName: string;
        csrfToken: string;
      };
      if (
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken ||
        !current() ||
        signal.aborted
      )
        throw new Error("Phiên đăng nhập hoặc bảo vệ CSRF chưa khả dụng.");
      const body = preview
        ? {
            requestKey: requestKey.current,
            receiverAssignmentId: Number(receiver),
            previousId: validId(previous) ? previous : null,
            generalNote: general.trim() || null,
            notes: preview.items.map((i) => ({
              kind: i.kind,
              id: i.id,
              note: notes[`${i.kind}:${i.id}`]?.trim() || null,
            })),
          }
        : { acknowledge, reason: acknowledge ? null : reason.trim() };
      const version = preview?.version ?? receipt?.version;
      if (!version) throw new Error("Tải lại phiên bản hiện tại.");
      dispatched = true;
      const response = await fetch(
        preview
          ? `/api/shift-handovers/workdays/${day}`
          : `/api/shift-handovers/${id}/decision`,
        {
          method: "POST",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": version,
          },
          body: JSON.stringify(body),
        },
      );
      if (!current() || signal.aborted) return;
      if (!response.ok) {
        if ([400, 401, 403, 404, 409, 412, 428].includes(response.status)) {
          dispatched = false;
          setPreview(null);
          setReceipt(null);
        }
        if (response.status === 401 || response.status === 403) recover();
        throw new Error(
          `Chưa xác nhận yêu cầu (${response.status}). Tải lại trước khi tiếp tục.`,
        );
      }
      const result = (await response.json()) as { id: number; message: string };
      if (!validId(result.id) || result.message !== "MSG08")
        throw new Error("Xác nhận lưu không hợp lệ.");
      setMessage("Đã lưu bàn giao.");
      setConfirmed(false);
      const saved = (await read(
        `/api/shift-handovers/${result.id}`,
        signal,
      )) as Receipt;
      if (current() && !signal.aborted) {
        setPreview(null);
        setReceipt(saved);
      }
    } catch (error) {
      if (current() && !signal.aborted) {
        setUnknown(dispatched);
        setMessage(
          dispatched
            ? "Chưa xác nhận đầy đủ kết quả. Kiểm tra lịch sử bàn giao trước khi thay đổi tiếp."
            : error instanceof Error
              ? error.message
              : "Lưu chưa khả dụng.",
        );
      }
    } finally {
      pending.current = false;
      if (current() && !signal.aborted) setBusy(false);
    }
  }
  const selectedBranch = receipt?.branchId ?? preview?.branchId ?? branch;
  return (
    <main>
      <h1>Bàn giao ca vận hành</h1>
      <p>
        Xác nhận chỉ ghi nhận việc đã biết thông tin. Dùng quy trình phân công
        để đổi người thực hiện.
      </p>
      <Link to="/workday">Ngày làm việc của tôi</Link>
      {validId(selectedBranch) && (
        <>
          {" "}
          ·{" "}
          <Link to={`/workday/handovers?branchId=${selectedBranch}`}>
            Lịch sử bàn giao
          </Link>
        </>
      )}
      {message && <p role="status">{userMessage(message)}</p>}
      {busy && <p role="status">Đang tải hoặc lưu…</p>}
      <button
        disabled={busy || unknown}
        onClick={() => {
          setMessage("");
          setReload((n) => n + 1);
        }}
      >
        Tải lại từ máy chủ
      </button>
      {!validId(id) && !validId(day) && (
        <>
          <CollapsibleFilterPanel activeCount={Number(validId(branch))}>
            <form method="get">
              <label>
                Mã chi nhánh
                <input
                  name="branchId"
                  type="number"
                  min="1"
                  defaultValue={branch || ""}
                />
              </label>
              <button>Tải lịch sử</button>
            </form>
          </CollapsibleFilterPanel>
          <ul>
            {rows.map((r) => (
              <li key={r.id}>
                <Link to={`/workday/handovers/${r.id}`}>{referenceLabel(r, "id")}</Link> —{" "}
                {displayCode(r.state)} — {r.submittedAt}
              </li>
            ))}
          </ul>
          <button
            disabled={busy || page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Trước
          </button>
          <span> Trang {page} </span>
          <button
            disabled={busy || rows.length < 25}
            onClick={() => setPage((p) => p + 1)}
          >
            Tiếp
          </button>
        </>
      )}
      {preview && (
        <section>
          <h2>Xem lại dữ liệu ngày làm việc {referenceLabel(preview, "workdayId")}</h2>
          <label>
            Người nhận ca tiếp theo
            <SearchableSelect
              disabled={busy || unknown}
              value={receiver}
              onChange={(e) => {
                setReceiver(e.target.value);
                setConfirmed(false);
              }}
            >
              <option value="">Chọn người nhận</option>
              {preview.receivers.map((r) => (
                <option key={r.assignmentId} value={r.assignmentId}>
                  {r.name} — {displayCode(r.start)} đến {displayCode(r.end)}
                </option>
              ))}
            </SearchableSelect>
          </label>
          {!preview.receivers.length && (
            <p>
              Không có người nhận đủ điều kiện: cần phân công hiện tại, ca kế
              tiếp, quyền VIEW và DECIDE.
            </p>
          )}
          <ul>
            {preview.items.map((i) => (
              <li key={`${i.kind}:${i.id}`}>
                {displayCode(i.kind)} {referenceLabel(i, "id")}: {i.label} —{" "}
                {displayCode(i.state)}
                <label>
                  Ghi chú vật tư
                  <textarea
                    maxLength={4000}
                    disabled={busy || unknown}
                    value={notes[`${i.kind}:${i.id}`] ?? ""}
                    onChange={(e) => {
                      setNotes((n) => ({
                        ...n,
                        [`${i.kind}:${i.id}`]: e.target.value,
                      }));
                      setConfirmed(false);
                    }}
                  />
                </label>
              </li>
            ))}
          </ul>
          <label>
            Ghi chú chung
            <textarea
              maxLength={4000}
              value={general}
              disabled={busy || unknown}
              onChange={(e) => {
                setGeneral(e.target.value);
                setConfirmed(false);
              }}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={confirmed}
              disabled={busy || unknown}
              onChange={(e) => setConfirmed(e.target.checked)}
            />
            Xác nhận bản chụp dữ liệu và người nhận
          </label>
          <button
            disabled={busy || unknown || !confirmed || !receiver}
            onClick={() => void save()}
          >
            Gửi bàn giao
          </button>
        </section>
      )}
      {receipt && (
        <section>
          <h2>
            Bàn giao {referenceLabel(receipt, "id")} — {displayCode(receipt.state)}
          </h2>
          <p>
            Đã nộp: {receipt.submittedAt}; Quyết định:{" "}
            {receipt.decisionAt ?? "đang chờ"}
          </p>
          {receipt.previousId && (
            <Link to={`/workday/handovers/${receipt.previousId}`}>
              Phiên bản trước
            </Link>
          )}
          <p>{receipt.generalNote}</p>
          <ul>
            {receipt.items.map((i) => (
              <li key={`${i.kind}:${i.id}`}>
                {displayCode(i.kind)} {referenceLabel(i, "id")}: {i.label} —{" "}
                {displayCode(i.state)}
                <p>{i.note}</p>
                {sourcePath(i, receipt.branchId) && (
                  <Link to={sourcePath(i, receipt.branchId)!}>
                    Mở quy trình nguồn
                  </Link>
                )}
              </li>
            ))}
          </ul>
          {receipt.restrictedItems > 0 && (
            <p>
              {receipt.restrictedItems} mục nguồn bị ẩn vì chưa có quyền đọc
              nguồn hiện tại.
            </p>
          )}
          {receipt.reason && <p>Lý do từ chối: {receipt.reason}</p>}
          {receipt.canDecide && (
            <>
              <label>
                Lý do từ chối (ít nhất 10 ký tự)
                <textarea
                  maxLength={4000}
                  value={reason}
                  disabled={busy || unknown}
                  onChange={(e) => {
                    setReason(e.target.value);
                    setConfirmed(false);
                  }}
                />
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={confirmed}
                  disabled={busy || unknown}
                  onChange={(e) => setConfirmed(e.target.checked)}
                />
                Xác nhận quyết định
              </label>
              <button
                disabled={busy || unknown || !confirmed}
                onClick={() => void save(true)}
              >
                Xác nhận đã nhận
              </button>
              <button
                disabled={
                  busy || unknown || !confirmed || reason.trim().length < 10
                }
                onClick={() => void save(false)}
              >
                Từ chối
              </button>
            </>
          )}
          {receipt.state === "REJECTED" && receipt.senderId === user && (
            <Link
              to={`/workday/handovers?workdayId=${receipt.workdayId}&previousId=${receipt.id}`}
            >
              Chuẩn bị phiên bản mới (cần ngày làm việc đang hoạt động)
            </Link>
          )}
        </section>
      )}
    </main>
  );
}
