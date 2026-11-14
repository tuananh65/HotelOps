import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createWorkdayApi, type WorkdayApi } from "./workdayApi";
import { safeId, type ReadFailure, type ReadResult } from "./contracts";

const messages: Record<ReadFailure, string> = {
  "invalid-request": "Chưa có chi nhánh hợp lệ.",
  "invalid-response": "Dữ liệu phản hồi không hợp lệ.",
  unauthorized: "Phiên không còn hợp lệ. Vui lòng xác minh phiên đăng nhập.",
  forbidden: "Máy chủ từ chối quyền truy cập.",
  unavailable: "Workday API chưa sẵn sàng. Không thể xác định ca hoặc workday.",
  network: "Không thể kết nối máy chủ.",
  cancelled: "Yêu cầu đã hủy.",
  http: "Không thể đọc dữ liệu workday.",
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
    <section aria-label={title}>
      <h2>{title}</h2>
      {!view || view.kind === "loading" ? (
        <p role="status">Đang tải…</p>
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

/** Source-only component; mount under the existing AuthProvider and router.
 * branchId is a candidate, NOT authority. Each GET independently authorizes
 * its own operation on the server. No permission is inferred from another GET.
 * Both responses are bound to the confirmed session and requested branch.
 */
export function WorkdayReadPanel({
  branchId,
  client: injectedClient,
}: {
  branchId: number;
  client?: WorkdayApi;
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
  const shifts = useRead(key, enabled, branchId, client.ownShifts);
  const current = useRead(key, enabled, branchId, client.current);

  if (!confirmed)
    return (
      <section>
        <h1>Ca làm và workday của tôi</h1>
        <p>Chưa xác nhận phiên đăng nhập không hạn chế.</p>
        <Link to="/login">Đăng nhập</Link>
        <Link to="/change-password">Đổi mật khẩu</Link>
      </section>
    );
  if (!safeId(branchId))
    return <p role="alert">{messages["invalid-request"]}</p>;
  return (
    <section>
      <h1>Ca làm và workday của tôi</h1>
      <p>Chi nhánh {branchId}. Backend Workday đang chờ kích hoạt.</p>
      <nav>
        <Link to="/profile">Hồ sơ</Link>{" "}
        <Link to="/hotel">Cấu trúc khách sạn</Link>
      </nav>
      <ReadSection
        title="Ca được phân công trong ngày"
        result={shifts}
        revalidate={() => void actions.revalidate()}
      >
        {(data) =>
          data.length === 0 ? (
            <p>Không có ca đủ điều kiện trong ngày.</p>
          ) : (
            <ul>
              {data.map((s) => (
                <li key={s.shiftAssignmentId}>
                  Ca {s.shiftId}: {s.shiftDate}, {s.scheduledStartAtUtc} –{" "}
                  {s.scheduledEndAtUtc}
                </li>
              ))}
            </ul>
          )
        }
      </ReadSection>
      <ReadSection
        title="Workday hiện tại"
        result={current}
        revalidate={() => void actions.revalidate()}
      >
        {(data) =>
          data === null ? (
            <p>Không có workday đang làm.</p>
          ) : (
            <p>
              Workday {data.workdayId}, ngày {data.workDate}, bắt đầu{" "}
              {data.startedAtUtc}
            </p>
          )
        }
      </ReadSection>
    </section>
  );
}
