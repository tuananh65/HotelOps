import { userMessage } from "../i18n/userMessage";
import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import { safeId } from "./contracts";
import { StatusBadge } from "../components/design-system";
import { staffDate } from "../internalRequests/staffDate";
type Summary = {
  name: string;
  departmentName: string;
  shiftDate: string;
  startUtc: string;
  endUtc: string;
  assignmentStatus: string;
  cancellationReason: string | null;
};
export function ShiftAssignmentSummary({
  branchId,
  assignmentId,
}: {
  branchId: number;
  assignmentId: number;
}) {
  const { state, actions } = useAuth(),
    owner = state.authorityGeneration;
  const [result, setResult] = useState<Summary | null>(null),
    [error, setError] = useState("");
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError("");
    void createHttpClient()({
      method: "GET",
      url: "/api/branches/" + branchId + "/workdays/own-shifts/" + assignmentId,
      expectedStatus: 200,
      signal: abort.signal,
      decode(v) {
        if (
          !isObject(v) ||
          v.branchId !== branchId ||
          v.shiftAssignmentId !== assignmentId ||
          !safeId(v.shiftId) ||
          !safeId(v.departmentId) ||
          typeof v.name !== "string" ||
          typeof v.departmentName !== "string" ||
          typeof v.shiftDate !== "string" ||
          typeof v.startUtc !== "string" ||
          typeof v.endUtc !== "string" ||
          !["ASSIGNED", "CANCELLED"].includes(String(v.assignmentStatus)) ||
          (v.cancellationReason !== null &&
            typeof v.cancellationReason !== "string")
        )
          return;
        return v as Summary;
      },
    }).then((r) => {
      if (abort.signal.aborted) return;
      if (r.ok) setResult(r.data);
      else {
        setError("Không thể phân công hoặc quyền đã thay đổi.");
        if (r.error.kind === "http" && r.error.status === 401)
          actions.invalidateSession(owner);
      }
    });
    return () => abort.abort();
  }, [branchId, assignmentId, owner, actions]);
  return (
    <section className="time-card" aria-label="Ca được phân">
      <h2>Ca được phân</h2>
      {error ? (
        <p role="alert">{userMessage(error)}</p>
      ) : result ? (
        <>
          <h3>{result.name}</h3>
          <p>{result.departmentName}</p>
          <StatusBadge>
            {result.assignmentStatus === "ASSIGNED" ? "Đã phân công" : "Đã hủy"}
          </StatusBadge>
          {result.cancellationReason && (
            <p>Lý do hủy: {result.cancellationReason}</p>
          )}
          <dl className="time-summary">
            <div>
              <dt>Bắt đầu ca</dt>
              <dd>{staffDate(result.startUtc)}</dd>
            </div>
            <div>
              <dt>Kết thúc ca</dt>
              <dd>{staffDate(result.endUtc)}</dd>
            </div>
          </dl>
        </>
      ) : (
        <p>Đang tải phân công được phép truy cập...</p>
      )}
    </section>
  );
}
