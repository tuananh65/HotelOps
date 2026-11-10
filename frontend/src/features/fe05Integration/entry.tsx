import React, { useState } from "react";
import { createRoot } from "react-dom/client";
import {
  BrowserRouter,
  Routes,
  Route,
  Link,
  Outlet,
  useNavigate,
  useSearchParams,
} from "react-router-dom";
import { InspectionAssignmentCandidate } from "../inspectionAssignment/InspectionAssignmentCandidate";
import { ChecklistWorkspace } from "../inspectionChecklists/ChecklistWorkspace";
import { InspectionEntry, ExecutionPage } from "../../inspection/ExecutionPage";
import { HistoryPage, HistoryDetailPage } from "../../inspection/HistoryPage";
import "../../inspection/inspection.css";
// Separate FE05 development entry. Shared router/authentication is unchanged.
function Boundary() {
  const [blocked, setBlocked] = useState(false);
  return (
    <>
      <nav>
        <Link to="/assignment">Assignment</Link> |{" "}
        <Link to="/inspections">Assigned inspections</Link> |{" "}
        <Link to="/checklists">Checklists</Link> |{" "}
        <Link to="/inspections/history">History</Link>
      </nav>
      {blocked ? (
        <p role="alert">
          Session needs verification. Reload after signing in through the real
          authentication host.
        </p>
      ) : (
        <Outlet context={{ sessionFailure: () => setBlocked(true) }} />
      )}
    </>
  );
}
function Assignment() {
  const [query, setQuery] = useSearchParams();
  const navigate = useNavigate();
  const branch = Number(query.get("branchId"));
  const [text, setText] = useState(query.get("branchId") || "");
  return (
    <main>
      <h1>Assign Room Inspection</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (Number.isSafeInteger(Number(text)) && Number(text) > 0)
            setQuery({ branchId: text });
        }}
      >
        <label>
          Assignment branch ID
          <input value={text} onChange={(e) => setText(e.target.value)} />
        </label>
        <button>Load assignment branch</button>
      </form>
      {Number.isSafeInteger(branch) && branch > 0 && (
        <InspectionAssignmentCandidate
          key={branch}
          branchId={branch}
          onOpenInspection={(id) =>
            navigate("/inspections/" + branch + "/" + id + "/execution")
          }
        />
      )}
    </main>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route element={<Boundary />}>
          <Route path="/fe05.html" element={<Assignment />} />
          <Route path="/assignment" element={<Assignment />} />
          <Route path="/checklists" element={<ChecklistWorkspace />} />
          <Route path="/inspections" element={<InspectionEntry />} />
          <Route
            path="/inspections/:branch/:id/execution"
            element={<ExecutionPage />}
          />
          <Route path="/inspections/history" element={<HistoryPage />} />
          <Route
            path="/inspections/history/:branch/:id"
            element={<HistoryDetailPage />}
          />
        </Route>
      </Routes>
    </BrowserRouter>
  </React.StrictMode>,
);
