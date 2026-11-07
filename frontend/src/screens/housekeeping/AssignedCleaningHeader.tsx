import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError, getJson } from "../../api/dev1/client";
import { positiveId } from "../../api/dev1/rooms";
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
      {error && <p role="alert">{error}</p>}
      {!data && !error && <p role="status">Đang tải…</p>}
      {data && (
        <>
          <h2>
            {data.taskNumber} · {data.roomNumber}
          </h2>
          <p>
            {data.recordedStatus} · Chi nhánh {data.branchId}
          </p>
          <Link
            to={`/cleaning/tasks/${data.id}?branchId=${branch}&execution=1`}
          >
            Mở Start / Reassign
          </Link>
          {data.recordedStatus === "IN_PROGRESS" && (
            <Link to={`/cleaning/tasks/${data.id}?branchId=${branch}`}>
              Mở tiến độ UC032
            </Link>
          )}
        </>
      )}
    </>
  );
}
