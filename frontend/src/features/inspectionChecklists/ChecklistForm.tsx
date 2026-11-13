import { useState } from "react";
import {
  criteriaInput,
  createInput,
  emptyCriterion,
  reasonInput,
  type CriterionInput,
} from "./model";
import {
  ChecklistApiError,
  useChecklistApi,
  type LoadedChecklist,
} from "./api";
export function ChecklistForm({
  branch,
  initial,
  onDone,
  onCancel,
  onBlocked,
  disabled = false,
}: {
  branch: number;
  initial?: { criteria: CriterionInput[]; id: number; etag: string };
  onDone: (value: LoadedChecklist) => void;
  onCancel: () => void;
  onBlocked?: (error: ChecklistApiError) => void;
  disabled?: boolean;
}) {
  const api = useChecklistApi();
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [ids, setIds] = useState("");
  const [criteria, setCriteria] = useState<CriterionInput[]>(
    initial?.criteria || [emptyCriterion()],
  );
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [blocked, setBlocked] = useState(false);
  function change(index: number, patch: Partial<CriterionInput>) {
    setCriteria((items) =>
      items.map((c, i) => (i === index ? { ...c, ...patch } : c)),
    );
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    try {
      const payload = initial
        ? { criteria: criteriaInput(criteria), reason: reasonInput(reason) }
        : createInput(code, name, description, ids, criteria);
      setBusy(true);
      const result = initial
        ? await api.update(
            branch,
            initial.id,
            initial.etag,
            payload.criteria,
            (payload as { reason: string }).reason,
          )
        : await api.create(branch, payload as ReturnType<typeof createInput>);
      onDone(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không lưu được checklist.");
      if (
        e instanceof ChecklistApiError &&
        (e.uncertain || [401, 403, 409, 412, 428].includes(e.status))
      ) {
        setBlocked(true);
        onBlocked?.(e);
      }
    } finally {
      setBusy(false);
    }
  }
  return (
    <form
      className="fe05-checklist-form"
      onSubmit={save}
      aria-label={initial ? "Chỉnh sửa tiêu chí" : "Tạo checklist"}
    >
      <h2>{initial ? "Lưu tiêu chí vào Draft" : "Tạo checklist mới"}</h2>
      {error && <p role="alert">{error}</p>}
      <fieldset disabled={disabled || busy || blocked}>
        {!initial && (
          <>
            <label>
              Mã checklist
              <input
                required
                maxLength={40}
                value={code}
                onChange={(e) => setCode(e.target.value)}
              />
            </label>
            <label>
              Tên checklist
              <input
                required
                maxLength={200}
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </label>
            <label>
              Mô tả
              <textarea
                maxLength={4000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
            <p>Nhập ID loại phòng đã biết thuộc chi nhánh đang chọn.</p>
            <label>
              Mã loại phòng áp dụng (phân cách bằng dấu phẩy)
              <input
                required
                value={ids}
                onChange={(e) => setIds(e.target.value)}
              />
            </label>
          </>
        )}
        <h3>Tiêu chí kiểm tra</h3>
        {criteria.map((c, i) => (
          <fieldset key={i}>
            <legend>Tiêu chí {i + 1}</legend>
            <label>
              Nội dung
              <input
                required
                maxLength={250}
                value={c.text}
                onChange={(e) => change(i, { text: e.target.value })}
              />
            </label>
            <label>
              Nhóm
              <input
                maxLength={100}
                value={c.group || ""}
                onChange={(e) => change(i, { group: e.target.value })}
              />
            </label>
            <label className="fe05-inline">
              <input
                type="checkbox"
                checked={c.mandatory}
                onChange={(e) => change(i, { mandatory: e.target.checked })}
              />
              Bắt buộc
            </label>
            <label>
              Điều kiện đạt
              <textarea
                maxLength={4000}
                value={c.passCondition || ""}
                onChange={(e) => change(i, { passCondition: e.target.value })}
              />
            </label>
            <label>
              Mô tả tiêu chí
              <textarea
                maxLength={4000}
                value={c.description || ""}
                onChange={(e) => change(i, { description: e.target.value })}
              />
            </label>
            <button
              type="button"
              disabled={criteria.length === 1}
              onClick={() =>
                setCriteria((items) => items.filter((_, index) => index !== i))
              }
            >
              Xóa tiêu chí {i + 1}
            </button>
          </fieldset>
        ))}
        <button
          type="button"
          disabled={criteria.length >= 200}
          onClick={() => setCriteria((items) => [...items, emptyCriterion()])}
        >
          Thêm tiêu chí
        </button>
        {initial && (
          <label>
            Lý do cập nhật
            <textarea
              required
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
        )}
        <button type="submit">{busy ? "Đang lưu…" : "Lưu Draft"}</button>
      </fieldset>
      <button type="button" disabled={busy} onClick={onCancel}>
        {blocked ? "Đóng để tải lại / kiểm tra kết quả" : "Hủy"}
      </button>
    </form>
  );
}
