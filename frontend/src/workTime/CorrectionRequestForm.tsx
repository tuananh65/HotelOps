import { userMessage } from "../i18n/userMessage";
import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState, type PropsWithChildren } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import type { TimeLogEntry } from "./timeLogApi";
import { TimeLogFailure } from "./timeLogApi";
import { saveCorrection } from "./timeCorrectionsApi";
import { correctionMessage } from "./correctionMessage";
export const hotelInput = (value: string) =>
  new Date(Date.parse(value) + 7 * 3600000).toISOString().slice(0, 19);
export const utcInput = (value: string) =>
  new Date(value + "+07:00").toISOString();
function InlineCorrection({ children }: PropsWithChildren) {
  return <>{children}</>;
}
export function CorrectionRequestForm({
  branch,
  interval,
  owner,
  onSaved,
  embedded = false,
  onBusyChange,
}: {
  branch: number;
  interval: TimeLogEntry;
  owner: AuthorityGeneration;
  onSaved: () => void;
  embedded?: boolean;
  onBusyChange?: (busy: boolean) => void;
}) {
  const { actions } = useAuth();
  const [open, setOpen] = useState(false);
  const [start, setStart] = useState(hotelInput(interval.start));
  const [end, setEnd] = useState(interval.end ? hotelInput(interval.end) : "");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [locked, setLocked] = useState(false);
  const active = useRef<AbortController | null>(null);
  const saving = useRef(false);
  useEffect(() => () => active.current?.abort(), []);
  const Container = embedded ? InlineCorrection : ActionDialog;
  if (!interval.end) return null;
  return (
    <>
      {!embedded && (
        <button disabled={busy || locked} onClick={() => setOpen((v) => !v)}>
          Đề nghị điều chỉnh
        </button>
      )}
      {(open || embedded) && (
        <Container
          open
          title="Đề nghị điều chỉnh thời gian"
          onClose={() => setOpen(false)}
          busy={busy}
          error={error}
        >
          <form
            className="time-correction-form"
            onSubmit={(e) => {
              e.preventDefault();
              if (saving.current || locked) return;
              saving.current = true;
              setBusy(true);
              onBusyChange?.(true);
              setError("");
              const controller = new AbortController();
              active.current = controller;
              void (async () => {
                try {
                  await saveCorrection(
                    branch,
                    interval.intervalId,
                    interval.version,
                    { start: utcInput(start), end: utcInput(end), reason },
                    false,
                    controller.signal,
                  );
                  if (!controller.signal.aborted) {
                    setOpen(false);
                    setLocked(true);
                    onSaved();
                  }
                } catch (error) {
                  if (!controller.signal.aborted) {
                    if (error instanceof TimeLogFailure && error.status === 401)
                      actions.invalidateSession(owner);
                    setError(correctionMessage(error));
                    setLocked(true);
                  }
                } finally {
                  saving.current = false;
                  if (!controller.signal.aborted) {
                    setBusy(false);
                    onBusyChange?.(false);
                  }
                }
              })();
            }}
          >
            <p>
              Thời gian đề xuất theo giờ Việt Nam (UTC+7). Tải lại nhật ký sau
              mọi xung đột.
            </p>
            <label>
              Bắt đầu
              <input
                type="datetime-local"
                step="1"
                required
                value={start}
                onChange={(e) => setStart(e.target.value)}
              />
            </label>
            <label>
              Kết thúc
              <input
                type="datetime-local"
                step="1"
                required
                value={end}
                onChange={(e) => setEnd(e.target.value)}
              />
            </label>
            <label>
              Lý do
              <textarea
                required
                minLength={10}
                maxLength={1000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button
              data-dialog-action
              disabled={busy || locked || reason.trim().length < 10}
            >
              Gửi điều chỉnh
            </button>
            {error && <p role="alert">{userMessage(error)}</p>}
          </form>
        </Container>
      )}
    </>
  );
}
