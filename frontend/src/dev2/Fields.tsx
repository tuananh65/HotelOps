import { SearchableSelect } from "../components/SearchableSelect";
import type { Field, Values } from "./contracts";
import { label } from "./contracts";
import { displayCode } from "../i18n/vi";

export function defaults(fields: Field[], source: Values = {}): Values {
  const values: Values = {};
  for (const f of fields) {
    if (
      f.name === "expectedRowVersion" &&
      typeof source.rowVersion === "string"
    )
      values[f.name] = source.rowVersion;
    else if (f.name === "expectedCondition" && source.conditionStatus)
      values[f.name] = source.conditionStatus;
    else if (f.fields)
      values[f.name] = Array.isArray(source[f.name])
        ? (source[f.name] as Values[]).map((v) =>
            defaults(f.fields!, {
              ...v,
              requestLineId: v.requestLineId ?? v.lineId,
            }),
          )
        : [];
    else if (source[f.name] !== undefined && source[f.name] !== null)
      values[f.name] = source[f.name];
    else if (f.default !== undefined) values[f.name] = f.default;
  }
  return values;
}

export function Fields({
  fields,
  values,
  onChange,
  actor,
  prefix = "",
}: {
  fields: Field[];
  values: Values;
  onChange: (v: Values) => void;
  actor: string;
  prefix?: string;
}) {
  return (
    <div className="dev2-fields">
      {fields.map((f) => {
        const name = prefix + f.name;
        if (f.options)
          return (
            <label key={name}>
              {label(f.name)}
              <SearchableSelect
                name={name}
                required={!f.optional}
                value={String(values[f.name] ?? "")}
                onChange={(e) =>
                  onChange({ ...values, [f.name]: e.target.value })
                }
              >
                <option value="">Chọn {label(f.name).toLowerCase()}</option>
                {f.options.map((option) => (
                  <option key={option} value={option}>
                    {displayCode(option)}
                  </option>
                ))}
              </SearchableSelect>
            </label>
          );
        if (f.name === "actorUserId" || f.name === "reportedBy")
          return (
            <label key={name}>
              {label(f.name)}
              <input name={name} value={actor} readOnly aria-readonly="true" />
              <small>Người dùng đang đăng nhập</small>
            </label>
          );
        if (f.fields) {
          const rows = (
            Array.isArray(values[f.name]) ? values[f.name] : []
          ) as Values[];
          return (
            <fieldset key={name}>
              <legend>
                {label(f.name)}
                {f.optional ? " (không bắt buộc)" : ""}
              </legend>
              {rows.map((row, i) => (
                <fieldset key={i}>
                  <legend>Dòng {i + 1}</legend>
                  <Fields
                    fields={f.fields!}
                    values={row}
                    actor={actor}
                    prefix={`${name}.${i}.`}
                    onChange={(v) =>
                      onChange({
                        ...values,
                        [f.name]: rows.map((r, n) => (n === i ? v : r)),
                      })
                    }
                  />
                  <button
                    type="button"
                    onClick={() =>
                      onChange({
                        ...values,
                        [f.name]: rows.filter((_, n) => n !== i),
                      })
                    }
                  >
                    Xóa dòng {i + 1}
                  </button>
                </fieldset>
              ))}
              <button
                type="button"
                onClick={() =>
                  onChange({
                    ...values,
                    [f.name]: [
                      ...rows,
                      { ...defaults(f.fields!), lineNo: rows.length + 1 },
                    ],
                  })
                }
              >
                Thêm {label(f.name).toLowerCase()} Dòng
              </button>
            </fieldset>
          );
        }
        const bool = f.type.startsWith("bool"),
          date = f.type.startsWith("DateOnly"),
          time = f.type.startsWith("DateTime"),
          number = /^(long|int|decimal)/.test(f.type);
        const value = values[f.name] ?? f.default ?? "";
        if(f.type === "bool?" && f.optional) return <label key={name}>
          {label(f.name)} (không bắt buộc)
          <SearchableSelect name={name} value={value===true?"true":value===false?"false":""}
            onChange={e=>onChange({...values,[f.name]:e.target.value===""?"":e.target.value==="true"})}>
            <option value="">Tất cả</option><option value="true">Có</option><option value="false">Không</option>
          </SearchableSelect>
        </label>;
        const display =
          time && value ? String(value).slice(0, 16) : String(value);
        return (
          <label key={name}>
            {label(f.name)}
            {f.optional ? " (không bắt buộc)" : " *"}
            <input
              name={name}
              type={
                bool
                  ? "checkbox"
                  : date
                    ? "date"
                    : time
                      ? "datetime-local"
                      : number
                        ? "number"
                        : "text"
              }
              required={!f.optional && !bool}
              step={
                f.type.startsWith("decimal") ? "any" : number ? "1" : undefined
              }
              min={
                f.name.endsWith("Id") ||
                f.name === "page" ||
                f.name === "pageSize"
                  ? 1
                  : undefined
              }
              max={f.name === "pageSize" ? 100 : undefined}
              checked={bool ? value === true : undefined}
              value={bool ? undefined : display}
              onChange={(e) =>
                onChange({
                  ...values,
                  [f.name]: bool ? e.target.checked : e.target.value,
                })
              }
            />
            {f.name.includes("RowVersion") && (
              <small>
                Dùng phiên bản mới nhất từ hồ sơ. Phiên bản cũ sẽ bị từ chối.
              </small>
            )}
          </label>
        );
      })}
    </div>
  );
}
