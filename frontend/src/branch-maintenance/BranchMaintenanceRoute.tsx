import { Link, useParams } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { BranchMaintenancePanel } from "./BranchMaintenancePanel";
export function BranchMaintenanceRoute() {
  const { branchId = "" } = useParams();
  const id = /^[1-9][0-9]*$/.test(branchId) ? Number(branchId) : NaN;
  return (
    <div className="branch-workspace">
      <nav aria-label="Điều hướng chi nhánh" className="branch-back">
        <Link to="/branches">
          <ArrowLeft size={16} aria-hidden="true" />
          Quản lý chi nhánh
        </Link>
      </nav>
      <BranchMaintenancePanel branchId={id} />
    </div>
  );
}
