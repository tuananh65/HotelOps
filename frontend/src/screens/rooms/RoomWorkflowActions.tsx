import { useNavigationAccess } from "../../shared-host/useNavigationAccess";
import { userMessage } from "../../i18n/userMessage";
import { useEffect, useMemo, useState } from "react";
import { ActionLink as Link } from "../../components/ActionLink";
import { cleaningActionHref } from "../../api/dev1/roomActions";
import { createError } from "../../api/dev1/cleaningCreate";
import { readInitialInspectionQueue } from "../../api/dev1/initialInspectionAssignment";
import { useWorkspacePermissions } from "../housekeeping/useWorkspacePermissions";

export default function RoomWorkflowActions({
  branch,
  room,
}: {
  branch: string;
  room: string;
}) {
  const access = useWorkspacePermissions(branch);
  const canClean = access.allows("CLEANING_ASSIGN");
  const canInspect = access.allows("INSPECTION_PERFORM");
  const admits = useNavigationAccess();
  const canReport =
    admits("/incidents/new") && access.allows("INCIDENT_CREATE", true);
  const [retry, setRetry] = useState(0);
  const contextKey = useMemo(
    () => Symbol("room-action"),
    [branch, room, retry],
  );
  const [result, setResult] = useState<{
    key: symbol;
    href?: string;
    error?: string;
  } | null>(null);
  const [inspection, setInspection] = useState<{
    key: symbol;
    available: boolean;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    if (canInspect)
      void readInitialInspectionQueue(branch, room, 1, abort.signal)
        .then((data) => {
          if (!abort.signal.aborted)
            setInspection({
              key: contextKey,
              available: data.items.length > 0,
            });
        })
        .catch(() => {
          if (!abort.signal.aborted)
            setInspection({ key: contextKey, available: false });
        });
    if (canClean)
      void cleaningActionHref(branch, room, abort.signal)
        .then((href) => {
          if (!abort.signal.aborted) setResult({ key: contextKey, href });
        })
        .catch((error) => {
          if (!abort.signal.aborted)
            setResult({ key: contextKey, error: createError(error) });
        });
    return () => abort.abort();
  }, [branch, room, contextKey, canClean, canInspect]);
  const current = result?.key === contextKey ? result : null;
  if (access.error)
    return (
      <p role="status">
        Chưa tải được các thao tác.{" "}
        <button onClick={access.reload}>Thử lại</button>
      </p>
    );
  if (!canClean && !canInspect && !canReport) return null;
  return (
    <section aria-label="Hành động tiếp theo">
      <h3>Hành động tiếp theo</h3>
      {canClean &&
        (current?.href ? (
          <Link to={current.href}>Tạo công việc vệ sinh</Link>
        ) : (
          <>
            <p>
              {userMessage(current?.error || "Đang kiểm tra quyền và phòng…")}
            </p>
            {current?.error && (
              <button
                onClick={() => {
                  setResult(null);
                  setRetry((v) => v + 1);
                }}
              >
                Kiểm tra lại hành động
              </button>
            )}
          </>
        ))}
      <p>
        {inspection?.key === contextKey && inspection.available ? (
          <Link
            to={`/inspections/assignment?branchId=${branch}&roomId=${room}`}
          >
            Phân công kiểm tra phòng
          </Link>
        ) : null}
      </p>
      {canReport && (
        <p>
          <Link
            to={`/operations/incidents?action=A004A-021&branchId=${branch}&roomId=${room}`}
          >
            Báo sự cố cho phòng này
          </Link>
        </p>
      )}
    </section>
  );
}
