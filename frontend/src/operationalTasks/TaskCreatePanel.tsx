import { referenceLabel } from "../i18n/referenceLabel";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { ActionDialog } from "../components/workspace-interactions";
import { listRooms, type RoomDirectoryItem } from "../api/dev1/rooms";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  createTask,
  creationOptions,
  utcPlanTime,
  validateTaskCreation,
  type CreateTaskInput,
  type PlanningOptions,
  type TaskRequestContext,
  localPlanTime,
} from "./planning";
import { TaskPlanFields, type PlanDraft } from "./TaskPlanFields";
import {
  createSeries,
  validateRecurrence,
  type RecurrenceInput,
} from "./series";
import { TaskRecurrenceFields } from "./TaskRecurrenceFields";

export function TaskCreatePanel({
  branch,
  branchName,
  owner,
  onCreated,
  onSeriesCreated,
  source,
  onSourceInvalid,
}: {
  branch: number;
  branchName?: string;
  owner: AuthorityGeneration;
  onCreated: (task: number, assign: boolean) => void;
  onSeriesCreated: () => void;
  source?: TaskRequestContext;
  onSourceInvalid?: () => void;
}) {
  const { actions } = useAuth();
  const [department, setDepartment] = useState(
    source ? String(source.departmentId) : "",
  );
  const [category, setCategory] = useState("");
  const [options, setOptions] = useState<PlanningOptions | null>(null);
  const [title, setTitle] = useState(source?.title ?? "");
  const [description, setDescription] = useState(source?.description ?? "");
  const [mandatory, setMandatory] = useState("");
  const [outcome, setOutcome] = useState("");
  const [criteria, setCriteria] = useState([{ label: "", required: true }]);
  const [locationType, setLocationType] = useState(
    source?.roomId ? "room" : source?.areaId ? "area" : "department",
  );
  const [location, setLocation] = useState(
    String(source?.roomId ?? source?.areaId ?? ""),
  );
  const [roomNumber, setRoomNumber] = useState("");
  const [rooms, setRooms] = useState<RoomDirectoryItem[]>([]);
  const [roomError, setRoomError] = useState("");
  useEffect(() => {
    const abort = new AbortController();
    setRooms([]);
    setRoomError("");
    if (locationType === "room")
      void (async () => {
        const all: RoomDirectoryItem[] = [];
        for (let page = 1; !abort.signal.aborted; page++) {
          const response = await listRooms(
            String(branch),
            page,
            "",
            "",
            abort.signal,
          );
          all.push(...response.items);
          if (page * response.pageSize >= response.totalCount) break;
        }
        if (!abort.signal.aborted) {
          setRooms(all);
          if (source?.roomId)
            setRoomNumber(
              all.find((r) => r.id === source.roomId)?.roomNumber ?? "",
            );
        }
      })().catch(() => {
        if (!abort.signal.aborted)
          setRoomError("Chưa tải được số phòng trong chi nhánh này.");
      });
    return () => abort.abort();
  }, [branch, owner, locationType, source?.roomId]);
  const [plan, setPlan] = useState<PlanDraft>({
    priority: source?.priorityId ? String(source.priorityId) : "",
    start: "",
    due: localPlanTime(source?.dueAt ?? null),
    minutes: "",
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [hidden, setHidden] = useState(false);
  const [revision, setRevision] = useState(0);
  const [repeat, setRepeat] = useState(false);
  const [recurrence, setRecurrence] = useState<RecurrenceInput>({
    rule: "DAILY",
    weekdays: [],
    monthDay: null,
    defaultAssigneeId: 0,
    endDate: null,
  });
  const [pending, setPending] = useState<{
    input: CreateTaskInput;
    assign: boolean;
    recurrence: RecurrenceInput | null;
  } | null>(null);
  const key = useRef(crypto.randomUUID());
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setOptions(null);
    setError("");
    setHidden(false);
    void creationOptions(
      branch,
      department ? Number(department) : null,
      controller.signal,
    )
      .then((data) => {
        if (!controller.signal.aborted) setOptions(data);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (error instanceof TaskProgressError && error.status === 403)
          setHidden(true);
        else setError("Chưa có lựa chọn tạo việc. Tải lại để thử tiếp.");
      });
    return () => controller.abort();
  }, [branch, department, owner, actions, revision]);
  useEffect(() => () => active.current?.abort(), []);
  async function save(assign: boolean) {
    if (saving.current || busy || (!options && !pending)) return;
    let submission: {
      input: CreateTaskInput;
      assign: boolean;
      recurrence: RecurrenceInput | null;
    };
    try {
      if (
        !pending &&
        locationType === "room" &&
        !rooms.some((r) => r.roomNumber === roomNumber.trim())
      ) {
        setError("Chọn số phòng trong danh sách của chi nhánh này.");
        return;
      }
      if (source && !pending && !["required", "optional"].includes(mandatory)) {
        setError(
          "Chọn công việc này có bắt buộc để hoàn tất yêu cầu hay không.",
        );
        return;
      }
      submission = pending ?? {
        assign,
        recurrence: repeat ? recurrence : null,
        input: {
          requestKey: key.current,
          departmentId: Number(department),
          categoryId: Number(category),
          title,
          description: description.trim() || null,
          requestedResult: outcome,
          criteria,
          priorityId: Number(plan.priority),
          plannedStartAt: utcPlanTime(plan.start),
          dueAt: utcPlanTime(plan.due),
          slaMinutes: plan.minutes ? Number(plan.minutes) : null,
          roomId:
            locationType === "room"
              ? rooms.find((r) => r.roomNumber === roomNumber.trim())!.id
              : null,
          areaId: locationType === "area" ? Number(location) : null,
          internalRequestId: source?.requestId ?? null,
          ...(source
            ? {
                requestLink: {
                  version: source.version,
                  mandatory: mandatory === "required",
                },
              }
            : {}),
        },
      };
      if (
        !pending &&
        ((plan.start && plan.due && plan.due < plan.start) ||
          !department ||
          !category ||
          !plan.priority ||
          !title.trim() ||
          !outcome.trim() ||
          criteria.some((c) => !c.label.trim()))
      ) {
        setError(
          "Nhập đủ trường bắt buộc; thời hạn không được trước thời điểm bắt đầu dự kiến.",
        );
        return;
      }
      validateTaskCreation(submission.input);
      if (submission.recurrence)
        validateRecurrence(submission.input, submission.recurrence);
    } catch {
      setError(
        "Kiểm tra trường công việc, tiêu chí hoàn tất không trùng và ngày kế hoạch. Công việc định kỳ cần người thực hiện hợp lệ và hạn đầu khớp lịch.",
      );
      return;
    }
    saving.current = true;
    setBusy(true);
    setError("");
    setPending(submission);
    const controller = new AbortController();
    active.current = controller;
    try {
      if (submission.recurrence) {
        await createSeries(
          branch,
          submission.input,
          submission.recurrence,
          controller.signal,
        );
        if (!controller.signal.aborted) onSeriesCreated();
        return;
      }
      const result = await createTask(
        branch,
        submission.input,
        controller.signal,
      );
      if (!controller.signal.aborted)
        onCreated(result.taskId, submission.assign);
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof TaskProgressError && error.status === 401)
        actions.invalidateSession(owner);
      if (
        source &&
        error instanceof TaskProgressError &&
        [403, 409, 412].includes(error.status)
      ) {
        setPending(null);
        setOptions(null);
        setHidden(true);
        onSourceInvalid?.();
        return;
      }
      if (
        error instanceof TaskProgressError &&
        [400, 403, 422].includes(error.status)
      ) {
        setPending(null);
        setError(
          "Chưa tạo công việc. Kiểm tra phạm vi, nhóm, vị trí, tiêu chí và kế hoạch.",
        );
      } else
        setError(
          "Chưa xác nhận tạo mới. Kiểm tra kết quả của cùng yêu cầu trước khi tạo công việc khác.",
        );
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <section aria-label="Tạo công việc vận hành">
      <h2>Tạo công việc vận hành</h2>
      {error && <p role="alert">{userMessage(error)}</p>}
      {pending && (
        <>
          <p>
            Mã tham chiếu tạo mới: {pending.recurrence ? "SERIES-" : "TASK-"}
            {pending.input.requestKey.replaceAll("-", "").toUpperCase()}
          </p>
          <button disabled={busy} onClick={() => void save(pending.assign)}>
            Kiểm tra kết quả tạo mới
          </button>
        </>
      )}
      {!options && !pending && (
        <button onClick={() => setRevision((n) => n + 1)}>
          Tải lại lựa chọn tạo mới
        </button>
      )}
      {options && (
        <ActionDialog error={error} drawer title="Tạo công việc" busy={busy}>
          {branchName && (
            <p>
              <strong>Chi nhánh:</strong> {branchName}
            </p>
          )}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void save(false);
            }}
          >
            <fieldset disabled={busy || pending !== null}>
              <legend>Định nghĩa công việc</legend>
              {source && (
                <>
                  <p>
                    Liên kết với yêu cầu {referenceLabel(source, "requestId")}. The request stays
                    open after creation.
                  </p>
                  <label>
                    Điều kiện hoàn tất yêu cầu
                    <SearchableSelect
                      aria-label="Điều kiện hoàn tất yêu cầu"
                      required
                      value={mandatory}
                      onChange={(e) => setMandatory(e.target.value)}
                    >
                      <option value="">Chọn điều kiện</option>
                      <option value="required">Bắt buộc để hoàn tất</option>
                      <option value="optional">Không bắt buộc</option>
                    </SearchableSelect>
                  </label>
                </>
              )}
              <label>
                Tiêu đề công việc
                <input
                  required
                  maxLength={200}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </label>
              <label>
                Bộ phận
                <SearchableSelect
                  aria-label="Bộ phận"
                  required
                  value={department}
                  onChange={(e) => {
                    setDepartment(e.target.value);
                    setCategory("");
                    setRecurrence((r) => ({ ...r, defaultAssigneeId: 0 }));
                  }}
                >
                  <option value="">Chọn bộ phận</option>
                  {options.departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Nhóm công việc
                <SearchableSelect
                  aria-label="Nhóm công việc"
                  required
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                >
                  <option value="">Chọn nhóm</option>
                  {options.categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <p>Vệ sinh, sự cố và yêu cầu vật tư sử dụng quy trình riêng.</p>
              <label>
                Loại vị trí
                <SearchableSelect
                  aria-label="Loại vị trí"
                  value={locationType}
                  onChange={(e) => {
                    setLocationType(e.target.value);
                    setLocation("");
                  }}
                >
                  <option value="department">Chỉ trong bộ phận</option>
                  <option value="room">Phòng</option>
                  <option value="area">Khu vực</option>
                </SearchableSelect>
              </label>
              {locationType === "room" && (
                <label>
                  Số phòng
                  <input
                    aria-label="Số phòng"
                    required
                    list="operational-task-rooms"
                    value={roomNumber}
                    onChange={(e) => setRoomNumber(e.target.value)}
                    placeholder="Ví dụ: 201"
                  />
                  <datalist id="operational-task-rooms">
                    {rooms.map((r) => (
                      <option key={r.id} value={r.roomNumber}>
                        {r.floor.name}
                      </option>
                    ))}
                  </datalist>
                  {roomError && <span role="alert">{userMessage(roomError)}</span>}
                </label>
              )}
              {locationType === "area" && (
                <label>
                  Mã vị trí
                  <input
                    required
                    inputMode="numeric"
                    pattern="[1-9][0-9]*"
                    value={location}
                    onChange={(e) => setLocation(e.target.value)}
                  />
                </label>
              )}
              <label>
                Mô tả công việc
                <textarea
                  aria-label="Mô tả công việc"
                  maxLength={4000}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </label>
              <label>
                Kết quả mong đợi
                <textarea
                  aria-label="Kết quả mong đợi"
                  required
                  maxLength={4000}
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value)}
                />
              </label>
              <fieldset>
                <legend>Tiêu chí hoàn tất</legend>
                {criteria.map((criterion, index) => (
                  <div key={index}>
                    <label>
                      Tiêu chí {index + 1}
                      <input
                        required
                        maxLength={250}
                        value={criterion.label}
                        onChange={(e) =>
                          setCriteria((rows) =>
                            rows.map((c, i) =>
                              i === index ? { ...c, label: e.target.value } : c,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        checked={criterion.required}
                        onChange={(e) =>
                          setCriteria((rows) =>
                            rows.map((c, i) =>
                              i === index
                                ? { ...c, required: e.target.checked }
                                : c,
                            ),
                          )
                        }
                      />
                      Tiêu chí bắt buộc {index + 1}
                    </label>
                    <button
                      type="button"
                      disabled={criteria.length === 1}
                      onClick={() =>
                        setCriteria((rows) =>
                          rows.filter((_, i) => i !== index),
                        )
                      }
                    >
                      Xóa tiêu chí {index + 1}
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  disabled={criteria.length >= 100}
                  onClick={() =>
                    setCriteria((rows) => [
                      ...rows,
                      { label: "", required: true },
                    ])
                  }
                >
                  Thêm tiêu chí
                </button>
              </fieldset>
              <TaskPlanFields
                value={plan}
                onChange={setPlan}
                priorities={options.priorities}
              />
              {options.recurringAvailable && (
                <label>
                  <input
                    type="checkbox"
                    checked={repeat}
                    onChange={(e) => setRepeat(e.target.checked)}
                  />
                  Lặp công việc
                </label>
              )}
              {repeat && (
                <TaskRecurrenceFields
                  value={recurrence}
                  onChange={setRecurrence}
                  executors={options.defaultExecutors ?? []}
                />
              )}
              <button data-dialog-action>
                {repeat ? "Lưu chuỗi công việc định kỳ" : "Lưu công việc"}
              </button>
              <button
                data-dialog-action
                type="button"
                disabled={repeat}
                onClick={(e) => {
                  if (e.currentTarget.form?.reportValidity()) void save(true);
                }}
              >
                Lưu và phân công
              </button>
            </fieldset>
          </form>
        </ActionDialog>
      )}
    </section>
  );
}
