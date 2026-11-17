import { Link, useParams } from "react-router-dom";
import { BranchMaintenancePanel } from "./BranchMaintenancePanel";
import { BranchDeactivationReview } from "./BranchDeactivationReview";
export function BranchMaintenanceRoute() {
  const { branchId = "" } = useParams();
  const id = /^[1-9][0-9]*$/.test(branchId) ? Number(branchId) : NaN;
  return <><Link to="/branches">Quản lý chi nhánh</Link><BranchMaintenancePanel branchId={id} /><BranchDeactivationReview branchId={id} /></>;
}
