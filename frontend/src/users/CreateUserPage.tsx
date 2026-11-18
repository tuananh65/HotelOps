import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ArrowLeft, Plus } from "lucide-react";
import {
  AppButton,
  AppToast,
  AppInput,
  AppSelect,
} from "../components/design-system";
import { LoadingIndicator } from "../components/LoadingIndicator";
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
import { useUserOptions } from "./useUserOptions";
import "./user-workspace.css";

// Matches the approved UC-004 allowlist enforced by UserAccountCreationService.
const roleDepartments: Record<InitialRoleCode, string> = {
  HK_STAFF: "HK",
  HK_SUPERVISOR: "HK",
  MAINTENANCE_STAFF: "MT",
  MAINTENANCE_LEAD: "MT",
  STOREKEEPER: "WH",
  RECEPTION: "FO",
};
const roleLabels: Record<InitialRoleCode, string> = {
  HK_STAFF: "Nhân viên buồng phòng",
  HK_SUPERVISOR: "Giám sát buồng phòng",
  MAINTENANCE_STAFF: "Nhân viên kỹ thuật",
  MAINTENANCE_LEAD: "Trưởng nhóm kỹ thuật",
  STOREKEEPER: "Thủ kho",
  RECEPTION: "Lễ tân",
};
const fields = {
  fullName: { label: "Họ và tên", max: 200, placeholder: "Nguyễn Văn An" },
  employeeCode: {
    label: "Mã nhân viên",
    max: 40,
    placeholder: "Nhập mã nhân viên",
  },
  username: {
    label: "Tên đăng nhập",
    max: 100,
    placeholder: "Ví dụ: an.nguyen",
  },
  email: { label: "Email", max: 150, placeholder: "ten@khachsan.vn" },
  phone: { label: "Số điện thoại", max: 20, placeholder: "Không bắt buộc" },
};

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
    roleCode: "",
  });
  const [validation, setValidation] = useState("");
  const [toastDismissed, setToastDismissed] = useState(false);
  const handoff = useRef<CredentialHandoffHandle>(null);
  const branches = useUserOptions("branches", "", true);
  const branchId =
    draft.branchId ||
    (branches.items.length === 1 ? String(branches.items[0].id) : "");
  const departments = useUserOptions("departments", branchId, true);
  const roles = useUserOptions("roles", "", true);
  const branch = branches.items.find((item) => String(item.id) === branchId);
  const department = departments.items.find(
    (item) => String(item.id) === draft.departmentId,
  );
  const availableRoles = roles.items.filter(
    (item) =>
      Object.hasOwn(roleDepartments, item.code) &&
      roleDepartments[item.code as InitialRoleCode] === department?.code,
  );
  const selectedRole = availableRoles.find(
    (item) => item.code === draft.roleCode,
  );
  const { progress } = flow;
  const busy = progress.phase === "pending";
  const ready =
    !branches.loading &&
    !departments.loading &&
    !roles.loading &&
    !!branch &&
    !!department &&
    !!selectedRole;
  const canSubmit = !["pending", "unknown", "created"].includes(progress.phase);
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit) return;
    if (
      !ready ||
      Object.entries(fields).some(([key, field]) => {
        const value = draft[key as keyof typeof fields].trim();
        return value.length > field.max || (key !== "phone" && !value);
      })
    ) {
      setValidation(
        "Nhập đủ thông tin bắt buộc, chọn chi nhánh, bộ phận và vai trò phù hợp.",
      );
      return;
    }
    if (!event.currentTarget.reportValidity()) return;
    setValidation("");
    void flow.submit(
      {
        ...draft,
        branchId: branch.id,
        departmentId: department.id,
        roleCode: selectedRole.code as InitialRoleCode,
      },
      (data) => handoff.current?.receive(data),
    );
  }
  return (
    <main className="user-workspace user-create">
      <header className="user-page-heading">
        <div>
          <p className="user-eyebrow">Quản lý người dùng</p>
          <h1>Tạo tài khoản nhân viên</h1>
          <p>Nhập thông tin nhân viên và chọn nơi làm việc để cấp tài khoản.</p>
        </div>
        <Link to="/users">
          <ArrowLeft size={16} aria-hidden="true" /> Danh sách người dùng
        </Link>
      </header>
      {progress.phase !== "created" && progress.phase !== "unknown" && (
        <div className="user-create-layout">
          <form
            className="user-create-form"
            aria-label="Tạo tài khoản người dùng"
            onSubmit={submit}
          >
            <fieldset className="user-card" disabled={busy}>
              <legend>1. Thông tin nhân viên</legend>
              <p className="user-hint">Các trường có dấu * là bắt buộc.</p>
              <div className="user-grid">
                {(
                  Object.entries(fields) as [
                    keyof typeof fields,
                    (typeof fields)[keyof typeof fields],
                  ][]
                ).map(([key, field]) => {
                  const duplicate =
                    progress.phase === "rejected" &&
                    progress.error.duplicateFields?.some(
                      (value) => value === key,
                    );
                  return (
                    <label key={key}>
                      {field.label}
                      {key !== "phone" ? " *" : " (không bắt buộc)"}
                      <AppInput
                        name={key}
                        type={
                          key === "email"
                            ? "email"
                            : key === "phone"
                              ? "tel"
                              : "text"
                        }
                        value={draft[key]}
                        maxLength={field.max}
                        required={key !== "phone"}
                        placeholder={field.placeholder}
                        autoComplete="off"
                        aria-invalid={duplicate || undefined}
                        aria-describedby={
                          duplicate ? `create-${key}-duplicate` : undefined
                        }
                        onChange={(event) => {
                          setDraft({ ...draft, [key]: event.target.value });
                          setValidation("");
                        }}
                      />
                      {duplicate && (
                        <span
                          className="user-field-error"
                          id={`create-${key}-duplicate`}
                        >
                          {field.label} đã được sử dụng. Vui lòng nhập giá trị
                          khác.
                        </span>
                      )}
                    </label>
                  );
                })}
              </div>
            </fieldset>
            <fieldset className="user-card" disabled={busy}>
              <legend>2. Nơi làm việc và vai trò</legend>
              <p className="user-hint">
                Chọn chi nhánh trước, sau đó chọn bộ phận và vai trò của nhân
                viên.
              </p>
              <div className="user-grid">
                <label>
                  Chi nhánh *
                  <AppSelect
                    name="branchId"
                    required
                    value={branchId}
                    disabled={branches.loading || !!branches.error}
                    onChange={(event) =>
                      setDraft({
                        ...draft,
                        branchId: event.target.value,
                        departmentId: "",
                        roleCode: "",
                      })
                    }
                  >
                    <option value="">Chọn chi nhánh</option>
                    {branches.items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name}
                      </option>
                    ))}
                  </AppSelect>
                </label>
                <label>
                  Bộ phận *
                  <AppSelect
                    name="departmentId"
                    required
                    value={department ? draft.departmentId : ""}
                    disabled={
                      !branch || departments.loading || !!departments.error
                    }
                    onChange={(event) =>
                      setDraft({
                        ...draft,
                        departmentId: event.target.value,
                        roleCode: "",
                      })
                    }
                  >
                    <option value="">
                      {branch ? "Chọn bộ phận" : "Chọn chi nhánh trước"}
                    </option>
                    {departments.items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name}
                      </option>
                    ))}
                  </AppSelect>
                </label>
                <label>
                  Vai trò ban đầu *
                  <AppSelect
                    name="roleCode"
                    required
                    value={selectedRole ? draft.roleCode : ""}
                    disabled={!department || roles.loading || !!roles.error}
                    onChange={(event) =>
                      setDraft({ ...draft, roleCode: event.target.value })
                    }
                  >
                    <option value="">
                      {department ? "Chọn vai trò" : "Chọn bộ phận trước"}
                    </option>
                    {availableRoles.map((item) => (
                      <option key={item.id} value={item.code}>
                        {roleLabels[item.code as InitialRoleCode]}
                      </option>
                    ))}
                  </AppSelect>
                </label>
              </div>
              {(branches.loading || departments.loading || roles.loading) && (
                <LoadingIndicator
                  label="Đang tải nơi làm việc và vai trò"
                  compact
                />
              )}
              {[
                { name: "chi nhánh", lookup: branches },
                { name: "bộ phận", lookup: departments },
                { name: "vai trò", lookup: roles },
              ].map(
                ({ name, lookup }) =>
                  lookup.error && (
                    <div key={name}>
                      <p role="alert">
                        Danh sách {name}: {lookup.error}
                      </p>
                      <button type="button" onClick={lookup.reload}>
                        Tải lại {name}
                      </button>
                    </div>
                  ),
              )}
              {!branches.loading &&
                !branches.error &&
                !branches.items.length && (
                  <p role="status">
                    Chưa có chi nhánh hoạt động được phép chọn.
                  </p>
                )}
              {!!branch &&
                !departments.loading &&
                !departments.error &&
                !departments.items.length && (
                  <p role="status">Chi nhánh này chưa có bộ phận hoạt động.</p>
                )}
              {!!department &&
                !roles.loading &&
                !roles.error &&
                !availableRoles.length && (
                  <p role="status">
                    Bộ phận này chưa có vai trò được hỗ trợ khi tạo tài khoản.
                    Vui lòng chọn bộ phận khác hoặc liên hệ quản trị viên.
                  </p>
                )}
            </fieldset>
            {validation && <p role="alert">{validation}</p>}
            {(progress.phase === "rejected" ||
              progress.phase === "not-dispatched") && (
              <p role="alert">{createUserFailureMessage(progress.error)}</p>
            )}
            <div className="user-actions">
              <AppButton
                type="submit"
                variant="primary"
                icon={Plus}
                loading={busy}
                disabled={!canSubmit || !ready}
              >
                Tạo tài khoản
              </AppButton>
              {busy ? (
                <LoadingIndicator label="Đang tạo tài khoản" compact />
              ) : (
                <Link to="/users">Hủy và quay lại</Link>
              )}
            </div>
          </form>
          <aside className="user-card user-summary">
            <h2>Thông tin cấp tài khoản</h2>
            <dl>
              <dt>Nhân viên</dt>
              <dd>{draft.fullName.trim() || "Chưa nhập họ tên"}</dd>
              <dt>Chi nhánh</dt>
              <dd>{branch?.name || "Chưa chọn"}</dd>
              <dt>Bộ phận</dt>
              <dd>{department?.name || "Chưa chọn"}</dd>
              <dt>Vai trò</dt>
              <dd>
                {selectedRole
                  ? roleLabels[selectedRole.code as InitialRoleCode]
                  : "Chưa chọn"}
              </dd>
            </dl>
            <p className="user-hint">
              Sau khi tạo thành công, hệ thống cấp mật khẩu tạm thời để bạn bàn
              giao. Nhân viên cần đổi mật khẩu trong lần đăng nhập đầu tiên.
            </p>
          </aside>
        </div>
      )}
      {progress.phase === "unknown" && (
        <p role="alert">{unknownCreationMessage}</p>
      )}
      {progress.phase === "created" && (
        <p className="user-success" role="status">
          Tạo tài khoản thành công. Hãy bàn giao mật khẩu tạm thời cho nhân
          viên.
        </p>
      )}
      {progress.phase === "created" && !toastDismissed && (
        <AppToast
          message="Tạo tài khoản thành công."
          onClose={() => setToastDismissed(true)}
        />
      )}
      <InitialCredentialHandoff ref={handoff} />
    </main>
  );
}
