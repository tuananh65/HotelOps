import { useParams } from "react-router-dom";
import { ChecklistWorkspace } from "../../features/inspectionChecklists/ChecklistWorkspace";
export default function InspectionChecklistDetailScreen() {
  const { id } = useParams();
  return <ChecklistWorkspace initialId={id} />;
}
