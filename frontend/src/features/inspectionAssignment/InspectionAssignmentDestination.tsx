import { useLocation } from "react-router-dom";
import { InspectionAssignmentCandidate } from "./InspectionAssignmentCandidate";
import { parseAssignmentContext } from "./navigation";

// Router owner must opt in only in an approved authenticated host with candidate reads available.
// This capability flag grants no permission and does not activate assignment mutations.
export function InspectionAssignmentDestination({ enabled = false, onOpenInspection }: {
  enabled?: boolean;
  onOpenInspection: (branchId: number, inspectionId: number) => void;
}) {
  const location = useLocation();
  const context = parseAssignmentContext(location.search);
  if (!context) return <p role="alert">Ngữ cảnh phân công không hợp lệ. Cần mã chi nhánh hợp lệ; mã phòng không bắt buộc.</p>;
  if (!enabled) return <p role="alert">Phân công kiểm tra chưa được kích hoạt.</p>;
  return <InspectionAssignmentCandidate key={location.key + location.search}
    branchId={context.branchId} roomId={context.roomId}
    onOpenInspection={id => onOpenInspection(context.branchId, id)} />;
}
