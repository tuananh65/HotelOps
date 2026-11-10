import React from "react";
import {createRoot} from "react-dom/client";
import {ReinspectionWorkflowCandidate} from "../../src/features/inspectionReinspection/ReinspectionWorkflowCandidate";
createRoot(document.getElementById("root")!).render(React.createElement(ReinspectionWorkflowCandidate,{branchId:1}));
