import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  changeTaskPlan,
  creationOptions,
  localPlanTime,
  readTaskPlan,
  utcPlanTime,
  type PlanFields,
  type PlanSnapshot,
  type PlanningOptions,
} from "./planning";
import { TaskPlanFields, type PlanDraft } from "./TaskPlanFields";
import { TaskSeriesPanel } from "./TaskSeriesPanel";

export function TaskPlanPanel({
  branch,
  task,
  owner,
  onChanged,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onChanged: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<PlanSnapshot | null>(null);
  const [priorities, setPriorities] = useState<PlanningOptions["priorities"]>(
    [],
  );
  const [plan, setPlan] = useState<PlanDraft>({
    priority: "",
    start: "",
    due: "",
    minutes: "",
  });
  const [reason, setReason] = useState("");
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [query, setQuery] = useState({ page: 1, revision: 0 });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [locked, setLocked] = useState(true);
  const [hidden, setHidden] = useState(false);
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  function failure(error: unknown) {
    if (error instanceof TaskProgressError && error.status === 401)
      actions.invalidateSession(owner);
    setLocked(true);
    setError(
      error instanceof TaskProgressError && error.status === 412
        ? "Công việc đã đổi. Tải lại kế hoạch trước khi tiếp tục."
        : "Kế hoạch hoặc kết quả thay đổi chưa khả dụng. Tải lại và kiểm tra trước khi tiếp tục.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setPriorities([]);
    setLocked(true);
    setBusy(true);
    setHidden(false);
    setError("");
    setReason("");
    setConfirmCancel(false);
    void readTaskPlan(branch, task, query.page, controller.signal)
      .then(async (value) => {
        const options = value.data.canManage
          ? await creationOptions(branch, null, controller.signal)
          : null;
        if (controller.signal.aborted) return;
        setSnapshot(value);
        setPriorities(options?.priorities ?? []);
        setPlan({
          priority: value.data.priorityId?.toString() ?? "",
          start: localPlanTime(value.data.plannedStartAt),
          due: localPlanTime(value.data.dueAt),
          minutes: value.data.slaMinutes?.toString() ?? "",
        });
        setLocked(false);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (
          error instanceof TaskProgressError &&
          [403, 404, 409].includes(error.status)
        )
          setHidden(true);
        else failure(error);
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, query, actions]);
  async function save(cancel: boolean) {
    if (!snapshot || busy || locked || saving.current) return;
    if (cancel && (!confirmCancel || reason.trim().length < 10)) {
      setError(
        "Xác nhận hủy và nhập lý do ít nhất 10 ký tự.",
      );
      return;
    }
    if (
      !cancel &&
      snapshot.data.state !== "PENDING_ASSIGNMENT" &&
      !reason.trim()
    ) {
      setError("Cần lý do khi thay đổi công việc đã phân công.");
      return;
    }
    let input: PlanFields | null;
    try {
      input = cancel
        ? null
        : {
            priorityId: Number(plan.priority),
            plannedStartAt: utcPlanTime(
              plan.start,
              snapshot.data.plannedStartAt,
            ),
            dueAt: utcPlanTime(plan.due, snapshot.data.dueAt),
            slaMinutes: plan.minutes ? Number(plan.minutes) : null,
          };
      if (
        input &&
        (!priorities.some((p) => p.id === Number(plan.priority)) ||
          (input.plannedStartAt &&
            input.dueAt &&
            Date.parse(input.dueAt) < Date.parse(input.plannedStartAt)))
      ) {
        setError(
          "Chọn ưu tiên hoạt động và thời hạn bằng hoặc sau thời điểm bắt đầu dự kiến.",
        );
        return;
      }
    } catch {
      setError("Kiểm tra ngày trong kế hoạch trước khi lưu.");
      return;
    }
    saving.current = true;
    setBusy(true);
    setError("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await changeTaskPlan(
        branch,
        task,
        snapshot,
        input,
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        onChanged();
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
    <section aria-label="Kế hoạch công việc">
      <h3>Lịch sử kế hoạch và thời hạn công việc</h3>
      <button
        disabled={busy}
        onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
      >
        Tải lại kế hoạch công việc
      </button>
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <p>
            Kết quả mong đợi: {snapshot.data.requestedResult ?? "Chưa ghi nhận"}
          </p>
          <p>
            Bắt đầu dự kiến: {snapshot.data.plannedStartAt ?? "Chưa thiết lập"}; Thời hạn: {snapshot.data.dueAt ?? "Chưa thiết lập"}; Thời lượng dự kiến:{" "}
            {snapshot.data.slaMinutes ?? "Chưa thiết lập"} phút.
          </p>
          {snapshot.data.canManage && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void save(false);
              }}
            >
              <fieldset disabled={busy || locked}>
                <legend>Cập nhật kế hoạch công việc</legend>
                <TaskPlanFields
                  value={plan}
                  onChange={setPlan}
                  priorities={priorities}
                />
                <label>
                  Lý do thay đổi hoặc hủy kế hoạch
                  <textarea
                    maxLength={4000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button>Lưu kế hoạch công việc</button>
                {snapshot.data.canCancel && (
                  <>
                    <label>
                      <input
                        type="checkbox"
                        checked={confirmCancel}
                        onChange={(e) => setConfirmCancel(e.target.checked)}
                      />
                      MSG33: Xác nhận hủy công việc này
                    </label>
                    <button
                      type="button"
                      disabled={!confirmCancel || reason.trim().length < 10}
                      onClick={() => void save(true)}
                    >
                      Hủy công việc
                    </button>
                  </>
                )}
              </fieldset>
            </form>
          )}
          {snapshot.data.seriesId && (
            <TaskSeriesPanel
              key={`Chuỗi định kỳ:${owner}:${branch}:${snapshot.data.seriesId}`}
              branch={branch}
              owner={owner}
              seriesId={snapshot.data.seriesId}
              taskId={task}
            />
          )}
          <ol aria-label="Lịch sử kế hoạch công việc">
            {snapshot.data.history.map((h) => (
              <li key={h.auditId}>
                {h.at}: Thời hạn {h.previousDueAt ?? "Chưa thiết lập"} →{" "}
                {h.dueAt ?? "Chưa thiết lập"}; Độ ưu tiên{" "}
                {h.previousPriorityId ?? "Chưa thiết lập"} →{" "}
                {h.priorityId ?? "Chưa thiết lập"}; Thời lượng dự kiến{" "}
                {h.previousSlaMinutes ?? "Chưa thiết lập"} →{" "}
                {h.slaMinutes ?? "Chưa thiết lập"} phút. {h.reason}
              </li>
            ))}
          </ol>
          <nav aria-label="Phân trang lịch sử kế hoạch">
            <button
              disabled={busy || query.page === 1}
              onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
            >
              Thay đổi kế hoạch trước
            </button>
            <span>
              Trang {query.page}; {snapshot.data.historyTotalCount} thay đổi
            </span>
            <button
              disabled={
                busy || query.page * 25 >= snapshot.data.historyTotalCount
              }
              onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
            >
              Thay đổi kế hoạch tiếp theo
            </button>
          </nav>
        </>
      )}
    </section>
  );
}
