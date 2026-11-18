import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useCallback, useState, type FormEvent, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import type {
  UserAccountDetailDto,
  UserAccessReviewDto,
  UserAccessReviewQuery,
} from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";
import { useUserRead, usersReadMessage } from "./useUserRead";
import { UserDeactivationReview } from "./UserDeactivationReview";

function Account({ account }: { account: UserAccountDetailDto }) {
  return (
    <dl>
      {Object.entries({
        ID: account.id,
        "Employee code": account.employeeCode,
        Username: account.username,
        "Full name": account.fullName,
        Email: account.email,
        Phone: account.phone ?? "Chưa cung cấp",
        "Account status": account.accountStatus,
        "Primary branch":
          account.primaryBranch?.name ?? "Không có trong phản hồi này",
        Department: account.department?.name ?? "Không có trong phản hồi này",
      }).map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}
function Table({
  label,
  columns,
  rows,
}: {
  label: string;
  columns: readonly string[];
  rows: readonly (readonly ReactNode[])[];
}) {
  return (
    <section>
      <h3>{label}</h3>
      {rows.length === 0 ? (
        <p>MSG10: Không có hồ sơ phù hợp bộ lọc đã áp dụng.</p>
      ) : (
        <table>
          <caption>{label}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column} scope="col">
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={index}>
                {row.map((cell, cellIndex) => (
                  <td key={cellIndex}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
function Report({ report }: { report: UserAccessReviewDto }) {
  return (
    <>
      <p>
        Thời điểm đánh giá{" "}
        <time dateTime={report.evaluatedAtUtc}>{report.evaluatedAtUtc}</time>
      </p>
      <p>
        {report.reviewerScope.isGlobal
          ? "Phạm vi xét duyệt toàn hệ thống"
          : `Báo cáo giới hạn chi nhánh: ${report.reviewerScope.branchIds.join(", ")}`}
      </p>
      <p>
        Báo cáo này không cấp quyền thực hiện thao tác. Phạm vi OWN vẫn cần xác
        minh sở hữu đối tượng.
      </p>
      <section aria-label="Tài khoản cần kiểm tra quyền">
        <h3>Tài khoản cần kiểm tra quyền</h3>
        <Account account={report.account} />
      </section>
      <p>
        Hồ sơ phân công có thể gồm mục ngừng hoạt động hoặc lịch sử. Quyền có
        hiệu lực được liệt kê riêng.
      </p>
      <Table
        label="Assigned roles"
        columns={[
          "Mã phân công",
          "Mã vai trò",
          "Vai trò",
          "Mã chi nhánh",
          "Phân công đang hiệu lực",
          "Vai trò đang hoạt động",
          "Thời điểm phân công (UTC)",
          "Hết hạn (UTC)",
        ]}
        rows={report.assignedRoles.map((role) => [
          role.assignmentId,
          role.roleId,
          `${role.code} — ${role.name}`,
          role.branchId ?? "Chưa gắn phạm vi",
          String(role.assignmentActive),
          String(role.roleActive),
          role.assignedAtUtc,
          role.expiresAtUtc ?? "Không có",
        ])}
      />
      <Table
        label="Branch memberships"
        columns={[
          "Mã chi nhánh",
          "Mã",
          "Tên",
          "Mức truy cập",
          "Chi nhánh đang hoạt động",
          "Cấp lúc (UTC)",
          "Thu hồi lúc (UTC)",
        ]}
        rows={report.branchMemberships.map((member) => [
          member.branchId,
          member.code,
          member.name,
          member.accessLevel,
          String(member.branchActive),
          member.grantedAtUtc,
          member.revokedAtUtc ?? "Không có",
        ])}
      />
      <Table
        label="Configured grants"
        columns={[
          "Quyền",
          "Chức năng",
          "Phạm vi đã cấu hình",
          "Mã phân công",
          "Vai trò",
          "Mã chi nhánh",
          "Có hiệu lực",
        ]}
        rows={report.configuredGrants.map((grant) => [
          `${grant.permissionCode} — ${grant.permissionName}`,
          grant.feature ?? "Chưa cung cấp",
          grant.configuredScope,
          grant.assignmentId,
          grant.roleCode,
          grant.branchId ?? "Chưa gắn phạm vi",
          String(grant.isEffective),
        ])}
      />
      {report.departmentMembershipsAvailable ? (
        <Table
          label="Department memberships"
          columns={[
            "Mã chi nhánh",
            "Mã bộ phận",
            "Bộ phận",
            "Đang hoạt động",
            "Cấp lúc (UTC)",
            "Thu hồi lúc (UTC)",
          ]}
          rows={report.departmentMemberships.map((m) => [
            m.branchId,
            m.departmentId,
            `${m.code} — ${m.name}`,
            String(m.departmentActive),
            m.grantedAtUtc,
            m.revokedAtUtc ?? "Không có",
          ])}
        />
      ) : (
        <p>
          Dữ liệu phân công bộ phận chưa khả dụng. Bộ phận chính không chứng
          minh tư cách thành viên hoặc quyền quản lý.
        </p>
      )}
      <Table
        label="Effective permissions"
        columns={["Quyền", "Chức năng", "Nguồn từ máy chủ"]}
        rows={report.effectivePermissions.map((permission) => [
          `${permission.code} — ${permission.name}`,
          permission.feature ?? "Chưa cung cấp",
          <ul>
            {permission.sources.map((source, index) => (
              <li key={index}>
                Phân công {source.assignmentId}; Vai trò{" "}
                {displayCode(source.roleCode)}; Phạm vi {source.scope}; Chi
                nhánh {source.branchId ?? "Chưa gắn phạm vi"};{" "}
                {source.requiresResourceOwnership
                  ? "Cần sở hữu đối tượng"
                  : "Nguồn này không yêu cầu sở hữu đối tượng"}
              </li>
            ))}
          </ul>,
        ])}
      />
    </>
  );
}
function UserReads({
  userId,
  roleAssignmentEnabled,
  accountActivationEnabled,
  accountManagementEnabled,
}: {
  userId: number;
  roleAssignmentEnabled: boolean;
  accountActivationEnabled: boolean;
  accountManagementEnabled: boolean;
}) {
  const { api } = useUsersContext();
  const [draft, setDraft] = useState({ feature: "", permissionCode: "" });
  const [query, setQuery] = useState<UserAccessReviewQuery>({});
  const loadDetail = useCallback(
    (signal: AbortSignal) => api.getUser(userId, signal),
    [api, userId],
  );
  const loadReview = useCallback(
    (signal: AbortSignal) => api.getEffectiveAccess(userId, query, signal),
    [api, userId, query],
  );
  const detail = useUserRead(`Chi tiết:${userId}`, loadDetail);
  const review = useUserRead(
    `Xem xét:${userId}:${JSON.stringify(query)}`,
    loadReview,
  );
  function filter(event: FormEvent) {
    event.preventDefault();
    setQuery({
      feature: draft.feature || undefined,
      permissionCode: draft.permissionCode || undefined,
    });
  }
  return (
    <main>
      <header>
        <h1>Tài khoản người dùng {userId}</h1>
        <Link to="/users">Danh mục người dùng</Link>
        <Link to="/profile">Hồ sơ cá nhân</Link>
        {accountManagementEnabled && (
          <>
            <Link to={`/users/${userId}/edit`}>Cập nhật tài khoản</Link>
            <Link to={`/users/${userId}/reset-credential`}>
              Đặt lại mật khẩu
            </Link>
            <Link to={`/users/${userId}/branches/add`}>
              Thêm phân quyền chi nhánh
            </Link>
            <Link to={`/users/${userId}/departments`}>Phân công bộ phận</Link>
            <Link to={`/users/${userId}/deactivate`}>
              Vô hiệu hóa tài khoản
            </Link>
          </>
        )}
        {roleAssignmentEnabled && (
          <Link to={`/users/${userId}/roles/assign`}>Phân công vai trò</Link>
        )}
        {accountActivationEnabled && (
          <Link to={`/users/${userId}/activate`}>Kích hoạt tài khoản</Link>
        )}
      </header>
      <section aria-label="Thông tin tài khoản cơ bản">
        <h2>Thông tin tài khoản cơ bản</h2>
        {detail.result === null ? (
          <p role="status">Đang tải chi tiết tài khoản...</p>
        ) : detail.result.ok ? (
          <>
            <Account account={detail.result.data} />
          </>
        ) : (
          <>
            <p role="alert">{usersReadMessage(detail.result.error)}</p>
            <button type="button" onClick={detail.reload}>
              Tải lại chi tiết
            </button>
          </>
        )}
      </section>
      {accountManagementEnabled && <UserDeactivationReview userId={userId} />}
      <section aria-label="Kiểm tra quyền truy cập">
        <h2>Kiểm tra quyền truy cập</h2>
        <CollapsibleFilterPanel
          activeCount={Object.values(query).filter(Boolean).length}
          onOpen={() =>
            setDraft({
              feature: query.feature ?? "",
              permissionCode: query.permissionCode ?? "",
            })
          }
          onClose={() =>
            setDraft({
              feature: query.feature ?? "",
              permissionCode: query.permissionCode ?? "",
            })
          }
        >
          <form onSubmit={filter} aria-label="Bộ lọc kiểm tra quyền">
            <label>
              Chức năng
              <input
                value={draft.feature}
                maxLength={60}
                onChange={(event) =>
                  setDraft({ ...draft, feature: event.target.value })
                }
              />
            </label>
            <label>
              Mã quyền
              <input
                value={draft.permissionCode}
                maxLength={100}
                onChange={(event) =>
                  setDraft({ ...draft, permissionCode: event.target.value })
                }
              />
            </label>
            <button type="submit">Áp dụng bộ lọc xét duyệt</button>
          </form>
        </CollapsibleFilterPanel>
        <p>
          Chức năng đã áp dụng: {query.feature?.trim() || "Tất cả"}; Quyền:{" "}
          {query.permissionCode?.trim() || "Tất cả"}.
        </p>
        {review.result === null ? (
          <p role="status">Đang tải kiểm tra quyền truy cập...</p>
        ) : review.result.ok ? (
          <Report report={review.result.data} />
        ) : (
          <>
            <p role="alert">{usersReadMessage(review.result.error)}</p>
            <button type="button" onClick={review.reload}>
              Thử xét duyệt lại
            </button>
          </>
        )}
      </section>
    </main>
  );
}

export function UserAccessReviewPage({
  roleAssignmentEnabled = false,
  accountActivationEnabled = false,
  accountManagementEnabled = false,
}: {
  roleAssignmentEnabled?: boolean;
  accountActivationEnabled?: boolean;
  accountManagementEnabled?: boolean;
} = {}) {
  const { id } = useParams();
  const numeric = id !== undefined && /^\d+$/.test(id) ? Number(id) : NaN;
  if (!Number.isSafeInteger(numeric) || numeric <= 0)
    return (
      <main>
        <h1>Mã người dùng không hợp lệ</h1>
        <p role="alert">
          Mã người dùng phải là số nguyên dương trong giới hạn cho phép.
        </p>
        <Link to="/users">Danh mục người dùng</Link>
      </main>
    );
  return (
    <UserReads
      key={numeric}
      userId={numeric}
      roleAssignmentEnabled={roleAssignmentEnabled}
      accountActivationEnabled={accountActivationEnabled}
      accountManagementEnabled={accountManagementEnabled}
    />
  );
}
