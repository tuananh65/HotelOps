import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Link } from "react-router-dom";
import { InspectionAssignmentDestination } from "../../src/features/inspectionAssignment/InspectionAssignmentDestination";
createRoot(document.getElementById("root")!).render(<BrowserRouter>
  <Link to="?branchId=1&roomId=9">Other room</Link>
  <InspectionAssignmentDestination enabled={location.hash !== "#disabled"} onOpenInspection={() => {}} />
</BrowserRouter>);
