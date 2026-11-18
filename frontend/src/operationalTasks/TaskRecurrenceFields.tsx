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
      <legend>Lịch công việc định kỳ</legend>
      <p>
        Ngày giờ theo Việt Nam. Hạn đầu phải khớp ngày đã chọn. Các lần thực hiện được tạo trước thời hạn một ngày lịch.
      </p>
      <label>
        Quy tắc lặp lại
        <select
          aria-label="Quy tắc lặp lại"
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
          <option value="DAILY">Hằng ngày</option>
          <option value="WEEKLY">Hằng tuần vào các ngày đã chọn</option>
          <option value="MONTHLY">Hằng tháng vào một ngày cố định</option>
        </select>
      </label>
      {value.rule === "WEEKLY" && (
        <fieldset>
          <legend>Ngày trong tuần</legend>
          {[
            "Chủ nhật",
            "Thứ Hai",
            "Thứ Ba",
            "Thứ Tư",
            "Thứ Năm",
            "Thứ Sáu",
            "Thứ Bảy",
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
            Ngày trong tháng
            <input
              aria-label="Ngày trong tháng"
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
          <p>Tháng không có ngày đã chọn sẽ không tạo lần thực hiện.</p>
        </>
      )}
      <label>
        Người thực hiện mặc định
        <select
          aria-label="Người thực hiện mặc định"
          required
          value={value.defaultAssigneeId || ""}
          onChange={(e) =>
            onChange({ ...value, defaultAssigneeId: Number(e.target.value) })
          }
        >
          <option value="">Chọn người thực hiện đủ điều kiện</option>
          {executors.map((u) => (
            <option key={u.id} value={u.id}>
              {u.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Ngày kết thúc chuỗi
        <input
          aria-label="Ngày kết thúc chuỗi"
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
