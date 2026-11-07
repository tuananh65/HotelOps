import AssignedCleaningHeader from "./AssignedCleaningHeader";
import { useParams, useSearchParams } from "react-router-dom";
import CleaningProgressPanel from "./CleaningProgressPanel";
import CleaningMonitorDetail from "./CleaningMonitorDetail";
import CleaningExecutionPanel from "./CleaningExecutionPanel";
export default function CleaningTaskDetailScreen() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") || "";
  if (params.get("execution") === "1" && branch)
    return (
      <CleaningExecutionPanel
        key={`${branch}:${id}`}
        branch={branch}
        task={id || ""}
      />
    );
  if (params.has("assignmentId"))
    return (
      <AssignedCleaningHeader
        key={branch + ":" + id + ":" + params.get("assignmentId")}
        branch={branch}
        task={id || ""}
        assignment={params.get("assignmentId") || ""}
      />
    );
  if (params.get("monitor") === "1") {
    const kind = params.get("kind") || "cleaning";
    return (
      <CleaningMonitorDetail
        key={`${branch}:${kind}:${id}`}
        branch={branch}
        kind={kind}
        id={id || ""}
      />
    );
  }
  if (branch)
    return (
      <CleaningProgressPanel
        key={`${branch}:${id}`}
        branch={branch}
        task={id || ""}
      />
    );
  return (
    <p role="alert">
      Cần ID chi nhánh hợp lệ để tải chi tiết từ máy chủ. Mở công việc từ
      Cleaning Monitor hoặc dùng liên kết UC032 được cấp quyền.
    </p>
  );
}
