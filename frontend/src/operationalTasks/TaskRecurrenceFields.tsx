import type { RecurrenceInput } from "./series";

export function TaskRecurrenceFields({
  value,
  onChange,
  executors,
}: {
  value: RecurrenceInput;
  onChange: (value: RecurrenceInput) => void;
  executors: { id: number; name: string }[];
}) {
  return (
    <fieldset>
      <legend>Recurring task schedule</legend>
      <p>
        Dates use Asia/Ho_Chi_Minh. The first deadline must match the selected
        days. Occurrences are created one calendar day before their deadline.
      </p>
      <label>
        Recurrence rule
        <select
          aria-label="Recurrence rule"
          value={value.rule}
          onChange={(e) =>
            onChange({
              ...value,
              rule: e.target.value as RecurrenceInput["rule"],
              weekdays: [],
              monthDay: e.target.value === "MONTHLY" ? 1 : null,
            })
          }
        >
          <option value="DAILY">Daily</option>
          <option value="WEEKLY">Weekly on selected days</option>
          <option value="MONTHLY">Monthly on a day</option>
        </select>
      </label>
      {value.rule === "WEEKLY" && (
        <fieldset>
          <legend>Weekdays</legend>
          {[
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
          ].map((name, day) => (
            <label key={day}>
              <input
                type="checkbox"
                checked={value.weekdays.includes(day)}
                onChange={(e) =>
                  onChange({
                    ...value,
                    weekdays: e.target.checked
                      ? [...value.weekdays, day].sort()
                      : value.weekdays.filter((d) => d !== day),
                  })
                }
              />
              {name}
            </label>
          ))}
        </fieldset>
      )}
      {value.rule === "MONTHLY" && (
        <>
          <label>
            Day of month
            <input
              aria-label="Day of month"
              required
              type="number"
              min={1}
              max={31}
              value={value.monthDay ?? ""}
              onChange={(e) =>
                onChange({ ...value, monthDay: Number(e.target.value) })
              }
            />
          </label>
          <p>Months without this calendar day have no occurrence.</p>
        </>
      )}
      <label>
        Default executor
        <select
          aria-label="Default executor"
          required
          value={value.defaultAssigneeId || ""}
          onChange={(e) =>
            onChange({ ...value, defaultAssigneeId: Number(e.target.value) })
          }
        >
          <option value="">Choose eligible executor</option>
          {executors.map((u) => (
            <option key={u.id} value={u.id}>
              {u.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Series end date
        <input
          aria-label="Series end date"
          type="date"
          value={value.endDate ?? ""}
          onChange={(e) =>
            onChange({ ...value, endDate: e.target.value || null })
          }
        />
      </label>
    </fieldset>
  );
}
