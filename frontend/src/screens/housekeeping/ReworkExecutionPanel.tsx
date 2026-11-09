import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/dev1/client";
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
    setError("");
    readRework(branch, id, abort.signal)
      .then((d) => {
        if (!abort.signal.aborted) setData(d);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(progressError(e));
      });
    return () => abort.abort();
  }, [branch, id, refresh]);
  const action: ReworkAction | null =
    data?.state === "PENDING_ACCEPTANCE"
      ? "accept"
      : data?.state === "ACCEPTED"
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
      <h1>Rework execution</h1>
      <Link to={`/cleaning/tasks?branchId=${branch}&kind=rework`}>
        Cleaning Monitor
      </Link>
      {notice && <p role="status">{notice}</p>}
      {error && <p role="alert">{error}</p>}
      {!data && !error && <p role="status">Đang tải rework…</p>}
      {data && (
        <>
          <p>
            {data.state} · Phòng {data.roomId} · Nhân viên {data.assigneeId} ·
            Hạn UTC {data.dueAtUtc || "—"}
          </p>
          <p>
            Inspection {data.inspectionId} · Cycle {data.cleaningTaskId} ·
            PROVISIONAL_NOT_ACCEPTED · {data.sourceAuthority === "FE05_COMMITTED_V1"
              ? "Nguồn Inspection đã được xác minh; quy trình vẫn chờ nghiệm thu tích hợp."
              : "Nguồn synthetic chỉ dùng trial, chưa shared acceptance."}
          </p>
          <form key={data.sourceETag} onSubmit={submit}>
            <fieldset disabled={busy || !!pending}>
              {data.defects.map((d) => (
                <section key={d.id}>
                  <h2>Defect {d.id}</h2>
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
                        Remediation {d.id}
                        <textarea
                          name={`defect-${d.id}`}
                          required
                          maxLength={500}
                          defaultValue={d.remediation || ""}
                        />
                      </label>
                      <label>
                        Ảnh remediation {d.id}
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
                      ? "Accept rework"
                      : action === "start"
                        ? "Start rework"
                        : "Complete rework"}
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
