import { displayCode } from "../i18n/vi";
﻿import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { Link, useParams } from "react-router-dom";
import { createRoleCatalogApi } from "./roleCatalogApi";
import {
  createCustomRoleApi,
  validCustomRole,
  validMetadata,
  customRoleMessage,
  type CustomRoleInput,
} from "./customRoleApi";
import { type RoleDto, id } from "./contracts";
const keys = new WeakMap<object, number>();
let n = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++n);
  return keys.get(x)!;
}
export function CustomRoleEditRoute({ deactivate = false }: { deactivate?: boolean }) {
  const { roleId = "" } = useParams();
  if (!/^[1-9][0-9]*$/.test(roleId) || !id(Number(roleId))) return <p role="alert">Mã vai trò không hợp lệ.</p>;
  return <CustomRoleForm roleId={Number(roleId)} dispatchEnabled deactivate={deactivate} />;
}
/** Both operations retain current-session binding and never replay a mutation. */
export function CustomRoleForm({
  roleId,
  dispatchEnabled = false,
  deactivate = false,
}: {
  roleId?: number;
  dispatchEnabled?: boolean;
  deactivate?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const [invalid, setInvalid] = useState<object | null>(null);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, roleId, dispatchEnabled, deactivate],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null ||
    invalid === identity
  )
    return (
      <section>
        <p>Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>
        <button onClick={() => void actions.revalidate()}>
          Xác minh lại phiên
        </button>
      </section>
    );
  if (roleId !== undefined && !id(roleId))
    return <p role="alert">Mã vai trò không hợp lệ.</p>;
  return (
    <Bound
      key={key(binding)}
      {...{ roleId, dispatchEnabled, current, deactivate }}
      invalidate={() => setInvalid(identity)}
      revalidate={() => void actions.revalidate()}
    />
  );
}
function Bound({
  roleId,
  dispatchEnabled,
  current,
  invalidate,
  deactivate,
  revalidate,
}: {
  roleId?: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  invalidate: () => void;
  deactivate: boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() =>
      createCustomRoleApi(globalThis.fetch.bind(globalThis), dispatchEnabled),
    ),
    [read] = useState(() => createRoleCatalogApi());
  const [input, setInput] = useState<CustomRoleInput>({
      roleCode: "",
      roleName: "",
      description: null,
    }),
    [review, setReview] = useState<CustomRoleInput | null>(null),
    [record, setRecord] = useState<RoleDto | null>(null),
    [version, setVersion] = useState<string | null>(null),
    [loading, setLoading] = useState(roleId !== undefined),
    [pending, setPending] = useState(false),
    [message, setMessage] = useState(""),
    [locked, setLocked] = useState(false),
    [duplicateCode, setDuplicateCode] = useState(false),
    [readError, setReadError] = useState(false);
  const mounted = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    if (roleId === undefined) return;
    const abort = new AbortController();
    let active = true;
    void api.metadata(roleId, abort.signal).then((r) => {
      if (!active || !current()) return;
      setLoading(false);
      if (!r.ok) {
        setReadError(true);
        setMessage("Vai trò chưa khả dụng hoặc không có quyền truy cập.");
        return;
      }
      setRecord(r.data.role);
      setVersion(r.data.etag);
      setInput({
        roleCode: r.data.role.roleCode,
        roleName: r.data.role.roleName,
        description: r.data.role.description,
      });
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [api, roleId, current]);
  async function create() {
    if (
      !review ||
      !dispatchEnabled ||
      attempted.current ||
      !mounted.current ||
      !current() || (deactivate && (!record?.isActive || roleId === undefined))
      || (roleId !== undefined && (!version || !record || record.isSystemRole))
    )
      return;
    attempted.current = true;
    setPending(true);
    controller.current = new AbortController();
    const result = roleId !== undefined ? await api.update(roleId,
      { roleName: review.roleName, description: review.description }, version!,
      () => mounted.current && current(), controller.current.signal, deactivate) : await api.create(
      review,
      () => mounted.current && current(),
      controller.current.signal,
    );
    if (!mounted.current || !current()) return;
    setPending(false);
    setLocked(true);
    setReview(null);
    if (result.outcome !== "CONFIRMED") {
      if (result.status === 401) {
        invalidate();
        return;
      }
      setMessage(customRoleMessage(result));
      // A confirmed rejection permits a corrected explicit submission. Uncertain outcomes stay locked.
      if (result.outcome === "REJECTED" && (result.reason === "ROLE_CODE_EXISTS"
        || result.status === 400)) {
        attempted.current = false;
        setLocked(false);
        setDuplicateCode(result.reason === "ROLE_CODE_EXISTS");
      }
      return;
    }
    if (deactivate) {
      setRecord(result.data);
      setMessage("Đã vô hiệu hóa vai trò. Vai trò khác và lịch sử giữ nguyên. Người dùng bị ảnh hưởng cần đăng nhập lại.");
      return;
    }
    setMessage(
      `Đã lưu vai trò tùy chỉnh ${result.data.roleId}. Đang tải lại chi tiết từ máy chủ…`,
    );
    const fresh = await read.detail(
      result.data.roleId,
      controller.current.signal,
    );
    if (!mounted.current || !current()) return;
    if (fresh.ok) {
      setRecord(fresh.data.role);
      setMessage(
        `Đã lưu vai trò tùy chỉnh ${fresh.data.role.roleId}. Đã tải lại chi tiết từ máy chủ.`,
      );
    } else {
      setRecord(null);
      setMessage(
        "Đã xác nhận lưu nhưng chưa tải lại được dữ liệu. Không gửi lại thao tác lưu.",
      );
    }
  }
  return (
    <main>
      <h1>
        {deactivate ? "Vô hiệu hóa vai trò tùy chỉnh" : roleId === undefined
          ? "Tạo vai trò tùy chỉnh"
          : "Chỉnh sửa thông tin vai trò tùy chỉnh"}
      </h1>
      <Link to="/roles">Quay lại vai trò</Link>
      {deactivate && <p>Xác nhận vô hiệu hóa. Người dùng mất quyền từ vai trò này và phiên hiện tại kết thúc. Vai trò khác vẫn giữ nguyên.</p>}
      {deactivate && locked && <button onClick={revalidate}>Xác minh lại phiên</button>}
      <p>
        Không tự cấp quyền hoặc phân công. Tên gọi không xác lập thẩm quyền.
      </p>
      {!dispatchEnabled && <p>Thao tác ghi đang tắt.</p>}
      {loading ? (
        <p>Đang tải vai trò...</p>
      ) : readError ? (
        <p role="alert">{message}</p>
      ) : (
        <>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (
                deactivate ? !!record?.isActive && !record.isSystemRole : roleId === undefined
                  ? validCustomRole(input)
                  : validMetadata(input.roleName, input.description)
              ) {
                setReview({ ...input });
                setMessage(
                  deactivate ? "Xác nhận ngừng hoạt động vai trò này." : roleId === undefined
                    ? "Xem lại thông tin trước khi xác nhận."
                    : "Xem lại thay đổi thông tin trước khi xác nhận.",
                );
              } else setMessage("Mã, tên hoặc thông tin bắt buộc không hợp lệ.");
            }}
          >
            {roleId === undefined && (
              <label>
                Mã vai trò
                <input
                  value={input.roleCode}
                  aria-invalid={duplicateCode || undefined}
                  aria-describedby={duplicateCode ? "role-code-conflict" : undefined}
                  disabled={pending || locked}
                  onChange={(e) => {
                    setInput({ ...input, roleCode: e.target.value });
                    setReview(null);
                    setDuplicateCode(false);
                  }}
                />
                {duplicateCode && <span id="role-code-conflict">MSG35: Mã vai trò đã tồn tại.</span>}
              </label>
            )}
            <label>
              Tên vai trò
              <input
                value={input.roleName}
                disabled={deactivate || pending || locked || record?.isSystemRole}
                onChange={(e) => {
                  setInput({ ...input, roleName: e.target.value });
                  setReview(null);
                }}
              />
            </label>
            <label>
              Mô tả
              <textarea
                value={input.description ?? ""}
                disabled={deactivate || pending || locked || record?.isSystemRole}
                onChange={(e) => {
                  setInput({ ...input, description: e.target.value });
                  setReview(null);
                }}
              />
            </label>
            {record?.isSystemRole ? (
              <p>Vai trò hệ thống được bảo vệ. Không được đổi thông tin.</p>
            ) : (
              <button disabled={pending || locked || (deactivate && !record?.isActive)}>{deactivate ? "Xem lại vô hiệu hóa" : "Xem lại thông tin mô tả"}</button>
            )}
          </form>
          {review && !locked && (
            <section aria-label="Xem lại thông tin">
              <p>
                {displayCode(review.roleCode)} — {review.roleName}
              </p>
              <p>{review.description}</p>
              <button
                disabled={!dispatchEnabled || pending || (roleId !== undefined && (!version || record?.isSystemRole))}
                onClick={() => void create()}
              >
                {deactivate ? "Xác nhận vô hiệu hóa" : roleId === undefined ? "Xác nhận tạo mới" : "Xác nhận cập nhật"}
              </button>
            </section>
          )}
          {record && (
            <section aria-label="Vai trò được máy chủ xác nhận">
              <p>
                {displayCode(record.roleCode)} — {record.roleName}
              </p>
              <p>
                {record.isSystemRole ? "Hệ thống" : "Tùy chỉnh"}; Quyền được cấp{" "}
                {record.permissionGrantCount}; Người dùng {record.assignedUserCount}
              </p>
            </section>
          )}
        </>
      )}
      {pending && <p role="status">Đang gửi…</p>}
      {message && <p role="status">{message}</p>}
    </main>
  );
}
