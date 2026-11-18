import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createWorkdayApi, type WorkdayApi } from "./workdayApi";
import { safeId, type ReadFailure, type ReadResult } from "./contracts";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { staffDate } from "../internalRequests/staffDate";

const messages: Record<ReadFailure, string> = {
  "invalid-request": "Chưa có chi nhánh hợp lệ.",
  "invalid-response": "Dữ liệu phản hồi không hợp lệ.",
  unauthorized: "Phiên không còn hợp lệ. Vui lòng xác minh phiên đăng nhập.",
  forbidden: "Máy chủ từ chối quyền truy cập.",
  unavailable: "Chưa tải được dữ liệu ca làm việc. Vui lòng thử lại.",
  network: "Không thể kết nối máy chủ.",
  cancelled: "Yêu cầu đã hủy.",
  http: "Không thể đọc dữ liệu ngày làm việc.",
};
type View<T> =
  | { key: object; kind: "loading" }
  | { key: object; kind: "error"; error: ReadFailure }
  | {
      key: object;
      kind: "ready";
      data: T;
    };

function useRead<T>(
  key: object,
  enabled: boolean,
  branchId: number,
  read: (branch: number, signal?: AbortSignal) => Promise<ReadResult<T>>,
) {
  const [view, setView] = useState<View<T> | null>(null);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!enabled) {
      setView(null);
      return;
    }
    const controller = new AbortController();
    let active = true;
    setView({ key, kind: "loading" });
    void read(branchId, controller.signal).then((result) => {
      if (!active) return;
      setView(
        result.ok
          ? { key, kind: "ready", data: result.data }
          : { key, kind: "error", error: result.error },
      );
    });
    return () => {
      active = false;
      controller.abort();
    };
  }, [key, enabled, branchId, read, reload]);
  // Reject old session/branch data during render, before effect cleanup.
  return {
    view: view?.key === key ? view : null,
    retry: () => setReload((v) => v + 1),
  };
}

function ReadSection<T>({
  title,
  result,
  revalidate,
  children,
}: {
  title: string;
  result: ReturnType<typeof useRead<T>>;
  revalidate: () => void;
  children: (data: T) => ReactNode;
}) {
  const view = result.view;
  return (
    <section className="time-card" aria-label={title}>
      <h2>{title}</h2>
      {!view || view.kind === "loading" ? (
        <LoadingIndicator label="Đang tải ca làm việc" />
      ) : view.kind === "error" ? (
        <div role="alert">
          <p>{messages[view.error]}</p>
          {view.error === "unauthorized" ? (
            <button onClick={revalidate}>Xác minh phiên</button>
          ) : (
            <button onClick={result.retry}>Đọc lại</button>
          )}
        </div>
      ) : (
        children(view.data)
      )}
    </section>
  );
}

/** Read component mounted under the existing AuthProvider and router.
 * branchId is a candidate, NOT authority. Each GET independently authorizes
 * its own operation on the server. No permission is inferred from another GET.
 * Both responses are bound to the confirmed session and requested branch.
 */
export function WorkdayReadPanel({
  branchId,
  client: injectedClient,
  canReadShifts = true,
  canReadCurrent = true,
}: {
  branchId: number;
  client?: WorkdayApi;
  canReadShifts?: boolean;
  canReadCurrent?: boolean;
}) {
  const { state, actions } = useAuth();
  const defaultClient = useMemo(() => createWorkdayApi(), []);
  const client = injectedClient ?? defaultClient;
  const confirmed =
    state.status === "AUTHENTICATED" &&
    state.session !== null &&
    !state.session.mustChangePassword &&
    state.pending === null;
  // Session object reference, not merely user ID, also invalidates the effect.
  const session = state.session;
  const key = useMemo(() => ({}), [session, branchId, confirmed]);
  const enabled = confirmed && safeId(branchId);
  const shifts = useRead(key, enabled && canReadShifts, branchId, client.ownShifts);
  const current = useRead(key, enabled && canReadCurrent, branchId, client.current);

  if (!confirmed)
    return (
      <section>
        <h2>Ca làm việc của tôi</h2>
        <p>Chưa xác nhận phiên đăng nhập không hạn chế.</p>
        <Link to="/login">Đăng nhập</Link>
        <Link to="/change-password">Đổi mật khẩu</Link>
      </section>
    );
  if (!safeId(branchId))
    return <p role="alert">{messages["invalid-request"]}</p>;
  return (
    <section className="time-columns">
      {canReadShifts && <ReadSection
        title="Ca có thể bắt đầu hôm nay"
        result={shifts}
        revalidate={() => void actions.revalidate()}
      >
        {(data) =>
          data.length === 0 ? (
            <p className="time-empty">Không có ca chờ bắt đầu hôm nay. Ca đã bắt đầu hoặc đã kết thúc không xuất hiện trong danh sách này.</p>
          ) : (
            <ul className="time-list">
              {data.map((s) => (
                <li key={s.shiftAssignmentId}>
                  <strong>{staffDate(s.scheduledStartAtUtc)} – {staffDate(s.scheduledEndAtUtc)}</strong>
                  <p><Link to={`/workday?branchId=${branchId}&shiftAssignmentId=${s.shiftAssignmentId}`}>Xem chi tiết ca</Link></p>
                </li>
              ))}
            </ul>
          )
        }
      </ReadSection>}
      {canReadCurrent && <ReadSection
        title="Ca đang làm"
        result={current}
        revalidate={() => void actions.revalidate()}
      >
        {(data) =>
          data === null ? (
            <p className="time-empty">Bạn chưa bắt đầu ca hoặc đã kết thúc ca gần nhất.</p>
          ) : (
            <div><span className="time-status">Đang làm việc</span>
              <p>Bắt đầu lúc {staffDate(data.startedAtUtc)}</p>
              <Link to={`/workday/handovers?workdayId=${data.workdayId}`}>Chuẩn bị bàn giao ca</Link>
            </div>
          )
        }
      </ReadSection>}
    </section>
  );
}
