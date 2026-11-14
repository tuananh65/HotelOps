import { useState } from "react";
import { Link, Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { WorkdayReadPanel } from "./WorkdayReadPanel";
import { safeId } from "./contracts";

function BranchSelection() {
  const [draft, setDraft] = useState("");
  const [branch, setBranch] = useState<number | null>(null);
  const [invalid, setInvalid] = useState(false);
  return (
    <main>
      <h1>Workday</h1>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          const candidate = /^[1-9][0-9]*$/.test(draft) ? Number(draft) : NaN;
          if (!safeId(candidate)) {
            setBranch(null);
            setInvalid(true);
            return;
          }
          setInvalid(false);
          setBranch(candidate);
        }}
      >
        <label>
          Branch ID
          <input
            inputMode="numeric"
            value={draft}
            onChange={(event) => {
              setDraft(event.target.value);
              setBranch(null);
              setInvalid(false);
            }}
          />
        </label>
        <button type="submit">View workday</button>
      </form>
      <p>
        Chọn chi nhánh không cấp quyền truy cập. Máy chủ xác minh từng yêu cầu.
      </p>
      {invalid && (
        <p role="alert">Branch ID phải là số nguyên dương an toàn.</p>
      )}
      {branch === null ? (
        <p>Nhập chi nhánh để đọc dữ liệu.</p>
      ) : (
        <WorkdayReadPanel branchId={branch} />
      )}
      <Link to="/dashboard">Dashboard</Link>
    </main>
  );
}

/** Mounted only behind the build flag in HotelRouteHost. No new auth store. */
export function WorkdayRoute() {
  const { state, actions } = useAuth();
  if (state.status === "INITIALIZING" || state.pending !== null)
    return <p role="status">Checking your session...</p>;
  if (state.status === "ERROR_UNAVAILABLE")
    return (
      <section>
        <p role="alert">Session unavailable.</p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
      </section>
    );
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to="/login" />;
  // Reset the selection on session replacement, including same-user replacement.
  return <SessionSelection session={state.session} />;
}

function SessionSelection({ session }: { session: object }) {
  const [binding, setBinding] = useState(session);
  const [generation, setGeneration] = useState(0);
  if (binding !== session) {
    setBinding(session);
    setGeneration((value) => value + 1);
  }
  return <BranchSelection key={generation} />;
}
