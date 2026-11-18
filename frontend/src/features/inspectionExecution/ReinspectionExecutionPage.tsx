import { ActionDialog } from "../../components/workspace-interactions";
import { displayCode } from "../../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readReinspection,
  concludeReinspection,
  reinspectionEvidenceUrl,
  type ReinspectionExecution,
  type ReinspectionConclusion,
} from "../../api/dev1/reinspectionExecution";
import { encodeEvidence } from "./model";
import { RequestReworkForm } from "./RequestReworkForm";

export function ReinspectionExecutionPage() {
  const { branch = "", id = "" } = useParams();
  const { state } = useAuth();
  const identity = useRef(state.authorityGeneration),
    revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  return (
    <Execution
      key={`${branch}:${id}:${revision.current}`}
      branch={branch}
      id={id}
    />
  );
}
function Execution({ branch, id }: { branch: string; id: string }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const lifetime = useRef<AbortController | null>(null),
    writing = useRef(false);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    data: ReinspectionExecution;
  } | null>(null);
  const [decisions, setDecisions] = useState<
    Record<number, { fixed: string; note: string; files: File[] }>
  >({});
  const [reason, setReason] = useState(""),
    [message, setMessage] = useState("");
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [locked, setLocked] = useState(false);
  const [showValidation, setShowValidation] = useState(false);
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    data: ReinspectionConclusion;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setLoaded(null);
    setDecisions({});
    setReview(false);
    setShowValidation(false);
    void readReinspection(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted && current.current === owner) {
          setLoaded({ owner, data });
          setDecisions(
            Object.fromEntries(
              data.defects.map((d) => [
                d.id,
                { fixed: "", note: "", files: [] },
              ]),
            ),
          );
        }
      })
      .catch((error) => {
        if (!abort.signal.aborted && current.current === owner) {
          setMessage("Kiểm tra lại chưa khả dụng hoặc bạn không còn quyền.");
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, branch, id, actions]);
  const data = owner && loaded?.owner === owner ? loaded.data : null;
  const result =
    owner && saved?.owner === owner ? saved.data : (data?.conclusion ?? null);
  const terminalRead = data && data.state !== "ASSIGNED" ? data : null;
  const change = (
    defect: number,
    value: Partial<{ fixed: string; note: string; files: File[] }>,
  ) =>
    setDecisions((old) => ({ ...old, [defect]: { ...old[defect], ...value } }));
  async function submit() {
    const abort = lifetime.current;
    if (
      !owner ||
      !data ||
      !abort ||
      abort.signal.aborted ||
      !review ||
      writing.current ||
      locked ||
      result
    )
      return;
    const selected = data.defects.map((d) => ({
      id: d.id,
      ...decisions[d.id],
    }));
    if (
      selected.some((d) => !["FIXED", "NOT_FIXED"].includes(d.fixed)) ||
      !reason.trim() ||
      selected.reduce((n, d) => n + d.files.length, 0) > 5
    ) {
      setMessage("Quyết định từng lỗi, nhập lý do và chọn tối đa 5 ảnh.");
      return;
    }
    writing.current = true;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      current.current === owner && lifetime.current === abort;
    let sending = false;
    try {
      const defects = await Promise.all(
        selected.map(async (d) => ({
          defectId: d.id,
          fixed: d.fixed === "FIXED",
          note: d.note.trim(),
          photos: await Promise.all(
            d.files.map((file) => encodeEvidence(file, abort.signal)),
          ),
        })),
      );
      abort.signal.throwIfAborted();
      if (!isCurrent()) return;
      sending = true;
      const concluded = await concludeReinspection(
        branch,
        id,
        data,
        crypto.randomUUID(),
        reason.trim(),
        defects,
        { signal: abort.signal, isCurrent },
      );
      if (isCurrent() && !abort.signal.aborted) {
        setSaved({ owner, data: concluded });
        setLocked(true);
        setReview(false);
      }
    } catch (error) {
      if (isCurrent() && !abort.signal.aborted) {
        setReview(false);
        setLocked(sending);
        setMessage(
          !sending
            ? "Ảnh đã chọn không hợp lệ. Chọn ảnh PNG hoặc JPEG và kiểm tra lại."
            : error instanceof ApiError &&
                [400, 403, 409, 412, 428].includes(error.status)
              ? "Kết luận bị từ chối. Tải lại trạng thái hiện tại; không tự gửi lại."
              : "Chưa xác nhận kết luận. Không gửi kết luận khác; kiểm tra hồ sơ hiện tại.",
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (isCurrent() && !abort.signal.aborted) setBusy(false);
    }
  }
  if (!owner) return <p role="status">Đang kiểm tra phiên đăng nhập…</p>;
  return (
    <main>
      <h1>Kiểm tra lại phòng</h1>
      <Link to="/notifications">Thông báo</Link>
      {data && (
        <p>
          <Link to={`/reinspections/${branch}/${id}/original`}>
            Lần kiểm tra ban đầu #{data.originalInspectionId}
          </Link>
        </p>
      )}
      {message && <p role="alert">{message}</p>}
      {!data && !message && <p role="status">Đang tải kiểm tra lại…</p>}
      {result && (
        <section aria-label="Kết luận kiểm tra lại">
          {saved?.owner === owner && (
            <p role="status">Đã lưu kết quả kiểm tra lại.</p>
          )}
          <p role="status">
            Kiểm tra lại {result.state === "PASSED" ? "Đạt" : "Không đạt"}.
            Rework{" "}
            {result.reworkState === "COMPLETED" ? "Đã hoàn tất" : "Không đạt"}.
          </p>
          <p>
            {terminalRead
              ? `Mức sẵn sàng của phòng hiện tại: ${terminalRead.effectiveReadiness}.`
              : result.effectiveReadiness === "BLOCKED"
                ? "Phòng vẫn bị khóa (MSG17)."
                : result.state === "PASSED"
                  ? "Phòng sẵn sàng tại thời điểm kết luận."
                  : "Chu kỳ vệ sinh cần làm lại lần nữa."}
          </p>
          {terminalRead && (
            <>
              <p>
                Kết quả đã ghi nhận: {displayCode(result.state)}; Mức sẵn sàng
                khi kết luận: {displayCode(result.effectiveReadiness)}.
              </p>
              {terminalRead.defects.map((d) => (
                <section key={d.id} aria-label={`Lỗi đã ghi nhận ${d.id}`}>
                  <h2>{d.description}</h2>
                  <p>Khắc phục: {d.remediation}</p>
                  <p>Kết quả: {d.fixed ? "Đã khắc phục" : "Chưa khắc phục"}</p>
                  <p>{d.note}</p>
                  {(
                    [
                      ["Ban đầu", d.originalPhotos],
                      ["Khắc phục", d.remediationPhotos],
                      ["Kiểm tra lại", d.reinspectionPhotos],
                    ] as const
                  ).map(([label, files]) => (
                    <p key={label}>
                      {files.map((file) => (
                        <a
                          key={file}
                          href={reinspectionEvidenceUrl(branch, id, file)}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {label} ảnh #{file}{" "}
                        </a>
                      ))}
                    </p>
                  ))}
                </section>
              ))}
            </>
          )}
          <p>Thông báo đang chờ gửi.</p>
          {result.state === "FAILED" && (
            <RequestReworkForm
              branch={Number(branch)}
              id={Number(id)}
              onDenied={(status) => {
                setLoaded(null);
                setSaved(null);
                setMessage(
                  "Quyền hiện tại không cho phép xem lần làm lại kế tiếp.",
                );
                if (status === 401) actions.invalidateSession(owner);
              }}
              onSessionInvalid={() => actions.invalidateSession(owner)}
            />
          )}
        </section>
      )}
      {data && !result && (
        <>
          <p>
            Phòng #{data.roomId} · Làm lại #{data.reworkId} · Chu kỳ vệ sinh #
            {data.cleaningTaskId}
          </p>
          {data.effectiveReadiness === "BLOCKED" && (
            <p role="status">Phòng bị khóa (MSG17).</p>
          )}
          <form
            onInvalidCapture={() => setShowValidation(true)}
            onSubmit={(e) => {
              e.preventDefault();
              setMessage("");
              setReview(true);
            }}
          >
            <fieldset disabled={busy || locked || review}>
              <legend>Quyết định xử lý lỗi</legend>
              {data.defects.map((d) => (
                <section key={d.id} aria-label={`Lỗi ${d.id}`}>
                  <h2>{d.description}</h2>
                  <p>Khắc phục: {d.remediation}</p>
                  {(
                    [
                      ["Ban đầu", d.originalPhotos],
                      ["Khắc phục", d.remediationPhotos],
                    ] as const
                  ).map(([label, photos]) => (
                    <p key={label}>
                      {photos.map((file) => (
                        <a
                          key={file}
                          href={reinspectionEvidenceUrl(branch, id, file)}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {label} ảnh #{file}{" "}
                        </a>
                      ))}
                    </p>
                  ))}
                  <label htmlFor={`decision-${d.id}`}>
                    Kết quả lỗi #{d.id}
                  </label>
                  <select
                    id={`decision-${d.id}`}
                    required
                    aria-invalid={showValidation && !decisions[d.id]?.fixed}
                    aria-describedby={
                      showValidation && !decisions[d.id]?.fixed
                        ? `decision-error-${d.id}`
                        : undefined
                    }
                    value={decisions[d.id]?.fixed ?? ""}
                    onChange={(e) => change(d.id, { fixed: e.target.value })}
                  >
                    <option value="">Chọn kết quả</option>
                    <option value="FIXED">Đã khắc phục</option>
                    <option value="NOT_FIXED">Chưa khắc phục</option>
                  </select>
                  {showValidation && !decisions[d.id]?.fixed && (
                    <p
                      id={`decision-error-${d.id}`}
                      role="alert"
                      style={{ color: "#b91c1c" }}
                    >
                      Kết quả: chọn Đã khắc phục hoặc Chưa khắc phục. Kiểm tra
                      và thử lại.
                    </p>
                  )}
                  <label htmlFor={`note-${d.id}`}>Ghi chú lỗi #{d.id}</label>
                  <textarea
                    id={`note-${d.id}`}
                    maxLength={500}
                    value={decisions[d.id]?.note ?? ""}
                    onChange={(e) => change(d.id, { note: e.target.value })}
                  />
                  <label htmlFor={`photos-${d.id}`}>Ảnh lỗi #{d.id}</label>
                  <input
                    id={`photos-${d.id}`}
                    type="file"
                    accept="image/png,image/jpeg"
                    multiple
                    onChange={(e) =>
                      change(d.id, { files: Array.from(e.target.files ?? []) })
                    }
                  />
                </section>
              ))}
              <label htmlFor="conclusion-reason">Lý do kết luận</label>
              <textarea
                id="conclusion-reason"
                required
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
              <button type="submit">Xem lại kết luận</button>
            </fieldset>
            {review && !locked && (
              <ActionDialog
                open
                title="Xem lại kết luận"
                onClose={() => setReview(false)}
                busy={busy}
                error={message}
              >
                <section aria-label="Xem lại kết luận">
                  <p>
                    {data.defects.every(
                      (d) => decisions[d.id]?.fixed === "FIXED",
                    )
                      ? "Tất cả lỗi đã khắc phục. Kết luận đạt."
                      : "Còn lỗi chưa khắc phục. Kết luận không đạt và yêu cầu làm lại."}
                  </p>
                  <p>{reason}</p>
                  <button
                    data-dialog-action
                    type="button"
                    disabled={busy}
                    onClick={() => void submit()}
                  >
                    Xác nhận kết luận
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setReview(false)}
                  >
                    Chỉnh sửa quyết định
                  </button>
                </section>
              </ActionDialog>
            )}
          </form>
        </>
      )}
      {busy && <p role="status">Đang chờ kết luận…</p>}
    </main>
  );
}
