import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { ActionDialog } from "../../components/workspace-interactions";
import { useEffect, useRef, useState, type ComponentProps } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import { inspectionAssignmentRejection } from "../../api/dev1/inspectionReassignment";
import { parseAssignmentContext } from "../inspectionAssignment/navigation";
import { InspectionReassignmentForm } from "./InspectionReassignmentForm";
import { useWorkspacePermissions } from "../../screens/housekeeping/useWorkspacePermissions";
import {
  assignInitialInspection,
  readInitialInspectionOptions,
  type InitialInspectionOptions,
} from "../../api/dev1/initialInspectionAssignment";

function InlineEditor({ children }: ComponentProps<typeof ActionDialog>) {
  return <>{children}</>;
}

export function InitialInspectionAssignmentPage() {
  const { state } = useAuth();
  const { taskId: id = "" } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") ?? "";
  const identity = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  const context = parseAssignmentContext(params.toString());
  if (
    !context ||
    context.roomId !== undefined ||
    !/^[1-9][0-9]*$/.test(id) ||
    !Number.isSafeInteger(Number(id))
  )
    return <p role="alert">Đích phân công kiểm tra không hợp lệ.</p>;
  return (
    <Assignment
      key={`${branch}:${id}:${revision.current}`}
      branch={branch}
      id={id}
    />
  );
}

export function Assignment({
  branch,
  id,
  embedded = false,
  onClose,
}: {
  branch: string;
  id: string;
  embedded?: boolean;
  onClose?: () => void;
}) {
  const { state, actions } = useAuth();
  const access = useWorkspacePermissions(branch);
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [refresh, setRefresh] = useState(0);
  const key = `${branch}:${id}:${refresh}`;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: InitialInspectionOptions;
    error?: string;
  } | null>(null);
  const [inspector, setInspector] = useState("");
  const [due, setDue] = useState("");
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(false);
  const [message, setMessage] = useState("");
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    source: string;
    id: number;
    inspectorId: number;
  } | null>(null);
  const sourceKey = `${branch}:${id}`;
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setResult(null);
    setReview(false);
    setInspector("");
    void readInitialInspectionOptions(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setResult({
            owner,
            key,
            error: "Chu kỳ vệ sinh này chưa khả dụng hoặc bạn không còn quyền.",
          });
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const confirmed =
    owner && saved?.owner === owner && saved.source === sourceKey
      ? saved
      : null;
  async function submit() {
    const scope = lifetime.current;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      !data?.canAssign ||
      !review ||
      writing.current ||
      locked ||
      confirmed
    )
      return;
    const inspectorId = Number(inspector),
      deadline = new Date(due);
    if (
      !data.inspectors.some((p) => p.id === inspectorId) ||
      !Number.isFinite(deadline.getTime()) ||
      deadline.getTime() <= Date.now() ||
      !reason.trim() ||
      reason.trim().length > 500
    ) {
      setMessage("Chọn người kiểm tra hợp lệ, hạn tương lai và lý do.");
      return;
    }
    writing.current = true;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      current.current.owner === owner &&
      current.current.key === key &&
      lifetime.current === scope;
    try {
      const assigned = await assignInitialInspection(
        branch,
        id,
        data.taskETag,
        crypto.randomUUID(),
        {
          inspectorId,
          checklistVersionId: data.checklistVersionId!,
          cycleETag: data.cycleETag,
          dueAt: deadline.toISOString(),
          reason: reason.trim(),
        },
        { signal: scope.signal, isCurrent },
      );
      if (!scope.signal.aborted && isCurrent()) {
        setSaved({ owner, source: sourceKey, id: assigned.id, inspectorId });
        setLocked(true);
        setReview(false);
      }
    } catch (error) {
      if (!scope.signal.aborted && isCurrent()) {
        setLocked(true);
        setReview(false);
        setMessage(
          inspectionAssignmentRejection(error) ??
            (error instanceof ApiError &&
            [400, 403, 409, 412, 428].includes(error.status)
              ? "Phân công bị từ chối. Tải lại lần kiểm tra hiện tại; không tự gửi lại."
              : "Chưa xác nhận kết quả phân công. Tải lại để kiểm tra trạng thái; không gửi thêm phân công."),
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (!scope.signal.aborted && isCurrent()) setBusy(false);
    }
  }
  if (!owner) return <p role="status">Đang kiểm tra phiên đăng nhập…</p>;
  const Editor = embedded ? InlineEditor : ActionDialog;
  const content = (
    <main>
      <h1>Phân công kiểm tra</h1>
      <Link to={`/inspections/assignment?branchId=${branch}`}>
        Các lượt kiểm tra đang chờ
      </Link>
      {!visible && <p role="status">Đang tải chu kỳ vệ sinh…</p>}
      {visible?.error && <p role="alert">{userMessage(visible.error)}</p>}
      {confirmed && (
        <p role="status">
          <span>Đã phân công kiểm tra. Mã kiểm tra {referenceLabel(confirmed, "id")}.</span>
          {access.allows("INSPECTION_PERFORM") &&
            String(confirmed.inspectorId) === state.session?.user.id && (
              <Link to={`/inspections/${branch}/${confirmed.id}/execution`}>
                Mở kiểm tra
              </Link>
            )}
        </p>
      )}
      {message && <p role="alert">{userMessage(message)}</p>}
      {data && (
        <>
          <h2>Phòng {data.roomNumber}</h2>
          <p>Chu kỳ vệ sinh {referenceLabel(data, "taskId")}</p>
          <p>
            {data.checklistName} · Phiên bản {data.versionNo}
          </p>
          {data.existingInspectionId !== null && (
            <p role="status">
              <span>
                Đã có phân công kiểm tra: {referenceLabel(data, "existingInspectionId")}.
              </span>
              {access.allows("INSPECTION_PERFORM") &&
                String(data.existingInspectorId) === state.session?.user.id && (
                  <Link
                    to={`/inspections/${branch}/${data.existingInspectionId}/execution`}
                  >
                    Mở kiểm tra
                  </Link>
                )}
            </p>
          )}
          {data.existingInspectionId !== null &&
            access.allows("INSPECTION_PERFORM") && (
              <InspectionReassignmentForm
                key={`${key}:${data.existingInspectionId}`}
                branch={branch}
                id={data.existingInspectionId}
              />
            )}
          {!data.canAssign && data.existingInspectionId === null && (
            <p role="status">
              {data.unavailableReason === "CHECKLIST_UNAVAILABLE"
                ? "Chưa có danh sách kiểm tra đã công bố còn hiệu lực cho loại phòng này. Công bố danh sách hợp lệ trước khi phân công."
                : data.unavailableReason === "NO_ELIGIBLE_INSPECTOR"
                  ? "Không có người kiểm tra đủ điều kiện cho chu kỳ vệ sinh này."
                  : "Chức năng lưu phân công chưa sẵn sàng hoặc quyền thực hiện đã thay đổi. Liên hệ quản trị để kiểm tra cấu hình."}
            </p>
          )}
          {data.canAssign && !confirmed && !locked && (
            <Editor
              error={message}
              title="Phân công kiểm tra phòng"
              busy={busy}
            >
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  setMessage("");
                  setReview(true);
                }}
              >
                <fieldset disabled={busy || review}>
                  <label htmlFor="inspection-inspector">Người kiểm tra</label>
                  <SearchableSelect
                    id="inspection-inspector"
                    required
                    value={inspector}
                    onChange={(event) => setInspector(event.target.value)}
                  >
                    <option value="">Chọn người kiểm tra</option>
                    {data.inspectors.map((person) => (
                      <option key={person.id} value={person.id}>
                        {person.name}
                      </option>
                    ))}
                  </SearchableSelect>
                  <label>
                    Thời hạn
                    <input
                      required
                      type="datetime-local"
                      value={due}
                      onChange={(event) => setDue(event.target.value)}
                    />
                  </label>
                  <label>
                    Lý do phân công
                    <textarea
                      required
                      maxLength={500}
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                    />
                  </label>
                  <button data-dialog-action type="submit">
                    Xem lại phân công
                  </button>
                </fieldset>
                {review && (
                  <section aria-label="Xem lại phân công kiểm tra">
                    <p>
                      Phân công đến{" "}
                      {
                        data.inspectors.find(
                          (person) => person.id === Number(inspector),
                        )?.name
                      }{" "}
                      bởi {due.replace("T", " ")}.
                    </p>
                    <p>{reason}</p>
                    <button
                      data-dialog-action
                      type="button"
                      disabled={busy}
                      onClick={() => void submit()}
                    >
                      Xác nhận phân công
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => setReview(false)}
                    >
                      Chỉnh sửa phân công
                    </button>
                  </section>
                )}
              </form>
            </Editor>
          )}
        </>
      )}
      {busy && <p role="status">Đang chờ phân công…</p>}
      <button
        type="button"
        disabled={busy}
        onClick={() => setRefresh((value) => value + 1)}
      >
        Tải lại kiểm tra
      </button>
    </main>
  );
  return embedded ? (
    <ActionDialog
      title="Phân công kiểm tra phòng"
      open
      busy={busy}
      onClose={onClose}
    >
      {content}
    </ActionDialog>
  ) : (
    content
  );
}
