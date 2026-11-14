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

export function UserDirectoryPage() {
  const { api } = useUsersContext();
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
  return (
    <main>
      <header>
        <h1>User directory</h1>
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
      </form>
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
        <section aria-label="Directory results">
          <p>Matching accounts: {result.data.totalCount}</p>
          <table>
            <caption>User accounts</caption>
            <thead>
              <tr>
                {[
                  "ID",
                  "Employee code",
                  "Full name",
                  "Username",
                  "Email",
                  "Primary branch",
                  "Department",
                  "Roles",
                  "Account status",
                ].map((label) => (
                  <th key={label} scope="col">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {result.data.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.id}</td>
                  <td>{item.employeeCode}</td>
                  <td>
                    <Link to={`/users/${item.id}`}>{item.fullName}</Link>
                  </td>
                  <td>{item.username}</td>
                  <td>{item.email}</td>
                  <td>{item.primaryBranch?.name ?? "Not supplied"}</td>
                  <td>{item.department?.name ?? "Not supplied"}</td>
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
                  <td>{item.accountStatus}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {result.data.items.length === 0 && <p>No matching accounts.</p>}
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
              result.data.page * result.data.pageSize >= result.data.totalCount
            }
            onClick={() => setQuery({ ...query, page: result.data.page + 1 })}
          >
            Next page
          </button>
        </section>
      )}
    </main>
  );
}
