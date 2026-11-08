import { useState } from "react";
import { Link, Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import "./inspection.css";

// Authentication admission only. SQL-backed permission/assignment checks remain authoritative.
export function InspectionBoundary() {
  const { state, actions } = useAuth();
  const [blocked, setBlocked] = useState(false);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status === "INITIALIZING")
    return <p role="status">Checking session…</p>;
  if (state.status !== "AUTHENTICATED" || !state.session || blocked)
    return (
      <main>
        <p role="alert">
          Session needs verification. Inspection data is hidden. No mutation has
          been retried.
        </p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
        <button onClick={() => window.location.reload()}>Reload page</button>
        <Link to="/login">Sign in</Link>
      </main>
    );
  return (
    <div className="inspection-app" key={state.session.user.id}>
      <nav>
        <Link to="/inspections">Perform inspection</Link>
        <Link to="/inspections/history">Inspection history</Link>
        <Link to="/profile">Profile / Sign out</Link>
      </nav>
      <Outlet
        context={{
          sessionFailure: () => {
            setBlocked(true);
            void actions.revalidate();
          },
        }}
      />
    </div>
  );
}
