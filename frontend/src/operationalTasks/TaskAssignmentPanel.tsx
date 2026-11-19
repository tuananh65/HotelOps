import { SearchInput, searchSuggestions } from "../components/SearchInput";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  assignTask,
  readAssignment,
  type AssignmentSnapshot,
} from "./assignment";

export function TaskAssignmentPanel({
  branch,
  task,
  owner,
  onAssigned,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onAssigned: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<AssignmentSnapshot | null>(null);
  const [assignee, setAssignee] = useState("");
  const [reason, setReason] = useState("");
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState({ search: "", page: 1, revision: 0 });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [hidden, setHidden] = useState(false);
  const [locked, setLocked] = useState(true);
  const active = useRef<AbortController | null>(null);
  const saving = useRef(false);
  function failure(error: unknown) {
    if (error instanceof TaskProgressError && error.status === 401)
      actions.invalidateSession(owner);
    setLocked(true);
    setError(
      error instanceof TaskProgressError && error.status === 412
        ? "Công việc đã đổi. Tải lại phân công và kiểm tra người phụ trách hiện tại."
        : "Không thể phân công hoặc chưa xác minh được kết quả. Tải lại trước khi thử lại.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setLocked(true);
    setBusy(true);
    setAssignee("");
    setReason("");
    setError("");
    setHidden(false);
    void readAssignment(
      branch,
      task,
      query.page,
      query.search,
      controller.signal,
    )
      .then((value) => {
        if (!controller.signal.aborted) {
          setSnapshot(value);
          setLocked(false);
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          if (
            error instanceof TaskProgressError &&
            [403, 409].includes(error.status)
          )
            setHidden(true);
          else failure(error);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, query, actions]);
  async function save() {
    if (!snapshot || busy || locked || saving.current) return;
    if (!assignee || (snapshot.data.assigneeId !== null && !reason.trim())) {
      setError("Chọn người thực hiện và nhập lý do khi phân công lại.");
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    try {
      await assignTask(
        branch,
        task,
        snapshot,
        Number(assignee),
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        announceWorkflowSuccess(
          "Đã phân công công việc.",
          "operationalTasks/TaskAssignmentPanel.tsx",
        );
        onAssigned();
      }
    } catch (error) {
      if (!controller.signal.aborted) failure(error);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <ActionDialog
      scope="operationalTasks/TaskAssignmentPanel.tsx"
      title="Phân công người thực hiện công việc"
      busy={busy}
    >
      <section aria-label="Phân công công việc">
        <h3>Phân công hoặc phân công lại công việc</h3>
        <button
          disabled={busy}
          onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
        >
          Tải lại phân công
        </button>
        {error && <p role="alert">{userMessage(error)}</p>}
        {snapshot && (
          <>
            <p>
              Quy trình: {displayCode(snapshot.data.state)}; người thực hiện
              hiện tại: {snapshot.data.assigneeId ?? "Chưa phân công"}
            </p>
            <p>
              Khối lượng công việc trong chi nhánh tính đến {snapshot.data.asOf}
              . Logged time today ({snapshot.data.timezone}) gồm các khoảng làm
              việc đã kết thúc; không tính thời gian tạm dừng.
            </p>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (!busy)
                  setQuery((q) => ({
                    search,
                    page: 1,
                    revision: q.revision + 1,
                  }));
              }}
            >
              <label>
                Tìm người thực hiện
                <SearchInput suggestions={searchSuggestions(snapshot)}
                  maxLength={150}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>
              <button data-dialog-action disabled={busy}>
                Tìm người thực hiện
              </button>
            </form>
            <fieldset disabled={busy || locked}>
              <legend>Chọn người thực hiện đủ điều kiện</legend>
              <label>
                Người thực hiện
                <SearchableSelect
                  aria-label="Người thực hiện"
                  value={assignee}
                  onChange={(e) => setAssignee(e.target.value)}
                >
                  <option value="">Chọn người thực hiện</option>
                  {snapshot.data.executors.map((e) => (
                    <option
                      key={e.userId}
                      value={e.userId}
                      disabled={e.userId === snapshot.data.assigneeId}
                    >
                      {e.name} — {displayCode(e.openTasks)} công việc đang mở;{" "}
                      {displayCode(e.loggedSecondsToday)} giây ghi nhận hôm nay
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Lý do phân công lại
                <textarea
                  maxLength={4000}
                  required={snapshot.data.assigneeId !== null}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <button onClick={() => void save()}>
                {snapshot.data.assigneeId === null
                  ? "Phân công công việc"
                  : "Phân công lại công việc"}
              </button>
            </fieldset>
            <nav aria-label="Phân trang người thực hiện">
              <button
                disabled={busy || query.page === 1}
                onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
              >
                Người thực hiện trước
              </button>
              <span>
                Trang {query.page}; {snapshot.data.totalCount} người thực hiện
                đủ điều kiện
              </span>
              <button
                disabled={busy || query.page * 25 >= snapshot.data.totalCount}
                onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
              >
                Người thực hiện tiếp theo
              </button>
            </nav>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
