import { userMessage } from "../../i18n/userMessage";
import { displayCode } from "../../i18n/vi";
import { useEffect, useState } from "react";
import { ActionLink, ActionLinks } from "../../components/ActionLink";
import { ApiError, getJson } from "../../api/dev1/client";
import { positiveId } from "../../api/dev1/rooms";
import { useWorkspacePermissions } from "./useWorkspacePermissions";
interface Header {
  id: number;
  branchId: number;
  roomId: number;
  roomNumber: string;
  taskNumber: string;
  recordedStatus: string;
  assignmentId: number;
}
export default function AssignedCleaningHeader({
  branch,
  task,
  assignment,
}: {
  branch: string;
  task: string;
  assignment: string;
}) {
  const access = useWorkspacePermissions(branch);
  const [data, setData] = useState<Header | null>(null),
    [error, setError] = useState("");
  useEffect(() => {
    const abort = new AbortController();
    setData(null);
    setError("");
    (async () => {
      const b = positiveId(branch),
        id = positiveId(task),
        a = positiveId(assignment);
      const d = (await getJson(
        `/branches/${b}/cleaning-tasks/${id}?assignmentId=${a}`,
        abort.signal,
      )) as Header;
      if (
        !d ||
        d.id !== id ||
        d.branchId !== b ||
        d.assignmentId !== a ||
        ![d.roomNumber, d.taskNumber, d.recordedStatus].every(
          (x) => typeof x === "string",
        )
      )
        throw new ApiError(502, "INVALID_SOURCE_HEADER");
      if (!abort.signal.aborted) setData(d);
    })().catch((e) => {
      if (!abort.signal.aborted)
        setError(
          e instanceof ApiError && e.status === 403
            ? "Không có quyền đọc công việc từ phân công này."
            : "Không tải được công việc hoặc phiên đăng nhập không hợp lệ.",
        );
    });
    return () => abort.abort();
  }, [branch, task, assignment]);
  return (
    <>
      <h1>Công việc được phân công</h1>
      {error && <p role="alert">{userMessage(error)}</p>}
      {!data && !error && <p role="status">Đang tải…</p>}
      {data && (
        <>
          <h2>
            {data.taskNumber} · {data.roomNumber}
          </h2>
          <p>
            {displayCode(data.recordedStatus)}
          </p>
          <ActionLinks>
            {access.allows("CLEANING_EXECUTE", true) && (
              <ActionLink
                variant="primary"
                to={`/cleaning/tasks/${data.id}?branchId=${branch}&execution=1`}
              >
                Mở thực hiện công việc
              </ActionLink>
            )}
            {data.recordedStatus === "IN_PROGRESS" &&
              access.allows("CLEANING_EXECUTE", true) && (
                <ActionLink
                  to={`/cleaning/tasks/${data.id}?branchId=${branch}`}
                >
                  Cập nhật tiến độ
                </ActionLink>
              )}
          </ActionLinks>
        </>
      )}
    </>
  );
}
