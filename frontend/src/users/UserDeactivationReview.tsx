import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

type Work = { kind: string; id: number; branchId: number };
export type UserDeactivationPreview = {
  userId: number; version: string; accountStatus: string; workItems: Work[];
  hasRestrictedWork: boolean; hasMore: boolean;
  lastDurableAdministrator: boolean; coverageIncomplete: boolean; hasOpenResponsibilities: boolean;
};
const positive = (v: unknown): v is number => Number.isSafeInteger(v) && (v as number) > 0;
export function decodeUserDeactivationPreview(value: unknown, userId: number): UserDeactivationPreview | null {
  if (!value || typeof value !== "object") return null;
  const p = value as UserDeactivationPreview;
  if (p.userId !== userId || !/^[0-9A-F]{16}$/.test(p.version)
    || !["ACTIVE", "INACTIVE"].includes(p.accountStatus)
    || !Array.isArray(p.workItems) || p.workItems.length > 200
    || !p.workItems.every(w => w && typeof w.kind === "string" && positive(w.id) && positive(w.branchId))
    || [p.hasRestrictedWork, p.hasMore, p.lastDurableAdministrator, p.coverageIncomplete, p.hasOpenResponsibilities].some(v => typeof v !== "boolean")) return null;
  return p;
}

export function UserDeactivationReview({ userId, autoLoad = false }: { userId: number; autoLoad?: boolean }) {
  const { state } = useAuth();
  const [result, setResult] = useState<{ session: typeof state.session; userId: number; data: UserDeactivationPreview | null; error: string }>();
  const [reload, setReload] = useState(autoLoad ? 1 : 0);
  useEffect(() => {
    if (reload === 0 || state.status !== "AUTHENTICATED" || state.pending !== null || !state.session || !positive(userId)) return;
    const controller = new AbortController();
    let active = true;
    const session = state.session;
    setResult(undefined);
    void (async () => {
      try {
        const response = await fetch(`/api/users/${userId}/deactivation-preview`, {
          method: "GET", credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal,
        });
        const data = response.ok ? decodeUserDeactivationPreview(await response.json(), userId) : null;
        if (active) setResult({ session, userId, data, error: data ? "" : response.status === 403
          ? "Bạn không được kiểm tra ràng buộc vô hiệu hóa tài khoản này."
          : "Chưa kiểm tra được ràng buộc. Chưa thay đổi tài khoản." });
      } catch {
        if (active) setResult({ session, userId, data: null, error: "Không tải được kiểm tra ràng buộc. Chưa thay đổi tài khoản." });
      }
    })();
    return () => { active = false; controller.abort(); };
  }, [userId, state.session, state.status, state.pending, reload]);
  if (state.status !== "AUTHENTICATED" || state.pending !== null || !state.session) return null;
  const current = result?.session === state.session && result.userId === userId ? result : undefined;
  return <section aria-label="Các ràng buộc khi vô hiệu hóa tài khoản">
    <h2>Công việc cần xử lý trước khi vô hiệu hóa</h2>
    {reload === 0 ? <p>Kiểm tra công việc được giao trước khi yêu cầu đổi trạng thái tài khoản.</p>
      : !current ? <p role="status">Đang tải các ràng buộc…</p> : current.data ? <>
      {current.data.lastDurableAdministrator && <p role="alert">Đây là quản trị viên thường trực cuối cùng và không được vô hiệu hóa.</p>}
      {current.data.workItems.length > 0 ? <>
        <p>Nhờ người phụ trách phân công lại hoặc bàn giao các mục này trước khi vô hiệu hóa.</p>
        <ul>{current.data.workItems.map(w => <li key={`${w.kind}:${w.id}`}>
          {w.kind.replaceAll("_", " ")} #{w.id} — Chi nhánh {w.branchId}
        </li>)}</ul>
      </> : <p>Không có công việc đang mở trong phạm vi quản lý hiện tại.</p>}
      {current.data.hasRestrictedWork && <p>Còn trách nhiệm ngoài phạm vi quản lý của bạn. Liên hệ quản trị viên có quyền.</p>}
      {current.data.hasMore && <p>Hiển thị 200 mục đầu tiên. Xử lý và tải lại để tiếp tục.</p>}
      {current.data.hasOpenResponsibilities && current.data.workItems.length === 0 && <p>Cần xử lý các trách nhiệm khác của tài khoản trước khi vô hiệu hóa.</p>}
      {current.data.coverageIncomplete && <p>Chỉ có thể vô hiệu hóa sau khi hoàn tất mọi kiểm tra ràng buộc.</p>}
      <p>Xét duyệt này không cấp quyền xem hồ sơ công việc bên dưới.</p>
    </> : <p role="alert">{current.error}</p>}
    <button type="button" onClick={() => setReload(n => n + 1)}>{reload === 0 ? "Xem lại các ràng buộc" : "Tải lại các ràng buộc"}</button>
  </section>;
}
