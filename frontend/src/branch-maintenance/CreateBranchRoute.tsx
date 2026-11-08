import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createBranchApi } from "./createBranchApi";
import {
  createRequest,
  type CreateBranchRequest,
  type CreatedBranch,
} from "./createBranchContracts";
const blank: CreateBranchRequest = {
  code: "",
  name: "",
  address: "",
  phone: null,
  email: null,
};
const unknownMessage =
  "Create outcome unknown. The branch may already exist. Do not replay this request. Ask an authorized administrator to verify before starting another flow.";
function CreateForm({
  session,
  locked,
}: {
  session: object;
  locked: RefObject<boolean>;
}) {
  const { state, actions } = useAuth();
  const key = useMemo(() => ({}), [session]);
  const latest = useRef(key);
  latest.current = key;
  const mounted = useRef(false),
    busy = useRef(false),
    writer = useRef<AbortController | null>(null);
  const reviewed = useRef<{ key: object; input: CreateBranchRequest } | null>(
    null,
  );
  const [api] = useState(() => createBranchApi());
  const [fields, setFields] = useState<CreateBranchRequest>({ ...blank });
  const [review, setReview] = useState<{
    key: object;
    input: CreateBranchRequest;
  } | null>(null);
  const [result, setResult] = useState<{
    key: object;
    data: CreatedBranch;
  } | null>(null);
  const [phase, setPhase] = useState("idle"),
    [message, setMessage] = useState("");
  const [denied, setDenied] = useState(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    reviewed.current = null;
    setReview(null);
    setResult(null);
    setFields({ ...blank });
    setPhase(locked.current ? "unknown" : "idle");
    setMessage(locked.current ? unknownMessage : "");
    return () => {
      writer.current?.abort();
      if (busy.current) {
        locked.current = true;
        busy.current = false;
      }
    };
  }, [key]);
  const current = () =>
    mounted.current &&
    latest.current === key &&
    state.status === "AUTHENTICATED" &&
    state.pending === null;
  async function confirm() {
    const selected = reviewed.current;
    if (
      !selected ||
      selected.key !== key ||
      busy.current ||
      locked.current ||
      denied
    )
      return;
    reviewed.current = null;
    setReview(null);
    busy.current = true;
    setPhase("saving");
    setMessage("");
    const controller = new AbortController();
    writer.current = controller;
    const outcome = await api(selected.input, current, controller.signal);
    if (!current()) return;
    busy.current = false;
    writer.current = null;
    if (outcome.outcome === "CONFIRMED") {
      setResult({ key, data: outcome.data });
      setPhase("created");
      setFields({ ...blank });
    } else if (outcome.outcome === "OUTCOME_UNKNOWN") {
      locked.current = true;
      setPhase("unknown");
      setMessage(unknownMessage);
    } else {
      setPhase("rejected");
      setMessage(
        outcome.error.status === 409
          ? "Branch code already exists. Review a different code."
          : outcome.error.status === 401
            ? "Session invalid. Check session before continuing."
            : outcome.error.status === 403
              ? "Server denied Create Branch."
              : outcome.error.status === 400
                ? "Server rejected the branch fields. Correct them and review again."
                : outcome.error.status === 404
                  ? "Create Branch endpoint unavailable."
                  : "Create was not dispatched or confirmed. Check session and review again.",
      );
      if (outcome.error.status === 401 || outcome.error.status === 403) {
        setDenied(true);
        setFields({ ...blank });
      }
    }
  }
  const created = result?.key === key ? result.data.branch : null;
  return (
    <main>
      <h1>Create Branch</h1>
      <p>
        Signing in does not grant MASTER_DATA_MANAGE. Server authorization
        applies.
      </p>
      <nav>
        <Link to="/hotel">Hotel structure</Link>
      </nav>
      {created ? (
        <section aria-label="Created branch">
          <h2>Branch created</h2>
          <p>Branch ID: {created.id}</p>
          <dl>
            {(
              [
                "code",
                "name",
                "address",
                "phone",
                "email",
                "timezone",
                "isActive",
              ] as const
            ).map((field) => (
              <div key={field}>
                <dt>{field}</dt>
                <dd>{String(created[field] ?? "")}</dd>
              </div>
            ))}
          </dl>
          <Link to={`/branches/${created.id}/edit`}>
            View branch information
          </Link>
        </section>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (busy.current || locked.current || denied) return;
            const input = createRequest(fields);
            if (!input) {
              setMessage(
                "Code/name/address are required. Check code, contact formats and field limits.",
              );
              return;
            }
            const next = { key, input };
            reviewed.current = next;
            setReview(next);
            setMessage("");
          }}
        >
          {(["code", "name", "address", "phone", "email"] as const).map(
            (field) => (
              <label key={field}>
                {field}
                <input
                  aria-label={field}
                  value={fields[field] ?? ""}
                  maxLength={
                    field === "code"
                      ? 30
                      : field === "name"
                        ? 200
                        : field === "address"
                          ? 4000
                          : field === "phone"
                            ? 20
                            : 150
                  }
                  disabled={phase === "saving" || locked.current || denied}
                  onChange={(event) => {
                    reviewed.current = null;
                    setReview(null);
                    setFields((v) => ({
                      ...v,
                      [field]:
                        event.target.value === "" &&
                        (field === "phone" || field === "email")
                          ? null
                          : event.target.value,
                    }));
                  }}
                />
              </label>
            ),
          )}
          <button
            type="submit"
            disabled={phase === "saving" || locked.current || denied}
          >
            Review create
          </button>
        </form>
      )}
      {review?.key === key && (
        <section aria-label="Review create branch">
          <p>
            {review.input.code}; {review.input.name}; {review.input.address};{" "}
            {review.input.phone ?? "(no phone)"};{" "}
            {review.input.email ?? "(no email)"}
          </p>
          <button
            type="button"
            disabled={busy.current || locked.current}
            onClick={() => void confirm()}
          >
            Confirm create
          </button>
        </section>
      )}
      {phase === "saving" && <p role="status">Creating branch...</p>}
      {message && <p role="alert">{message}</p>}
      <button
        disabled={phase === "saving"}
        onClick={() => void actions.revalidate()}
      >
        Check session
      </button>
    </main>
  );
}
export function CreateBranchRoute() {
  const { state } = useAuth();
  const locked = useRef(false);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return (
      <section>
        <h1>Create Branch</h1>
        <p role="alert">A confirmed unrestricted session is required.</p>
        <Link to="/login">Sign in</Link>
        <Link to="/change-password">Change password</Link>
      </section>
    );
  return <CreateForm session={state.session} locked={locked} />;
}
