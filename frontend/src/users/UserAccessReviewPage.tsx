import { useCallback, useState, type FormEvent, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import type {
  UserAccountDetailDto,
  UserAccessReviewDto,
  UserAccessReviewQuery,
} from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";
import { useUserRead, usersReadMessage } from "./useUserRead";

function Account({ account }: { account: UserAccountDetailDto }) {
  return (
    <dl>
      {Object.entries({
        ID: account.id,
        "Employee code": account.employeeCode,
        Username: account.username,
        "Full name": account.fullName,
        Email: account.email,
        Phone: account.phone ?? "Not supplied",
        "Account status": account.accountStatus,
        "Primary branch":
          account.primaryBranch?.name ?? "Not provided in this response",
        Department: account.department?.name ?? "Not provided in this response",
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
        <p>No records returned.</p>
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
        Evaluated at{" "}
        <time dateTime={report.evaluatedAtUtc}>{report.evaluatedAtUtc}</time>
      </p>
      <p>
        {report.reviewerScope.isGlobal
          ? "Global reviewer scope"
          : `Limited branch report: ${report.reviewerScope.branchIds.join(", ")}`}
      </p>
      <p>
        This server report is not authorization to execute an operation. OWN
        still requires verified resource ownership.
      </p>
      <section aria-label="Access review account">
        <h3>Access review account</h3>
        <Account account={report.account} />
      </section>
      <p>
        Assigned records can include inactive or historical entries. Effective
        permissions are listed separately.
      </p>
      <Table
        label="Assigned roles"
        columns={[
          "Assignment ID",
          "Role ID",
          "Role",
          "Branch ID",
          "Assignment active",
          "Role active",
          "Assigned UTC",
          "Expires UTC",
        ]}
        rows={report.assignedRoles.map((role) => [
          role.assignmentId,
          role.roleId,
          `${role.code} — ${role.name}`,
          role.branchId ?? "Unbound",
          String(role.assignmentActive),
          String(role.roleActive),
          role.assignedAtUtc,
          role.expiresAtUtc ?? "None",
        ])}
      />
      <Table
        label="Branch memberships"
        columns={[
          "Branch ID",
          "Code",
          "Name",
          "Access level",
          "Branch active",
          "Granted UTC",
          "Revoked UTC",
        ]}
        rows={report.branchMemberships.map((member) => [
          member.branchId,
          member.code,
          member.name,
          member.accessLevel,
          String(member.branchActive),
          member.grantedAtUtc,
          member.revokedAtUtc ?? "None",
        ])}
      />
      <Table
        label="Configured grants"
        columns={[
          "Permission",
          "Feature",
          "Configured scope",
          "Assignment ID",
          "Role",
          "Branch ID",
          "Effective",
        ]}
        rows={report.configuredGrants.map((grant) => [
          `${grant.permissionCode} — ${grant.permissionName}`,
          grant.feature ?? "Not supplied",
          grant.configuredScope,
          grant.assignmentId,
          grant.roleCode,
          grant.branchId ?? "Unbound",
          String(grant.isEffective),
        ])}
      />
      <Table
        label="Effective permissions"
        columns={["Permission", "Feature", "Server sources"]}
        rows={report.effectivePermissions.map((permission) => [
          `${permission.code} — ${permission.name}`,
          permission.feature ?? "Not supplied",
          <ul>
            {permission.sources.map((source, index) => (
              <li key={index}>
                Assignment {source.assignmentId}; role {source.roleCode}; scope{" "}
                {source.scope}; branch {source.branchId ?? "Unbound"};{" "}
                {source.requiresResourceOwnership
                  ? "Resource ownership required"
                  : "Resource ownership not required by this source"}
              </li>
            ))}
          </ul>,
        ])}
      />
    </>
  );
}
function UserReads({ userId }: { userId: number }) {
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
  const detail = useUserRead(`detail:${userId}`, loadDetail);
  const review = useUserRead(
    `review:${userId}:${JSON.stringify(query)}`,
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
        <h1>User account {userId}</h1>
        <Link to="/users">User directory</Link>
        <Link to="/profile">Profile</Link>
      </header>
      <section aria-label="Minimal account detail">
        <h2>Minimal account detail</h2>
        {detail.result === null ? (
          <p role="status">Loading account detail...</p>
        ) : detail.result.ok ? (
          <Account account={detail.result.data} />
        ) : (
          <>
            <p role="alert">{usersReadMessage(detail.result.error)}</p>
            <button type="button" onClick={detail.reload}>
              Retry detail
            </button>
          </>
        )}
      </section>
      <section aria-label="Access review">
        <h2>Access review</h2>
        <form onSubmit={filter} aria-label="Access review filters">
          <label>
            Feature
            <input
              value={draft.feature}
              maxLength={60}
              onChange={(event) =>
                setDraft({ ...draft, feature: event.target.value })
              }
            />
          </label>
          <label>
            Permission code
            <input
              value={draft.permissionCode}
              maxLength={100}
              onChange={(event) =>
                setDraft({ ...draft, permissionCode: event.target.value })
              }
            />
          </label>
          <button type="submit">Apply review filters</button>
        </form>
        {review.result === null ? (
          <p role="status">Loading access review...</p>
        ) : review.result.ok ? (
          <Report report={review.result.data} />
        ) : (
          <>
            <p role="alert">{usersReadMessage(review.result.error)}</p>
            <button type="button" onClick={review.reload}>
              Retry review
            </button>
          </>
        )}
      </section>
    </main>
  );
}

export function UserAccessReviewPage() {
  const { id } = useParams();
  const numeric = id !== undefined && /^\d+$/.test(id) ? Number(id) : NaN;
  if (!Number.isSafeInteger(numeric) || numeric <= 0)
    return (
      <main>
        <h1>Invalid user ID</h1>
        <p role="alert">A positive safe integer user ID is required.</p>
        <Link to="/users">User directory</Link>
      </main>
    );
  return <UserReads key={numeric} userId={numeric} />;
}
