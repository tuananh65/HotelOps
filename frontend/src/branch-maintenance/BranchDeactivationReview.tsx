import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { useAuth } from "../auth/AuthProvider";
import { AppButton } from "../components/design-system";
import { LoadingIndicator } from "../components/LoadingIndicator";
import {
  ActionDialog,
  announceWorkflowSuccess,
} from "../components/workspace-interactions";
import {
  decodeBranch,
  safeId,
  strongBranchTag,
  type BranchSnapshot,
} from "./contracts";
import { branchReadError } from "./branch-ui";
import { displayCode } from "../i18n/vi";

type Review = {
  branchId: number;
  activeRooms: number;
  warehousesWithNonzeroStock: number;
  states: {
    kind: string;
    status: string | null;
    count: number;
    classification: string;
  }[];
  canDeactivate: boolean;
  coordination: string;
  scopeResolutionComplete: boolean;
  hasUnresolvedStates: boolean;
};
const unknownMessage =
  "Chưa xác định kết quả ngừng hoạt động. Không gửi lại; tải lại thông tin chi nhánh và nhờ quản trị viên kiểm tra kết quả.";
const kinds: Record<string, string> = {
  operational_tasks: "Công việc vận hành",
  cleaning_tasks: "Công việc dọn phòng",
  cleaning_reworks: "Công việc làm lại",
  room_inspections: "Kiểm tra phòng",
  incidents: "Sự cố",
  internal_requests: "Yêu cầu nội bộ",
  material_requests: "Yêu cầu vật tư",
  maintenance_works: "Công việc bảo trì",
  replenishments: "Bổ sung vật tư",
  work_intervals: "Ca làm việc đang mở",
  unscoped_maintenance: "Bảo trì chưa xác định chi nhánh",
};
function validReview(value: unknown, branchId: number): value is Review {
  if (!value || typeof value !== "object") return false;
  const v = value as Review;
  return (
    v.branchId === branchId &&
    Number.isSafeInteger(v.activeRooms) &&
    v.activeRooms >= 0 &&
    Number.isSafeInteger(v.warehousesWithNonzeroStock) &&
    v.warehousesWithNonzeroStock >= 0 &&
    typeof v.canDeactivate === "boolean" &&
    typeof v.scopeResolutionComplete === "boolean" &&
    typeof v.hasUnresolvedStates === "boolean" &&
    typeof v.coordination === "string" &&
    Array.isArray(v.states) &&
    v.states.every(
      (s) =>
        s &&
        typeof s.kind === "string" &&
        (s.status === null || typeof s.status === "string") &&
        Number.isSafeInteger(s.count) &&
        s.count >= 0 &&
        ["OPEN", "CLOSED", "UNRESOLVED"].includes(s.classification),
    )
  );
}

/** Admission, dependency classification and concurrency remain server-owned. */
export function BranchDeactivationReview({
  branchId,
  isActive,
  disabled = false,
  onDeactivated,
  uncertain,
}: {
  branchId: number;
  isActive: boolean;
  disabled?: boolean;
  onDeactivated: (data: BranchSnapshot) => void;
  uncertain: RefObject<boolean>;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const key = useMemo(() => ({}), [owner, branchId]);
  const current = useRef(key);
  current.current = key;
  const [open, setOpen] = useState(false),
    [loading, setLoading] = useState(false),
    [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{
    key: object;
    value: Review;
    version: string;
  } | null>(null);
  const [reason, setReason] = useState(""),
    [confirmed, setConfirmed] = useState(false),
    [message, setMessage] = useState("");
  const reader = useRef<AbortController | null>(null),
    writer = useRef<AbortController | null>(null);
  const locked = uncertain,
    inFlight = useRef(false),
    dispatched = useRef(false),
    mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      reader.current?.abort();
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    setOpen(false);
    setResult(null);
    setReason("");
    setConfirmed(false);
    setMessage(locked.current ? unknownMessage : "");
    setLoading(false);
    setBusy(false);
    return () => {
      reader.current?.abort();
      writer.current?.abort();
      if (dispatched.current) locked.current = true;
      inFlight.current = false;
    };
  }, [key]);
  const visible = result?.key === key ? result : null;
  const admitted =
    !!visible &&
    visible.value.canDeactivate &&
    visible.value.scopeResolutionComplete &&
    !visible.value.hasUnresolvedStates &&
    visible.value.activeRooms === 0 &&
    visible.value.warehousesWithNonzeroStock === 0 &&
    visible.value.states.every((s) => s.classification === "CLOSED");
  async function inspect() {
    if (
      !owner ||
      disabled ||
      inFlight.current ||
      locked.current ||
      !safeId(branchId)
    )
      return;
    reader.current?.abort();
    const abort = new AbortController();
    reader.current = abort;
    setOpen(true);
    setLoading(true);
    setResult(null);
    setReason("");
    setConfirmed(false);
    setMessage("");
    try {
      const response = await fetch(
        `/api/branches/${branchId}/deactivation-dependencies`,
        {
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        },
      );
      if (abort.signal.aborted || current.current !== key) return;
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok)
        throw new Error(
          response.status >= 500
            ? "Chưa kiểm tra được điều kiện ngừng hoạt động. Cơ sở dữ liệu cần được cập nhật đầy đủ các cấu trúc kiểm tra phụ thuộc; liên hệ quản trị viên."
            : await branchReadError(response),
        );
      const value: unknown = await response.json(),
        version = response.headers.get("ETag");
      if (!validReview(value, branchId) || !strongBranchTag(version))
        throw new Error(
          "Kết quả kiểm tra phụ thuộc không hợp lệ. Chưa thể ngừng hoạt động chi nhánh.",
        );
      if (!abort.signal.aborted && current.current === key)
        setResult({ key, value, version });
    } catch (e) {
      if (!abort.signal.aborted && current.current === key)
        setMessage(
          e instanceof TypeError
            ? "Không kết nối được máy chủ. Vui lòng thử lại."
            : e instanceof Error
              ? e.message
              : "Chưa kiểm tra được điều kiện ngừng hoạt động.",
        );
    } finally {
      if (!abort.signal.aborted && current.current === key) setLoading(false);
    }
  }
  async function deactivate() {
    if (
      !owner ||
      !visible ||
      !admitted ||
      !confirmed ||
      reason.trim().length < 10 ||
      reason.length > 1000 ||
      inFlight.current ||
      locked.current ||
      disabled
    )
      return;
    const abort = new AbortController();
    writer.current = abort;
    inFlight.current = true;
    dispatched.current = false;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      mounted.current && !abort.signal.aborted && current.current === key;
    try {
      const csrf = await fetch("/api/auth/csrf", {
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!isCurrent()) return;
      if (csrf.status === 401) actions.invalidateSession(owner);
      const token = await csrf.json().catch(() => null);
      if (
        !csrf.ok ||
        token?.headerName !== "X-CSRF-TOKEN" ||
        typeof token.csrfToken !== "string" ||
        !/^[\x21-\x7e]{1,8192}$/.test(token.csrfToken)
      )
        throw new Error(
          "Chưa xác minh được phiên đăng nhập. Thao tác chưa được gửi.",
        );
      if (!isCurrent()) return;
      dispatched.current = true;
      const response = await fetch(`/api/branches/${branchId}/deactivate`, {
        method: "POST",
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": token.csrfToken,
          "If-Match": visible.version,
        },
        body: JSON.stringify({ confirmation: true, reason: reason.trim() }),
      });
      if (!isCurrent()) return;
      if (response.status === 401) actions.invalidateSession(owner);
      if ([400, 401, 403, 404, 409, 412, 428].includes(response.status)) {
        dispatched.current = false;
        setResult(null);
        setConfirmed(false);
        setMessage(
          response.status === 412 || response.status === 428
            ? "Thông tin chi nhánh đã thay đổi. Kiểm tra lại điều kiện trước khi xác nhận mới."
            : response.status === 409
              ? "Chi nhánh còn dữ liệu đang hoạt động. Kiểm tra lại các công việc và tồn kho."
              : "Máy chủ từ chối thao tác. Kiểm tra lại quyền và thông tin chi nhánh.",
        );
        return;
      }
      const branch =
        response.status === 200
          ? decodeBranch(await response.json().catch(() => null), branchId)
          : undefined;
      const etag = response.headers.get("ETag");
      if (!isCurrent()) return;
      if (!branch || branch.isActive || !strongBranchTag(etag))
        throw new Error(unknownMessage);
      dispatched.current = false;
      setResult(null);
      setOpen(false);
      setConfirmed(false);
      setReason("");
      onDeactivated({ branch, etag });
      announceWorkflowSuccess(
        "Đã ngừng hoạt động chi nhánh.",
        "branch-deactivation",
      );
      setMessage("Đã ngừng hoạt động chi nhánh.");
    } catch (e) {
      if (isCurrent()) {
        if (dispatched.current) {
          locked.current = true;
          setResult(null);
        }
        setMessage(
          dispatched.current
            ? unknownMessage
            : e instanceof Error
              ? e.message
              : "Chưa gửi được thao tác.",
        );
      }
    } finally {
      if (writer.current === abort) {
        writer.current = null;
        inFlight.current = false;
        if (isCurrent()) setBusy(false);
      }
    }
  }
  return (
    <section className="branch-card branch-danger">
      <h2>Trạng thái hoạt động</h2>
      <p>
        {isActive
          ? "Chỉ ngừng hoạt động khi chi nhánh không còn phòng hoạt động, tồn kho hoặc công việc chưa hoàn tất."
          : "Chi nhánh đã ngừng hoạt động. Thông tin và lịch sử vẫn được lưu giữ."}
      </p>
      {isActive && (
        <AppButton
          variant="danger"
          disabled={!owner || disabled || busy || locked.current}
          onClick={() => void inspect()}
        >
          Ngừng hoạt động chi nhánh
        </AppButton>
      )}
      {message && !open && (
        <p
          role="status"
          className={`branch-feedback ${!isActive ? "is-success" : "is-warning"}`}
        >
          {message}
        </p>
      )}
      <ActionDialog
        open={open}
        title="Kiểm tra ngừng hoạt động chi nhánh"
        onClose={() => {
          reader.current?.abort();
          setOpen(false);
          setResult(null);
          setReason("");
          setConfirmed(false);
        }}
        busy={busy}
        locked={locked.current}
        error={
          message ? (
            <p role="alert" className="branch-feedback">
              {message}
            </p>
          ) : undefined
        }
      >
        {loading ? (
          <LoadingIndicator label="Đang kiểm tra điều kiện ngừng hoạt động" />
        ) : visible ? (
          <>
            <p>
              Phòng đang hoạt động: <strong>{visible.value.activeRooms}</strong>
              . Kho còn tồn:{" "}
              <strong>{visible.value.warehousesWithNonzeroStock}</strong>.
            </p>
            <ul>
              {visible.value.states
                .filter((s) => s.classification !== "CLOSED")
                .map((s, i) => (
                  <li key={i}>
                    {kinds[s.kind] ?? "Dữ liệu phụ thuộc cần kiểm tra"}:{" "}
                    {displayCode(s.status ?? "N/A")} — {s.count}
                    {s.classification === "UNRESOLVED"
                      ? " (chưa xác định đầy đủ)"
                      : ""}
                  </li>
                ))}
            </ul>
            {!admitted ? (
              <p className="branch-feedback is-warning" role="alert">
                Chưa thể ngừng hoạt động chi nhánh.{" "}
                {visible.value.coordination === "BRANCH_WRITER_GUARDS_REQUIRED"
                  ? "Cấu hình kiểm soát dữ liệu chi nhánh chưa đầy đủ; liên hệ quản trị viên."
                  : "Cần xử lý các dữ liệu phụ thuộc và kiểm tra lại."}
              </p>
            ) : (
              <form
                className="branch-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  void deactivate();
                }}
              >
                <label>
                  Lý do ngừng hoạt động
                  <textarea
                    required
                    minLength={10}
                    maxLength={1000}
                    disabled={busy}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label className="branch-check">
                  <input
                    type="checkbox"
                    disabled={busy}
                    checked={confirmed}
                    onChange={(e) => setConfirmed(e.target.checked)}
                  />
                  Tôi xác nhận ngừng hoạt động chi nhánh và giữ nguyên lịch sử.
                </label>
                <button
                  data-dialog-action
                  type="submit"
                  disabled={busy || !confirmed || reason.trim().length < 10}
                >
                  Xác nhận ngừng hoạt động
                </button>
              </form>
            )}
          </>
        ) : null}
        {!loading && !busy && !locked.current && (
          <button type="button" onClick={() => void inspect()}>
            Kiểm tra lại điều kiện
          </button>
        )}
      </ActionDialog>
    </section>
  );
}
