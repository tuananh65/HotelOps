import { userMessage } from "../i18n/userMessage";
import { useWorkspacePermissions } from "../screens/housekeeping/useWorkspacePermissions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createAuthApi } from "../auth/authApi";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

const http = createHttpClient(),
  auth = createAuthApi();
type Row = {
  taskId: number;
  code: string;
  title: string;
  state: string | null;
  workId?: number | null;
};
type View = {
  requestId: number;
  branchId: number;
  version: string;
  canLink: boolean;
  candidates: boolean;
  page: number;
  total: number;
  items: Row[];
};
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function decode(v: unknown): View | undefined {
  if (
    !isObject(v) ||
    !id(v.requestId) ||
    !id(v.branchId) ||
    typeof v.version !== "string" ||
    !/^[A-Za-z0-9+/]{11}=$/.test(v.version) ||
    typeof v.canLink !== "boolean" ||
    typeof v.candidates !== "boolean" ||
    !id(v.page) ||
    typeof v.total !== "number" ||
    !Number.isSafeInteger(v.total) ||
    v.total < 0 ||
    !Array.isArray(v.items) ||
    v.items.some(
      (r) =>
        !isObject(r) ||
        !id(r.taskId) ||
        typeof r.code !== "string" ||
        typeof r.title !== "string" ||
        !(r.state === null || typeof r.state === "string") ||
        !(r.workId == null || id(r.workId)),
    )
  )
    return undefined;
  return v as View;
}

export function MaterialTaskLinks({
  record,
  kind = "operational",
}: {
  record: Values;
  kind?: "operational" | "cleaning" | "maintenance" | "incident";
}) {
  const access = useWorkspacePermissions(String(record.branchId ?? ""));
  const permission = {
    operational: "TASK_VIEW",
    cleaning: "CLEANING_VIEW",
    maintenance: "MAINTENANCE_VIEW",
    incident: "INCIDENT_VIEW",
  }[kind];
  if (!access.allows(permission, true)) return null;
  if (
    Array.isArray(record.availableLinkKinds) &&
    !record.availableLinkKinds.includes(kind)
  )
    return (
      <p role="status">
        {kind === "operational"
          ? "Liên kết công việc vận hành"
          : kind === "cleaning"
            ? "Liên kết vệ sinh"
            : "Liên kết bảo trì"}{" "}
        chưa được cấu hình. Thông tin yêu cầu vẫn có thể xem ở trên.
      </p>
    );
  return <MaterialTaskLinksContent record={record} kind={kind} />;
}
function MaterialTaskLinksContent({
  record,
  kind,
}: {
  record: Values;
  kind: "operational" | "cleaning" | "maintenance" | "incident";
}) {
  const endpoint = {
    operational: "linked-tasks",
    cleaning: "linked-cleaning-tasks",
    maintenance: "linked-maintenance-occurrences",
    incident: "linked-incidents",
  }[kind];
  const label = {
    operational: "Công việc vận hành",
    cleaning: "Công việc vệ sinh",
    maintenance: "Lần thực hiện bảo trì",
    incident: "Sự cố",
  }[kind];
  const permission = {
    operational: "TASK_VIEW",
    cleaning: "CLEANING_VIEW",
    maintenance: "MAINTENANCE_VIEW",
    incident: "INCIDENT_VIEW",
  }[kind];
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const user = state.session?.user.id;
  const [candidates, setCandidates] = useState(false),
    [page, setPage] = useState(1),
    [revision, setRevision] = useState(0);
  const key = `${kind}:${record.requestId}:${record.rowVersion}:${candidates}:${page}:${revision}`;
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    data?: View;
    error?: string;
  }>();
  const [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const sending = useRef(false),
    lifetime = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    lifetime.current = abort;
    setLoaded(undefined);
    setBusy(false);
    sending.current = false;
    if (owner !== null && id(record.requestId))
      void http({
        method: "GET",
        url: `/api/material-requests/${record.requestId}/${endpoint}?candidates=${candidates}&page=${page}`,
        expectedStatus: 200,
        decode,
        signal: abort.signal,
      }).then((result) => {
        if (abort.signal.aborted) return;
        const matching =
          result.ok &&
          result.data.requestId === record.requestId &&
          result.data.branchId === record.branchId &&
          result.data.candidates === candidates &&
          result.data.page === page &&
          (kind !== "maintenance" ||
            result.data.items.every((row) => id(row.workId)));
        setLoaded({
          owner,
          key,
          data: matching ? result.data : undefined,
          error:
            !result.ok &&
            result.error.kind === "http" &&
            result.error.status === 503
              ? "Liên kết công việc cần cấu trúc dữ liệu đã cài đặt và xác minh."
              : "Liên kết công việc chưa khả dụng trong phạm vi hiện tại.",
        });
        if (
          !result.ok &&
          (result.error.kind === "stale" ||
            (result.error.kind === "http" && result.error.status === 401))
        )
          void actions.revalidate();
      });
    return () => abort.abort();
  }, [
    owner,
    key,
    record.requestId,
    record.branchId,
    candidates,
    page,
    actions,
    endpoint,
  ]);
  const current =
    loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  async function link(task: Row) {
    const view = current?.data,
      abort = lifetime.current;
    if (
      !view?.canLink ||
      !view.candidates ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const session = await auth.getSession(abort.signal);
      if (abort.signal.aborted) return;
      if (
        !session.ok ||
        session.data.mustChangePassword ||
        session.data.user.id !== user
      ) {
        setMessage("Phiên đăng nhập đã thay đổi. Tải lại trước khi liên kết.");
        void actions.revalidate();
        return;
      }
      const result = await http({
        method: "POST",
        url: `/api/material-requests/${view.requestId}/${endpoint}`,
        expectedStatus: 200,
        csrfToken: session.data.csrfToken,
        signal: abort.signal,
        body: { taskId: task.taskId, expectedRowVersion: view.version },
        decode: (v) =>
          isObject(v) &&
          v.requestId === view.requestId &&
          v.taskId === task.taskId &&
          typeof v.version === "string"
            ? v
            : undefined,
      });
      if (abort.signal.aborted) return;
      const rejected =
        !result.ok && result.error.kind === "http"
          ? result.error.status
          : undefined;
      setMessage(
        result.ok
          ? "MSG08: Đã liên kết công việc. Tải lại yêu cầu vật tư trước khi thay đổi tiếp."
          : rejected === 412
            ? "Yêu cầu vật tư đã đổi. Tải lại và kiểm tra công việc đã chọn."
            : rejected === 404
              ? "MSG03: Công việc đã chọn không còn mở hoặc không được xem trong chi nhánh. Chọn hồ sơ khác."
              : rejected === 409
                ? "MSG03: Công việc đã liên kết, đã đóng hoặc không còn hợp lệ. Tải lại yêu cầu và danh sách công việc."
                : rejected === 403
                  ? "Quyền hoặc quan hệ với người yêu cầu hiện tại không cho phép liên kết này."
                  : "Chưa xác nhận liên kết. Tải lại các liên kết để kiểm tra kết quả trước khi thử lại.",
      );
      // Clear the old version after every attempted mutation; never retry automatically.
      setLoaded(undefined);
      setCandidates(false);
      setPage(1);
      setRevision((v) => v + 1);
      if (
        !result.ok &&
        result.error.kind === "http" &&
        result.error.status === 401
      )
        void actions.revalidate();
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  return (
    <section aria-label={`Liên quan ${label.toLowerCase()} Công việc`}>
      <h3>{label} liên kết</h3>
      <p>
        Chỉ công việc trong phạm vi hiện tại {permission} được hiển thị trong
        phạm vi. Liên kết ghi nhận nguồn công việc; không cấp vật tư hoặc đổi
        trạng thái công việc.
      </p>
      {message && <p role="status">{userMessage(message)}</p>}
      {!current ? (
        <p role="status">Đang tải liên kết công việc…</p>
      ) : !current.data ? (
        <p role="alert">{userMessage(current.error)}</p>
      ) : (
        <>
          {current.data.canLink && (
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                setCandidates((v) => !v);
                setPage(1);
              }}
            >
              {candidates
                ? "Hiện công việc liên quan"
                : "Chọn công việc chưa hoàn tất để liên kết"}
            </button>
          )}
          {current.data.items.length === 0 && (
            <p>
              Không {candidates ? "đang mở và đủ điều kiện" : "linked"} công
              việc được phép xem.
            </p>
          )}
          <ul>
            {current.data.items.map((task) => (
              <li key={task.taskId}>
                <Link
                  to={
                    kind === "incident"
                      ? `/operations/incidents?action=A004A-023&id=${task.taskId}`
                      : kind === "maintenance"
                        ? `/operations/maintenance?action=A004A-036&id=${task.workId}`
                        : kind === "cleaning"
                          ? `/cleaning/tasks/${task.taskId}?branchId=${current.data!.branchId}&execution=1`
                          : `/operational-tasks?branchId=${current.data!.branchId}&taskId=${task.taskId}`
                  }
                >
                  {task.code}
                  {kind === "maintenance"
                    ? " / lần thực hiện"
                    : ""}
                </Link>{" "}
                {task.title} —{" "}
                {displayCode(
                  task.state ?? "Trạng thái quy trình chưa khả dụng",
                )}
                {candidates && current.data!.canLink && (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void link(task)}
                  >
                    Liên kết công việc
                  </button>
                )}
              </li>
            ))}
          </ul>
          <button
            type="button"
            disabled={busy || page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Trước
          </button>
          <button
            type="button"
            disabled={busy || page * 25 >= current.data.total}
            onClick={() => setPage((p) => p + 1)}
          >
            Tiếp
          </button>
        </>
      )}
      <button
        type="button"
        disabled={busy}
        onClick={() => setRevision((v) => v + 1)}
      >
        Tải lại liên kết công việc
      </button>
    </section>
  );
}
