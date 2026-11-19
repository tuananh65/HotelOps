import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { uploadFile } from "../../api/dev1/cleaningProgress";
import {
  validateReworkCommand,
  type ReworkCommand,
  type ReworkExecution,
} from "../../api/dev1/reworkExecution";

export function ReworkSubmissionForm({
  data,
  busy,
  submit,
}: {
  data: ReworkExecution;
  busy: boolean;
  submit: (command: ReworkCommand) => Promise<void>;
}) {
  const [preparing, setPreparing] = useState(false);
  const [error, setError] = useState("");
  const [invalid, setInvalid] = useState<number[]>([]);
  const alive = useRef(false);
  const working = useRef(false);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || working.current) return;
    const form = new FormData(event.currentTarget);
    const missing = data.defects
      .filter((d) => !String(form.get(`remediation-${d.id}`) || "").trim())
      .map((d) => d.id);
    setInvalid(missing);
    setError("");
    if (missing.length) {
      setError("Nhập ghi chú khắc phục cho từng lỗi.");
      return;
    }
    working.current = true;
    setPreparing(true);
    try {
      const files = (id: number) =>
        form
          .getAll(`photo-${id}`)
          .filter((v): v is File => v instanceof File && v.size > 0);
      if (data.defects.reduce((count, d) => count + files(d.id).length, 0) > 5)
        throw Error("TOO_MANY_FILES");
      const command: ReworkCommand = {
        note: "Kết quả làm lại đã nộp",
        remediations: await Promise.all(
          data.defects.map(async (d) => ({
            defectId: d.id,
            note: String(form.get(`remediation-${d.id}`)).trim(),
            photos: await Promise.all(files(d.id).map(uploadFile)),
          })),
        ),
      };
      validateReworkCommand("complete", command);
      if (alive.current) await submit(command);
    } catch {
      if (alive.current)
        setError(
          "Kiểm tra ghi chú khắc phục và tệp đính kèm. Tối đa 5 tệp đúng định dạng.",
        );
    } finally {
      working.current = false;
      if (alive.current) setPreparing(false);
    }
  }
  return (
    <form aria-label="Hoàn tất làm lại" onSubmit={onSubmit}>
      <fieldset disabled={busy || preparing}>
        <legend>Hoàn tất làm lại</legend>
        {data.defects.map((d) => (
          <section key={d.id}>
            <p>{d.description}</p>
            <label>
              Khắc phục lỗi {referenceLabel(d, "id")}
              <textarea
                name={`remediation-${d.id}`}
                required
                maxLength={500}
                aria-invalid={invalid.includes(d.id)}
              />
            </label>
            {invalid.includes(d.id) && (
              <p role="alert">Trường này là bắt buộc.</p>
            )}
            <label>
              Ảnh khắc phục lỗi {referenceLabel(d, "id")}
              <input
                name={`photo-${d.id}`}
                type="file"
                accept="image/png,image/jpeg,application/pdf"
                multiple
              />
            </label>
          </section>
        ))}
        <p>Nộp sẽ chuyển kết quả làm lại sang kiểm tra lại.</p>
        <button type="submit">Nộp kết quả làm lại</button>
      </fieldset>
      {error && <p role="alert">{userMessage(error)}</p>}
    </form>
  );
}
