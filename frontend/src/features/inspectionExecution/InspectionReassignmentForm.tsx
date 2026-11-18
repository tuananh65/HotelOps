import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../../components/workspace-interactions";
import { displayCode } from "../../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readReassignment,
  inspectionAssignmentRejection,
  reassignInspection,
  type ReassignmentOptions,
} from "../../api/dev1/inspectionReassignment";

export function InspectionReassignmentForm({
  branch,
  id,
  reinspection = false,
}: {
  branch: string;
  id: number;
  reinspection?: boolean;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [refresh, setRefresh] = useState(0);
  const key = `${reinspection}:${branch}:${id}:${refresh}`;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    data: ReassignmentOptions;
  } | null>(null);
  const [inspector, setInspector] = useState("");
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [locked, setLocked] = useState(false);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setLoaded(null);
    setReview(false);
    setInspector("");
    setLocked(true);
    setMessage("");
    void readReassignment(branch, id, abort.signal, reinspection)
      .then((data) => {
        if (!abort.signal.aborted) {
          setLoaded({ owner, key, data });
          setLocked(false);
        }
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setMessage(
            "Không thể đổi người kiểm tra với hồ sơ hoặc quyền hiện tại.",
          );
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions, reinspection]);
  const data =
    owner && loaded?.owner === owner && loaded.key === key ? loaded.data : null;
  async function submit() {
    const scope = lifetime.current;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      !data?.canReassign ||
      !review ||
      locked ||
      writing.current
    )
      return;
    const isCurrent = () =>
      current.current.owner === owner &&
      current.current.key === key &&
      lifetime.current === scope;
    writing.current = true;
    setBusy(true);
    setMessage("");
    try {
      await reassignInspection(
        branch,
        data,
        Number(inspector),
        reason,
        crypto.randomUUID(),
        { signal: scope.signal, isCurrent },
        reinspection,
      );
      if (!scope.signal.aborted && isCurrent())
        (announceWorkflowSuccess(
          "MSG08: Đã đổi người kiểm tra. Tải lại phân công hiện tại.",
          "features/inspectionExecution/InspectionReassignmentForm.tsx",
        ),
          setMessage(
            "MSG08: Đã đổi người kiểm tra. Tải lại phân công hiện tại.",
          ));
    } catch (error) {
      if (!scope.signal.aborted && isCurrent()) {
        setMessage(
          inspectionAssignmentRejection(error) ??
            (error instanceof ApiError &&
            [400, 403, 409, 412, 428].includes(error.status)
              ? "Thay đổi bị từ chối. Tải lại phân công hiện tại trước khi thử lại."
              : "Chưa xác nhận thay đổi. Tải lại để kiểm tra phân công; không tự gửi lại."),
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (!scope.signal.aborted && isCurrent()) {
        setBusy(false);
        setReview(false);
        setLocked(true);
      }
    }
  }
  if (!owner) return null;
  return (
    <ActionDialog
      scope="features/inspectionExecution/InspectionReassignmentForm.tsx"
      title="Thay đổi người kiểm tra"
      busy={busy}
    >
      <section aria-label="Thay đổi người kiểm tra">
        <h2>Thay đổi người kiểm tra</h2>
        {message && <p role="status">{message}</p>}
        {data && (
          <p>
            Người kiểm tra hiện tại #{data.inspectorId}. State:{" "}
            {displayCode(data.state)}. Checklist and deadline remain unchanged.
          </p>
        )}
        {data?.canReassign && !locked && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              setReview(true);
            }}
          >
            <fieldset disabled={busy || review}>
              <label htmlFor={`reassign-inspector-${id}`}>
                Người kiểm tra mới
              </label>
              <select
                id={`reassign-inspector-${id}`}
                required
                value={inspector}
                onChange={(e) => setInspector(e.target.value)}
              >
                <option value="">Chọn người kiểm tra</option>
                {data.inspectors.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              <label>
                Lý do thay đổi
                <input
                  required
                  maxLength={500}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <button data-dialog-action type="submit">
                Xem lại đổi người kiểm tra
              </button>
            </fieldset>
            {review && (
              <div>
                <p>
                  Thay đổi đến{" "}
                  {
                    data.inspectors.find((p) => p.id === Number(inspector))
                      ?.name
                  }
                  : {reason}
                </p>
                <button
                  data-dialog-action
                  type="button"
                  disabled={busy}
                  onClick={() => void submit()}
                >
                  Xác nhận đổi người kiểm tra
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setReview(false)}
                >
                  Chỉnh sửa thay đổi
                </button>
              </div>
            )}
          </form>
        )}
        {data && !data.canReassign && (
          <p>
            Không có người kiểm tra thay thế đủ điều kiện với quyền hiện tại.
          </p>
        )}
        <button
          type="button"
          disabled={busy}
          onClick={() => setRefresh((v) => v + 1)}
        >
          Tải lại lựa chọn người kiểm tra
        </button>
      </section>
    </ActionDialog>
  );
}
