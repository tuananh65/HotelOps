import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  destinationBranchId,
  unknownMembershipMessage,
  type useAddUserBranch,
} from "./useAddUserBranch";

function ConfirmationForm({
  target,
  add,
}: {
  target: number;
  add(branchId: number): Promise<void>;
}) {
  const [draft, setDraft] = useState("");
  const [review, setReview] = useState<number | null>(null);
  const reviewed = useRef<number | null>(null);
  const [error, setError] = useState("");
  function preview() {
    const branch = destinationBranchId(draft);
    if (branch === null) {
      setError(
        "Enter a positive safe integer Branch ID using decimal digits only.",
      );
      return;
    }
    setError("");
    reviewed.current = branch;
    setReview(branch);
  }
  function confirm() {
    if (reviewed.current === null) return;
    const branch = reviewed.current;
    reviewed.current = null;
    setReview(null);
    void add(branch);
  }
  return (
    <form
      aria-label="Add branch membership"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      <label>
        Destination Branch ID
        <input
          type="text"
          inputMode="numeric"
          name="branchId"
          autoComplete="off"
          required
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            reviewed.current = null;
            setReview(null);
            setError("");
          }}
        />
      </label>
      <button type="button" onClick={preview}>
        Review Add Branch
      </button>
      {review !== null && (
        <section aria-label="Review branch membership">
          <p>
            Add STANDARD, nonprimary branch membership for Account ID: {target},
            Destination Branch ID: {review}?
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
        This operation only adds a STANDARD, nonprimary membership. It does not
        change the primary branch, department or roles.
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
          <p>Branch membership addition confirmed by the server.</p>
          <p>accessId: {membership.accessId}</p>
          <p>userId: {membership.userId}</p>
          <p>branchId: {membership.branchId}</p>
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
