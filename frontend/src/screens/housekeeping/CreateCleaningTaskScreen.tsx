import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  cleaningOptions,
  createCleaning,
  validateCleaningInput,
  type CreateInput,
  createError,
  type CreateOptions,
  cleaningSource,
  type CleaningSource,
} from "../../api/dev1/cleaningCreate";
import { ActionDialog } from "../../components/workspace-interactions";
import {
  useRoomWorkspace,
  useRoomChoices,
  RoomBranchField,
  RoomNumberField,
} from "./RoomWorkspace";
import { hotelInputToUtc, utcToHotelInput } from "./OperationalReadView";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
export default function CreateCleaningTaskScreen({
  onClose,
}: { onClose?: () => void } = {}) {
  const scope = useRoomWorkspace("cleaning");
  const choices = useRoomChoices(scope);
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const pending = useRef(false),
    controller = useRef<AbortController | null>(null);
  const [params] = useSearchParams(),
    nav = useNavigate();
  const [room, setRoom] = useState(params.get("roomId") || "");
  const branch = String(scope.branch?.id ?? ""),
    request = params.get("requestId") || "",
    key = branch + ":" + room + ":" + request + ":" + owner;
  const [result, setResult] = useState<{
      key: string;
      data: CreateOptions;
      source?: CleaningSource;
    } | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [retry, setRetry] = useState(0),
    [submitted, setSubmitted] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    pending.current = false;
    setBusy(false);
    setResult(null);
    setError("");
    setSubmitted(false);
    if (owner && branch && (room || request))
      (async () => {
        const source = request
          ? await cleaningSource(branch, request, abort.signal)
          : undefined;
        const selectedRoom =
          room || (source?.roomId ? String(source.roomId) : "");
        if (!selectedRoom) throw new ApiError(400, "SOURCE_ROOM_REQUIRED");
        return {
          data: await cleaningOptions(branch, selectedRoom, abort.signal),
          source,
        };
      })()
        .then(({ data, source }) => {
          if (!abort.signal.aborted) setResult({ key, data, source });
        })
        .catch((e) => {
          if (!abort.signal.aborted)
            setError(
              request && e instanceof ApiError && e.status === 403
                ? "MSG02: Tạo vệ sinh cần quyền công việc đích và thẩm quyền yêu cầu hiện tại. Liên kết công việc sẵn có hoặc nhờ người có trách nhiệm."
                : e instanceof ApiError && e.code === "SOURCE_ROOM_REQUIRED"
                  ? "Yêu cầu chưa có phòng. Chọn phòng trong chi nhánh của yêu cầu để tiếp tục."
                  : createError(e),
            );
        });
    return () => abort.abort();
  }, [branch, room, request, owner, key, retry]);
  const data = result?.key === key ? result.data : null;
  const source = result?.key === key ? result.source : undefined;
  function load(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const selected = String(f.get("roomId") || "");
    if (!choices.items?.some((r) => String(r.id) === selected)) {
      setError("Chọn số phòng trong chi nhánh đang làm việc.");
      return;
    }
    setRoom(selected);
    setRetry((v) => v + 1);
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const abort = controller.current;
    if (
      !data ||
      !owner ||
      busy ||
      submitted ||
      pending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    const f = new FormData(e.currentTarget);
    if (
      source &&
      !["required", "optional"].includes(String(f.get("mandatory")))
    ) {
      setError("Chọn việc vệ sinh này có bắt buộc để hoàn tất hay không.");
      return;
    }
    const assignImmediately =
      (e.nativeEvent as SubmitEvent).submitter?.getAttribute("name") ===
      "saveAndAssign";
    const input: CreateInput = {
      roomId: data.roomId,
      cleaningTypeId: Number(f.get("cleaningTypeId")),
      priorityId: Number(f.get("priorityId")),
      dueAt: hotelInputToUtc(String(f.get("dueAt") || "")) ?? "",
      instruction: String(f.get("instruction") || "").trim() || null,
      ...(source
        ? {
            internalRequestId: source.requestId,
            requestLink: {
              version: source.version,
              mandatory: f.get("mandatory") === "required",
            },
          }
        : {}),
    };
    try {
      validateCleaningInput(input);
    } catch {
      setError(
        "Chọn loại vệ sinh, ưu tiên và thời hạn trong tương lai; kiểm tra độ dài hướng dẫn.",
      );
      return;
    }
    if (data.existingTaskId) {
      setError("MSG03: Phòng đã có công việc vệ sinh đang mở.");
      return;
    }
    setBusy(true);
    setError("");
    setSubmitted(true);
    pending.current = true;
    try {
      const d = await createCleaning(branch, input, abort.signal);
      if (abort.signal.aborted) return;
      nav(
        assignImmediately
          ? `/cleaning/assignment?branchId=${branch}&taskId=${d.id}`
          : `/cleaning/tasks/${d.id}?branchId=${branch}&monitor=1&kind=cleaning`,
        {
          state: {
            cleaningCreatedId: d.id,
            cleaningCreatedBranchId: d.branchId,
            cleaningRoomBlocked: d.hasEffectiveBlock,
          },
        },
      );
    } catch (e) {
      if (!abort.signal.aborted) {
        setError(
          request
            ? e instanceof ApiError && e.status === 403
              ? "MSG02: Tạo vệ sinh cần quyền công việc đích và thẩm quyền yêu cầu hiện tại. Liên kết công việc sẵn có hoặc nhờ người có trách nhiệm."
              : "Chưa xác nhận lưu. Tải lại yêu cầu và công việc vệ sinh liên quan trước khi thử lại."
            : createError(e),
        );
        if (
          e instanceof ApiError &&
          e.status === 409 &&
          e.code === "DUPLICATE"
        ) {
          try {
            const latest = await cleaningOptions(
              branch,
              String(data.roomId),
              abort.signal,
            );
            if (!abort.signal.aborted) {
              setResult({ key, data: latest, source });
              setError(
                "MSG03: Phòng đã có công việc vệ sinh đang mở. Mở công việc hiện có bên dưới.",
              );
            }
          } catch {
            if (!abort.signal.aborted) setResult(null);
          }
        } else if (e instanceof ApiError && e.status === 400) {
          setSubmitted(false); // Explicit rejection: allow corrections, never auto-submit.
        } else if (request) setResult(null);
      }
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        pending.current = false;
      }
    }
  }
  return (
    <ActionDialog
      title="Tạo công việc vệ sinh"
      open
      busy={busy}
      onClose={
        onClose ??
        (() => nav(`/cleaning/tasks?branchId=${branch}`, { replace: true }))
      }
    >
      <RoomBranchField scope={scope} />
      <form className="filter-bar" onSubmit={load}>
        <RoomNumberField
          key={`${branch}:${!!choices.items}`}
          items={choices.items ?? []}
          selected={room}
          onSelect={setRoom}
          onUnmatched={() => setRoom("")}
        />
        {choices.error && (
          <p role="alert">
            {userMessage(choices.error)}
            <button type="button" onClick={choices.reload}>
              Thử lại
            </button>
          </p>
        )}
        <button disabled={busy}>Tải cấu hình phòng</button>
      </form>
      {error && <p role="alert">{userMessage(error)}</p>}
      {request && error && (
        <button disabled={busy} onClick={() => setRetry((v) => v + 1)}>
          Tải lại thông tin điền sẵn của yêu cầu
        </button>
      )}
      {!data && (
        <p>Chọn phòng và chi nhánh hợp lệ để tải cấu hình từ máy chủ.</p>
      )}
      {data && (
        <section className="panel form-panel">
          <p aria-label="Thời gian vệ sinh tiêu chuẩn">
            Thời gian vệ sinh tiêu chuẩn:{" "}
            {data.standardCleaningMinutes == null
              ? "Chưa khả dụng"
              : `${data.standardCleaningMinutes} phút`}
            . Thông tin kế hoạch tham khảo; nhập thời hạn bắt buộc bên dưới.
          </p>
          <h2>
            Phòng {data.roomNumber} · {scope.branch?.name}
          </h2>
          <p>
            {data.cleaningItemCount} hạng mục vệ sinh. Danh sách sẽ được ghi
            nhận khi nhân viên bắt đầu công việc.
          </p>
          <ul>
            {data.cleaningItems?.map((item) => (
              <li key={item.id}>
                {item.label}
                {item.isRequired ? " *" : ""}
              </li>
            ))}
          </ul>
          {data.existingTaskId && (
            <p role="alert">Phòng đã có công việc vệ sinh đang mở.</p>
          )}
          {data.existingTaskId && (
            <Link
              to={`/cleaning/tasks/${data.existingTaskId}?branchId=${branch}&monitor=1&kind=cleaning`}
            >
              Mở công việc vệ sinh hiện có
            </Link>
          )}
          <form key={`${key}:${retry}`} onSubmit={submit}>
            {source && (
              <>
                <p>
                  Yêu cầu nguồn: {referenceLabel(source, "requestId")}. Creating cleaning does not
                  close the request.
                </p>
                <label>
                  Điều kiện hoàn tất yêu cầu
                  <SearchableSelect
                    aria-label="Điều kiện hoàn tất yêu cầu"
                    name="mandatory"
                    required
                    defaultValue=""
                  >
                    <option value="">Chọn điều kiện</option>
                    <option value="required">Bắt buộc để hoàn tất</option>
                    <option value="optional">Không bắt buộc</option>
                  </SearchableSelect>
                </label>
              </>
            )}
            <label>
              Loại vệ sinh
              <SearchableSelect aria-label="Loại vệ sinh" name="cleaningTypeId" required>
                {data.cleaningTypes.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </SearchableSelect>
            </label>
            <label>
              Ưu tiên
              <SearchableSelect
                aria-label="Ưu tiên"
                name="priorityId"
                required
                defaultValue={source?.priorityId ?? undefined}
              >
                {data.priorities.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </SearchableSelect>
            </label>
            <label>
              Thời hạn (giờ Việt Nam)
              <input
                type="datetime-local"
                name="dueAt"
                required
                defaultValue={utcToHotelInput(source?.deadline)}
              />
            </label>
            <label>
              Hướng dẫn
              <input
                name="instruction"
                maxLength={4000}
                defaultValue={source?.instruction ?? ""}
              />
            </label>
            <button
              className="primary"
              disabled={
                busy ||
                submitted ||
                !!data.existingTaskId ||
                data.cleaningItemCount < 1 ||
                data.cleaningItemCount > 100
              }
            >
              {busy ? "Đang tạo…" : "Tạo công việc"}
            </button>
            <button
              name="saveAndAssign"
              disabled={
                busy ||
                submitted ||
                !!data.existingTaskId ||
                data.cleaningItemCount < 1 ||
                data.cleaningItemCount > 100
              }
            >
              Lưu và phân công
            </button>
          </form>
          {submitted && !busy && (
            <button
              onClick={() => {
                setSubmitted(false);
                setRetry((v) => v + 1);
              }}
            >
              Tải lại cấu hình sau khi kiểm tra Monitor
            </button>
          )}
        </section>
      )}
      <Link to={`/cleaning/tasks?branchId=${encodeURIComponent(branch)}`}>
        Về danh sách công việc
      </Link>
    </ActionDialog>
  );
}
