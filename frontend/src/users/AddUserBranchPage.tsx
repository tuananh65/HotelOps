import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  destinationBranchId,
  unknownMembershipMessage,
  type useAddUserBranch,
  type BranchAssignmentOption,
} from "./useAddUserBranch";

function ConfirmationForm({
  target,
  add,
  options,
}: {
  target: number;
  add(branchId: number, departmentId: number): Promise<void>;
  options:BranchAssignmentOption[];
}) {
  const [draft, setDraft] = useState("");
  const [departmentDraft, setDepartmentDraft] = useState("");
  const [review, setReview] = useState<{branch:number;department:number} | null>(null);
  const reviewed = useRef<{branch:number;department:number} | null>(null);
  const [error, setError] = useState("");
  function preview() {
    const branch = destinationBranchId(draft);
    const department = destinationBranchId(departmentDraft);
    if (branch === null || department === null || !options.some(b=>b.id===branch&&b.departments.some(d=>d.id===department))) {
      setError(
        "Select an available branch and working department.",
      );
      return;
    }
    setError("");
    reviewed.current = {branch,department};
    setReview({branch,department});
  }
  function confirm() {
    if (reviewed.current === null) return;
    const branch = reviewed.current;
    reviewed.current = null;
    setReview(null);
    void add(branch.branch, branch.department);
  }
  return (
    <form
      aria-label="Add branch membership"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      <label>
        Destination branch
        <select
          name="branchId"
          required
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            setDepartmentDraft("");
            reviewed.current = null;
            setReview(null);
            setError("");
          }}
        ><option value="">Select branch</option>{options.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select>
      </label>
      <label>Working department
        <select name="departmentId" required value={departmentDraft}
          onChange={e=>{setDepartmentDraft(e.target.value);reviewed.current=null;setReview(null);setError("");}}>
          <option value="">Select department</option>{options.find(b=>b.id===Number(draft))?.departments.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </label>
      <p>The department must be active and belong to the destination branch. Both memberships are saved together.</p>
      {options.length===0&&<p role="status">No new branch with an active department is available in your current management scope.</p>}
      <button type="button" disabled={!draft||!departmentDraft} onClick={preview}>
        Review Add Branch
      </button>
      {review !== null && (
        <section aria-label="Review branch membership">
          <p>
            Add STANDARD, nonprimary branch membership for Account ID: {target},
            Destination Branch ID: {review.branch}, Working Department ID: {review.department}?
          </p>
          <button type="button" onClick={confirm}>
            Confirm Add Branch
          </button>
        </section>
      )}
      {error && <p role="alert">{error}</p>}
    </form>
  );
}

export function AddUserBranchPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useAddUserBranch>;
}) {
  const { snapshot, progress } = flow;
  const locked = ["pending", "unknown", "stale", "confirmed"].includes(
    progress.phase,
  );
  const membership =
    progress.phase === "confirmed" ? progress.membership : undefined;
  return (
    <main>
      <h1>Add branch membership for user {target}</h1>
      <Link to="/profile">Profile</Link>
      <p>
        The server verifies target account eligibility, destination branch
        eligibility, existing membership and source/destination authority when
        POST is processed. Loading a profile does not grant permission to add
        membership, including for your own account.
      </p>
      <p>
        This operation adds a STANDARD, nonprimary branch membership and the selected
        working department membership. It does not change the primary branch, primary department or roles.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownMembershipMessage}</p>
          <p>
            A subsequent profile GET confirms only identity and version. It does
            not prove that the earlier POST committed. Add remains disabled in
            this flow.
          </p>
        </>
      )}
      {membership && (
        <section aria-label="Confirmed membership" role="status">
          <p>Branch membership addition confirmed by the server. The employee must sign in again to use the updated branch scope.</p>
          <p>accessId: {membership.accessId}</p>
          <p>userId: {membership.userId}</p>
          <p>branchId: {membership.branchId}</p>
          <p>Working department: {membership.departmentId}</p>
          <p>accessLevel: {membership.accessLevel}</p>
          <p>isPrimary: {String(membership.isPrimary)} (nonprimary)</p>
        </section>
      )}
      {progress.phase === "pending" && (
        <p role="status">Branch membership request pending...</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "No membership POST was sent. "
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
              target={target}
              add={flow.add}
              options={snapshot.options}
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
