import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { displayCode } from "../../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readRework,
  mutateRework,
  type ReworkExecution,
  type ReworkCommand,
} from "../../api/dev1/reworkExecution";
import { ReworkSubmissionForm } from "./ReworkSubmissionForm";

export function AssignedReworkRead() {
  const { state, actions } = useAuth();
  const { id = "" } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") || "";
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [revision, setRevision] = useState(0);
  const key = `${branch}:${id}:${revision}`;
  const currentOwner = useRef(owner);
  currentOwner.current = owner;
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    source: string;
    submitted: boolean;
  } | null>(null);
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: ReworkExecution;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setBusy(false);
    setResult(null);
    void readRework(branch, id, abort.signal)
      .then((data) => {
        if (
          !["READ_ONLY", "ACCEPT_START", "RESUME_REWORK", "SUBMIT_REWORK"].includes(
            data.capability,
          ) ||
          ![
            "PENDING_ACCEPTANCE",
            "ACCEPTED",
            "IN_PROGRESS",
            "ON_HOLD",
            "PENDING_REINSPECTION",
            "COMPLETED",
            "FAILED",
          ].includes(data.state)
        )
          throw new ApiError(502, "UNSUPPORTED_REWORK_READ_STATE");
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted)
          setResult({
            owner,
            key,
            error:
              "Công việc làm lại chưa khả dụng hoặc bạn không còn quyền. Tải lại để thử tiếp.",
          });
        if (
          !abort.signal.aborted &&
          error instanceof ApiError &&
          error.status === 401
        )
          actions.invalidateSession(owner);
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  async function execute(
    action: "accept" | "start" | "complete",
    command?: ReworkCommand,
  ) {
    const scope = lifetime.current;
    const data = visible?.data;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      writing.current ||
      !data ||
      currentOwner.current !== owner ||
      data.capability !==
        (action === "complete" ? "SUBMIT_REWORK"
          : action === "start" && data.state === "ON_HOLD" ? "RESUME_REWORK" : "ACCEPT_START") ||
      data.state !==
        (action === "accept"
          ? "PENDING_ACCEPTANCE"
          : action === "start"
            ? (data.capability === "RESUME_REWORK" ? "ON_HOLD" : "ACCEPTED")
            : "IN_PROGRESS") ||
      (action === "complete" && !command)
    )
      return;
    writing.current = true;
    setBusy(true);
    try {
      await mutateRework(
        branch,
        id,
        action,
        data.sourceETag,
        crypto.randomUUID(),
        command || {
          note: action === "accept" ? "Kết quả làm lại đã chấp nhận"
            : data.state === "ON_HOLD" ? "Đã tiếp tục làm lại sau khi hết ca" : "Đã bắt đầu làm lại",
        },
        {
          signal: scope.signal,
          isCurrent: () => currentOwner.current === owner,
        },
      );
      if (!scope.signal.aborted && currentOwner.current === owner) {
        setSaved({
          owner,
          source: `${branch}:${id}`,
          submitted: action === "complete",
        });
        setRevision((v) => v + 1);
      }
    } catch (error) {
      if (!scope.signal.aborted && currentOwner.current === owner) {
        setResult({
          owner,
          key,
          error:
            error instanceof ApiError && error.code === "TIME_WORKDAY_REQUIRED"
              ? "Bắt đầu ngày làm việc, rồi tải lại công việc làm lại trước khi bắt đầu."
              : error instanceof ApiError &&
                  error.code === "TIME_INTERVAL_ALREADY_OPEN"
                ? "Hoàn tất hoặc tạm dừng công việc đang chạy trước khi bắt đầu làm lại. Tải lại để thử tiếp."
                : "Chưa xác nhận thay đổi. Tải lại công việc làm lại trước khi thử tiếp.",
        });
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (currentOwner.current === owner) setBusy(false);
    }
  }
  return (
    <main aria-label="Công việc vệ sinh lại được giao">
      <h1>Công việc vệ sinh lại được giao</h1>
      <Link to="/notifications">Thông báo</Link>
      <button
        disabled={!owner || busy}
        onClick={() => setRevision((v) => v + 1)}
      >
        Tải lại làm lại
      </button>
      {owner && !visible && <p role="status">Đang tải làm lại...</p>}
      {visible?.error && <p role="alert">{userMessage(visible.error)}</p>}
      {visible?.data &&
        saved?.owner === owner &&
        saved.source === `${branch}:${id}` && (
          <p role="status">
            {saved.submitted
              ? "Đã nộp kết quả làm lại và đang chờ kiểm tra lại."
              : "Đã lưu kết quả làm lại."}
          </p>
        )}
      {busy && <p role="status">Đang lưu làm lại...</p>}
      {visible?.data && (
        <>
          <p>
            Làm lại {referenceLabel(visible.data, "id")}:{" "}
            {visible.data.state.replaceAll("_", " ").toLowerCase()}.
          </p>
          <p>
            Phòng {referenceLabel(visible.data, "roomId")}; kiểm tra không đạt{" "}
            {referenceLabel(visible.data, "inspectionId")}.
          </p>
          <p>Đến hạn: {visible.data.dueAtUtc || "Chưa chỉ định"}</p>
          {visible.data.conclusion && (
            <section aria-label="Kết quả kiểm tra lại">
              <p>Kiểm tra lại {referenceLabel(visible.data.conclusion, "reinspectionId")}: {visible.data.conclusion.state.toLowerCase()}.</p>
              <p>Mức sẵn sàng của phòng hiện tại: {displayCode(visible.data.conclusion.effectiveReadiness)}.</p>
              <p>Mức sẵn sàng khi kết luận: {visible.data.conclusion.readinessAtConclusion}.</p>
              <p>Công việc làm lại đã đóng. Khả dụng phòng được kiểm tra theo điều kiện hiện tại.</p>
            </section>
          )}
          <ul>
            {visible.data.defects.map((defect) => (
              <li key={defect.id}>
                <p>{defect.description}</p>
                {defect.remediation && <p>Khắc phục: {defect.remediation}</p>}
                {defect.remediationAttachmentIds.map((file) => (
                  <a
                    key={`remediation-${file}`}
                    href={`/api/branches/${visible.data!.branchId}/reworks/${visible.data!.id}/evidence/${file}`}
                  >
                    Xem ảnh khắc phục {file}
                  </a>
                ))}
                {defect.inspectionAttachmentIds.map((file) => (
                  <a
                    key={file}
                    href={`/api/branches/${visible.data!.branchId}/reworks/${visible.data!.id}/evidence/${file}`}
                  >
                    Xem ảnh lỗi {file}
                  </a>
                ))}
              </li>
            ))}
          </ul>
          {visible.data.capability === "ACCEPT_START" &&
            visible.data.state === "PENDING_ACCEPTANCE" && (
              <button disabled={busy} onClick={() => void execute("accept")}>
                Tiếp nhận làm lại
              </button>
            )}
          {visible.data.capability === "ACCEPT_START" &&
            visible.data.state === "ACCEPTED" && (
              <button disabled={busy} onClick={() => void execute("start")}>
                Bắt đầu làm lại
              </button>
            )}
          {visible.data.capability === "RESUME_REWORK" &&
            visible.data.state === "ON_HOLD" && (
              <>
                <p>Đã tạm dừng do hết ca. Bắt đầu ngày làm việc trước khi tiếp tục.</p>
                <button disabled={busy} onClick={() => void execute("start")}>
                  Tiếp tục làm lại
                </button>
              </>
            )}
          {visible.data.capability === "READ_ONLY" && !visible.data.conclusion && (
            <p>
              {visible.data.state === "PENDING_REINSPECTION"
                ? "Đang chờ kiểm tra lại."
                : "Hiện chưa thể thực hiện thao tác tiếp theo."}
            </p>
          )}
          {visible.data.capability === "SUBMIT_REWORK" &&
            visible.data.state === "IN_PROGRESS" && (
              <ReworkSubmissionForm
                key={visible.data.sourceETag}
                data={visible.data}
                busy={busy}
                submit={(command) => execute("complete", command)}
              />
            )}
        </>
      )}
    </main>
  );
}
