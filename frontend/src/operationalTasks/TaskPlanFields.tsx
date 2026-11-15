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
        Priority
        <select
          aria-label="Priority"
          required
          value={value.priority}
          onChange={(e) => onChange({ ...value, priority: e.target.value })}
        >
          <option value="">Choose priority</option>
          {priorities.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>
      <p>Planning times use Asia/Ho_Chi_Minh (UTC+07:00).</p>
      <label>
        Planned start
        <input
          type="datetime-local"
          step="0.001"
          value={value.start}
          onChange={(e) => onChange({ ...value, start: e.target.value })}
        />
      </label>
      <label>
        Deadline
        <input
          type="datetime-local"
          step="0.001"
          value={value.due}
          onChange={(e) => onChange({ ...value, due: e.target.value })}
        />
      </label>
      <label>
        Expected duration (minutes)
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
