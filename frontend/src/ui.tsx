import { ThemeToggle } from "./components/ThemeToggle";
import {
  ArrowLeft,
  ArrowRight,
  ArrowUpDown,
  CheckCircle2,
  Clock3,
  CircleAlert,
  CircleMinus,
  LoaderCircle,
  BedDouble,
  Users,
  UserCheck,
  UserX,
  Bell,
  Building2,
  CalendarDays,
  ChevronRight,
  ChevronUp,
  ClipboardCheck,
  FileText,
  Flower2,
  LayoutDashboard,
  Menu,
  Package,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Wrench,
  X,
} from "lucide-react";
import React, {
  useEffect,
  useState,
  useRef,
  useSyncExternalStore,
} from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  domainFor,
  domains,
  pathFor,
  screenById,
  useCases,
  type Action,
  type Domain,
  type Field,
  type RecordData,
} from "./domain";
import { readEvidence } from "./evidence";
import { ApiError } from "./api/http";
import { formFieldForApiPath } from "./api/adapters";
import { getLanguage, LanguageSwitcher, t, useLanguage } from "./i18n";
import * as service from "./services";
import "./styles.css";
const cellText = (value: unknown) =>
  typeof value === "number" && !Number.isFinite(value)
    ? "N/A"
    : String(value ?? "—");
export const quantity = (value: number | null | undefined) =>
  value != null && Number.isFinite(value) ? value : "N/A";
export const date = (value: string) =>
  value
    ? new Date(value).toLocaleString(
        getLanguage() === "vi" ? "vi-VN" : "en-GB",
        {
          timeZone: "Asia/Ho_Chi_Minh",
          dateStyle: "medium",
          timeStyle: "short",
        },
      )
    : "—";
export const name = (key: string, id: string) =>
  t(service.snapshot()[key]?.find((r) => r.id === id)?.name || "") ||
  t(
    key === "rooms"
      ? "Shared hotel areas"
      : key === "users"
        ? "Awaiting assignment"
        : "Shared areas",
  );
export const useDB = () => (
  useLanguage(),
  useSyncExternalStore(service.subscribe, service.snapshot)
);
export const notify = (message: string) =>
  window.dispatchEvent(new CustomEvent("hotelops-toast", { detail: message }));
export function StatusBadge({ status }: { status: string }) {
  const kind = /Inactive|Disabled|Retired|Cancelled|Read$/.test(status)
    ? "neutral"
    : /Blocked|Failed|Rejected|Rework|Critical|Overdue/.test(status)
      ? "danger"
      : /Pending|Paused|Waiting|Temporary|Below minimum|Low stock|Draft|Unread/.test(
            status,
          )
        ? "pending"
        : /Ready|Passed|Completed|Closed|Posted|Active|Accepted|Acknowledged|Received|Reconciled|Available/.test(
              status,
            )
          ? "success"
          : /Assigned/.test(status)
            ? "assigned"
            : /Progress|Inspecting|Working/.test(status)
              ? "working"
              : "neutral";
  const Icon =
    kind === "success"
      ? CheckCircle2
      : kind === "danger"
        ? CircleAlert
        : kind === "working" || kind === "assigned"
          ? LoaderCircle
          : kind === "neutral"
            ? CircleMinus
            : Clock3;
  return (
    <span className={`status ${kind}`}>
      <Icon size={15} aria-hidden="true" />
      {t(status)}
    </span>
  );
}
export function Logo() {
  return (
    <div className="brand">
      <Flower2 size={38} strokeWidth={1} />
      <div>
        {t("DUC VUONG")}
        <small>{t("HOTEL OPERATIONS")}</small>
      </div>
    </div>
  );
}
export const showTemporaryPassword = (value: string, hours: number) =>
  window.dispatchEvent(
    new CustomEvent("hotelops-temporary-password", {
      detail: { value, hours },
    }),
  );
export function Toast() {
  const [text, setText] = useState("");
  const [temporaryPassword, setTemporaryPassword] = useState<{
    value: string;
    hours: number;
  }>();
  useEffect(() => {
    const handler = (event: Event) =>
      setTemporaryPassword((event as CustomEvent).detail);
    window.addEventListener("hotelops-temporary-password", handler);
    return () =>
      window.removeEventListener("hotelops-temporary-password", handler);
  }, []);
  useEffect(() => {
    let timer: number;
    const handler = (event: Event) => {
      setText((event as CustomEvent).detail);
      clearTimeout(timer);
      timer = window.setTimeout(() => setText(""), 7000);
    };
    window.addEventListener("hotelops-toast", handler);
    return () => {
      clearTimeout(timer);
      window.removeEventListener("hotelops-toast", handler);
    };
  }, []);
  return (
    <>
      {temporaryPassword && (
        <Modal
          title={t("Temporary password")}
          onClose={() => setTemporaryPassword(undefined)}
        >
          <p>
            {t("Temporary password:")} <code>{temporaryPassword.value}</code>
          </p>
          <p>
            {t("It is shown only once and expires in {hours} hours.").replace(
              "{hours}",
              String(temporaryPassword.hours),
            )}
          </p>
          <button
            className="primary"
            onClick={() => setTemporaryPassword(undefined)}
          >
            {t("Close dialog")}
          </button>
        </Modal>
      )}
      {text ? (
        <div className="toast" role="status">
          {t(text)}
          <button aria-label={t("Dismiss message")} onClick={() => setText("")}>
            <X size={16} />
          </button>
        </div>
      ) : null}
    </>
  );
}
export function Modal({
  title,
  children,
  onClose,
}: {
  title: string;
  children: React.ReactNode;
  onClose: () => void;
}) {
  const ref = React.useRef<HTMLDialogElement>(null);
  useEffect(() => {
    ref.current?.showModal();
  }, []);
  return (
    <dialog ref={ref} onCancel={onClose} aria-label={title}>
      <header>
        <h2>{title}</h2>
        <button onClick={onClose} aria-label={t("Close dialog")}>
          <X size={20} />
        </button>
      </header>
      {children}
    </dialog>
  );
}
export function ErrorMessage({ message }: { message: string }) {
  return message ? (
    <div className="error" role="alert">
      {t(message)}
    </div>
  ) : null;
}
export function selectActionValues(a: Action, r: RecordData) {
  return Object.fromEntries(
    (a.fields || []).map((f) => [f.key, f.key === "note" ? "" : r[f.key]]),
  );
}
export function FieldControl({
  field,
  value,
  onChange,
  record,
  errorMessage,
}: {
  field: Field;
  value: any;
  onChange: (value: any) => void;
  record?: RecordData;
  errorMessage?: string;
}) {
  const db = useDB();
  const f = field;
  const [uploadError, setUploadError] = useState("");
  const refs: Record<string, string> = {
    room: "rooms",
    location: "locations",
    roomType: "roomTypes",
    warehouse: "warehouses",
    asset: "assets",
    checklist: "checklists",
    cleaning: "cleaning",
    inspection: "inspections",
    user: "users",
    technician: "users",
    housekeeper: "users",
    inspector: "users",
    replenishment: "replenishments",
    transaction: "transactions",
  };
  let options = f.options?.map((v) => ({ id: v, name: v }));
  if (refs[f.type || ""])
    options = service
      .visible(refs[f.type!])
      .filter(
        (r) =>
          r.status !== "Inactive" &&
          (f.type !== "checklist" || r.status === "Published"),
      )
      .filter((r) =>
        f.type === "housekeeper"
          ? r.roles?.includes("Housekeeping Staff")
          : f.type === "technician"
            ? r.roles?.some((v: string) =>
                ["Maintenance Lead", "Maintenance Staff"].includes(v),
              )
            : f.type === "inspector"
              ? r.roles?.includes("Housekeeping Supervisor")
              : true,
      )
      .map((r) => ({ id: r.id, name: r.name }));
  // Administrators can choose scoped staff; operational users can choose active staff for assignments.
  if (["user", "technician", "housekeeper", "inspector"].includes(f.type || ""))
    options = db.users
      .filter((r) => r.status === "Active" && service.inScope(r))
      .filter((r) =>
        f.type === "housekeeper"
          ? r.roles.includes("Housekeeping Staff")
          : f.type === "technician"
            ? r.roles.some((v: string) =>
                ["Maintenance Lead", "Maintenance Staff"].includes(v),
              )
            : f.type === "inspector"
              ? r.roles.includes("Housekeeping Supervisor")
              : true,
      )
      .map((r) => ({ id: r.id, name: r.name }));
  if (f.key === "branch")
    options = (
      db.branches.length ? db.branches : service.allowedBranches()
    ).map((r) => ({ id: f.useIds ? r.id : r.name, name: r.name }));
  if (["department", "receivingDepartment"].includes(f.key))
    options = (
      db.departments.length ? db.departments : service.allowedDepartments()
    ).map((r) => ({ id: f.useIds ? r.id : r.name, name: r.name }));
  if (f.key === "roles")
    options = db.roles.map((r) => ({ id: r.name, name: r.name }));
  if (f.key === "unit")
    options = db.units.map((r) => ({ id: r.id, name: r.name }));
  const controlId = React.useId();
  const displayValue =
    f.type === "datetime-local" && value && String(value).endsWith("Z")
      ? new Date(new Date(value).getTime() + 7 * 3600000)
          .toISOString()
          .slice(0, 16)
      : value;
  if (f.type === "lines") {
    const lines: any[] = value || [];
    return (
      <fieldset className="full material-lines">
        <legend>{t(f.label)}</legend>
        <div className="line-head">
          <span>{t("Item")}</span>
          <span>{t("Quantity / unit")}</span>
          <span />
        </div>
        {lines.map((l, i) => (
          <div className="material-line" key={i}>
            <select
              aria-label={`Item ${i + 1}`}
              required
              value={l.item || ""}
              onChange={(e) =>
                onChange(
                  lines.map((v, j) =>
                    j === i
                      ? {
                          ...v,
                          item: e.target.value,
                          unit: db.items.find(
                            (item) => item.id === e.target.value,
                          )?.unit,
                        }
                      : v,
                  ),
                )
              }
            >
              <option value="">{t("Select item")}</option>
              {db.items
                .filter((r) => r.status === "Active")
                .map((r) => (
                  <option key={r.id} value={r.id}>
                    {t(r.name)} {t("·")}
                    {r.unit}
                  </option>
                ))}
            </select>
            <div className="quantity-unit">
              <input
                aria-label={`Quantity ${i + 1}`}
                type="number"
                min="0"
                step="any"
                required
                value={l.quantity ?? ""}
                onChange={(e) =>
                  onChange(
                    lines.map((v, j) =>
                      j === i ? { ...v, quantity: Number(e.target.value) } : v,
                    ),
                  )
                }
              />
              <select
                disabled={!!record?.lines}
                aria-label={`Unit ${i + 1}`}
                value={
                  l.unit || db.items.find((v) => v.id === l.item)?.unit || ""
                }
                onChange={(e) =>
                  onChange(
                    lines.map((v, j) =>
                      j === i ? { ...v, unit: e.target.value } : v,
                    ),
                  )
                }
              >
                {[
                  db.items.find((v) => v.id === l.item)?.unit,
                  ...(
                    db.items.find((v) => v.id === l.item)?.conversions || []
                  ).map((c: any) => c.unit),
                ]
                  .filter(Boolean)
                  .map((u) => (
                    <option key={u} value={u}>
                      {name("units", u)}
                    </option>
                  ))}
              </select>
            </div>
            <button
              type="button"
              aria-label={`Remove line ${i + 1}`}
              onClick={() => onChange(lines.filter((_, j) => j !== i))}
            >
              <X size={16} />
            </button>
            {record?.type === "Transfer" && (
              <div className="transfer-quantities">
                <label>
                  {t("Damaged")}
                  <input
                    aria-label={`Damaged quantity ${i + 1}`}
                    type="number"
                    min="0"
                    value={l.damaged || 0}
                    onChange={(e) =>
                      onChange(
                        lines.map((v, j) =>
                          j === i
                            ? { ...v, damaged: Number(e.target.value) }
                            : v,
                        ),
                      )
                    }
                  />
                </label>
                <label>
                  {t("Missing")}
                  <input
                    aria-label={`Missing quantity ${i + 1}`}
                    type="number"
                    min="0"
                    value={l.missing || 0}
                    onChange={(e) =>
                      onChange(
                        lines.map((v, j) =>
                          j === i
                            ? { ...v, missing: Number(e.target.value) }
                            : v,
                        ),
                      )
                    }
                  />
                </label>
              </div>
            )}
            {l.system !== undefined && (
              <small>
                {t("System at cutoff:")}
                {l.system} {t("· Difference:")} {Number(l.quantity) - l.system}
              </small>
            )}
          </div>
        ))}
        <button
          type="button"
          className="secondary"
          onClick={() => onChange([...lines, { item: "", quantity: 1 }])}
        >
          <Plus size={15} /> {t("Add item")}
        </button>
      </fieldset>
    );
  }
  if (f.type === "results") {
    const criteria = record?.criteria?.split("\n").filter(Boolean) || [];
    const results: any[] =
      value ||
      criteria.map((text: string) => ({ text, result: "", defect: "" }));
    return (
      <fieldset className="full">
        <legend>{t(f.label)}</legend>
        {results.map((r, i) => (
          <div className="criterion" key={i}>
            <span>{r.text}</span>
            <select
              required
              aria-label={r.text + " result"}
              value={r.result || ""}
              onChange={(e) =>
                onChange(
                  results.map((v, j) =>
                    j === i ? { ...v, result: e.target.value } : v,
                  ),
                )
              }
            >
              <option value="">{t("Select result")}</option>
              <option value="Pass">{record?.rework ? "Fixed" : "Pass"}</option>
              <option value="Fail">
                {record?.rework ? "Not Fixed" : "Fail"}
              </option>
            </select>
            {r.result === "Fail" && (
              <input
                aria-label={r.text + " defect"}
                required
                placeholder={t("Describe the defect")}
                value={r.defect || ""}
                onChange={(e) =>
                  onChange(
                    results.map((v, j) =>
                      j === i ? { ...v, defect: e.target.value } : v,
                    ),
                  )
                }
              />
            )}
          </div>
        ))}
      </fieldset>
    );
  }
  if (f.type === "permissions") {
    return (
      <fieldset className="full permissions-editor">
        <legend>{t("Permissions by feature")}</legend>
        {Object.values(useCases).map((uc) => (
          <label className="check-label" key={uc.id}>
            <input
              type="checkbox"
              checked={(value || []).includes(uc.id)}
              onChange={(e) =>
                onChange(
                  e.target.checked
                    ? [...(value || []), uc.id]
                    : (value || []).filter((v: string) => v !== uc.id),
                )
              }
            />
            <span>
              {uc.id} {t("·")}
              {t(uc.name)}
            </span>
          </label>
        ))}
      </fieldset>
    );
  }
  if (["multiselect", "employees"].includes(f.type || "")) {
    const opts =
      f.type === "employees"
        ? db.users
            .filter((r) => r.status === "Active" && service.inScope(r))
            .map((r) => ({ id: r.id, name: r.name }))
        : options || [];
    return (
      <fieldset className="full">
        <legend>{t(f.label)}</legend>
        <div className="checks">
          {opts.map((o) => (
            <label className="check-label" key={o.id}>
              <input
                type="checkbox"
                checked={(value || []).includes(o.id)}
                onChange={(e) =>
                  onChange(
                    e.target.checked
                      ? [...(value || []), o.id]
                      : (value || []).filter((v: string) => v !== o.id),
                  )
                }
              />
              {t(o.name)}
            </label>
          ))}
        </div>
      </fieldset>
    );
  }
  if (f.type === "work") {
    options = ["cleaning", "incidents", "tasks", "materials"].flatMap((key) =>
      service
        .visible(key)
        .filter((r) => !["Closed", "Completed", "Cancelled"].includes(r.status))
        .map((r) => ({ id: key + ":" + r.id, name: r.name })),
    );
  }
  return (
    <div
      className={`field ${["textarea", "file"].includes(f.type || "") ? "full" : ""}`}
    >
      <label htmlFor={controlId}>
        {t(f.label)}
        {f.required && <span className="required"> {t("*")}</span>}
      </label>
      {options ? (
        <select
          id={controlId}
          aria-invalid={!!errorMessage}
          aria-describedby={errorMessage ? "field-error-" + f.key : undefined}
          value={value || ""}
          required={f.required}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">
            {t("Select")} {t(f.label).toLowerCase()}
          </option>
          {options.map((o) => (
            <option key={o.id} value={o.id}>
              {t(o.name)}
            </option>
          ))}
        </select>
      ) : f.type === "textarea" ? (
        <textarea
          id={controlId}
          aria-invalid={!!errorMessage}
          aria-describedby={errorMessage ? "field-error-" + f.key : undefined}
          required={f.required}
          value={value || ""}
          onChange={(e) =>
            onChange(
              f.type === "number"
                ? e.target.value === ""
                  ? ""
                  : Number(e.target.value)
                : e.target.value,
            )
          }
        />
      ) : f.type === "file" ? (
        <>
          <input
            id={controlId}
            aria-invalid={!!errorMessage}
            aria-describedby={errorMessage ? "field-error-" + f.key : undefined}
            type="file"
            multiple
            accept="image/jpeg,image/png,application/pdf"
            onChange={async (e) => {
              const files = Array.from(e.target.files || []);
              if (
                files.length > 5 ||
                files.some(
                  (f) =>
                    f.size > 10 * 1024 * 1024 ||
                    !["image/jpeg", "image/png", "application/pdf"].includes(
                      f.type,
                    ),
                )
              ) {
                setUploadError(
                  "Use up to 5 JPG, PNG or PDF files, maximum 10 MB each.",
                );
                e.target.value = "";
                return;
              }
              let results;
              try {
                results = await readEvidence(files);
              } catch (error) {
                setUploadError((error as Error).message);
                e.target.value = "";
                return;
              }
              setUploadError("");
              onChange(results);
            }}
          />
          <small>{t("JPG, PNG or PDF · 10 MB per file · up to 5 files")}</small>
          <ErrorMessage message={uploadError} />
        </>
      ) : (
        <input
          id={controlId}
          aria-invalid={!!errorMessage}
          aria-describedby={errorMessage ? "field-error-" + f.key : undefined}
          type={f.type || "text"}
          min={f.type === "number" ? 0 : undefined}
          max={f.key === "progress" ? 100 : undefined}
          step={f.type === "number" ? "any" : undefined}
          required={f.required}
          value={displayValue ?? ""}
          onChange={(e) =>
            onChange(
              f.type === "number"
                ? e.target.value === ""
                  ? ""
                  : Number(e.target.value)
                : e.target.value,
            )
          }
        />
      )}
    </div>
  );
}
function sampleFieldValue(f: Field, record?: RecordData) {
  if (f.type === "lines") return [];
  if (f.type === "results") return record?.results || [];
  if (["permissions", "employees", "multiselect"].includes(f.type || ""))
    return [];
  return "";
}
export function Form({
  fields,
  initial = {},
  record,
  onSubmit,
  label = "Save",
  onCancel,
}: {
  fields: Field[];
  initial?: any;
  record?: RecordData;
  onSubmit: (values: any) => void | Promise<void>;
  label?: string;
  onCancel?: () => void;
}) {
  const [values, setValues] = useState<any>(() =>
    Object.fromEntries(
      fields.map((f) => [
        f.key,
        ["branch", "department", "receivingDepartment"].includes(f.key) &&
        initial[f.key + "Id"]
          ? initial[f.key + "Id"]
          : initial[f.key] !== undefined
            ? ["branch", "department", "receivingDepartment"].includes(f.key) &&
              initial[f.key]
              ? service.resolveReference(
                  f.key === "branch" ? "branches" : "departments",
                  initial[f.key],
                  initial.branchId || initial.branch,
                )
              : initial[f.key]
            : sampleFieldValue(f, record),
      ]),
    ),
  );
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const [uncertain, setUncertain] = useState(false);
  const [uploadsPending, setUploadsPending] = useState(0);
  useEffect(() => {
    const onUpload = (event: Event) =>
      setUploadsPending((event as CustomEvent<number>).detail);
    window.addEventListener("hotelops-upload", onUpload);
    return () => window.removeEventListener("hotelops-upload", onUpload);
  }, []);
  return (
    <form
      noValidate
      onSubmit={async (e) => {
        e.preventDefault();
        if (submitting.current || uncertain || uploadsPending > 0) return;
        submitting.current = true;
        const formElement = e.currentTarget;
        setBusy(true);
        setError("");
        try {
          const invalid: Record<string, string> = {};
          for (const f of fields) {
            const value = values[f.key];
            if (
              f.required &&
              (value == null ||
                value === "" ||
                (Array.isArray(value) && !value.length))
            )
              invalid[f.key] = f.label + " is required.";
            else if (
              f.type === "email" &&
              value &&
              !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
            )
              invalid[f.key] = "Enter a valid email address.";
            else if (
              f.type === "number" &&
              value != null &&
              value !== "" &&
              (!Number.isFinite(Number(value)) || Number(value) < (f.min ?? 0))
            )
              invalid[f.key] = "Enter a valid non-negative number.";
          }
          setFieldErrors(invalid);
          if (Object.keys(invalid).length) {
            requestAnimationFrame(() => {
              const first = formElement.querySelector<HTMLElement>(
                ".invalid-field input, .invalid-field select, .invalid-field textarea",
              );
              first?.focus();
            });
            return;
          }
          await onSubmit(values);
        } catch (err) {
          setError((err as Error).message);
          if (err instanceof ApiError) {
            const mapped: Record<string, string> = {};
            for (const [path, message] of Object.entries(err.fieldErrors)) {
              const key = formFieldForApiPath(path);
              mapped[key] = [mapped[key], message].filter(Boolean).join(" ");
            }
            setFieldErrors(mapped);
            if (err.outcomeUnknown) setUncertain(true);
          }
        } finally {
          submitting.current = false;
          setBusy(false);
        }
      }}
    >
      <div className="form-grid">
        {fields.map((f) => (
          <div
            key={f.key}
            className={`form-field-wrap ${["textarea", "file", "multiselect", "employees", "permissions", "lines", "results"].includes(f.type || "") ? "full" : ""} ${fieldErrors[f.key] ? "invalid-field" : ""}`}
          >
            <FieldControl
              key={f.key}
              field={{ ...f, useIds: true }}
              errorMessage={fieldErrors[f.key]}
              value={values[f.key]}
              record={record}
              onChange={(v) => {
                setValues({
                  ...values,
                  [f.key]: v,
                  ...(f.key === "branch"
                    ? { department: "", receivingDepartment: "" }
                    : {}),
                });
                setFieldErrors((previous) => ({ ...previous, [f.key]: "" }));
              }}
            />
            {fieldErrors[f.key] && (
              <p
                id={"field-error-" + f.key}
                className="field-error"
                role="alert"
              >
                {t(fieldErrors[f.key])}
              </p>
            )}
          </div>
        ))}
      </div>
      <ErrorMessage message={error} />
      {uncertain && (
        <button
          type="button"
          className="secondary"
          onClick={() => window.location.reload()}
        >
          {t("Reload screen")}
        </button>
      )}
      <div className="form-actions">
        {onCancel && (
          <button type="button" className="secondary" onClick={onCancel}>
            {t("Cancel")}
          </button>
        )}
        <button
          className="primary"
          disabled={busy || uncertain || uploadsPending > 0}
        >
          {t(busy ? "Saving…" : label)}
          <ArrowRight size={15} />
        </button>
      </div>
    </form>
  );
}
const navGroups = [
  { label: "OVERVIEW", icon: LayoutDashboard, ids: [5, 6] },
  { label: "ADMINISTRATION", icon: ShieldCheck, ids: [8, 10, 12] },
  { label: "HOTEL STRUCTURE", icon: Building2, ids: [13, 15, 17, 19, 21, 23] },
  { label: "HOUSEKEEPING", icon: BedDouble, ids: [24, 28, 30] },
  { label: "ROOM INSPECTION", icon: ClipboardCheck, ids: [31, 33, 37, 38] },
  { label: "MAINTENANCE", icon: Wrench, ids: [39, 43, 45, 47] },
  {
    label: "ASSETS & INVENTORY",
    icon: Package,
    ids: [48, 51, 53, 54, 56, 57, 59, 61],
  },
  { label: "REQUESTS", icon: FileText, ids: [62, 64, 76] },
  {
    label: "TASKS & SHIFTS",
    icon: CalendarDays,
    ids: [66, 69, 70, 71, 73, 74],
  },
  { label: "REPORTING", icon: FileText, ids: [78, 79] },
];
export function screenAllowed(id: number) {
  if ([1, 2].includes(id)) return true;
  const s = screenById(id);
  return (
    service.canOpenScreen(id)
  );
}
export function Shell({
  children,
  screen,
}: {
  children: React.ReactNode;
  screen: number;
}) {
  const [open, setOpen] = useState(false);
  const db = useDB();
  const user = service.currentUser()!;
  const nav = useNavigate();
  const location = useLocation();
  useEffect(() => setOpen(false), [location.pathname]);
  useEffect(() => {
    const interval = setInterval(() => {
      if (!service.currentSession()) nav("/login");
    }, 30000);
    const touch = () => service.touch();
    window.addEventListener("pointerdown", touch);
    window.addEventListener("keydown", touch);

    return () => {
      clearInterval(interval);
      window.removeEventListener("pointerdown", touch);
      window.removeEventListener("keydown", touch);
    };
  }, [nav]);
  useEffect(() => {
    const close = (event: Event) => {
      if (event instanceof KeyboardEvent && event.key !== "Escape") return;
      document
        .querySelectorAll<HTMLDetailsElement>("details.filter-popover[open]")
        .forEach((popover) => {
          if (
            event instanceof KeyboardEvent ||
            !popover.contains(event.target as Node)
          )
            popover.open = false;
        });
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", close);
    };
  }, []);
  return (
    <div className="app-shell">
      {open && (
        <button
          className="drawer-backdrop"
          aria-label={t("Close navigation")}
          onClick={() => setOpen(false)}
        />
      )}
      <aside className={open ? "sidebar open" : "sidebar"}>
        <Link to={screenAllowed(5) ? "/dashboard" : "/notifications"}>
          <Logo />
        </Link>
        <div className="property-label">
          {t("SAIGON HOTEL")}
          <span>{t("INTERNAL")}</span>
        </div>
        <nav>
          {navGroups.map((g) => {
            const allowed = g.ids.filter(screenAllowed);
            return allowed.length ? (
              <div className="nav-group" key={t(g.label)}>
                <p>{t(g.label)}</p>
                {allowed.map((id) => {
                  const s = screenById(id);
                  const activeScreen = navGroups.some((group) =>
                    group.ids.includes(screen),
                  )
                    ? screen
                    : domainFor(screen).list;
                  const active = id === activeScreen;
                  return (
                    <Link
                      key={id}
                      aria-current={active ? "page" : undefined}
                      className={active ? "active" : ""}
                      to={s.route.replace(":id", "select")}
                    >
                      <g.icon size={17} strokeWidth={1.5} />
                      <span>{t(s.name)}</span>
                      {active && <ChevronRight size={14} />}
                    </Link>
                  );
                })}
              </div>
            ) : null;
          })}
        </nav>
        <div className="sidebar-foot">
          <span className="live-dot" /> {t("HOTEL WORKSPACE")}
          <small>{t("Duc Vuong Group · HotelOps v1")}</small>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <button
            className="mobile-menu"
            aria-label={t("Open navigation")}
            onClick={() => setOpen(true)}
          >
            <Menu size={21} />
          </button>
          <div className="breadcrumbs">
            {t("HotelOps")}
            <ChevronRight size={12} />{" "}
            {screenById(screen).feature.split(" (")[0] !==
              screenById(screen).name && (
              <>
                {t(screenById(screen).feature.split(" (")[0])}
                <ChevronRight size={12} />
              </>
            )}
            <span>{t(screenById(screen).name)}</span>
          </div>
          <div className="top-actions">
            <LanguageSwitcher />
            <ThemeToggle />
            <Link to="/dev/screens">{t("Screens")}</Link>
            <span className="branch-context">
              <Building2 size={15} />
              <select
                aria-label={t("Branch context")}
                value={service.branchContext()}
                onChange={(e) => service.setBranchContext(e.target.value)}
              >
                {Array.from(
                  new Set([user.branch, ...(user.branches || [])]),
                ).map((b) => (
                  <option key={b}>{b}</option>
                ))}
                {user.branches?.length > 1 && (
                  <option>{t("All branches")}</option>
                )}
              </select>
            </span>
            <Link
              aria-label={t("Notifications")}
              to="/notifications"
              className="bell"
            >
              <Bell size={19} />
              {db.notifications.filter(
                (n) => n.recipient === user.id && n.status === "Unread",
              ).length > 0 && <i />}
            </Link>
            <Link className="user-menu" to="/profile">
              <span className="avatar">{user.name[0]}</span>
              <div>
                {t(user.name)}
                <small>
                  {t(user.roles[0] || "") +
                    (user.roles.length > 1
                      ? " +" + (user.roles.length - 1)
                      : "")}
                </small>
              </div>
            </Link>
          </div>
        </header>
        <main className="main-content" data-screen-id={screen}>
          {children}
        </main>
        <footer className="workspace-footer">
          {t("DUC VUONG GROUP")}
          <span>{t("HotelOps · Asia/Ho_Chi_Minh")}</span>
        </footer>
      </div>
    </div>
  );
}
export function PageHeader({
  id,
  children,
}: {
  id: number;
  children?: React.ReactNode;
}) {
  const s = screenById(id);
  return (
    <div className="page-header">
      <div>
        <span className="eyebrow">{s.feature.match(/FE-\d+/)?.[0]}</span>
        <h1>{t(s.name)}</h1>
        {shortDescription(id) && (
          <p title={t(s.description)}>{t(shortDescription(id))}</p>
        )}
      </div>
      <div className="header-actions">{children}</div>
    </div>
  );
}
export function Summary({
  items,
  variant = "compact",
}: {
  variant?: "compact" | "cards";
  items: {
    label: string;
    value: React.ReactNode;
    detail?: string;
    to?: string;
    onClick?: () => void;
    selected?: boolean;
  }[];
}) {
  const [collapsed, setCollapsed] = useState(false);
  return (
    <section className={`summary-wrap summary-${variant}`}>
      <button
        className="summary-toggle"
        aria-expanded={!collapsed}
        aria-label={t(collapsed ? "Expand summary" : "Collapse summary")}
        onClick={() => setCollapsed(!collapsed)}
      >
        {collapsed ? <Plus size={14} /> : <ChevronUp size={14} />}
      </button>
      <div
        className={`summary-grid ${collapsed ? "collapsed" : ""}`}
        style={{
          gridTemplateColumns: `repeat(${Math.min(Math.max(items.length, 1), 7)}, minmax(0, 1fr))`,
        }}
      >
        {items.map((item) => {
          const label = item.label;
          const value = typeof item.value === "string" ? item.value : "";
          const tone = /Severity|priority|stage|status/i.test(label)
            ? /Critical|High|Failed|Blocked|Rejected/i.test(value)
              ? "danger"
              : /Resolved|Completed|Closed|Ready|Active/i.test(value) &&
                  !/Inactive/i.test(value)
                ? "success"
                : /Pending|Waiting|Medium|Warning/i.test(value)
                  ? "warning"
                  : /priority/i.test(label)
                    ? "cyan"
                    : "neutral"
            : /Room blocks/i.test(label)
              ? Number(item.value) > 0
                ? "danger"
                : "success"
              : /Overdue|Blocked|Incident|Critical|Failed|Rejected|Rework/i.test(
                    label,
                  )
                ? "danger"
                : /Pending|minimum|Temporary|Warning|Low stock|Awaiting|Waiting|Open tasks/i.test(
                      label,
                    )
                  ? "warning"
                  : /Ready|^Active|Completed|Accepted|Available|Passed/i.test(
                        label,
                      )
                    ? "success"
                    : /Inactive|Disabled|Cancelled/i.test(label)
                      ? "neutral"
                      : /Department|Branch|Location/i.test(label)
                        ? "violet"
                        : /Assigned employee|effort|time|progress/i.test(label)
                          ? "cyan"
                          : "info";
          const Icon = /Department|Branch/i.test(label)
            ? Building2
            : /User|employee/i.test(label)
              ? tone === "success"
                ? UserCheck
                : tone === "neutral"
                  ? UserX
                  : Users
              : /Severity/i.test(label)
                ? ShieldCheck
                : /priority/i.test(label)
                  ? ArrowUpDown
                  : /stage|Resolved|Completed/i.test(label)
                    ? CheckCircle2
                    : /Task|work/i.test(label)
                      ? ClipboardCheck
                      : /Room|Ready|Cleaning/i.test(label)
                        ? BedDouble
                        : /stock|item|material/i.test(label)
                          ? Package
                          : tone === "danger"
                            ? CircleAlert
                            : tone === "warning"
                              ? Clock3
                              : tone === "success"
                                ? CheckCircle2
                                : LayoutDashboard;
          const content = (
            <>
              <span className="metric-label">{t(item.label)}</span>
              <strong>
                {typeof item.value === "string"
                  ? t(item.value)
                  : typeof item.value === "number"
                    ? quantity(item.value)
                    : item.value}
              </strong>
              <small>{t(item.detail || "")}</small>
              <span className="metric-icon">
                <Icon size={25} strokeWidth={1.8} />
              </span>
            </>
          );
          if (item.onClick)
            return (
              <button
                key={item.label}
                className="metric state-filter"
                data-tone={tone}
                aria-pressed={item.selected}
                onClick={item.onClick}
              >
                {content}
              </button>
            );
          return item.to ? (
            <Link
              key={t(item.label)}
              data-tone={tone}
              className="metric"
              to={item.to}
            >
              {content}
            </Link>
          ) : (
            <div className="metric" data-tone={tone} key={t(item.label)}>
              {content}
            </div>
          );
        })}
      </div>
    </section>
  );
}
export function DataTable({
  rows,
  columns,
  onOpen,
  initialSort,
  extraFilters,
  compact = false,
}: {
  rows: RecordData[];
  columns?: {
    key: string;
    label: string;
    render?: (r: RecordData) => React.ReactNode;
  }[];
  onOpen?: (r: RecordData) => void;
  initialSort?: string;
  extraFilters?: React.ReactNode;
  compact?: boolean;
}) {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [department, setDepartment] = useState("");
  const [role, setRole] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState(initialSort || "createdAt");
  const [ascending, setAscending] = useState(initialSort === "name");
  const [filtersOpen, setFiltersOpen] = useState(() => window.innerWidth > 767);
  const activeFilters = [status, priority, department, role, from, to].filter(
    Boolean,
  ).length;
  const resetFilters = () => {
    setSearch("");
    setStatus("");
    setPriority("");
    setDepartment("");
    setRole("");
    setFrom("");
    setTo("");
  };
  const cols = columns || [
    { key: "name", label: "Record" },
    { key: "branch", label: "Branch" },
    { key: "department", label: "Department" },
    {
      key: "status",
      label: "Status",
      render: (r) => <StatusBadge status={r.status} />,
    },
    {
      key: "due",
      label: "Deadline",
      render: (r) => (
        <span
          className={
            r.due &&
            new Date(r.due) < new Date() &&
            !["Closed", "Completed", "Cancelled"].includes(r.status)
              ? "overdue-text"
              : ""
          }
        >
          {date(r.due)}
        </span>
      ),
    },
  ];
  const rangeError = from && to && from > to;
  const filtered = rows
    .filter(
      (r) =>
        (
          JSON.stringify(r) +
          " " +
          cols
            .map((c) => {
              const value = c.render?.(r);
              return typeof value === "string" ? t(value) : "";
            })
            .join(" ") +
          " " +
          Object.values(r)
            .filter((v) => typeof v === "string")
            .map((v) => t(v as string))
            .join(" ")
        )
          .toLocaleLowerCase()
          .includes(search.toLocaleLowerCase()) &&
        (!status || r.status === status) &&
        (!priority || r.priority === priority) &&
        (!department || r.department === department) &&
        (!role || r.roles?.includes(role)) &&
        (!from ||
          (cols.some((c) => c.key === "due")
            ? r.due || r.createdAt
            : r.createdAt
          ).slice(0, 10) >= from) &&
        (!to ||
          (cols.some((c) => c.key === "due")
            ? r.due || r.createdAt
            : r.createdAt
          ).slice(0, 10) <= to),
    )
    .sort(
      (a, b) =>
        (String(a[sort] ?? "").localeCompare(String(b[sort] ?? ""), undefined, {
          numeric: true,
        }) || a.id.localeCompare(b.id)) * (ascending ? 1 : -1),
    );
  const pages = Math.max(1, Math.ceil(filtered.length / 20));
  useEffect(
    () => setPage(1),
    [search, status, priority, department, role, from, to],
  );
  return (
    <section className={`table-section ${compact ? "compact-table" : ""}`}>
      {!compact && (
        <div className="filter-bar">
          <div className="search-input">
            <Search size={17} />
            <input
              aria-label={t("Search records")}
              placeholder={t("Search records…")}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <button
            className="secondary filter-toggle"
            aria-expanded={filtersOpen}
            onClick={() => setFiltersOpen(!filtersOpen)}
          >
            {t("Filters")}{" "}
            {activeFilters > 0 && (
              <span className="filter-count">{activeFilters}</span>
            )}
          </button>
          <div className={`advanced-filters ${filtersOpen ? "expanded" : ""}`}>
            <select
              aria-label={t("Filter by status")}
              value={status}
              onChange={(e) => setStatus(e.target.value)}
            >
              <option value="">{t("All statuses")}</option>
              {Array.from(new Set(rows.map((r) => r.status)))
                .sort()
                .map((s) => (
                  <option key={s} value={s}>
                    {t(s)}
                  </option>
                ))}
            </select>
            {rows.some((r) => r.priority) && (
              <select
                aria-label={t("Filter by priority")}
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
              >
                <option value="">{t("All priorities")}</option>
                {["Low", "Normal", "High", "Critical"].map((p) => (
                  <option key={p} value={p}>
                    {t(p)}
                  </option>
                ))}
              </select>
            )}
            {cols.some((c) => c.key === "roles") && (
              <select
                aria-label={t("Filter by role")}
                value={role}
                onChange={(event) => setRole(event.target.value)}
              >
                <option value="">{t("All roles")}</option>
                {Array.from(
                  new Set<string>(rows.flatMap((record) => record.roles || [])),
                )
                  .sort()
                  .map((value) => (
                    <option key={value} value={value}>
                      {t(value)}
                    </option>
                  ))}
              </select>
            )}
            {cols.some((c) => c.key === "department") && (
              <select
                aria-label={t("Filter by department")}
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
              >
                <option value="">{t("All departments")}</option>
                {Array.from(new Set(rows.map((r) => r.department)))
                  .filter(Boolean)
                  .map((p) => (
                    <option key={p} value={p}>
                      {t(p)}
                    </option>
                  ))}
              </select>
            )}
            <div className="inline-dates">
              <label className="date-filter">
                {t(
                  cols.some((c) => c.key === "due")
                    ? "Deadline from"
                    : "Created from",
                )}
                <input
                  aria-label={t("From date")}
                  title={t("From date")}
                  type="date"
                  value={from}
                  onChange={(e) => setFrom(e.target.value)}
                />
              </label>
              <label className="date-filter">
                {t(
                  cols.some((c) => c.key === "due")
                    ? "Deadline to"
                    : "Created to",
                )}
                <input
                  aria-label={t("To date")}
                  title={t("To date")}
                  type="date"
                  value={to}
                  onChange={(e) => setTo(e.target.value)}
                />
              </label>
            </div>
            {extraFilters}
            <button
              className="secondary"
              aria-label={t("Clear filters")}
              onClick={resetFilters}
              disabled={!activeFilters && !search}
            >
              <RefreshCw size={16} />
              {t("Reset")}
            </button>
            <div className="mobile-sort">
              <label>
                {t("Sort by")}
                <select
                  aria-label={t("Sort by")}
                  value={cols.some((c) => c.key === sort) ? sort : "createdAt"}
                  onChange={(e) => setSort(e.target.value || "createdAt")}
                >
                  <option value="createdAt">{t("Default order")}</option>
                  {cols.map((c) => (
                    <option key={c.key} value={c.key}>
                      {t(c.label)}
                    </option>
                  ))}
                </select>
              </label>
              <button
                className="secondary"
                aria-label={t("Toggle sort direction")}
                onClick={() => setAscending(!ascending)}
              >
                {t(ascending ? "Ascending" : "Descending")}{" "}
                {ascending ? "↑" : "↓"}
              </button>
            </div>
          </div>
        </div>
      )}
      {(activeFilters > 0 || search) && (
        <div className="active-filter-summary" role="status">
          {t("Results")}: {filtered.length} · {t("Filters")}:{" "}
          {activeFilters + (search ? 1 : 0)}{" "}
          {[
            [status, setStatus],
            [priority, setPriority],
            [department, setDepartment],
            [role, setRole],
            [from, setFrom],
            [to, setTo],
          ].map(([value, setter], index) =>
            value ? (
              <button
                className="filter-chip"
                key={index}
                aria-label={t("Clear filters") + ": " + t(value as string)}
                onClick={() =>
                  (setter as React.Dispatch<React.SetStateAction<string>>)("")
                }
              >
                {t(value as string)} <X size={14} />
              </button>
            ) : null,
          )}
          <button onClick={resetFilters}>{t("Clear filters")}</button>
        </div>
      )}
      {rangeError ? (
        <ErrorMessage message="End date must not precede start date." />
      ) : (
        <>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  {cols.map((c) => (
                    <th
                      key={c.key}
                      aria-sort={
                        sort === c.key
                          ? ascending
                            ? "ascending"
                            : "descending"
                          : "none"
                      }
                    >
                      <button
                        onClick={() => {
                          setSort(c.key);
                          setAscending(sort === c.key ? !ascending : true);
                        }}
                      >
                        {t(c.label)}
                        <span aria-hidden="true">
                          {sort === c.key ? (ascending ? "↑" : "↓") : "↕"}
                        </span>
                      </button>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filtered
                  .slice(
                    (Math.min(page, pages) - 1) * 20,
                    Math.min(page, pages) * 20,
                  )
                  .map((r) => (
                    <tr
                      key={r.id}
                      className={onOpen ? "clickable-row" : undefined}
                      onClick={
                        onOpen
                          ? (event) => {
                              const target = event.target as Element;
                              if (
                                target.closest(
                                  "a,button,input,select,textarea,label,summary,[role='button'],[role='checkbox'],[contenteditable],[data-row-action-ignore]",
                                )
                              )
                                return;
                              if (window.getSelection()?.toString()) return;
                              onOpen(r);
                            }
                          : undefined
                      }
                    >
                      {cols.map((c) => (
                        <td key={c.key} data-label={t(c.label)}>
                          {c.key ===
                            (cols.find((col) => col.key === "name")?.key ||
                              cols[0]?.key) && onOpen ? (
                            <button
                              className="record-link"
                              onClick={() => onOpen(r)}
                            >
                              {c.render ? c.render(r) : t(cellText(r[c.key]))}
                            </button>
                          ) : c.render ? (
                            typeof c.render(r) === "string" ? (
                              t(c.render(r) as string)
                            ) : (
                              c.render(r)
                            )
                          ) : (
                            cellText(r[c.key])
                          )}
                        </td>
                      ))}
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {!filtered.length && (
            <div className="empty">
              <Search size={28} />
              <h3>{t("No matching records")}</h3>
              <p>{t("Change your search or filters to see more results.")}</p>
            </div>
          )}
          <div className="pagination">
            <span>
              {filtered.length} {t("records · 20 per page")}
            </span>
            <div>
              <button disabled={page <= 1} onClick={() => setPage(page - 1)}>
                {t("Previous")}
              </button>
              <span>
                {Math.min(page, pages)} {t("/")}
                {pages}
              </span>
              <button
                disabled={page >= pages}
                onClick={() => setPage(page + 1)}
              >
                {t("Next")}
              </button>
            </div>
          </div>
        </>
      )}
    </section>
  );
}
export function EntityForm({
  d,
  record,
  onDone,
  onCancel,
}: {
  d: Domain;
  record?: RecordData;
  onDone: (r: RecordData) => void;
  onCancel?: () => void;
}) {
  const user = service.currentUser()!;
  const selectedRoom = service
    .visible("rooms")
    .find(
      (room) =>
        room.id === new URLSearchParams(window.location.search).get("room"),
    );
  return (
    <Form
      fields={
        d.key === "users" && record
          ? d.fields.filter((field) =>
              ["name", "email", "phone"].includes(field.key),
            )
          : d.fields
      }
      initial={
        record || {
          room: selectedRoom?.id,
          branch: selectedRoom?.branch || user.branch,
          branchId: selectedRoom?.branchId || user.branchId,
          department: user.department,
          departmentId: user.departmentId,
          priority: "Normal",
          submission: "Submit",
          lines: [],
          roles: [],
        }
      }
      record={record}
      onCancel={onCancel}
      label={record ? "Save changes" : createLabel(d.key)}
      onSubmit={async (v) => {
        const result = await service.save(d, v, record);
        if (result.temporaryPassword)
          showTemporaryPassword(result.temporaryPassword, result.validForHours);
        notify("Record saved.");
        const source = new URLSearchParams(window.location.search).get(
          "source",
        );
        if (source) {
          try {
            await service.linkWork(source, d.key, result.id);
          } catch (error) {
            notify("Record saved. Linking failed: " + (error as Error).message);
          }
        }
        onDone(result);
      }}
    />
  );
}
export function RelatedRecords({
  entity,
  recordId,
  title,
}: {
  entity: string;
  recordId?: string;
  title: string;
}) {
  const db = useDB();
  const record = service.visible(entity).find((r) => r.id === recordId);
  if (!record) return null;
  const reference: Record<string, string> = {
    rooms: "room",
    users: "assignee",
    locations: "floor",
    roomTypes: "roomType",
    checklists: "checklist",
    assets: "asset",
    warehouses: "warehouse",
    cleaning: "cleaning",
    inspections: "inspection",
    incidents: "incident",
    tasks: "task",
    schedules: "schedule",
    requests: "source",
  };
  const field = reference[entity];
  const groups = domains
    .filter((d) => d.key !== entity)
    .map((d) => ({
      domain: d,
      rows: service
        .visible(d.key)
        .filter(
          (r) =>
            (field && r[field] === recordId) ||
            r.recordId === recordId ||
            r.linkedWork?.id === recordId ||
            (typeof r.linkedWork === "string" &&
              r.linkedWork.split(":")[1] === recordId) ||
            r.linkedRecords?.some(
              (linked: { id: string }) => linked.id === recordId,
            ) ||
            (entity === "items" &&
              r.lines?.some((l: { item: string }) => l.item === recordId)),
        ),
    }))
    .filter((g) => g.rows.length);
  return (
    <section className="related-records">
      <h3 title={title}>{t("Related activity")}</h3>
      {groups.length ? (
        groups.map(({ domain, rows }) => (
          <section className="related-group" key={domain.key}>
            <h4 className="related-group-heading">
              <span>{t(screenById(domain.list).name)}</span>
              <span className="related-count">{rows.length}</span>
            </h4>
            {rows.slice(0, 4).map((r) => (
              <Link
                className="tree-link"
                key={r.id}
                to={pathFor(domain.detail, r.id)}
              >
                {t(r.name)}
                <ChevronRight size={14} />
              </Link>
            ))}
          </section>
        ))
      ) : (
        <p className="quiet">
          {t("No linked activity has been recorded for this entry.")}
        </p>
      )}
    </section>
  );
}
export function Timeline({
  record,
  keyName,
}: {
  record: RecordData;
  keyName: string;
}) {
  const db = useDB();
  const history = db.audit.filter(
    (a) => a.entity === keyName && a.recordId === record.id,
  );
  return (
    <div className="timeline">
      {history.length ? (
        history.map((h) => (
          <div key={h.id}>
            <i />
            <div>
              <strong>{h.action}</strong>
              <p>
                {h.actor} {t("·")}
                {date(h.createdAt)}
              </p>
              {h.after?.reason && <small>{h.after.reason}</small>}
            </div>
          </div>
        ))
      ) : (
        <p>{t("No changes have been recorded for this sample record.")}</p>
      )}
    </div>
  );
}
export function DetailPage({
  id,
  selected,
  informationFields,
  workflowUCs,
  related,
  overview,
}: {
  id: number;
  selected?: RecordData;
  informationFields?: string[];
  workflowUCs?: string[];
  related?: React.ReactNode;
  overview?: React.ReactNode;
}) {
  const db = useDB();
  let d = domainFor(id);
  const params = useParams();
  const work =
    id === 44
      ? service.visible("maintenance").find((row) => row.id === params.id)
      : undefined;
  const sourceIncident = work?.incident
    ? service.visible("incidents").find((row) => row.id === work.incident)
    : undefined;
  if (sourceIncident) d = domains.find((domain) => domain.key === "incidents")!;
  const nav = useNavigate();
  const r =
    selected ||
    sourceIncident ||
    service.visible(d.key).find((r) => r.id === params.id);
  useEffect(() => {
    if (id === 7 && r?.status === "Unread") {
      if (
        service.availableActions(d, r).some((a) => a.label === "Mark as read")
      )
        void service
          .perform(d, r, {
            label: "Mark as read",
            uc: "UC-130",
            from: ["Unread"],
            to: "Read",
          })
          .catch((error) => notify((error as Error).message));
    }
  }, [id, r?.id]);
  const [editing, setEditing] = useState(false);
  const [action, setAction] = useState<Action>();
  const [tab, setTab] = useState("Overview");
  if (!r)
    return (
      <>
        <PageHeader id={id} />
        <div className="empty">
          <h2>{t("Record unavailable")}</h2>
          <p>
            {t("This record does not exist or is outside your assigned scope.")}
          </p>
          <Link to={pathFor(d.list)}>{t("Return to list")}</Link>
        </div>
      </>
    );
  const assignedRoute =
    d.key === "cleaning"
      ? 27
      : d.key === "incidents"
        ? 42
        : d.key === "tasks"
          ? 68
          : undefined;
  const nextActions = service
    .availableActions(d, r)
    .filter((a) => !workflowUCs || workflowUCs.includes(a.uc));
  const assignUC =
    d.key === "cleaning"
      ? r.assignee
        ? "UC-030"
        : "UC-029"
      : d.key === "incidents"
        ? "UC-053"
        : "UC-104";
  return (
    <>
      <Link className="back-link" to={pathFor(d.list)}>
        <ArrowLeft size={15} />
        {t("Back to")} {t(screenById(d.list).name)}
      </Link>
      <PageHeader id={id}>
        {d.key === "cleaning" && r.status === "Pending Inspection" && (
          <Link
            className="primary next-action"
            to={pathFor(33) + "?room=" + r.room}
          >
            {t("Open inspection queue")}
            <ArrowRight size={16} />
          </Link>
        )}
        {nextActions[0] &&
          !(d.key === "cleaning" && r.status === "Pending Inspection") && (
            <button
              className="primary next-action"
              onClick={() => setAction(nextActions[0])}
            >
              {t(nextActions[0].label)}
              <ArrowRight size={16} />
            </button>
          )}
        {d.updateUC && r.canEdit && service.can(d.updateUC, r) && (
          <button className="secondary" onClick={() => setEditing(true)}>
            {t("Edit information")}
          </button>
        )}
        {assignedRoute && service.canAssign(d, r) && (
          <Link
            className={
              !r.assignee &&
              ["Pending Assignment", "Pending", "Open", "Reported"].includes(
                r.status,
              ) &&
              !nextActions.length
                ? "primary"
                : "secondary"
            }
            to={pathFor(assignedRoute) + "?record=" + r.id}
          >
            {t("Assign / reassign")}
          </Link>
        )}
      </PageHeader>
      <section className="detail-heading">
        <div>
          <span className="eyebrow">{r.code}</span>
          <h2>{t(r.name)}</h2>
          <span>
            {r.branch} {t("·")} {t(r.department)}
          </span>
        </div>
        <StatusBadge status={r.status} />
      </section>
      {[34, 37].includes(id) && r.criteria && (
        <section className="panel inspection-checklist">
          <div className="section-header">
            <h3>{t("Inspection checklist")}</h3>
            <span>
              {r.results?.length || 0} /{" "}
              {r.criteria.split("\n").filter(Boolean).length}{" "}
              {t("Recorded results")}
            </span>
          </div>
          <ol>
            {r.criteria
              .split("\n")
              .filter(Boolean)
              .map((criterion: string, index: number) => (
                <li key={index}>
                  <span>{t(criterion)}</span>
                  <StatusBadge
                    status={
                      r.results?.[index]?.result === "Pass"
                        ? "Passed"
                        : r.results?.[index]?.result === "Fail"
                          ? "Failed"
                          : "Not recorded"
                    }
                  />
                </li>
              ))}
          </ol>
        </section>
      )}
      {(r.room || r.assignee || r.due) && (
        <dl className="task-facts">
          {r.room && (
            <div>
              <dt>{t("Room")}</dt>
              <dd>{name("rooms", r.room)}</dd>
            </div>
          )}
          {r.assignee && (
            <div>
              <dt>{t("Assignee")}</dt>
              <dd>{name("users", r.assignee)}</dd>
            </div>
          )}
          {r.due && (
            <div>
              <dt>{t("Deadline")}</dt>
              <dd
                className={
                  new Date(r.due) < new Date() &&
                  !["Completed", "Closed", "Cancelled"].includes(r.status)
                    ? "overdue-text"
                    : ""
                }
              >
                {date(r.due)}
              </dd>
            </div>
          )}
        </dl>
      )}
      {overview}
      <div className="tabs" role="tablist" aria-label={t("Record sections")}>
        {[
          "Overview",
          "Activity",
          ...(r.evidence?.length ? ["Evidence"] : []),
        ].map((tabName) => (
          <button
            key={tabName}
            role="tab"
            onKeyDown={(event) => {
              if (
                !["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
              )
                return;
              event.preventDefault();
              const tabs = Array.from(
                event.currentTarget.parentElement!.querySelectorAll<HTMLButtonElement>(
                  "[role=tab]",
                ),
              );
              const index = tabs.indexOf(event.currentTarget);
              const next =
                event.key === "Home"
                  ? 0
                  : event.key === "End"
                    ? tabs.length - 1
                    : (index +
                        (event.key === "ArrowRight" ? 1 : -1) +
                        tabs.length) %
                      tabs.length;
              tabs[next].focus();
              tabs[next].click();
            }}
            aria-selected={tab === tabName}
            className={tab === tabName ? "selected" : ""}
            onClick={() => setTab(tabName)}
          >
            {t(tabName)}
          </button>
        ))}
      </div>
      {tab === "Activity" ? (
        <section className="panel">
          <Timeline record={r} keyName={d.key} />
        </section>
      ) : tab === "Evidence" ? (
        <section className="evidence-gallery">
          {r.evidence.map((e: any, i: number) => (
            <a key={i} href={e.data} download={e.name}>
              {e.type.startsWith("image/") ? (
                <img src={e.data} alt={e.name} />
              ) : (
                <FileText size={40} />
              )}
              <span>{t(e.name)}</span>
            </a>
          ))}
        </section>
      ) : (
        <div className="detail-grid">
          <section className="panel">
            <h3>{t("Record information")}</h3>
            <dl className="metadata">
              {[
                ["Branch", r.branch],
                ["Department", r.department],
                ["Assigned to", name("users", r.assignee)],
                ["Priority", r.priority || "—"],
                ["Deadline", date(r.due)],
                ["Created", date(r.createdAt)],
              ]
                .filter(
                  ([, v]) => v && v !== "—" && v !== t("Awaiting assignment"),
                )
                .map(([k, v]) => (
                  <div key={k}>
                    <dt>{t(k)}</dt>
                    <dd>{t(v)}</dd>
                  </div>
                ))}
            </dl>
            {r.description && (
              <>
                <h3>{t("Description")}</h3>
                <p className="preserve">{t(r.description)}</p>
              </>
            )}
            {d.fields
              .filter(
                (f) => !informationFields || informationFields.includes(f.key),
              )
              .filter(
                (f) =>
                  ![
                    "branch",
                    "department",
                    "name",
                    "description",
                    "priority",
                    "due",
                    "lines",
                    "criteria",
                    "roles",
                    "evidence",
                  ].includes(f.key) && r[f.key],
              )
              .map((f) => (
                <div className="detail-value" key={f.key}>
                  <span>{t(f.label)}</span>
                  <strong>
                    {[
                      "room",
                      "location",
                      "roomType",
                      "warehouse",
                      "asset",
                      "checklist",
                      "user",
                      "inspection",
                      "cleaning",
                      "replenishment",
                      "transaction",
                      "technician",
                      "housekeeper",
                      "inspector",
                    ].includes(f.type || "")
                      ? name(
                          (
                            {
                              room: "rooms",
                              location: "locations",
                              roomType: "roomTypes",
                              warehouse: "warehouses",
                              asset: "assets",
                              checklist: "checklists",
                              user: "users",
                              inspection: "inspections",
                              cleaning: "cleaning",
                              replenishment: "replenishments",
                              transaction: "transactions",
                              technician: "users",
                              housekeeper: "users",
                              inspector: "users",
                            } as Record<string, string>
                          )[f.type!],
                          r[f.key],
                        )
                      : String(r[f.key])}
                  </strong>
                </div>
              ))}
            {r.criteria && ![34, 37].includes(id) && (
              <>
                <h3>{t("Mandatory criteria")}</h3>
                {r.criteria
                  .split("\n")
                  .filter(Boolean)
                  .map((c: string) => (
                    <label className="criterion-check" key={c}>
                      <input
                        type="checkbox"
                        disabled={
                          !["cleaning", "tasks"].includes(d.key) ||
                          r.status !== "In Progress" ||
                          r.assignee !== service.currentUser()?.id ||
                          !r.allowedActions?.includes(d.key + ".criteria") ||
                          service.isPending(d.key + ":" + r.id)
                        }
                        checked={(r.completedItems || []).includes(c)}
                        onChange={async (e) => {
                          try {
                            await service.updateCriteria(
                              d,
                              r,
                              e.target.checked
                                ? [...(r.completedItems || []), c]
                                : (r.completedItems || []).filter(
                                    (v: string) => v !== c,
                                  ),
                            );
                          } catch (err) {
                            notify((err as Error).message);
                          }
                        }}
                      />
                      {c}
                    </label>
                  ))}
              </>
            )}
            {r.results && (
              <>
                <h3>{t("Inspection results")}</h3>
                {r.results.map((result: any, i: number) => (
                  <div className="detail-value" key={i}>
                    <span>
                      {result.text}
                      <small>{result.defect}</small>
                    </span>
                    <StatusBadge status={result.result} />
                  </div>
                ))}
              </>
            )}
            {r.defects && (
              <>
                <h3>{t("Defects to fix")}</h3>
                <p className="preserve">{r.defects}</p>
              </>
            )}
            {r.remediation && (
              <>
                <h3>{t("Remediation")}</h3>
                <p className="preserve">{r.remediation}</p>
              </>
            )}
            {r.lines && d.key !== "stocktakes" && (
              <>
                <h3>{t("Material quantities")}</h3>
                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th>{t("Item")}</th>
                        <th>{t("Requested / expected")}</th>
                        <th>{t("Approved")}</th>
                        <th>{t("Issued / received")}</th>
                        <th>{t("Available")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {r.lines.map((l: any, i: number) => (
                        <tr key={i}>
                          <td>{name("items", l.item)}</td>
                          <td>{l.quantity}</td>
                          <td>{l.approved ?? 0}</td>
                          <td>{l.issued ?? l.received ?? 0}</td>
                          <td>
                            {quantity(service.balance(db, r.warehouse, l.item))}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
            {r.snapshot && (
              <>
                <h3>{t("Handover snapshot")}</h3>
                {r.snapshot.map((s: any) => (
                  <div className="detail-value" key={s.id}>
                    <span>{t(s.name)}</span>
                    <StatusBadge status={s.status} />
                  </div>
                ))}
              </>
            )}
            {r.handover && (
              <>
                <h3>{t("Custody / handler handover")}</h3>
                <p>
                  {name("users", r.handover.receiver)} {t("·")}
                  {t(r.handover.status)}
                </p>
              </>
            )}
            {d.key === "roles" && (
              <>
                <h3>{t("Granted permissions")}</h3>
                <div className="permission-list">
                  {(r.permissions || []).map((uc: string) => (
                    <div key={uc}>
                      {uc} {t("·")}
                      {t(useCases[uc]?.name)}
                    </div>
                  ))}
                </div>
              </>
            )}
            {d.key === "users" && (
              <>
                <h3>{t("Effective access")}</h3>
                <p>{r.roles.join(" · ")}</p>
                <div className="permission-list">
                  {Array.from(
                    new Set(
                      db.roles
                        .filter(
                          (role) =>
                            r.roles.includes(role.name) &&
                            role.status === "Active",
                        )
                        .flatMap((role) => role.permissions || []),
                    ),
                  ).map((uc) => (
                    <div key={uc}>
                      {uc} {t("·")}
                      {t(useCases[uc]?.name)}
                    </div>
                  ))}
                </div>
              </>
            )}
            {d.key === "branches" && (
              <>
                <h3>{t("Hotel structure")}</h3>
                {db.locations
                  .filter((l) => l.branch === r.name)
                  .map((l) => (
                    <details key={l.id}>
                      <summary>{t(l.name)}</summary>
                      {db.rooms
                        .filter((room) => room.floor === l.id)
                        .map((room) => (
                          <Link
                            className="tree-link"
                            key={room.id}
                            to={pathFor(20, room.id)}
                          >
                            {t("Room")}
                            {t(room.name)} <StatusBadge status={room.status} />
                          </Link>
                        ))}
                    </details>
                  ))}
              </>
            )}
            {d.key === "rooms" && (
              <>
                <h3>{t("Operational status history")}</h3>
                {(r.history || []).map((h: any, i: number) => (
                  <p key={i}>
                    {h.from} {t("→")}
                    {h.to} {t("·")}
                    {date(h.time)}
                  </p>
                ))}
                <Link to={pathFor(30) + "?room=" + r.id}>
                  {t("View turnover history")}
                </Link>
              </>
            )}
            {d.key === "notifications" && r.relatedId && (
              <Link
                className="primary"
                to={pathFor(
                  domains.find((d) => d.key === r.relatedKey)!.detail,
                  r.relatedId,
                )}
              >
                {t("Open related record")}
              </Link>
            )}
          </section>
          <aside className="panel actions-panel">
            {related}
            <span className="eyebrow">{t("WORKFLOW")}</span>
            <h3>{t("Available actions")}</h3>
            {service
              .availableActions(d, r)
              .filter((a) => !workflowUCs || workflowUCs.includes(a.uc))
              .map((a) => (
                <button
                  key={t(a.label)}
                  className="action-button"
                  onClick={() => setAction(a)}
                >
                  {t(a.label)}
                  <ChevronRight size={16} />
                </button>
              ))}
            {d.key === "inspections" &&
              r.status === "Failed" &&
              service.can("UC-046") && (
                <Link className="action-button" to={pathFor(36, r.id)}>
                  {t("Request rework")}
                  <ChevronRight size={16} />
                </Link>
              )}
            {d.key === "inspections" &&
              id === 34 &&
              ["Pending Conclusion", "Passed", "Failed"].includes(r.status) && (
                <Link className="action-button" to={pathFor(35, r.id)}>
                  {t("View inspection result")}
                  <ChevronRight size={16} />
                </Link>
              )}
            {d.key === "inspections" &&
              !r.rework &&
              ["Assigned", "Inspecting"].includes(r.status) &&
              id !== 34 && (
                <Link className="action-button" to={pathFor(34, r.id)}>
                  {t("Perform inspection")}
                  <ChevronRight size={16} />
                </Link>
              )}
            {d.key === "assets" && (
              <Link className="action-button" to={pathFor(50, r.id)}>
                {t("Maintenance history")}
                <ChevronRight size={16} />
              </Link>
            )}
            {d.key === "requests" && (
              <>
                <h3>{t("Linked work")}</h3>
                {[
                  { key: "cleaning", id: 25, uc: "UC-028" },
                  { key: "incidents", id: 40, uc: "UC-049" },
                  { key: "materials", id: 62, uc: "UC-091" },
                  { key: "tasks", id: 66, uc: "UC-102" },
                ]
                  .filter((l) => service.can(l.uc))
                  .map((l) => (
                    <Link
                      className="action-button"
                      key={l.key}
                      to={pathFor(l.id) + "?source=" + r.id}
                    >
                      {t("Create " + l.key.replace(/s$/, ""))}
                      <ChevronRight size={16} />
                    </Link>
                  ))}
              </>
            )}
            <p className="quiet">
              {t(
                "Actions depend on your permissions and the current record status.",
              )}
            </p>
            <h3>{t("Recent activity")}</h3>
            <Timeline record={r} keyName={d.key} />
          </aside>
        </div>
      )}
      {editing && (
        <Modal title={"Edit " + r.name} onClose={() => setEditing(false)}>
          <EntityForm d={d} record={r} onDone={() => setEditing(false)} />
        </Modal>
      )}
      {action && (
        <Modal
          title={action.label + " · " + r.name}
          onClose={() => setAction(undefined)}
        >
          <p className="dialog-intro">
            {t("Confirm this action for")}
            {t(r.name)}
            {t(".")}
          </p>
          <Form
            fields={[
              ...(action.fields || []),
              ...(action.reason ||
              ["Post receipt", "Post return", "Confirm receipt"].includes(
                action.label,
              )
                ? [
                    {
                      key: "reason",
                      label: action.reason
                        ? "Reason (at least 10 characters)"
                        : "Quantity discrepancy reason",
                      type: "textarea",
                      required: !!action.reason,
                    },
                  ]
                : []),
            ]}
            initial={{
              ...selectActionValues(action, r),
              lines: r.lines?.map((l: any) => ({
                ...l,
                quantity:
                  action.label === "Approve"
                    ? l.quantity
                    : action.label === "Partial issue"
                      ? Math.max(0, l.approved - (l.issued || 0))
                      : l.quantity,
              })),
              note: "",
              reason: "",
            }}
            record={r}
            label={action.label}
            onCancel={() => setAction(undefined)}
            onSubmit={async (v) => {
              const result = await service.perform(d, r, action, v);
              if (action.label === "Reset password")
                showTemporaryPassword(
                  result.temporaryPassword,
                  result.validForHours,
                );
              else notify("Action completed.");
              setAction(undefined);
            }}
          />
        </Modal>
      )}
    </>
  );
}

export function DomainFilterPanel({
  children,
  activeCount = 0,
}: {
  children: React.ReactNode;
  activeCount?: number;
}) {
  return (
    <details className="domain-filter-panel filter-popover">
      <summary>
        {t("More filters")}{" "}
        {activeCount > 0 && <span className="filter-count">{activeCount}</span>}
      </summary>
      <div className="domain-filters filter-popover-content">{children}</div>
    </details>
  );
}

export function createLabel(key: string): string {
  const labels: Record<string, string> = {
    users: "user",
    roles: "role",
    branches: "branch",
    locations: "floor / area",
    roomTypes: "room type",
    rooms: "room",
    departments: "department",
    cleaning: "cleaning task",
    reworks: "rework task",
    checklists: "checklist",
    inspections: "inspection",
    incidents: "incident",
    schedules: "maintenance schedule",
    assets: "asset",
    warehouses: "warehouse",
    items: "item",
    units: "unit",
    transactions: "stock transaction",
    stocktakes: "stocktake",
    materials: "material request",
    replenishments: "replenishment request",
    tasks: "task",
    shifts: "shift",
    handovers: "handover",
    requests: "internal request",
    reference: "reference data",
  };
  return "Create " + (labels[key] || key.replace(/s$/, ""));
}

function shortDescription(id: number): string {
  const descriptions: Record<number, string> = {
    8: "Manage users, roles and account status.",
    19: "Manage room availability and readiness.",
    24: "Track cleaning jobs and assignments.",
    28: "Resolve failed cleaning checks.",
    33: "Assign and perform room inspections.",
    39: "Track incidents and maintenance priorities.",
    43: "Manage maintenance work and deadlines.",
    45: "Plan recurring maintenance.",
    48: "Track hotel assets and condition.",
    54: "Manage materials and minimum stock.",
    57: "Review stock receipts, issues and transfers.",
    59: "Review stock counts and discrepancies.",
    62: "Approve and fulfil material requests.",
    64: "Track replenishment requests.",
    66: "Manage work, assignments and deadlines.",
    70: "Resolve overdue operational work.",
    71: "Plan shifts and staff coverage.",
    74: "Review shift handovers.",
    76: "Track requests across departments.",
    79: "Review operational changes and events.",
  };
  return descriptions[id] || "";
}
