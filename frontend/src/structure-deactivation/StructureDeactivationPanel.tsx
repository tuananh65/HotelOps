import { displayCode } from "../i18n/vi";
import {
  createDeactivationPreviewApi,
  type DependencyPreview,
} from "./deactivationPreviewApi";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { useAuth } from "../auth/AuthProvider";
import { createStructureDeactivationApi } from "./structureDeactivationApi";
import {
  failureMessage,
  safeId,
  validReason,
  type WriteResult,
  type Failure,
  type StructureKind,
} from "./contracts";
// Deactivate-only page-lifetime reservations, not authorization or durable idempotency.
// Session/provider changes and dependency GET/ETag cannot reconcile an unknown POST.
// UNKNOWN has no client-side unlock or timeout; reload/restart loses this memory.
// Store only principal/resource identity and operation state, never credentials or reason.
type Reservation = Readonly<{ phase: "pending" | "unknown" }>;
const operations = new Map<string, Map<string, Reservation>>();
const listeners = new Set<() => void>();
function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
function operation(principal: string, resource: string) {
  return operations.get(principal)?.get(resource);
}
function notify() {
  for (const listener of listeners) listener();
}
function reserve(principal: string, resource: string) {
  if (operation(principal, resource)) return;
  // Object identity binds settlement to exactly this operation.
  const reservation: Reservation = { phase: "pending" };
  const records = operations.get(principal) ?? new Map<string, Reservation>();
  records.set(resource, reservation);
  operations.set(principal, records);
  notify();
  return reservation;
}
function settle(
  principal: string,
  resource: string,
  reservation: Reservation,
  result: WriteResult,
) {
  if (operation(principal, resource) !== reservation) return;
  const records = operations.get(principal)!;
  if (result.outcome === "OUTCOME_UNKNOWN")
    records.set(resource, { phase: "unknown" });
  else {
    // Only this attempt's definitive response or non-dispatch releases its reservation.
    records.delete(resource);
    if (!records.size) operations.delete(principal);
  }
  notify();
}
const keys = new WeakMap<object, number>();
let next = 0;
function keyFor(x: object) {
  if (!keys.has(x)) keys.set(x, ++next);
  return keys.get(x)!;
}
/** Canonical UC016 action; the server rechecks dependencies and schema at commit. */
export function StructureDeactivationPanel({
  kind,
  branchId,
  id,
  dispatchEnabled = false,
}: {
  kind: StructureKind;
  branchId: number;
  id: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [
      state.session,
      state.status,
      state.pending,
      kind,
      branchId,
      id,
      dispatchEnabled,
      actions,
    ],
  );
  const latest = useRef(identity);
  latest.current = identity;
  const current = useMemo(() => () => latest.current === identity, [identity]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return <p role="status">Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>;
  if (!safeId(branchId) || !safeId(id) || (kind !== "floor" && kind !== "area"))
    return <p role="alert">Chọn mã chi nhánh và hồ sơ hợp lệ.</p>;
  return (
    <Bound
      key={keyFor(identity)}
      principal={state.session.user.id}
      {...{ kind, branchId, id, dispatchEnabled, current }}
    />
  );
}
function Bound({
  principal,
  kind,
  branchId,
  id,
  dispatchEnabled,
  current,
}: {
  principal: string;
  kind: StructureKind;
  branchId: number;
  id: number;
  dispatchEnabled: boolean;
  current: () => boolean;
}) {
  const resource = `${branchId}:${kind}:${id}`;
  const reservation = useSyncExternalStore(
    subscribe,
    () => operation(principal, resource),
    () => undefined,
  );
  const [previewApi] = useState(() => createDeactivationPreviewApi());
  const [api] = useState(() =>
    createStructureDeactivationApi(
      globalThis.fetch.bind(globalThis),
      dispatchEnabled,
    ),
  );
  const [data, setData] = useState<DependencyPreview | null>(null),
    [error, setError] = useState<Failure | null>(null),
    [loading, setLoading] = useState(true),
    [reload, setReload] = useState(0),
    [reason, setReason] = useState(""),
    [review, setReview] = useState<{ reason: string; etag: string } | null>(
      null,
    ),
    [message, setMessage] = useState(""),
    [pending, setPending] = useState(false),
    [locked, setLocked] = useState(false);
  const busy = useRef(false),
    attempted = useRef(false),
    mounted = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    // A new editor must not read a preview as reconciliation of a pending/unknown write.
    if (operation(principal, resource) || locked) return;
    const abort = new AbortController();
    let active = true;
    setData(null);
    setError(null);
    setLoading(true);
    setReview(null);
    setReason("");
    void previewApi.read(kind, branchId, id, abort.signal).then((r) => {
      if (!active || !current()) return;
      setLoading(false);
      if (r.ok) setData(r.data);
      else setError(r.error);
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [
    previewApi,
    kind,
    branchId,
    id,
    reload,
    current,
    principal,
    resource,
    reservation,
    locked,
  ]);
  async function confirm() {
    if (
      !review ||
      !data ||
      busy.current ||
      attempted.current ||
      !current() ||
      !mounted.current ||
      !dispatchEnabled || data.writerCoordinationRequired || data.dependencies.length !== 0
    )
      return;
    const reserved = reserve(principal, resource);
    if (!reserved) return;
    busy.current = true;
    attempted.current = true;
    setPending(true);
    writer.current = new AbortController();
    const result = await api.deactivate(
      kind,
      branchId,
      id,
      review.etag,
      { reason: review.reason, confirmation: true },
      () => mounted.current && current(),
      writer.current.signal,
    );
    // Settle only the captured attempt, including after its editor has unmounted.
    // Stale results cannot update the replacement identity's UI.
    settle(principal, resource, reserved, result);
    if (!mounted.current || !current()) return;
    busy.current = false;
    setPending(false);
    setReview(null);
    setLocked(true);
    if (result.outcome === "CONFIRMED") {
      setData({
        ...result.data,
        dependencies: [],
        writerCoordinationRequired: false,
      });
      setMessage("Máy chủ đã xác nhận vô hiệu hóa.");
    } else {
      setError(result.error);
      setMessage(
        result.outcome === "OUTCOME_UNKNOWN"
          ? "Chưa xác định kết quả. Không gửi lại; nhờ người có trách nhiệm xác minh hồ sơ."
          : "Chưa xác nhận. Chỉ bắt đầu quy trình mới sau khi kiểm tra hồ sơ.",
      );
    }
  }
  return (
    <main>
      <h1>Vô hiệu hóa {kind}</h1>
      <p>
        Chi nhánh {branchId}. Backend decides access; selecting an ID grants no
        authority.
      </p>
      {!dispatchEnabled && (
        <p role="status">
          Vô hiệu hóa thực tế bị khóa, chờ nghiệm thu backend và trưởng dự án phê duyệt.
        </p>
      )}
      {loading && !reservation && <p role="status">Đang tải hồ sơ...</p>}
      {error && (
        <section role="alert">
          <p>{failureMessage(error)}</p>
          {error.dependencies?.map((d) => (
            <p key={d.kind}>
              {displayCode(d.kind)}: {d.count}
            </p>
          ))}
        </section>
      )}
      {data && !reservation && (
        <>
          <dl>
            <dt>Mã</dt>
            <dd>{data.record.code}</dd>
            <dt>Tên</dt>
            <dd>{data.record.name}</dd>
            <dt>Trạng thái</dt>
            <dd>{data.record.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</dd>
            <dt>Mô tả</dt>
            <dd>{data.record.description ?? "Không có"}</dd>
          </dl>
          <p>
            Xem trước ràng buộc chỉ là dữ liệu tại thời điểm đọc, không cấp quyền hay giữ chỗ. Máy chủ kiểm tra lại trước khi lưu.
          </p>
          <section aria-label="Xem trước các ràng buộc">
            {data.dependencies.length === 0 ? (
              <p>
                Không thấy trở ngại trong bản chụp dữ liệu này.
              </p>
            ) : (
              data.dependencies.map((d) => (
                <p key={d.kind}>
                  {displayCode(d.kind)}: {d.count}
                </p>
              ))
            )}
          </section>
          {data.writerCoordinationRequired && <p role="alert">Cần cập nhật cấu trúc bảo vệ vị trí trước khi vô hiệu hóa.</p>}
          {!locked && data.record.isActive && !data.writerCoordinationRequired && data.dependencies.length === 0 && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (validReason(reason) && current()) {
                  setReview({ reason, etag: data.etag });
                  setMessage("");
                } else
                  setMessage(
                    "Nhập lý do 10–1000 ký tự, không chứa ký tự điều khiển.",
                  );
              }}
            >
              <label>
                Lý do
                <textarea
                  value={reason}
                  onChange={(e) => {
                    setReason(e.target.value);
                    setReview(null);
                  }}
                />
              </label>
              <button disabled={pending}>Xem xét</button>
            </form>
          )}
          {review && !locked && (
            <section aria-label="Xem lại vô hiệu hóa">
              <h2>Xem lại vô hiệu hóa</h2>
              <p>
                {data.record.code} — {data.record.name}
              </p>
              <p>{review.reason}</p>
              <button disabled={pending} onClick={() => setReview(null)}>
                Hủy xem xét
              </button>
              <button
                disabled={!dispatchEnabled || pending || data.writerCoordinationRequired || data.dependencies.length !== 0}
                onClick={() => void confirm()}
              >
                Xác nhận vô hiệu hóa
              </button>
            </section>
          )}
        </>
      )}
      {reservation && (
        <p role="status">
          {reservation.phase === "pending"
            ? "Đang chờ vô hiệu hóa. Không gửi lại."
            : "Chưa xác định kết quả. Không gửi lại; nhờ người có trách nhiệm xác minh hồ sơ."}
        </p>
      )}
      {message && !reservation && <p role="status">{message}</p>}
      <button
        disabled={pending || locked || !!reservation}
        onClick={() => {
          setReload((n) => n + 1);
          setMessage("");
        }}
      >
        Tải lại hồ sơ
      </button>
    </main>
  );
}
