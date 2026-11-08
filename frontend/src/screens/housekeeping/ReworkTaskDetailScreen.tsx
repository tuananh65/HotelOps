import { useParams, useSearchParams } from "react-router-dom";
import ReworkExecutionPanel from "./ReworkExecutionPanel";
export default function ReworkTaskDetailScreen() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") || "";
  if (!branch)
    return (
      <p role="alert">
        Cần ID chi nhánh được cấp quyền để tải rework từ máy chủ.
      </p>
    );
  return (
    <ReworkExecutionPanel
      key={`${branch}:${id}`}
      branch={branch}
      id={id || ""}
    />
  );
}
