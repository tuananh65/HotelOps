import { displayCode } from "../i18n/vi";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { id } from "./contracts";
import { createRolePermissionConfigurationApi, type ConfigurationResult, type ConfiguredGrant } from "./rolePermissionConfigurationApi";
let generation = 0;
export function RolePermissionEditor() {
  const { roleId = "" } = useParams();
  const { state, actions } = useAuth();
  const binding = useMemo(() => ++generation, [roleId, state.session, state.status, state.pending]);
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword) return <Navigate to="/change-password" replace />;
  if (state.status !== "AUTHENTICATED" || !state.session || state.pending !== null)
    return <section>Cần phiên đăng nhập đã xác minh. <button onClick={() => void actions.revalidate()}>Xác minh lại phiên</button></section>;
  if (!/^[1-9][0-9]*$/.test(roleId) || !id(Number(roleId))) return <p role="alert">Mã vai trò không hợp lệ.</p>;
  return <Editor key={binding} roleId={Number(roleId)} revalidate={() => void actions.revalidate()} />;
}
function Editor({ roleId, revalidate }: { roleId: number; revalidate: () => void }) {
  const [api] = useState(() => createRolePermissionConfigurationApi());
  const [result, setResult] = useState<ConfigurationResult | null>(null);
  const [desired, setDesired] = useState<ConfiguredGrant[]>([]);
  const [review, setReview] = useState(false);
  const [pending, setPending] = useState(false);
  const [locked, setLocked] = useState(false);
  const [message, setMessage] = useState("");
  const mounted = useRef(false), attempted = useRef(false), writing = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    void api.read(roleId, controller.signal).then(value => {
      if (controller.signal.aborted || !mounted.current) return;
      setResult(value);
      if (value.ok) setDesired(value.data.grants);
    });
    return () => { mounted.current = false; controller.abort(); writing.current?.abort(); };
  }, [api, roleId]);
  async function save() {
    if (!review || !result?.ok || !result.data.role.isActive || attempted.current || !mounted.current) return;
    attempted.current = true; setPending(true);
    writing.current = new AbortController();
    const saved = await api.save(roleId, desired, result.etag, writing.current.signal, () => mounted.current);
    if (!mounted.current) return;
    setPending(false); setLocked(true); setReview(false);
    if (saved.ok) {
      setResult(saved);
      setMessage("Đã lưu quyền. Người dùng bị ảnh hưởng cần đăng nhập lại; phạm vi và quyền theo quy trình vẫn áp dụng.");
    } else if (!saved.unknown && saved.lastAdministrator) {
      // The transaction was rejected by the durable-admin guard. Return to
      // selection and require a fresh confirmation; never replay automatically.
      attempted.current = false;
      setLocked(false);
      setMessage("MSG03: Quản trị viên cuối cùng phải giữ quyền quản trị phân quyền. Sửa lựa chọn và kiểm tra lại.");
    } else setMessage(saved.unknown ? "Chưa xác định kết quả. Không gửi lại lần lưu này; nhờ quản trị viên có quyền kiểm tra hồ sơ trên máy chủ."
      : saved.status === 412 ? "Vai trò hoặc quyền đã đổi. Lựa chọn này chưa được lưu. Mở lại trình chỉnh sửa để xem dữ liệu hiện tại."
      : saved.status === 409 ? "Thay đổi bị từ chối: vai trò ngừng hoạt động hoặc đang bảo vệ quản trị viên cuối cùng."
      : "Lưu bị từ chối hoặc không có quyền. Xác minh lại phiên trước khi tiếp tục.");
  }
  if (!result) return <p role="status">Đang tải quyền của vai trò...</p>;
  if (!result.ok) return <section role="alert">Chưa có cấu hình quyền hoặc không có quyền truy cập. <button onClick={revalidate}>Xác minh lại phiên</button></section>;
  const { role, catalog } = result.data;
  const valid = desired.every(g => ["ALL", "BRANCH", "OWN"].includes(g.dataScope));
  return <main>
    <h1>Cấu hình quyền của vai trò</h1>
    <Link to={`/roles/${roleId}`}>Quay lại vai trò</Link>
    <h2>{displayCode(role.roleCode)} — {role.roleName}</h2>
    <p>Phạm vi vẫn phụ thuộc phân công chi nhánh, sở hữu đối tượng và quy trình. ALL không bỏ giới hạn của phân công gắn chi nhánh.</p>
    {!role.isActive && <p role="alert">Không thể cấu hình vai trò đã ngừng hoạt động.</p>}
    {!valid && <p role="alert">Phạm vi DEPARTMENT cũ không được hỗ trợ. Xóa hoặc thay thế rõ ràng trước khi lưu.</p>}
    <fieldset disabled={!role.isActive || pending || locked}>
      <legend>Quyền theo phân hệ</legend>
      {[...new Set(catalog.map(p => p.moduleCode))].map(module => <section key={module}>
        <h3>{module}</h3>
        {catalog.filter(p => p.moduleCode === module).map(p => <label key={p.permissionId} style={{ display: "block" }}>
          {p.permissionCode} — {p.permissionName}{" "}
          <select aria-label={`${p.permissionCode} Phạm vi`} value={desired.find(g => g.permissionId === p.permissionId)?.dataScope ?? ""}
            onChange={event => {
              const scope = event.target.value;
              setDesired(previous => [...previous.filter(g => g.permissionId !== p.permissionId), ...(scope ? [{ permissionId: p.permissionId, dataScope: scope }] : [])]);
              setReview(false);
            }}>
            <option value="">Chưa cấp quyền</option><option value="OWN">Hồ sơ của tôi</option><option value="BRANCH">Chi nhánh được phân công</option><option value="ALL">Tất cả trong phạm vi phân công</option>
            <option value="DEPARTMENT" disabled>Phạm vi DEPARTMENT cũ — không hỗ trợ</option>
          </select>
        </label>)}
      </section>)}
      <button disabled={!valid} onClick={() => setReview(true)}>Xem lại thay đổi</button>
    </fieldset>
    {review && !locked && <section aria-label="Xác nhận thay đổi quyền">
      <p>Lưu {desired.length} quyền, thay thế cấu hình hiện tại {result.data.grants.length}? Các quyền bỏ chọn sẽ bị xóa. Phiên đăng nhập bị ảnh hưởng sẽ kết thúc.</p>
      <ul>{desired.map(g => <li key={g.permissionId}>{catalog.find(p => p.permissionId === g.permissionId)?.permissionCode}: {displayCode(g.dataScope)}</li>)}</ul>
      <button disabled={pending || !valid} onClick={() => void save()}>Xác nhận quyền</button>
      <button disabled={pending} onClick={() => setReview(false)}>Hủy</button>
    </section>}
    {pending && <p role="status">Đang lưu…</p>}
    {message && <p role="status">{message}</p>}
    {locked && <button onClick={revalidate}>Xác minh lại phiên</button>}
  </main>;
}
