import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { InitialRoleCode } from "./createUserContracts";
import {
  InitialCredentialHandoff,
  type CredentialHandoffHandle,
} from "./InitialCredentialHandoff";
import {
  createUserFailureMessage,
  unknownCreationMessage,
  type useCreateUser,
} from "./useCreateUser";

const roles: readonly InitialRoleCode[] = [
  "HK_STAFF",
  "HK_SUPERVISOR",
  "MAINTENANCE_STAFF",
  "MAINTENANCE_LEAD",
  "STOREKEEPER",
  "RECEPTION",
];
const labels = {
  employeeCode: "Employee code",
  username: "Username",
  fullName: "Full name",
  email: "Email",
  phone: "Phone (optional)",
  branchId: "Branch ID",
  departmentId: "Department ID",
} as const;
const limits = {
  employeeCode: 40,
  username: 100,
  fullName: 200,
  email: 150,
  phone: 20,
};
function numericId(value: string) {
  const trimmed = value.trim();
  return /^\d+$/.test(trimmed) &&
    Number.isSafeInteger(Number(trimmed)) &&
    Number(trimmed) > 0
    ? Number(trimmed)
    : undefined;
}

export function CreateUserPage({
  flow,
}: {
  flow: ReturnType<typeof useCreateUser>;
}) {
  const [draft, setDraft] = useState({
    employeeCode: "",
    username: "",
    fullName: "",
    email: "",
    phone: "",
    branchId: "",
    departmentId: "",
    roleCode: "HK_STAFF" as InitialRoleCode,
  });
  const [validation, setValidation] = useState("");
  const handoff = useRef<CredentialHandoffHandle>(null);
  const { progress } = flow;
  const canSubmit = !["pending", "unknown", "created"].includes(progress.phase);
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!canSubmit) return;
    const branchId = numericId(draft.branchId),
      departmentId = numericId(draft.departmentId);
    if (
      !branchId ||
      !departmentId ||
      !roles.includes(draft.roleCode) ||
      Object.entries(limits).some(([key, max]) => {
        const value = draft[key as keyof typeof limits].trim();
        return value.length > max || (key !== "phone" && !value);
      })
    ) {
      setValidation(
        "Enter all required fields within their limits and positive safe integer branch and department IDs.",
      );
      return;
    }
    setValidation("");
    void flow.submit({ ...draft, branchId, departmentId }, (data) =>
      handoff.current?.receive(data),
    );
  }
  return (
    <main>
      <h1>Create user account</h1>
      <Link to="/profile">Profile</Link>
      <p>
        The server decides whether this account can be created and validates its
        branch, department and initial role.
      </p>
      {progress.phase !== "created" && progress.phase !== "unknown" && (
        <form aria-label="Create user account" onSubmit={submit} noValidate>
          <fieldset disabled={progress.phase === "pending"}>
            <legend>Employee account</legend>
            {(Object.keys(labels) as (keyof typeof labels)[]).map((key) => (
              <label key={key}>
                {labels[key]}
                <input
                  name={key}
                  value={draft[key]}
                  required={key !== "phone"}
                  maxLength={
                    key in limits
                      ? limits[key as keyof typeof limits]
                      : undefined
                  }
                  inputMode={
                    key === "branchId" || key === "departmentId"
                      ? "numeric"
                      : undefined
                  }
                  autoComplete="off"
                  onChange={(event) =>
                    setDraft({ ...draft, [key]: event.target.value })
                  }
                />
              </label>
            ))}
            <label>
              Initial role
              <select
                aria-label="Initial role"
                value={draft.roleCode}
                onChange={(event) =>
                  setDraft({
                    ...draft,
                    roleCode: event.target.value as InitialRoleCode,
                  })
                }
              >
                {roles.map((role) => (
                  <option key={role} value={role}>
                    {role}
                  </option>
                ))}
              </select>
            </label>
            <button type="submit">
              {progress.phase === "pending"
                ? "Creating account..."
                : "Create account"}
            </button>
          </fieldset>
        </form>
      )}
      {validation && <p role="alert">{validation}</p>}
      {(progress.phase === "rejected" ||
        progress.phase === "not-dispatched") && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "No account creation request was sent. "
            : ""}
          {createUserFailureMessage(progress.error)}
        </p>
      )}
      {progress.phase === "unknown" && (
        <p role="alert">{unknownCreationMessage}</p>
      )}
      {progress.phase === "created" && (
        <p>
          Account creation was confirmed. If the credential is no longer
          available on this page, ask an authorized administrator to arrange a
          reset; do not create this account again.
        </p>
      )}
      <InitialCredentialHandoff ref={handoff} />
    </main>
  );
}
