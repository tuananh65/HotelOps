import { useEffect, useRef, useState, type ReactNode } from "react";
import { api, message, type Row, timestamp } from "./api";
export function useQuery<T = any>(path: string | null, revision = 0) {
  const [state, set] = useState<{
    data?: T;
    error?: unknown;
    loading: boolean;
    path?: string | null;
  }>({ loading: true });
  useEffect(() => {
    const controller = new AbortController();
    set((previous) => ({
      path,
      data: previous.path === path ? previous.data : undefined,
      loading: true,
    }));
    if (!path) {
      set({ path, loading: false });
      return;
    }
    api<T>(path, undefined, "GET", controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) set({ path, data, loading: false });
      },
      (error) => {
        if (!controller.signal.aborted) set({ path, error, loading: false });
      },
    );
    return () => controller.abort();
  }, [path, revision]);
  return state.path === path ? state : { loading: !!path };
}
export function State({
  loading,
  error,
  children,
  retry,
  data,
}: {
  loading?: boolean;
  error?: unknown;
  children: ReactNode;
  retry?: () => void;
  data?: unknown;
}) {
  if (loading && data === undefined)
    return (
      <div className="empty" role="status">
        <span className="spinner" /> Đang tải dữ liệu…
      </div>
    );
  if (error)
    return (
      <div className="alert" role="alert">
        {message(error)} {retry && <button onClick={retry}>Thử lại</button>}
      </div>
    );
  return (
    <div className="query-state" aria-busy={!!loading} inert={!!loading}>
      {children}
    </div>
  );
}
export function Empty({
  children = "Chưa có dữ liệu trong phạm vi đã chọn.",
}: {
  children?: ReactNode;
}) {
  return <div className="empty">{children}</div>;
}
const labels: Record<string, string> = {
  PLANNED: "Đã phân ca",
  WORKING: "Đang trong ca",
  ENDED: "Đã kết thúc",
  CANCELLED: "Đã hủy",
  READY: "Sẵn sàng",
  DIRTY: "Cần dọn",
  PENDING_CLEANING: "Chờ dọn phòng",
  CLEANING_IN_PROGRESS: "Đang dọn phòng",
  CREATED: "Đã tạo",
  LINE_SAVED: "Đã lưu vật tư",
  ASSIGN: "Phân ca",
  START: "Bắt đầu ca",
  END: "Kết thúc ca",
  CANCEL: "Hủy ca",
  OCCUPIED: "Có khách",
  AVAILABLE: "Sẵn sàng",
  BLOCKED: "Đang chặn",
  IN_PROGRESS: "Đang thực hiện",
  ASSIGNED: "Đã giao",
  ACCEPTED: "Đã nhận",
  PENDING_ASSIGNMENT: "Chờ phân công",
  PENDING_ACCEPTANCE: "Chờ nhận",
  PENDING_INSPECTION: "Chờ kiểm tra",
  PENDING_REINSPECTION: "Chờ kiểm tra lại",
  PENDING_CONCLUSION: "Chờ kết luận",
  REWORK_REQUIRED: "Cần làm lại",
  COMPLETED: "Hoàn thành",
  CLOSED: "Đã đóng",
  OPEN: "Mới",
  PENDING: "Chờ xử lý",
  INSPECTING: "Đang kiểm tra",
  ON_HOLD: "Tạm dừng",
  PAUSED: "Tạm dừng",
  PENDING_VERIFICATION: "Chờ xác nhận",
  RESOLVED: "Đã xử lý",
  DRAFT: "Bản nháp",
  PENDING_APPROVAL: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  POSTED: "Đã ghi sổ",
  PASSED: "Đạt",
  FAILED: "Không đạt",
  REJECTED: "Từ chối",
  SUBMITTED: "Đã gửi",
  REQUESTED: "Đã yêu cầu",
  NEEDS_INFORMATION: "Cần bổ sung",
  PARTIALLY_ISSUED: "Đã cấp một phần",
  FULLY_ISSUED: "Đã cấp đủ",
  PARTIALLY_RECEIVED: "Đã nhận một phần",
  FULLY_RECEIVED: "Đã nhận đủ",
  RECEIVED: "Đã nhận",
  IN_TRANSIT: "Đang chuyển",
  COUNTED: "Đã kiểm đếm",
  REMAINDER_CLOSED: "Đã đóng phần còn lại",
  DERIVED: "Đã tạo chứng từ liên kết",
  POST: "Đã ghi sổ",
  APPROVE: "Đã duyệt",
  COUNT: "Đã kiểm đếm",
};
export const label = (s: string) => labels[s] || s;
export function Badge({ value }: { value: string }) {
  return (
    <span
      className={`badge ${["READY", "CLOSED", "ENDED", "PASSED", "POSTED", "COMPLETED"].includes(value) ? "good" : ["BLOCKED", "FAILED", "REJECTED", "REWORK_REQUIRED"].includes(value) ? "bad" : "neutral"}`}
    >
      {label(value)}
    </span>
  );
}
export function Dialog({
  title,
  children,
  close,
  className,
}: {
  title: string;
  children: ReactNode;
  close: () => void;
  className?: string;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const backdropPress = useRef(false);
  useEffect(() => {
    const before = document.activeElement as HTMLElement;
    const dialog = ref.current;
    dialog?.showModal();
    return () => {
      dialog?.close();
      before?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className={className}
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault();
        e.stopPropagation();
        close();
      }}
      onPointerDown={(e) => {
        const bounds = e.currentTarget.getBoundingClientRect();
        backdropPress.current =
          e.target === e.currentTarget &&
          (e.clientX < bounds.left ||
            e.clientX > bounds.right ||
            e.clientY < bounds.top ||
            e.clientY > bounds.bottom);
      }}
      onClick={(e) => {
        const bounds = e.currentTarget.getBoundingClientRect();
        if (
          backdropPress.current &&
          e.target === e.currentTarget &&
          (e.clientX < bounds.left ||
            e.clientX > bounds.right ||
            e.clientY < bounds.top ||
            e.clientY > bounds.bottom)
        )
          close();
        backdropPress.current = false;
      }}
    >
      <header>
        <h2>{title}</h2>
        <button type="button" aria-label="Đóng" onClick={close}>
          ×
        </button>
      </header>
      {children}
    </dialog>
  );
}
export function History({ rows }: { rows: Row[] }) {
  return (
    <ol className="history">
      {rows.map((r, i) => (
        <li key={i}>
          <strong>{label(r.action || r.eventType || "Cập nhật")}</strong>
          <span>
            {r.actor || ""} · {timestamp(r.occurredAt || r.createdAt)}
          </span>
          {(r.reason || r.note) && <p>{r.reason || r.note}</p>}
        </li>
      ))}
      {!rows.length && <Empty>Chưa có lịch sử.</Empty>}
    </ol>
  );
}
export function Pager({
  page,
  total,
  onPage,
  pageSize = 50,
}: {
  page: number;
  total: number;
  onPage: (p: number) => void;
  pageSize?: number;
}) {
  return (
    <div className="pager">
      <span role="status" aria-live="polite">
        {total} bản ghi · Trang {page} /{" "}
        {Math.max(1, Math.ceil(total / pageSize))}
      </span>
      <button
        type="button"
        disabled={page <= 1}
        onClick={() => onPage(page - 1)}
      >
        Trước
      </button>
      <button
        type="button"
        disabled={page * pageSize >= total}
        onClick={() => onPage(page + 1)}
      >
        Tiếp
      </button>
    </div>
  );
}
