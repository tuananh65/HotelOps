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
      setError("Enter a remediation note for every defect.");
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
        note: "Submitted rework",
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
          "Check the remediation notes and attachments. At most five supported files are allowed.",
        );
    } finally {
      working.current = false;
      if (alive.current) setPreparing(false);
    }
  }
  return (
    <form aria-label="Complete rework" onSubmit={onSubmit}>
      <fieldset disabled={busy || preparing}>
        <legend>Complete rework</legend>
        {data.defects.map((d) => (
          <section key={d.id}>
            <p>{d.description}</p>
            <label>
              Remediation for defect {d.id}
              <textarea
                name={`remediation-${d.id}`}
                required
                maxLength={500}
                aria-invalid={invalid.includes(d.id)}
              />
            </label>
            {invalid.includes(d.id) && (
              <p role="alert">This field is required.</p>
            )}
            <label>
              Remediation photos for defect {d.id}
              <input
                name={`photo-${d.id}`}
                type="file"
                accept="image/png,image/jpeg,application/pdf"
                multiple
              />
            </label>
          </section>
        ))}
        <p>Submitting sends this rework for reinspection.</p>
        <button type="submit">Submit rework</button>
      </fieldset>
      {error && <p role="alert">{error}</p>}
    </form>
  );
}
