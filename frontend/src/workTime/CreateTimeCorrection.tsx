import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { AppButton } from "../components/design-system";
import { ActionDialog } from "../components/workspace-interactions";
import { SearchableSelect } from "../components/SearchableSelect";
import { staffDate } from "../internalRequests/staffDate";
import { displayCode } from "../i18n/vi";
import { CorrectionRequestForm } from "./CorrectionRequestForm";
import { readTimeLog, TimeLogFailure, type TimeLog } from "./timeLogApi";

export function CreateTimeCorrection({
  branch,
  onSaved,
}: {
  branch: number;
  onSaved?: () => void;
}) {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
  const [open, setOpen] = useState(false),
    [busy, setBusy] = useState(false);
  const [date, setDate] = useState(() =>
    new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Ho_Chi_Minh",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date()),
  );
  const [page, setPage] = useState(1),
    [selected, setSelected] = useState("");
  const [revision, setRevision] = useState(0),
    [saved, setSaved] = useState(false);
  const [loaded, setLoaded] = useState<{
    key: string;
    data?: TimeLog;
    error?: string;
  } | null>(null);
  const key = `${owner}:${branch}:${date}:${page}:${revision}`;
  useEffect(() => {
    setLoaded(null);
    setSelected("");
    if (!open || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return;
    const controller = new AbortController();
    readTimeLog(branch, date, page, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setLoaded({ key, data });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TimeLogFailure && error.status === 401)
          actions.invalidateSession(owner);
        setLoaded({
          key,
          error: "Chưa tải được khoảng thời gian. Vui lòng thử lại.",
        });
      });
    return () => controller.abort();
  }, [open, date, page, revision, branch, owner, actions, key]);
  const data = loaded?.key === key ? loaded.data : undefined;
  const interval = data?.items.find(
    (item) => item.end && String(item.intervalId) === selected,
  );
  return (
    <>
      <AppButton
        variant="primary"
        onClick={() => {
          setSaved(false);
          setOpen(true);
        }}
      >
        Tạo đề nghị điều chỉnh
      </AppButton>
      {saved && <p role="status">Đã gửi đề nghị điều chỉnh.</p>}
      {open && (
        <ActionDialog
          open
          title="Tạo đề nghị điều chỉnh thời gian"
          busy={busy}
          onClose={() => setOpen(false)}
        >
          <div className="time-correction-picker">
            <label>
              Ngày có thời gian cần điều chỉnh
              <input
                type="date"
                required
                value={date}
                disabled={busy}
                onChange={(e) => {
                  setDate(e.target.value);
                  setPage(1);
                }}
              />
            </label>
            {loaded?.key === key && loaded.error && (
              <p role="alert">{loaded.error}</p>
            )}
            {!loaded && date && <p role="status">Đang tải khoảng thời gian…</p>}
            <AppButton
              disabled={busy || !date}
              onClick={() => setRevision((v) => v + 1)}
            >
              Tải lại khoảng thời gian
            </AppButton>
            {data && (
              <>
                <label>
                  Khoảng thời gian đã kết thúc
                  <SearchableSelect
                    aria-label="Khoảng thời gian đã kết thúc"
                    value={selected}
                    disabled={busy}
                    onChange={(e) => setSelected(e.target.value)}
                  >
                    <option value="">Chọn khoảng thời gian</option>
                    {data.items
                      .filter((item) => item.end)
                      .map((item) => (
                        <option key={item.intervalId} value={item.intervalId}>
                          {item.workItemName ?? displayCode(item.kind)} ·{" "}
                          {staffDate(item.start)} – {staffDate(item.end!)}
                        </option>
                      ))}
                  </SearchableSelect>
                </label>
                {!data.items.some((item) => item.end) && (
                  <p role="status">
                    Chưa có khoảng thời gian đã kết thúc trong ngày này. Chọn
                    ngày khác để tạo đề nghị.
                  </p>
                )}
                {data.total > data.pageSize && (
                  <div className="ds-action-links">
                    <AppButton
                      disabled={busy || page === 1}
                      onClick={() => setPage((v) => v - 1)}
                    >
                      Trang trước
                    </AppButton>
                    <span>Trang {page}</span>
                    <AppButton
                      disabled={busy || page * data.pageSize >= data.total}
                      onClick={() => setPage((v) => v + 1)}
                    >
                      Trang sau
                    </AppButton>
                  </div>
                )}
              </>
            )}
            {interval && (
              <CorrectionRequestForm
                embedded
                key={`${key}:${interval.intervalId}`}
                branch={branch}
                interval={interval}
                owner={owner}
                onBusyChange={setBusy}
                onSaved={() => {
                  setOpen(false);
                  setSaved(true);
                  setBusy(false);
                  onSaved?.();
                }}
              />
            )}
          </div>
        </ActionDialog>
      )}
    </>
  );
}
