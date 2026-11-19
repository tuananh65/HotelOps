import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { displayCode } from "../../i18n/vi";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/dev1/client";
import {
  readReworkRecord,
  type ReworkRecord,
} from "../../api/dev1/reworkRecord";
import { hotelTime, roomLabel } from "./RoomWorkspace";
import { uploadFile, progressError } from "../../api/dev1/cleaningProgress";
import {
  readRework,
  mutateRework,
  validateReworkCommand,
  type ReworkAction,
  type ReworkCommand,
  type ReworkExecution,
} from "../../api/dev1/reworkExecution";
export default function ReworkExecutionPanel({
  branch,
  id,
}: {
  branch: string;
  id: string;
}) {
  const [data, setData] = useState<ReworkExecution | null>(null),
    [error, setError] = useState(""),
    [notice, setNotice] = useState("");
  const [record, setRecord] = useState<ReworkRecord | null>(null);
  const [refresh, setRefresh] = useState(0),
    [busy, setBusy] = useState(false);
  const [pending, setPending] = useState<{
    action: ReworkAction;
    tag: string;
    key: string;
    command: ReworkCommand;
  } | null>(null);
  const alive = useRef(true),
    sending = useRef(false);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  useEffect(() => {
    const abort = new AbortController();
    setData(null);
    setRecord(null);
    setError("");
    readRework(branch, id, abort.signal)
      .then((d) => {
        if (!abort.signal.aborted) setData(d);
      })
      .catch(async (e) => {
        if (abort.signal.aborted) return;
        if (e instanceof ApiError && e.code === "DUPLICATE_REWORK_SOURCE") {
          try {
            const historical = await readReworkRecord(branch, id, abort.signal);
            if (!abort.signal.aborted) setRecord(historical);
          } catch (readError) {
            if (!abort.signal.aborted) setError(progressError(readError));
          }
        } else setError(progressError(e));
      });
    return () => abort.abort();
  }, [branch, id, refresh]);
  const action: ReworkAction | null =
    data?.capability === "READ_ONLY"
      ? null
      : data?.state === "PENDING_ACCEPTANCE"
        ? "accept"
        : data?.state === "ACCEPTED" ||
            (data?.state === "ON_HOLD" && data.capability === "RESUME_REWORK")
          ? "start"
          : data?.state === "IN_PROGRESS"
            ? "complete"
            : null;
  async function send(command: NonNullable<typeof pending>) {
    try {
      await mutateRework(
        branch,
        id,
        command.action,
        command.tag,
        command.key,
        command.command,
      );
      if (alive.current) {
        setPending(null);
        setNotice("Đã lưu rework; đang tải trạng thái nguồn.");
        setRefresh((v) => v + 1);
      }
    } catch (e) {
      if (alive.current) {
        if (
          e instanceof ApiError &&
          [400, 401, 403, 404, 409, 412, 428].includes(e.status)
        ) {
          setPending(null);
          setNotice(progressError(e));
          setRefresh((v) => v + 1);
        } else setError(progressError(e));
      }
    } finally {
      sending.current = false;
      if (alive.current) setBusy(false);
    }
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data || !action || pending || sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      const form = new FormData(e.currentTarget);
      const command: ReworkCommand = {
        note: String(form.get("note") || "").trim(),
      };
      if (action === "complete") {
        const allFiles = data.defects.flatMap((d) =>
          form
            .getAll(`photo-${d.id}`)
            .filter((v): v is File => v instanceof File && v.size > 0),
        );
        if (allFiles.length > 5) throw Error("TOO_MANY_FILES");
        command.remediations = await Promise.all(
          data.defects.map(async (d) => ({
            defectId: d.id,
            note: String(form.get(`defect-${d.id}`) || "").trim(),
            photos: await Promise.all(
              form
                .getAll(`photo-${d.id}`)
                .filter((v): v is File => v instanceof File && v.size > 0)
                .map(uploadFile),
            ),
          })),
        );
      }
      validateReworkCommand(action, command);
      const next = {
        action,
        command,
        tag: data.sourceETag,
        key: crypto.randomUUID(),
      };
      if (alive.current) {
        setPending(next);
        await send(next);
      }
    } catch (e) {
      sending.current = false;
      if (alive.current) {
        setBusy(false);
        setError(progressError(e));
      }
    }
  }
  return (
    <>
      <h1>Thực hiện làm lại</h1>
      <Link to={`/rework?branchId=${branch}`}>Danh sách công việc làm lại</Link>
      {notice && <p role="status">{userMessage(notice)}</p>}
      {error && <p role="alert">{userMessage(error)}</p>}
      {!data && !record && !error && (
        <p role="status">Đang tải công việc làm lại…</p>
      )}
      {record && (
        <section className="ds-card" aria-label="Hồ sơ làm lại lưu trữ">
          <h2>
            Phòng {record.roomNumber} · Làm lại {referenceLabel(record, "id")}
          </h2>
          <p role="status">
            Hồ sơ cũ · Chỉ xem. Có nhiều phiếu làm lại đang mở cho cùng lần kiểm
            tra; cần giám sát kiểm tra nguồn trước khi thực hiện.
          </p>
          <dl>
            <dt>Công việc vệ sinh</dt>
            <dd>{record.taskNumber}</dd>
            <dt>Lần kiểm tra</dt>
            <dd>{referenceLabel(record, "inspectionId")}</dd>
            <dt>Trạng thái đã ghi nhận</dt>
            <dd>{roomLabel(record.recordedStatus)}</dd>
            <dt>Yêu cầu làm lại</dt>
            <dd>{record.reason}</dd>
            <dt>Ngày yêu cầu</dt>
            <dd>{hotelTime(record.requestedAt)}</dd>
            {record.note && (
              <>
                <dt>Ghi chú</dt>
                <dd>{record.note}</dd>
              </>
            )}
            {record.completedAt && (
              <>
                <dt>Ngày hoàn tất đã ghi nhận</dt>
                <dd>{hotelTime(record.completedAt)}</dd>
              </>
            )}
          </dl>
        </section>
      )}
      {data && (
        <>
          <p>
            {displayCode(data.state)} · Phòng {referenceLabel(data, "roomId")} · Nhân viên{" "}
            {referenceLabel(data, "assigneeId")} · Hạn UTC {data.dueAtUtc || "—"}
          </p>
          <p>
            Kiểm tra {referenceLabel(data, "inspectionId")} · Chu kỳ {referenceLabel(data, "cleaningTaskId")} ·
            {data.sourceAuthority}
          </p>
          <form key={data.sourceETag} onSubmit={submit}>
            <fieldset disabled={busy || !!pending}>
              {data.defects.map((d) => (
                <section key={d.id}>
                  <h2>Lỗi {referenceLabel(d, "id")}</h2>
                  <p>{d.description}</p>
                  {[
                    ...d.inspectionAttachmentIds,
                    ...d.remediationAttachmentIds,
                  ].map((file) => (
                    <a
                      key={file}
                      href={`/api/branches/${branch}/reworks/${id}/evidence/${file}`}
                      target="_blank"
                      rel="noreferrer"
                    >
                      Ảnh / tệp {file}{" "}
                    </a>
                  ))}
                  {action === "complete" ? (
                    <>
                      <label>
                        Khắc phục {referenceLabel(d, "id")}
                        <textarea
                          name={`defect-${d.id}`}
                          required
                          maxLength={500}
                          defaultValue={d.remediation || ""}
                        />
                      </label>
                      <label>
                        Ảnh remediation {referenceLabel(d, "id")}
                        <input
                          name={`photo-${d.id}`}
                          type="file"
                          multiple
                          accept="image/png,image/jpeg,application/pdf"
                        />
                      </label>
                    </>
                  ) : (
                    <p>{d.remediation || "Chưa gửi remediation"}</p>
                  )}
                </section>
              ))}
              {action && (
                <>
                  <label>
                    Ghi chú thao tác
                    <textarea name="note" required maxLength={500} />
                  </label>
                  <button className="primary">
                    {action === "accept"
                      ? "Tiếp nhận làm lại"
                      : action === "start"
                        ? data?.state === "ON_HOLD"
                          ? "Tiếp tục làm lại"
                          : "Bắt đầu làm lại"
                        : "Hoàn tất làm lại"}
                  </button>
                </>
              )}
            </fieldset>
          </form>
          {pending && !busy && (
            <button
              onClick={() => {
                if (sending.current) return;
                sending.current = true;
                setBusy(true);
                setError("");
                void send(pending);
              }}
            >
              Thử lại cùng yêu cầu
            </button>
          )}
          {data.state === "PENDING_REINSPECTION" && (
            <p>
              Đã gửi rework; chờ supervisor tái kiểm tra. Chưa có kết luận
              Inspection.
            </p>
          )}
        </>
      )}
    </>
  );
}
