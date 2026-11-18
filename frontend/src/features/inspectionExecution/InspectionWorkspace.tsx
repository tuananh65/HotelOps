import { ActionDialog } from "../../components/workspace-interactions";
import { displayCode } from "../../i18n/vi";
import { useEffect, useRef, useState } from "react";
import {
  createInspectionApi,
  InspectionApiError,
  type InspectionIncomplete,
} from "./api";
import { RequestReworkForm } from "./RequestReworkForm";
import {
  encodeEvidence,
  optionalNote,
  reason,
  type CriterionResult,
  type LoadedInspection,
} from "./model";
/** Direct-ID UI only; mounted by an explicit trusted DEV3 host only after separate approval. */
export function InspectionWorkspace({
  branch,
  id,
  onDenied,
  onSessionInvalid,
}: {
  branch: number;
  id: number;
  onDenied: (s: 401 | 403) => void;
  onSessionInvalid?: () => void;
}) {
  const lifetime = useRef<AbortController | null>(null);
  const running = useRef<AbortController | null>(null);
  const [current, setCurrent] = useState<LoadedInspection | null>(null);
  const severities = current?.value.severities;
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [error, setError] = useState("");
  const [incomplete, setIncomplete] = useState<InspectionIncomplete | null>(
    null,
  );
  const [notice, setNotice] = useState("");
  const [why, setWhy] = useState("");
  const [rejectConfirmed, setRejectConfirmed] = useState(false);
  const [outcomes, setOutcomes] = useState<
    Record<number, "PASS" | "FAIL" | "">
  >({});
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [criterionUploads, setCriterionUploads] = useState<
    Record<number, File[]>
  >({});
  const [uploadGeneration, setUploadGeneration] = useState(0);
  const [selection, setSelection] = useState("");
  const [editingDefect, setEditingDefect] = useState<number | null>(null);
  const [severity, setSeverity] = useState("");
  const [description, setDescription] = useState("");
  const [location, setLocation] = useState("");
  const [uploads, setUploads] = useState<File[]>([]);
  const [blocked, setBlocked] = useState(false);
  useEffect(() => {
    const scope = new AbortController();
    lifetime.current = scope;
    return () => {
      scope.abort();
      running.current?.abort();
      lifetime.current = null;
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    const scope = lifetime.current;
    if (!scope) return;
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    setCurrent(null);
    setError("");
    setIncomplete(null);
    setNotice("");
    setBlocked(false);
    setBusy(true);
    void api
      .read(branch, id, controller.signal)
      .then((result) => {
        if (scope.signal.aborted || controller.signal.aborted) return;
        setCurrent(result);
        setEditingDefect(null);
        setUploads([]);
        setCriterionUploads({});
        setUploadGeneration((n) => n + 1);
        setOutcomes(
          Object.fromEntries(
            result.value.criteria.map((c) => [
              c.itemId,
              c.outcome === "PASS" || c.outcome === "FAIL" ? c.outcome : "",
            ]),
          ),
        );
        setNotes(
          Object.fromEntries(
            result.value.criteria.map((c) => [c.itemId, c.note ?? ""]),
          ),
        );
      })
      .catch((e) => {
        if (!controller.signal.aborted && !scope.signal.aborted) handle(e);
      })
      .finally(() => {
        if (!scope.signal.aborted && !controller.signal.aborted) setBusy(false);
      });
    return () => controller.abort();
    // Admission identity is controlled by parent key; onSessionInvalid changes do not trigger a replay.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, id, reload]);
  function handle(e: unknown) {
    if (e instanceof InspectionApiError) {
      if (
        e.incomplete &&
        current?.value.state === "INSPECTING" &&
        e.incomplete.versionId === current.value.versionId &&
        e.incomplete.missingItems.every((item) =>
          current.value.criteria.some((c) => c.itemId === item.itemId),
        )
      )
        setIncomplete(e.incomplete);
      if (e.status === 401 || e.status === 403) {
        onDenied(e.status);
        setCurrent(null);
        setBlocked(true);
      }
      if (e.uncertain || [409, 412, 428].includes(e.status)) {
        setBlocked(true);
        setCurrent(null);
      }
      setError(
        e.uncertain
          ? "Chưa xác định kết quả ghi. Đọc mới có quyền để xác minh trạng thái máy chủ; không tự gửi lại."
          : e.message,
      );
    } else if (e instanceof Error && e.name !== "AbortError")
      setError(e.message);
  }
  async function action(
    kind: "start" | "save" | "finish" | "defect" | "approve" | "reject",
  ) {
    if (
      !current ||
      busy ||
      running.current !== null ||
      blocked ||
      !lifetime.current ||
      lifetime.current.signal.aborted
    )
      return;
    const scope = lifetime.current,
      task = new AbortController();
    running.current = task;
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    setBusy(true);
    setError("");
    setIncomplete(null);
    setNotice("");
    try {
      let result: LoadedInspection;
      if (kind === "reject")
        result = await api.reject(
          branch,
          id,
          current.etag,
          why,
          rejectConfirmed,
          task.signal,
        );
      else if (kind === "approve")
        result = await api.approve(branch, id, current.etag, task.signal);
      else if (kind === "start")
        result = await api.start(
          branch,
          id,
          current.etag,
          reason(why),
          task.signal,
        );
      else if (kind === "finish")
        result = await api.finish(
          branch,
          id,
          current.etag,
          reason(why),
          task.signal,
        );
      else if (kind === "save") {
        if (
          Object.values(criterionUploads).reduce(
            (n, files) => n + files.length,
            0,
          ) > 5
        )
          throw new Error("MSG23: Tối đa 5 tệp bằng chứng mỗi lần lưu.");
        if (
          current.value.criteria.some(
            (c) => criterionUploads[c.itemId]?.length && !outcomes[c.itemId],
          )
        )
          throw new Error(
            "MSG03: Chọn Đạt hoặc Không đạt trước khi lưu bằng chứng tiêu chí.",
          );
        const results: CriterionResult[] = current.value.criteria
          .filter(
            (c) =>
              outcomes[c.itemId] === "PASS" || outcomes[c.itemId] === "FAIL",
          )
          .map((c) => ({
            itemId: c.itemId,
            outcome: outcomes[c.itemId] as "PASS" | "FAIL",
            note: optionalNote(notes[c.itemId] ?? ""),
          }));
        for (const result of results) {
          const photos = [];
          for (const file of criterionUploads[result.itemId] ?? [])
            photos.push(await encodeEvidence(file, task.signal));
          if (photos.length) result.photos = photos;
        }
        result = await api.save(
          branch,
          id,
          current,
          results,
          reason(why),
          task.signal,
        );
      } else {
        const choice = Number(severity),
          itemId = Number(selection);
        if (!severities?.length)
          throw new InspectionApiError(
            403,
            "Chưa có danh mục mức độ từ máy chủ; thao tác gửi lỗi bị khóa.",
          );
        const criterion = current.value.criteria.find(
          (c) => c.itemId === itemId,
        );
        if (!criterion || criterion.outcome !== "FAIL")
          throw new Error(
            "Ghi kết quả tiêu chí không đạt trước khi gửi lỗi tương ứng.",
          );
        const files = [];
        for (const file of uploads)
          files.push(await encodeEvidence(file, task.signal));
        result = await api.defect(
          branch,
          id,
          current.etag,
          severities,
          { itemId, severityId: choice, description, location, photos: files },
          task.signal,
          editingDefect ?? undefined,
        );
      }
      if (task.signal.aborted || scope.signal.aborted) return;
      setCurrent(result);
      if (kind === "save") {
        setCriterionUploads({});
        setUploadGeneration((n) => n + 1);
      }
      setBlocked(false);
      setUploads([]);
      setEditingDefect(null);
      setRejectConfirmed(false);
      setNotice(
        kind === "reject"
          ? result.value.effectiveReadiness === "BLOCKED"
            ? "MSG17: Kiểm tra không đạt. Cần vệ sinh lại; phòng vẫn bị khóa. Thông báo đang chờ gửi."
            : "Kiểm tra không đạt. Cần vệ sinh lại. Thông báo đang chờ gửi."
          : kind === "approve"
            ? result.value.effectiveReadiness === "BLOCKED"
              ? "MSG17: Kiểm tra đạt và vệ sinh hoàn tất. Phòng vẫn bị khóa, chưa thể sử dụng."
              : "MSG08: Kiểm tra đạt và vệ sinh hoàn tất. Phòng đã sẵn sàng."
            : kind === "finish"
              ? "Kiểm tra đã chuyển sang chờ kết luận; chưa được duyệt, chưa đạt và phòng chưa sẵn sàng."
              : kind === "defect"
                ? "MSG08: Đã lưu lỗi kiểm tra."
                : "Máy chủ đã xác nhận thao tác.",
      );
    } catch (e) {
      if (!task.signal.aborted && !scope.signal.aborted) handle(e);
    } finally {
      if (running.current === task) running.current = null;
      if (!task.signal.aborted && !scope.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Thực hiện kiểm tra">
      <h2>Kiểm tra #{id}</h2>
      <p>Chi nhánh #{branch}. Inspection execution and approval.</p>
      {error && (
        <p role="alert" style={{ color: "#b91c1c" }}>
          {error}
        </p>
      )}
      {incomplete && current && (
        <ul aria-label="Hạng mục kiểm tra còn thiếu">
          {incomplete.missingItems.map((item) => (
            <li key={item.itemId}>
              #{item.itemId}{" "}
              {
                current.value.criteria.find((c) => c.itemId === item.itemId)
                  ?.criterion
              }
              :{" "}
              {item.requirement === "RESULT_REQUIRED"
                ? "Ghi và lưu Đạt hoặc Không đạt cho tiêu chí bắt buộc này."
                : "Thêm lỗi kèm mô tả cho tiêu chí không đạt này."}
            </li>
          ))}
        </ul>
      )}
      {notice && <p role="status">{notice}</p>}
      {blocked && (
        <p role="alert">
          Thao tác bị chặn. Kiểm tra lại máy chủ trước khi làm tiếp.
        </p>
      )}
      <button
        type="button"
        disabled={busy}
        onClick={() => {
          running.current?.abort();
          setReload((n) => n + 1);
        }}
      >
        Kiểm tra trạng thái hiện tại
      </button>
      {busy && <p role="status">Đang xử lý…</p>}
      {current && (
        <>
          <p>
            Trạng thái: <strong>{displayCode(current.value.state)}</strong>.
            Assigned version #{current.value.versionId}; Phòng #
            {current.value.roomId}; Công việc #{current.value.cleaningTaskId}.
          </p>
          <label>
            Lý do{" "}
            <input
              value={why}
              maxLength={500}
              onChange={(e) => setWhy(e.target.value)}
            />
          </label>
          <p>
            Mức sẵn sàng thực tế của phòng:{" "}
            {current.value.effectiveReadiness ?? "UNKNOWN"}.
          </p>
          {current.value.state === "PENDING_CONCLUSION" && (
            <p aria-label="Tóm tắt kiểm tra">
              Tiêu chí đạt:{" "}
              {
                current.value.criteria.filter((c) => c.outcome === "PASS")
                  .length
              }
              ; tiêu chí không đạt:{" "}
              {
                current.value.criteria.filter((c) => c.outcome === "FAIL")
                  .length
              }
              ; Các lỗi: {current.value.defects.length}.
            </p>
          )}
          {current.value.notification && (
            <p>Thông báo: {current.value.notification}</p>
          )}
          {["PASSED", "FAILED"].includes(current.value.state) && (
            <p role="status">
              MSG24: Kiểm tra đang ở trạng thái{" "}
              {displayCode(current.value.state)} và các lỗi liên quan không còn
              được chỉnh sửa.
            </p>
          )}
          {current.value.state === "FAILED" && (
            <RequestReworkForm
              key={`${branch}:${id}`}
              branch={branch}
              id={id}
              onDenied={onDenied}
              onSessionInvalid={onSessionInvalid}
            />
          )}
          {current.value.state === "PENDING_CONCLUSION" &&
            current.value.criteria.some(
              (c) => c.mandatory && c.outcome === "FAIL",
            ) && (
              <p role="alert">
                MSG03: Tiêu chí bắt buộc không đạt. Không thể duyệt. Kiểm tra
                lỗi và chọn Từ chối kết quả kiểm tra.
              </p>
            )}
          <button
            type="button"
            disabled={
              busy ||
              blocked ||
              current.value.state !== "PENDING_CONCLUSION" ||
              current.value.criteria.some(
                (c) => c.mandatory && c.outcome !== "PASS",
              )
            }
            onClick={() => void action("approve")}
          >
            Duyệt kết quả kiểm tra
          </button>
          {current.value.state === "PENDING_CONCLUSION" && (
            <ActionDialog title="Từ chối kết quả kiểm tra" busy={busy}>
              <fieldset disabled={busy || blocked}>
                <legend>Từ chối kết quả kiểm tra</legend>
                {why.trim().length < 10 && (
                  <p role="alert" style={{ color: "#b91c1c" }}>
                    MSG03: Lý do từ chối cần ít nhất 10 ký tự.
                  </p>
                )}
                {current.value.defects.length === 0 && (
                  <p role="alert" style={{ color: "#b91c1c" }}>
                    MSG03: Cần ghi lỗi cho từng tiêu chí không đạt trước khi từ
                    chối kiểm tra. Kiểm tra và thử lại.
                  </p>
                )}
                <label>
                  <input
                    type="checkbox"
                    checked={rejectConfirmed}
                    onChange={(e) => setRejectConfirmed(e.target.checked)}
                  />
                  Xác nhận kiểm tra không đạt và yêu cầu vệ sinh lại
                </label>
                <button
                  type="button"
                  disabled={
                    !rejectConfirmed ||
                    why.trim().length < 10 ||
                    !current.value.defects.length ||
                    !current.value.criteria.some((c) => c.outcome === "FAIL")
                  }
                  onClick={() => void action("reject")}
                >
                  Từ chối kết quả kiểm tra
                </button>
              </fieldset>
            </ActionDialog>
          )}
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "ASSIGNED"}
            onClick={() => void action("start")}
          >
            Bắt đầu kiểm tra
          </button>
          <h3>Tiêu chí kiểm tra được giao</h3>
          {current.value.criteria.map((c) => (
            <fieldset
              key={c.itemId}
              disabled={busy || blocked || current.value.state !== "INSPECTING"}
            >
              <legend>
                #{c.itemId} {c.criterion} {c.mandatory ? "(required)" : ""}
              </legend>
              {c.description && <p>{c.description}</p>}
              <label>
                Kết quả{" "}
                <select
                  value={outcomes[c.itemId] ?? ""}
                  onChange={(e) =>
                    setOutcomes((v) => ({
                      ...v,
                      [c.itemId]: e.target.value as "PASS" | "FAIL" | "",
                    }))
                  }
                >
                  <option value="">Chưa ghi nhận</option>
                  <option value="PASS">Đạt</option>
                  <option value="FAIL">Không đạt</option>
                </select>
              </label>
              <label>
                Ghi chú{" "}
                <textarea
                  aria-label="Ghi chú"
                  value={notes[c.itemId] ?? ""}
                  maxLength={4000}
                  onChange={(e) =>
                    setNotes((v) => ({ ...v, [c.itemId]: e.target.value }))
                  }
                />
              </label>
              <label>
                Bằng chứng theo tiêu chí #{c.itemId} (JPG, PNG, PDF; tối đa 10
                MiB mỗi tệp)
                <input
                  key={`${c.itemId}:${uploadGeneration}`}
                  type="file"
                  multiple
                  accept="image/jpeg,image/png,application/pdf"
                  onChange={(e) =>
                    setCriterionUploads((v) => ({
                      ...v,
                      [c.itemId]: Array.from(e.target.files ?? []),
                    }))
                  }
                />
              </label>
              {!!c.evidenceIds?.length && (
                <p>Tệp bằng chứng đã lưu: {c.evidenceIds.length}</p>
              )}
            </fieldset>
          ))}
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "INSPECTING"}
            onClick={() => void action("save")}
          >
            Lưu kết quả
          </button>
          <button
            type="button"
            disabled={busy || blocked || current.value.state !== "INSPECTING"}
            onClick={() => void action("finish")}
          >
            Hoàn tất → Chờ kết luận
          </button>
          <h3>Các lỗi</h3>
          {current.value.defects.map((d) => (
            <p key={d.id}>
              Lỗi #{d.id}: {d.description} — {displayCode(d.status)}
              <button
                type="button"
                disabled={
                  busy ||
                  blocked ||
                  !severities?.length ||
                  d.itemId === null ||
                  !["INSPECTING", "PENDING_CONCLUSION"].includes(
                    current.value.state,
                  )
                }
                onClick={() => {
                  setEditingDefect(d.id);
                  setSelection(String(d.itemId));
                  setSeverity(String(d.severityId ?? ""));
                  setDescription(d.description);
                  setLocation(d.location ?? "");
                  setUploads([]);
                }}
              >
                Chỉnh sửa lỗi #{d.id}
              </button>
            </p>
          ))}
          {!severities?.length && (
            <p role="status">
              Chưa có mã mức độ từ máy chủ; thao tác gửi lỗi bị khóa.
              INSPECTION_PERFORM không cấp quyền xem bằng chứng.
            </p>
          )}
          <fieldset
            disabled={
              busy ||
              blocked ||
              !severities?.length ||
              !(
                current.value.state === "INSPECTING" ||
                current.value.state === "PENDING_CONCLUSION"
              )
            }
          >
            <legend>Ghi nhận lỗi (chỉ cho tiêu chí đã lưu là không đạt)</legend>
            <label>
              Tiêu chí không đạt{" "}
              <select
                value={selection}
                disabled={editingDefect !== null}
                onChange={(e) => setSelection(e.target.value)}
              >
                <option value="">Chọn</option>
                {current.value.criteria
                  .filter((c) => c.outcome === "FAIL")
                  .map((c) => (
                    <option key={c.itemId} value={c.itemId}>
                      {displayCode(c.criterion)}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Mức độ{" "}
              <select
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
              >
                <option value="">Chọn</option>
                {severities
                  ?.filter(
                    (s) =>
                      s.id > 0 && (s.code === "MINOR" || s.code === "MAJOR"),
                  )
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Mô tả{" "}
              <textarea
                aria-label="Mô tả"
                value={description}
                maxLength={4000}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
            <label>
              Vị trí{" "}
              <input
                value={location}
                maxLength={250}
                onChange={(e) => setLocation(e.target.value)}
              />
            </label>
            <label>
              Bằng chứng (JPG, PNG hoặc PDF, tối đa 5 tệp / 10 MiB mỗi tệp){" "}
              <input
                type="file"
                multiple
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setUploads(Array.from(e.target.files ?? []))}
              />
            </label>
            <button
              type="button"
              disabled={busy || blocked || !severities?.length}
              onClick={() => void action("defect")}
            >
              {editingDefect === null ? "Ghi nhận lỗi" : "Lưu thay đổi lỗi"}
            </button>
            {editingDefect !== null && (
              <button
                type="button"
                onClick={() => {
                  setEditingDefect(null);
                  setDescription("");
                  setLocation("");
                  setUploads([]);
                }}
              >
                Hủy chỉnh sửa
              </button>
            )}
          </fieldset>
          <p>
            Chưa cho phép xem / tải bằng chứng nếu chưa xác minh riêng quyền
            INSPECTION_VIEW.
          </p>
        </>
      )}
    </section>
  );
}
