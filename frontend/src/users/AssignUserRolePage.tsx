import { useRef, useState } from "react";
import { Link } from "react-router-dom";
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
  function invalidate() {
    reviewed.current = null;
    setReview(null);
    setError("");
  }
  function preview() {
    invalidate();
    const roleCode = normalizeAssignmentRole(role),
      branchId = assignmentBranchId(branch);
    if (!roleCode || branchId === null) {
      setError(
        "Enter an exact supported Role Code and a positive safe integer Branch ID using decimal digits only.",
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
        <input
          name="roleCode"
          type="text"
          autoComplete="off"
          required
          value={role}
          onChange={(e) => {
            setRole(e.target.value);
            invalidate();
          }}
        />
      </label>
      <p>
        Supported codes: HK_STAFF, HK_SUPERVISOR, MAINTENANCE_STAFF,
        MAINTENANCE_LEAD, STOREKEEPER.
      </p>
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
      <button type="button" onClick={preview}>
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
            First-role-only V1. The server may reject this request if any
            UserRole history exists, including inactive or expired assignments.
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
        verifies account eligibility, role history, active role and permission
        envelope, primary branch, department, membership, actor authority and
        administrator safety when POST is processed.
      </p>
      <p>
        This first-role-only V1 flow cannot revoke, replace, reassign,
        reactivate or regrant roles.
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
