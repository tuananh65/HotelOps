import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { decimalId } from "../hotel/contracts";
import {
  createDeactivationPreviewApi,
  type DependencyPreview,
} from "./deactivationPreviewApi";
import type { Failure, StructureKind } from "./contracts";
const identities = new WeakMap<object, number>();
let sequence = 0;
function identityKey(value: object) {
  if (!identities.has(value)) identities.set(value, ++sequence);
  return identities.get(value)!;
}
export function StructureDependencyReview({ kind }: { kind: StructureKind }) {
  const { state, actions } = useAuth();
  const { id: rawId } = useParams();
  const location = useLocation();
  const branchId = decimalId(
    new URLSearchParams(location.search).get("branchId") ?? "",
  );
  const id = decimalId(rawId ?? "");
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, kind, branchId, id],
  );
  const latest = useRef(identity);
  latest.current = identity;
  const current = useMemo(() => () => latest.current === identity, [identity]);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate to="/change-password" replace />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <p role="status">Xác minh phiên đăng nhập trước khi kiểm tra ràng buộc.</p>
    );
  if (!branchId || !id)
    return <p role="alert">Chọn mã chi nhánh và vị trí hợp lệ.</p>;
  return (
    <Review
      key={identityKey(identity)}
      {...{ kind, branchId, id, current }}
      revalidate={() => void actions.revalidate()}
    />
  );
}
const labels: Record<string, string> = {
  ACTIVE_ROOM: "Phòng đang hoạt động",
  ACTIVE_CHILD_AREA: "Khu vực con đang hoạt động",
  ACTIVE_ASSET: "Tài sản đang hoạt động",
  OPEN_INCIDENT: "Sự cố đang mở",
  OPEN_OPERATIONAL_TASK: "Công việc đang mở",
  TASK_COMPLETION_CONTRACT_UNRESOLVED: "Công việc cần xét duyệt hoàn tất",
  OPEN_MAINTENANCE_WORK: "Mở công việc bảo trì",
};
function Review({
  kind,
  branchId,
  id,
  current,
  revalidate,
}: {
  kind: StructureKind;
  branchId: number;
  id: number;
  current: () => boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() => createDeactivationPreviewApi());
  const [data, setData] = useState<DependencyPreview | null>(null);
  const [error, setError] = useState<Failure | null>(null);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const abort = new AbortController();
    let active = true;
    setData(null);
    setError(null);
    setLoading(true);
    void api.read(kind, branchId, id, abort.signal).then((result) => {
      if (!active || !current()) return;
      setLoading(false);
      if (result.ok) setData(result.data);
      else setError(result.error);
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [api, kind, branchId, id, current, reload]);
  return (
    <main>
      <h1>Xem xét {kind} Các ràng buộc</h1>
      <Link to={`/${kind}s?branchId=${branchId}`}>Quay lại vị trí</Link>
      <p>
        Xét duyệt này không vô hiệu hóa vị trí. Thao tác vẫn bị khóa chờ nghiệm thu backend.
      </p>
      {loading && <p role="status">Đang tải các ràng buộc...</p>}
      {error && (
        <p role="alert">
          {error.status === 401
            ? "Phiên đăng nhập không hợp lệ. Kiểm tra phiên trước khi tiếp tục."
            : error.status === 403
              ? "Bạn không có quyền xét duyệt vị trí này."
              : "Chưa thể kiểm tra ràng buộc. Chưa thực hiện thao tác nào."}
        </p>
      )}
      {data && (
        <section aria-label="Ràng buộc vị trí">
          <h2>{data.record.name}</h2>
          <p>
            {data.record.code} / Branch {data.record.branchId} /{" "}
            {data.record.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
          </p>
          {data.dependencies.length ? (
            <ul>
              {data.dependencies.map((d) => (
                <li key={d.kind}>
                  {labels[d.kind]}: {d.count}
                </li>
              ))}
            </ul>
          ) : (
            <p>Lần đọc này không ghi nhận ràng buộc.</p>
          )}
          <p>
            Ràng buộc có thể đổi sau khi đọc. Danh sách trống không đồng nghĩa được phép vô hiệu hóa.
          </p>
        </section>
      )}
      <button
        disabled={loading || error?.status === 401}
        onClick={() => setReload((value) => value + 1)}
      >
        Tải lại các ràng buộc
      </button>
      {error?.status === 401 && (
        <button onClick={revalidate}>Kiểm tra phiên đăng nhập</button>
      )}
    </main>
  );
}
