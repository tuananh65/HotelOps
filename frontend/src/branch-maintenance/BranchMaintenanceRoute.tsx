import { useParams } from "react-router-dom";
import { BranchMaintenancePanel } from "./BranchMaintenancePanel";
export function BranchMaintenanceRoute() {
  const { branchId = "" } = useParams();
  const id = /^[1-9][0-9]*$/.test(branchId) ? Number(branchId) : NaN;
  return <BranchMaintenancePanel branchId={id} />;
}
