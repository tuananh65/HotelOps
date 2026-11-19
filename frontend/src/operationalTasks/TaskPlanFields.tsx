import { SearchableSelect } from "../components/SearchableSelect";
import type { PlanningOptions } from "./planning";

export interface PlanDraft {
  priority: string;
  start: string;
  due: string;
  minutes: string;
}
export function TaskPlanFields({
  value,
  onChange,
  priorities,
}: {
  value: PlanDraft;
  onChange: (value: PlanDraft) => void;
  priorities: PlanningOptions["priorities"];
}) {
  return (
    <>
      <label>
        Độ ưu tiên
        <SearchableSelect
          aria-label="Độ ưu tiên"
          required
          value={value.priority}
          onChange={(e) => onChange({ ...value, priority: e.target.value })}
        >
          <option value="">Chọn độ ưu tiên</option>
          {priorities.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </SearchableSelect>
      </label>
      <p>Thời gian kế hoạch theo giờ Việt Nam (UTC+07:00).</p>
      <label>
        Bắt đầu dự kiến
        <input
          type="datetime-local"
          step="0.001"
          value={value.start}
          onChange={(e) => onChange({ ...value, start: e.target.value })}
        />
      </label>
      <label>
        Thời hạn
        <input
          type="datetime-local"
          step="0.001"
          value={value.due}
          onChange={(e) => onChange({ ...value, due: e.target.value })}
        />
      </label>
      <label>
        Thời lượng dự kiến (phút)
        <input
          type="number"
          min="1"
          max="2147483647"
          step="1"
          value={value.minutes}
          onChange={(e) => onChange({ ...value, minutes: e.target.value })}
        />
      </label>
    </>
  );
}
