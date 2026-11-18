import { displayCode } from "../i18n/vi";
import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { InitialRoleCode } from "./createUserContracts";
import {
  InitialCredentialHandoff,
  type CredentialHandoffHandle,
} from "./InitialCredentialHandoff";
import {
  createUserFailureMessage,
  unknownCreationMessage,
  type useCreateUser,
} from "./useCreateUser";

const roles: readonly InitialRoleCode[] = [
  "HK_STAFF",
  "HK_SUPERVISOR",
  "MAINTENANCE_STAFF",
  "MAINTENANCE_LEAD",
  "STOREKEEPER",
  "RECEPTION",
];
const labels = {
  employeeCode: "Mã nhân viên",
  username: "Tên đăng nhập",
  fullName: "Họ và tên",
  email: "Email",
  phone: "Số điện thoại (không bắt buộc)",
  branchId: "Mã chi nhánh",
  departmentId: "Mã bộ phận",
} as const;
const limits = {
  employeeCode: 40,
  username: 100,
  fullName: 200,
  email: 150,
  phone: 20,
};
function numericId(value: string) {
  const trimmed = value.trim();
  return /^\d+$/.test(trimmed) &&
    Number.isSafeInteger(Number(trimmed)) &&
    Number(trimmed) > 0
    ? Number(trimmed)
    : undefined;
}

export function CreateUserPage({
  flow,
}: {
  flow: ReturnType<typeof useCreateUser>;
}) {
  const [draft, setDraft] = useState({
    employeeCode: "",
    username: "",
    fullName: "",
    email: "",
    phone: "",
    branchId: "",
    departmentId: "",
    roleCode: "HK_STAFF" as InitialRoleCode,
  });
  const [validation, setValidation] = useState("");
  const handoff = useRef<CredentialHandoffHandle>(null);
  const { progress } = flow;
  const canSubmit = !["pending", "unknown", "created"].includes(progress.phase);
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!canSubmit) return;
    const branchId = numericId(draft.branchId),
      departmentId = numericId(draft.departmentId);
    if (
      !branchId ||
      !departmentId ||
      !roles.includes(draft.roleCode) ||
      Object.entries(limits).some(([key, max]) => {
        const value = draft[key as keyof typeof limits].trim();
        return value.length > max || (key !== "phone" && !value);
      })
    ) {
      setValidation(
        "Nhập đủ trường bắt buộc trong giới hạn cho phép; mã chi nhánh và bộ phận phải là số nguyên dương hợp lệ.",
      );
      return;
    }
    setValidation("");
    void flow.submit({ ...draft, branchId, departmentId }, (data) =>
      handoff.current?.receive(data),
    );
  }
  return (
    <main>
      <h1>Tạo tài khoản người dùng</h1>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <p>
        Máy chủ quyết định có thể tạo tài khoản và kiểm tra chi nhánh, bộ phận, vai trò ban đầu.
      </p>
      {progress.phase !== "created" && progress.phase !== "unknown" && (
        <form aria-label="Tạo tài khoản người dùng" onSubmit={submit} noValidate>
          <fieldset disabled={progress.phase === "pending"}>
            <legend>Tài khoản nhân viên</legend>
            {(Object.keys(labels) as (keyof typeof labels)[]).map((key) => (
              <label key={key}>
                {labels[key]}
                <input
                  name={key}
                  type={key === "email" ? "email" : "text"}
                  aria-invalid={progress.phase === "rejected"
                    && progress.error.duplicateFields?.some(field => field === key) || undefined}
                  aria-describedby={progress.phase === "rejected"
                    && progress.error.duplicateFields?.some(field => field === key)
                    ? `create-${key}-duplicate` : undefined}
                  value={draft[key]}
                  required={key !== "phone"}
                  maxLength={
                    key in limits
                      ? limits[key as keyof typeof limits]
                      : undefined
                  }
                  inputMode={
                    key === "branchId" || key === "departmentId"
                      ? "numeric"
                      : undefined
                  }
                  autoComplete="off"
                  onChange={(event) =>
                    setDraft({ ...draft, [key]: event.target.value })
                  }
                />
                {progress.phase === "rejected" && progress.error.duplicateFields?.some(field => field === key)
                  && <span id={`create-${key}-duplicate`}>MSG35: {labels[key]} đã được sử dụng.</span>}
              </label>
            ))}
            <label>
              Vai trò ban đầu
              <select
                aria-label="Vai trò ban đầu"
                value={draft.roleCode}
                onChange={(event) =>
                  setDraft({
                    ...draft,
                    roleCode: event.target.value as InitialRoleCode,
                  })
                }
              >
                {roles.map((role) => (
                  <option key={role} value={role}>
                    {displayCode(role)}
                  </option>
                ))}
              </select>
            </label>
            <button type="submit">
              {progress.phase === "pending"
                ? "Đang tạo tài khoản..."
                : "Tạo tài khoản"}
            </button>
          </fieldset>
        </form>
      )}
      {validation && <p role="alert">{validation}</p>}
      {(progress.phase === "rejected" ||
        progress.phase === "not-dispatched") && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "Không gửi yêu cầu tạo tài khoản."
            : ""}
          {createUserFailureMessage(progress.error)}
        </p>
      )}
      {progress.phase === "unknown" && (
        <p role="alert">{unknownCreationMessage}</p>
      )}
      {progress.phase === "created" && (
        <p>
          Đã xác nhận tạo tài khoản. Nếu không còn mật khẩu trên trang này, nhờ quản trị viên đặt lại; không tạo lại tài khoản.
        </p>
      )}
      <InitialCredentialHandoff ref={handoff} />
    </main>
  );
}
