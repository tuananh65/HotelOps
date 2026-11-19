import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { useEffect, useRef, useState } from "react";
import { Assignment } from "./InitialInspectionAssignmentPage";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import { HistoryBranchSelector } from "../inspectionHistory/HistoryBranchSelector";
import { parseAssignmentContext } from "../inspectionAssignment/navigation";
import {
  readInitialInspectionQueue,
  type InitialInspectionQueue,
} from "../../api/dev1/initialInspectionAssignment";

export function PendingInspectionsPage() {
  const { state } = useAuth();
  const [params] = useSearchParams();
  const branch = params.get("branchId") ?? "";
  const room = params.get("roomId");
  const identity = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  if (branch && !parseAssignmentContext(params.toString()))
    return <p role="alert">Đích kiểm tra đang chờ không hợp lệ.</p>;
  return (
    <main className="ops-history">
      <header>
        <h1>Phân công kiểm tra phòng</h1>
        <p>Chọn lượt vệ sinh đang chờ để phân công người kiểm tra.</p>
      </header>
      <HistoryBranchSelector
        selected={branch}
        autoSelect={!branch}
        assignment
      />
      {branch && (
        <Queue
          key={`${branch}:${room}:${revision.current}`}
          branch={branch}
          room={room}
        />
      )}
    </main>
  );
}

function Queue({ branch, room }: { branch: string; room: string | null }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [page, setPage] = useState(1);
  const [selectedTask, setSelectedTask] = useState<string | null>(null);
  const [refresh, setRefresh] = useState(0);
  const key = `${branch}:${room}:${page}:${refresh}`;
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: InitialInspectionQueue;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    void readInitialInspectionQueue(branch, room, page, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setResult({
            owner,
            key,
            error:
              "Chưa có kiểm tra đang chờ trong chi nhánh hoặc quyền hiện tại.",
          });
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => abort.abort();
  }, [owner, key, branch, room, page, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  if (!owner) return <p role="status">Đang kiểm tra phiên đăng nhập…</p>;
  return (
    <section className="ops-card">
      <h2>Các lượt kiểm tra đang chờ</h2>
      <Link to="/rooms/status">Bảng trạng thái phòng</Link>
      {!visible && <p role="status">Đang tải các lượt kiểm tra đang chờ…</p>}
      {visible?.error && <p role="alert">{userMessage(visible.error)}</p>}
      {visible?.data && (
        <>
          <p>{visible.data.totalCount} chu kỳ vệ sinh chờ kiểm tra.</p>
          {visible.data.items.length === 0 && (
            <p>Không có phòng chờ phân công kiểm tra trong phạm vi này.</p>
          )}
          <ul>
            {visible.data.items.map((row) => (
              <li key={row.taskId}>
                Phòng {row.roomNumber} · Chu kỳ vệ sinh {referenceLabel(row, "taskId")}{" "}
                <button
                  type="button"
                  onClick={() => setSelectedTask(String(row.taskId))}
                >
                  Phân công kiểm tra
                </button>
              </li>
            ))}
          </ul>
          <button
            disabled={page === 1}
            onClick={() => setPage((value) => value - 1)}
          >
            Trang trước
          </button>
          <span> Trang {page} </span>
          <button
            disabled={page * visible.data.pageSize >= visible.data.totalCount}
            onClick={() => setPage((value) => value + 1)}
          >
            Trang sau
          </button>
        </>
      )}
      <button onClick={() => setRefresh((value) => value + 1)}>
        Tải lại các lượt kiểm tra đang chờ
      </button>
      {selectedTask && (
        <section aria-label="Phân công phòng đã chọn">
          <Assignment
            key={selectedTask}
            branch={branch}
            id={selectedTask}
            embedded
            onClose={() => {
              setSelectedTask(null);
              setRefresh((v) => v + 1);
            }}
          />
        </section>
      )}
    </section>
  );
}
