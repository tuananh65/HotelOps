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

function ConfirmationForm({
  profile,
  assign,
}: {
  profile: UserProfileDto;
  assign(roleCode: string, branchId: number): Promise<void>;
}) {
  const [role, setRole] = useState("");
  const [branch, setBranch] = useState("");
  const [review, setReview] = useState<{ role: string; branch: number } | null>(
    null,
  );
  const reviewed = useRef<typeof review>(null);
  const [error, setError] = useState("");
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [catalogState, setCatalogState] = useState("Loading active role catalog...");
  useEffect(() => {
    const abort = new AbortController();
    const api = createRoleCatalogApi();
    void (async () => {
      const rows: RoleDto[] = [];
      for (let page = 1; page <= 80; page++) {
        const result = await api.roles(page, "", "true", abort.signal);
        if (abort.signal.aborted) return;
        if (!result.ok || result.data.total > 2000) {
          setCatalogState("Active role catalog unavailable or access denied."); return;
        }
        rows.push(...result.data.items);
        if (page * result.data.pageSize >= result.data.total) {
          setRoles([...new Map(rows.map(row => [row.roleId, row])).values()]);
          setCatalogState(rows.length ? "" : "No active roles are available."); return;
        }
      }
      setCatalogState("Role catalog exceeds the supported page limit.");
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
      branchId = assignmentBranchId(branch);
    if (!roleCode || branchId === null || !roles.some(r => r.roleCode === roleCode && r.isActive)) {
      setError(
        "Select an active role and enter a positive Branch ID for the user's current branch access.",
      );
      return;
    }
    const next = { role: roleCode, branch: branchId };
    reviewed.current = next;
    setReview(next);
  }
  function confirm() {
    const next = reviewed.current;
    if (!next) return;
    // Consume synchronously before any promise or React render.
    reviewed.current = null;
    setReview(null);
    void assign(next.role, next.branch);
  }
  return (
    <form
      aria-label="Assign user role"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      <label>
        Role Code
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
          <option value="">Select an active role</option>
          {roles.map(r => <option key={r.roleId} value={r.roleCode}>{r.roleCode} — {r.roleName}</option>)}
        </select>
      </label>
      {catalogState && <p role="status">{catalogState}</p>}
      <label>
        Branch ID
        <input
          name="branchId"
          type="text"
          inputMode="numeric"
          autoComplete="off"
          required
          value={branch}
          onChange={(e) => {
            setBranch(e.target.value);
            invalidate();
          }}
        />
      </label>
      <button type="button" onClick={preview} disabled={!!catalogState}>
        Review Assignment
      </button>
      {review && (
        <section aria-label="Review role assignment">
          <p>Account ID: {profile.id}</p>
          <p>Full name: {profile.fullName}</p>
          <p>Email: {profile.email}</p>
          <p>Role Code: {review.role}</p>
          <p>Branch ID: {review.branch}</p>
          <p>
            The assignment is limited to this branch. Existing roles remain assigned.
            A duplicate role/branch assignment is rejected, including historical records.
          </p>
          <button type="button" onClick={confirm}>
            Confirm Assign Role
          </button>
        </section>
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
      <h1>Assign role for user {target}</h1>
      <Link to="/profile">Profile</Link>
      <p>
        Loading a profile does not grant permission to assign roles. The server
        verifies account eligibility, active catalog role, duplicate assignment,
        branch membership, actor authority and
        administrator safety when POST is processed.
      </p>
      <p>
        Assignments add to the user's roles. Scope and segregation-of-duty checks
        still apply to each business operation. The user's sessions will end after assignment.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownRoleAssignmentMessage}</p>
          <p>
            A profile GET confirms only identity and version. It does not prove
            that the earlier POST committed or rolled back. Assign remains
            disabled in this flow.
          </p>
        </>
      )}
      {assignment && (
        <section aria-label="Confirmed role assignment" role="status">
          <p>
            Role assignment confirmed by the server. This receipt does not
            guarantee effective business permissions for every request.
          </p>
          <p>id: {assignment.id}</p>
          <p>userId: {assignment.userId}</p>
          <p>roleCode: {assignment.roleCode}</p>
          <p>branchId: {assignment.branchId}</p>
          <p>assignedBy: {assignment.assignedBy}</p>
          <p>assignedAtUtc: {assignment.assignedAtUtc}</p>
          <p>isActive: {String(assignment.isActive)}</p>
          <p>expiresAtUtc: {String(assignment.expiresAtUtc)}</p>
        </section>
      )}
      {progress.phase === "pending" && (
        <p role="status">Role assignment request pending...</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "No role assignment POST was sent. "
            : ""}
          {progress.message}
        </p>
      )}
      {flow.error && <p role="alert">{flow.error}</p>}
      {flow.loading && <p role="status">Loading profile...</p>}
      {snapshot && (
        <section aria-label="Server management snapshot">
          <p>Account ID: {snapshot.profile.id}</p>
          <p>Full name: {snapshot.profile.fullName}</p>
          <p>Email: {snapshot.profile.email}</p>
          {!locked && (
            <ConfirmationForm
              key={snapshot.revision}
              profile={snapshot.profile}
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
        Reload profile
      </button>
    </main>
  );
}
