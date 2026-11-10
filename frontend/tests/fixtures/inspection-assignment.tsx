import React from "react";
import { createRoot } from "react-dom/client";
import { InspectionAssignmentCandidate } from "../../src/features/inspectionAssignment/InspectionAssignmentCandidate";
createRoot(document.getElementById("root")!).render(
  React.createElement(InspectionAssignmentCandidate, {
    branchId: 1,
    onOpenInspection: (id: number) => {
      document.getElementById("opened")!.textContent = String(id);
    },
  }),
);
const output = document.createElement("output");
output.id = "opened";
document.body.append(output);
