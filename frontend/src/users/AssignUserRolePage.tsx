import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useEffect } from "react";
import { createRoleCatalogApi } from "../roles/roleCatalogApi";
import type { RoleDto } from "../roles/contracts";
import {
  assignmentBranchId,
  normalizeAssignmentRole,
  unknownRoleAssignmentMessage,
  type useAssignUserRole,
} from "./useAssignUserRole";
import type { UserProfileDto } from "./userProfileContracts";
import type { UserAccessReviewDto } from "./contracts";

function ConfirmationForm({
  profile,
  access,
  assign,
}: {
  profile: UserProfileDto;
  access: UserAccessReviewDto | null;
  assign(
    roleCode: string,
    branchId: number | null,
    reactivateExisting: boolean,
  ): Promise<void>;
}) {
  const [role, setRole] = useState("");
  const [branch, setBranch] = useState("");
  const [globalScope, setGlobalScope] = useState(false);
  const [reactivateExisting, setReactivateExisting] = useState(false);
  const [review, setReview] = useState<{
    role: string;
    branch: number | null;
    reactivateExisting: boolean;
  } | null>(null);
  const reviewed = useRef<typeof review>(null);
  const [error, setError] = useState("");
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [catalogState, setCatalogState] = useState(
    "Đang tải danh mục vai trò đang hoạt động...",
  );
  useEffect(() => {
    const abort = new AbortController();
    const api = createRoleCatalogApi();
    void (async () => {
      const rows: RoleDto[] = [];
      for (let page = 1; page <= 80; page++) {
        const result = await api.roles(page, "", "true", abort.signal);
        if (abort.signal.aborted) return;
        if (!result.ok || result.data.total > 2000) {
          setCatalogState(
            "Chưa có danh mục vai trò đang hoạt động hoặc không có quyền truy cập.",
          );
          return;
        }
        rows.push(...result.data.items);
        if (page * result.data.pageSize >= result.data.total) {
          setRoles([...new Map(rows.map((row) => [row.roleId, row])).values()]);
          setCatalogState(
            rows.length ? "" : "Không có vai trò đang hoạt động khả dụng.",
          );
          return;
        }
      }
      setCatalogState("Danh mục vai trò vượt giới hạn trang hỗ trợ.");
    })();
    return () => abort.abort();
  }, []);
  function invalidate() {
    reviewed.current = null;
    setReview(null);
    setError("");
  }
  function preview() {
    invalidate();
    const roleCode = normalizeAssignmentRole(role),
      branchId = globalScope ? null : assignmentBranchId(branch);
    if (
      !roleCode ||
      (!globalScope && branchId === null) ||
      !roles.some((r) => r.roleCode === roleCode && r.isActive)
    ) {
      setError(
        "Chọn vai trò hoạt động cùng phạm vi toàn hệ thống hoặc mã chi nhánh dương trong quyền chi nhánh của người dùng.",
      );
      return;
    }
    const next = { role: roleCode, branch: branchId, reactivateExisting };
    reviewed.current = next;
    setReview(next);
  }
  function confirm() {
    const next = reviewed.current;
    if (!next) return;
    // Consume synchronously before any promise or React render.
    reviewed.current = null;
    setReview(null);
    void assign(next.role, next.branch, next.reactivateExisting);
  }
  return (
    <form
      aria-label="Phân công vai trò người dùng"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      {access && (
        <section aria-label="Vai trò đang được gán">
          <h2>Vai trò đã gán</h2>
          <p>
            Đọc lúc{" "}
            <time dateTime={access.evaluatedAtUtc}>
              {access.evaluatedAtUtc}
            </time>
            .
            {access.reviewerScope.isGlobal
              ? "Kiểm tra quyền toàn hệ thống."
              : "Chỉ hiển thị chi nhánh được phép truy cập."}{" "}
            Máy chủ kiểm tra lại điều kiện khi lưu.
          </p>
          {access.assignedRoles.length === 0 ? (
            <p>Không có phân công trong phạm vi này.</p>
          ) : (
            <table>
              <caption>
                Vai trò đã gán, gồm cả mục đã thu hồi hoặc hết hạn
              </caption>
              <thead>
                <tr>
                  <th>Vai trò</th>
                  <th>Chi nhánh</th>
                  <th>Phân công</th>
                  <th>Trạng thái vai trò</th>
                  <th>Thời điểm phân công (UTC)</th>
                  <th>Hết hạn (UTC)</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {access.assignedRoles.map((row) => (
                  <tr key={row.assignmentId}>
                    <td>
                      {row.code} — {row.name}
                    </td>
                    <td>{row.branchId ?? "Chưa gắn phạm vi"}</td>
                    <td>
                      {!row.assignmentActive
                        ? "Đã thu hồi"
                        : Date.parse(row.assignedAtUtc) >
                            Date.parse(access.evaluatedAtUtc)
                          ? "Đã lên lịch"
                          : row.expiresAtUtc &&
                              Date.parse(row.expiresAtUtc) <=
                                Date.parse(access.evaluatedAtUtc)
                            ? "Đã hết hạn"
                            : "Đang hoạt động"}
                    </td>
                    <td>
                      {row.roleActive ? "Đang hoạt động" : "Ngừng hoạt động"}
                    </td>
                    <td>{row.assignedAtUtc}</td>
                    <td>{row.expiresAtUtc ?? "Không có"}</td>
                    <td>
                      {row.assignmentActive && (
                        <Link
                          to={`/users/${profile.id}/roles/${row.assignmentId}/revoke`}
                        >
                          Thu hồi vai trò
                        </Link>
                      )}
                      {!row.assignmentActive && row.roleActive && (
                        <button
                          type="button"
                          disabled={!!catalogState}
                          onClick={() => {
                            setRole(row.code);
                            setBranch(
                              row.branchId === null ? "" : String(row.branchId),
                            );
                            setGlobalScope(row.branchId === null);
                            setReactivateExisting(true);
                            invalidate();
                          }}
                        >
                          Chọn để kích hoạt lại
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      )}
      <label>
        Mã vai trò
        <select
          name="roleCode"
          required
          disabled={!!catalogState}
          value={role}
          onChange={(e) => {
            setRole(e.target.value);
            invalidate();
          }}
        >
          <option value="">Chọn vai trò đang hoạt động</option>
          {roles.map((r) => (
            <option key={r.roleId} value={r.roleCode}>
              {displayCode(r.roleCode)} — {displayCode(r.roleName)}
            </option>
          ))}
        </select>
      </label>
      {catalogState && <p role="status">{catalogState}</p>}
      <label>
        Phạm vi phân công
        <select
          value={globalScope ? "global" : "branch"}
          onChange={(e) => {
            setGlobalScope(e.target.value === "global");
            invalidate();
          }}
        >
          <option value="branch">Một chi nhánh</option>
          <option value="global">Toàn hệ thống (chỉ quyền ALL hiện có)</option>
        </select>
      </label>
      <label>
        Mã chi nhánh
        <input
          name="branchId"
          type="text"
          inputMode="numeric"
          autoComplete="off"
          required
          disabled={globalScope}
          value={branch}
          onChange={(e) => {
            setBranch(e.target.value);
            invalidate();
          }}
        />
      </label>
      <label>
        <input
          type="checkbox"
          checked={reactivateExisting}
          onChange={(e) => {
            setReactivateExisting(e.target.checked);
            invalidate();
          }}
        />
        Kích hoạt lại phân công đã thu hồi cho vai trò và phạm vi này
      </label>
      <button type="button" onClick={preview} disabled={!!catalogState}>
        Xem lại phân công
      </button>
      {review && (
        <ActionDialog
          open
          title="Xem lại gán vai trò"
          onClose={() => {
            setReview(null);
            reviewed.current = null;
          }}
        >
          <section aria-label="Xem lại gán vai trò">
            <p>Mã tài khoản: {profile.id}</p>
            <p>Họ và tên: {profile.fullName}</p>
            <p>Email: {profile.email}</p>
            <p>Mã vai trò: {review.role}</p>
            <p>
              Phạm vi:{" "}
              {review.branch === null
                ? "Toàn hệ thống"
                : `Chi nhánh ${review.branch}`}
            </p>
            <p>
              Thao tác:{" "}
              {review.reactivateExisting
                ? "Kích hoạt lại phân công đã thu hồi"
                : "Tạo phân công mới"}
            </p>
            <p>
              {review.branch === null
                ? "Gán toàn hệ thống cho phép quyền ALL hiện có của vai trò áp dụng qua các chi nhánh. Quyền BRANCH và OWN không trở thành toàn hệ thống."
                : "Phân công chỉ áp dụng trong chi nhánh này."}{" "}
              Các vai trò hiện có vẫn được giữ nguyên.
              {review.reactivateExisting
                ? "Quyền trước được giữ trong nhật ký. Không kích hoạt lại phân công hiện tại hoặc đã lên lịch."
                : "Vai trò và phạm vi đã tồn tại; chỉ kích hoạt lại nếu phân công đã bị thu hồi."}
            </p>
            <button data-dialog-action type="button" onClick={confirm}>
              Xác nhận phân công vai trò
            </button>
          </section>
        </ActionDialog>
      )}
      {error && <p role="alert">{error}</p>}
    </form>
  );
}

export function AssignUserRolePage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useAssignUserRole>;
}) {
  const { snapshot, progress } = flow;
  const locked = ["pending", "unknown", "stale", "confirmed"].includes(
    progress.phase,
  );
  const assignment =
    progress.phase === "confirmed" ? progress.assignment : undefined;
  return (
    <main>
      <h1>Gán vai trò cho người dùng {target}</h1>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <Link to={`/users/${target}`}>Xem lại quyền truy cập có hiệu lực</Link>
      <p>
        Tải hồ sơ không cấp quyền gán vai trò. Khi gửi, máy chủ kiểm tra điều
        kiện tài khoản, vai trò hoạt động, gán trùng, quyền chi nhánh, người
        thao tác và bảo vệ quản trị viên.
      </p>
      <p>
        Vai trò sẽ được bổ sung cho người dùng. Mọi thao tác vẫn kiểm tra phạm
        vi và phân tách trách nhiệm. Các phiên của người dùng sẽ kết thúc sau
        khi gán.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownRoleAssignmentMessage}</p>
          <p>
            Dữ liệu hồ sơ chỉ xác nhận danh tính và phiên bản, không xác nhận
            yêu cầu trước đã lưu hay hoàn tác. Thao tác gán vẫn bị khóa tại đây.
          </p>
        </>
      )}
      {assignment && (
        <section aria-label="Gán vai trò đã xác nhận" role="status">
          <p>
            Máy chủ đã xác nhận gán vai trò. Biên nhận này không bảo đảm quyền
            thực hiện mọi yêu cầu nghiệp vụ.
          </p>
          <p>mã: {assignment.id}</p>
          <p>Mã người dùng: {assignment.userId}</p>
          <p>Mã vai trò: {displayCode(assignment.roleCode)}</p>
          <p>
            Phạm vi:{" "}
            {assignment.branchId === null
              ? "Toàn hệ thống"
              : `Chi nhánh ${assignment.branchId}`}
          </p>
          <p>Người phân công: {assignment.assignedBy}</p>
          <p>Phân công lúc (UTC): {assignment.assignedAtUtc}</p>
          <p>Đang hoạt động: {String(assignment.isActive)}</p>
          <p>Hết hạn lúc (UTC): {String(assignment.expiresAtUtc)}</p>
        </section>
      )}
      {progress.phase === "pending" && (
        <p role="status">Đang chờ yêu cầu gán vai trò…</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "Không gửi yêu cầu gán vai trò."
            : ""}
          {progress.message}
        </p>
      )}
      {flow.error && <p role="alert">{flow.error}</p>}
      {flow.loading && <p role="status">Đang tải hồ sơ cá nhân...</p>}
      {snapshot && (
        <section aria-label="Bản chụp thông tin quản lý từ máy chủ">
          <p>Mã tài khoản: {snapshot.profile.id}</p>
          <p>Họ và tên: {snapshot.profile.fullName}</p>
          <p>Email: {snapshot.profile.email}</p>
          {snapshot.accessMessage && (
            <p role="status">{snapshot.accessMessage}</p>
          )}
          {!locked && (
            <ConfirmationForm
              key={snapshot.revision}
              profile={snapshot.profile}
              access={snapshot.access}
              assign={flow.assign}
            />
          )}
        </section>
      )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "pending"}
        onClick={flow.reload}
      >
        Tải lại hồ sơ cá nhân
      </button>
    </main>
  );
}
