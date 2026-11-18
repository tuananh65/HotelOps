import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import { TaskSubmissionReview } from "./TaskSubmissionReview";
import { readReopen, reopenTask, type ReopenSnapshot } from "./reopen";

export function TaskReopenPanel({
  branch,
  task,
  owner,
  onReopened,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onReopened: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<ReopenSnapshot | null>(null);
  const [assignee, setAssignee] = useState("");
  const [reason, setReason] = useState("");
  const [instructions, setInstructions] = useState("");
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
        ? "Công việc đã đổi. Tải lại chi tiết mở lại trước khi thử lại."
        : "Chưa thể mở lại hoặc chưa xác minh kết quả. Tải lại trước khi thử lại.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setLocked(true);
    setBusy(true);
    setAssignee("");
    setError("");
    setHidden(false);
    void readReopen(branch, task, query.page, query.search, controller.signal)
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
            [403, 404, 503].includes(error.status)
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
    if (!assignee || reason.trim().length < 10 || !instructions.trim()) {
      setError(
        "Chọn người thực hiện, nhập lý do ít nhất 10 ký tự và hướng dẫn khắc phục.",
      );
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    try {
      await reopenTask(
        branch,
        task,
        snapshot,
        Number(assignee),
        reason,
        instructions,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        onReopened();
      }
    } catch (error) {
      if (!controller.signal.aborted) failure(error);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (
    hidden ||
    (snapshot && !snapshot.data.canReopen && snapshot.data.history.length === 0)
  )
    return null;
  return (
    <section aria-label="Mở lại công việc">
      <h3>Chu kỳ xử lý</h3>
      <button
        disabled={busy}
        onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
      >
        Tải lại chi tiết mở lại
      </button>
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <ol>
            {snapshot.data.history.map((event) => (
              <li key={event.previousSubmissionId}>
                Đã mở lại {event.reopenedAt} bởi người dùng {event.reopenedBy}; giao cho người dùng {event.assigneeId}.<p>Lý do: {event.reason}</p>
                <p>Hướng dẫn khắc phục: {event.instructions}</p>
                <TaskSubmissionReview
                  branch={branch}
                  task={task}
                  owner={owner}
                  submission={event.previousSubmissionId}
                />
              </li>
            ))}
          </ol>
          {snapshot.data.canReopen && (
            <>
              <p>
                Xem lịch sử nộp và xác minh trước bên dưới. Mở lại giữ kết quả này và bắt đầu chu kỳ mới. Người được chọn phải tiếp nhận lại trước khi bắt đầu.
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
                  Tìm người thực hiện khi mở lại
                  <input
                    value={search}
                    maxLength={150}
                    disabled={busy}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </label>
                <button disabled={busy}>Tìm người thực hiện khi mở lại</button>
              </form>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void save();
                }}
              >
                <label>
                  Người thực hiện khi mở lại
                  <select
                    aria-label="Người thực hiện khi mở lại"
                    required
                    disabled={busy || locked}
                    value={assignee}
                    onChange={(e) => setAssignee(e.target.value)}
                  >
                    <option value="">Chọn người thực hiện</option>
                    {snapshot.data.executors.map((e) => (
                      <option key={e.userId} value={e.userId}>
                        {e.name} — {displayCode(e.openTasks)} công việc đang mở,{" "}
                        {displayCode(e.loggedSecondsToday)}giây ghi nhận hôm nay
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Lý do mở lại
                  <textarea
                    required
                    minLength={10}
                    maxLength={4000}
                    disabled={busy || locked}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label>
                  Hướng dẫn khắc phục
                  <textarea
                    required
                    maxLength={4000}
                    disabled={busy || locked}
                    value={instructions}
                    onChange={(e) => setInstructions(e.target.value)}
                  />
                </label>
                <button disabled={busy || locked}>Mở lại công việc</button>
              </form>
              <p>
                Trang danh sách người thực hiện {snapshot.data.page}; {snapshot.data.totalCount}{" "}
                người thực hiện đủ điều kiện. Thời gian ghi nhận tính từ khoảng đã kết thúc trong hôm nay theo giờ Việt Nam.
              </p>
              <button
                disabled={busy || query.page <= 1}
                onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
              >
                Người thực hiện khi mở lại trước
              </button>
              <button
                disabled={busy || query.page * 25 >= snapshot.data.totalCount}
                onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
              >
                Người thực hiện khi mở lại tiếp theo
              </button>
            </>
          )}
        </>
      )}
    </section>
  );
}
