import { useCallback, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { UserDirectoryQuery } from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";
import { useUserRead, usersReadMessage } from "./useUserRead";

const filterId = (value: string): number | undefined => {
  const input = value.trim();
  if (!input) return undefined;
  if (!/^\d+$/.test(input)) return NaN;
  const numeric = Number(input);
  return Number.isSafeInteger(numeric) && numeric > 0 ? numeric : NaN;
};

export function UserDirectoryPage({
  accountManagementEnabled = false,
}: { accountManagementEnabled?: boolean } = {}) {
  const { api } = useUsersContext();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState({
    search: "",
    branchId: "",
    departmentId: "",
    roleId: "",
    accountStatus: "",
    pageSize: "20",
  });
  const [query, setQuery] = useState<UserDirectoryQuery>({
    page: 1,
    pageSize: 20,
  });
  const load = useCallback(
    (signal: AbortSignal) => api.listUsers(query, signal),
    [api, query],
  );
  const { result, reload } = useUserRead(JSON.stringify(query), load);
  const selected = result?.ok
    ? result.data.items.find((item) => item.id === selectedId)
    : undefined;
  function apply(event: FormEvent) {
    event.preventDefault();
    setQuery({
      search: draft.search || undefined,
      branchId: filterId(draft.branchId),
      departmentId: filterId(draft.departmentId),
      roleId: filterId(draft.roleId),
      accountStatus: draft.accountStatus
        ? (draft.accountStatus as "ACTIVE" | "INACTIVE")
        : undefined,
      page: 1,
      pageSize: Number(draft.pageSize),
    });
  }
  const appliedFilters = [
    query.search?.trim() ? "Search: " + query.search.trim() : null,
    query.branchId ? "Primary branch: " + query.branchId : null,
    query.departmentId ? "Department: " + query.departmentId : null,
    query.roleId ? "Role: " + query.roleId : null,
    query.accountStatus ? "Status: " + query.accountStatus : null,
  ]
    .filter(Boolean)
    .join("; ");
  return (
    <main className="user-directory">
      <header>
        <h1>User directory</h1>
        {accountManagementEnabled && (
          <Link to="/users/new">Create user account</Link>
        )}
        <Link to="/dashboard">Workspace</Link>
        <Link to="/profile">Profile</Link>
      </header>
      <form onSubmit={apply} aria-label="Directory filters">
        <label>
          Search name, username or email
          <input
            value={draft.search}
            maxLength={200}
            onChange={(event) =>
              setDraft({ ...draft, search: event.target.value })
            }
          />
        </label>
        {(["branchId", "departmentId", "roleId"] as const).map((key) => (
          <label key={key}>
            {key === "branchId"
              ? "Primary branch ID"
              : key === "departmentId"
                ? "Department ID"
                : "Role ID"}
            <input
              inputMode="numeric"
              value={draft[key]}
              onChange={(event) =>
                setDraft({ ...draft, [key]: event.target.value })
              }
            />
          </label>
        ))}
        <label>
          Account status
          <select
            aria-label="Account status"
            value={draft.accountStatus}
            onChange={(event) =>
              setDraft({ ...draft, accountStatus: event.target.value })
            }
          >
            <option value="">All statuses</option>
            <option value="ACTIVE">ACTIVE</option>
            <option value="INACTIVE">INACTIVE</option>
          </select>
        </label>
        <label>
          Page size
          <select
            aria-label="Page size"
            value={draft.pageSize}
            onChange={(event) =>
              setDraft({ ...draft, pageSize: event.target.value })
            }
          >
            {[20, 50, 100].map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
        <button type="submit">Apply filters</button>
        <button
          type="button"
          onClick={() => {
            setDraft({
              search: "",
              branchId: "",
              departmentId: "",
              roleId: "",
              accountStatus: "",
              pageSize: "20",
            });
            setQuery({ page: 1, pageSize: 20 });
          }}
        >
          Clear filters
        </button>
      </form>
      <p aria-label="Applied filters">
        Applied filters: {appliedFilters || "None"}
      </p>
      {result === null ? (
        <p role="status">Loading accounts...</p>
      ) : !result.ok ? (
        <section>
          <p role="alert">{usersReadMessage(result.error)}</p>
          <button type="button" onClick={reload}>
            Retry directory
          </button>
        </section>
      ) : (
        <div
          className={`user-directory-workspace${selected ? " has-selection" : ""}`}
        >
          <section aria-label="Directory results">
            <p>Matching accounts: {result.data.totalCount}</p>
            <table>
              <caption>User accounts</caption>
              <thead>
                <tr>
                  {[
                    "User account",
                    "Branch / department",
                    "Roles",
                    "Account status",
                    "Details",
                  ].map((label) => (
                    <th key={label} scope="col">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {result.data.items.map((item) => (
                  <tr
                    key={item.id}
                    className={
                      selectedId === item.id ? "is-selected" : undefined
                    }
                  >
                    <td>
                      <Link
                        className="user-directory-name"
                        to={`/users/${item.id}`}
                      >
                        {item.fullName}
                      </Link>
                      <small>
                        {item.employeeCode} · @{item.username} · #{item.id}
                      </small>
                      <small>{item.email}</small>
                    </td>
                    <td>
                      {item.primaryBranch?.name ?? "Not supplied"}
                      <small>{item.department?.name ?? "Not supplied"}</small>
                    </td>
                    <td>
                      {item.roles.length ? (
                        <ul>
                          {item.roles.map((role, index) => (
                            <li key={`${role.id}-${index}`}>
                              {role.code} — {role.name} (
                              {role.branch?.name ?? "Branch not supplied"})
                            </li>
                          ))}
                        </ul>
                      ) : (
                        "None returned"
                      )}
                    </td>
                    <td>
                      <span
                        className={`user-account-status ${item.accountStatus === "ACTIVE" ? "active" : "inactive"}`}
                      >
                        {item.accountStatus}
                      </span>
                    </td>
                    <td>
                      <button
                        type="button"
                        aria-label={`Quick view ${item.fullName}`}
                        aria-expanded={selectedId === item.id}
                        onClick={() =>
                          setSelectedId(selectedId === item.id ? null : item.id)
                        }
                      >
                        Quick view
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {result.data.items.length === 0 && (
              <p role="status">MSG10: No records match the applied filters.</p>
            )}
            <p>
              Page {result.data.page} of{" "}
              {Math.max(
                1,
                Math.ceil(result.data.totalCount / result.data.pageSize),
              )}
            </p>
            <button
              type="button"
              disabled={result.data.page <= 1}
              onClick={() => setQuery({ ...query, page: result.data.page - 1 })}
            >
              Previous page
            </button>
            <button
              type="button"
              disabled={
                result.data.page * result.data.pageSize >=
                result.data.totalCount
              }
              onClick={() => setQuery({ ...query, page: result.data.page + 1 })}
            >
              Next page
            </button>
          </section>
          {selected && (
            <aside
              className="user-directory-inspector"
              aria-label="Account quick view"
            >
              <div className="user-inspector-heading">
                <h2>{selected.fullName}</h2>
                <button
                  type="button"
                  aria-label="Close quick view"
                  onClick={() => setSelectedId(null)}
                >
                  ×
                </button>
              </div>
              <p>
                @{selected.username} · {selected.employeeCode}
              </p>
              <dl>
                <dt>Email</dt>
                <dd>{selected.email}</dd>
                <dt>Primary branch</dt>
                <dd>{selected.primaryBranch?.name ?? "Not supplied"}</dd>
                <dt>Department</dt>
                <dd>{selected.department?.name ?? "Not supplied"}</dd>
                <dt>Account status</dt>
                <dd>{selected.accountStatus}</dd>
              </dl>
              <Link className="primary" to={`/users/${selected.id}`}>
                View full profile
              </Link>
            </aside>
          )}
        </div>
      )}
    </main>
  );
}
