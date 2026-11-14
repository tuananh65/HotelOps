import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createBranchMaintenanceApi } from "./branchMaintenanceApi";
import {
  safeId,
  validUpdate,
  type BranchSnapshot,
  type BranchUpdate,
} from "./contracts";
const unknownMessage =
  "Update outcome unknown. Do not replay. Ask an authorized administrator to verify the branch before opening a new flow.";
const empty: BranchUpdate = {
  name: "",
  address: "",
  phone: null,
  email: null,
  reason: "",
};
type ReadState =
  { key: object; data: BranchSnapshot } | { key: object; error: string };
type Review = { key: object; input: BranchUpdate; etag: string };
/** UC012 normal flow, source-only. Uses the active BranchMaintenanceController contract under accepted AuthProvider.
 * No role inference.
 */
export function BranchMaintenancePanel({ branchId }: { branchId: number }) {
  const { state, actions } = useAuth();
  const [api] = useState(() => createBranchMaintenanceApi());
  const confirmed =
    state.status === "AUTHENTICATED" &&
    state.session !== null &&
    !state.session.mustChangePassword &&
    state.pending === null;
  const key = useMemo(() => ({}), [state.session, branchId, confirmed]);
  const latest = useRef({ key, confirmed });
  latest.current = { key, confirmed };
  const [read, setRead] = useState<ReadState | null>(null),
    [attempt, setAttempt] = useState(0);
  const [fields, setFields] = useState<BranchUpdate>(empty),
    [review, setReview] = useState<Review | null>(null);
  const reviewRef = useRef<Review | null>(null),
    busy = useRef(false),
    locked = useRef(false),
    mounted = useRef(false);
  const writer = useRef<AbortController | null>(null);
  const [phase, setPhase] = useState("idle"),
    [message, setMessage] = useState("");
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    reviewRef.current = null;
    setReview(null);
    setRead(null);
    setFields({ ...empty });
    setPhase(locked.current ? "unknown" : "idle");
    setMessage(locked.current ? unknownMessage : "");
    if (!confirmed || !safeId(branchId)) return;
    const controller = new AbortController();
    let active = true;
    void api.read(branchId, controller.signal).then((result) => {
      if (!active || latest.current.key !== key) return;
      if (result.ok) {
        setRead({ key, data: result.data });
        setFields({
          name: result.data.branch.name,
          address: result.data.branch.address ?? "",
          phone: result.data.branch.phone,
          email: result.data.branch.email,
          reason: "",
        });
      } else
        setRead({
          key,
          error:
            result.error.status === 404
              ? "Branch read endpoint unavailable or branch not found. No data loaded."
              : result.error.status === 401
                ? "Session invalid. Revalidate before continuing."
                : result.error.status === 403
                  ? "Server denied branch management access."
                  : "Branch information unavailable.",
        });
    });
    return () => {
      active = false;
      controller.abort();
      writer.current?.abort();
      if (busy.current) {
        locked.current = true;
        busy.current = false;
      }
    };
  }, [key, confirmed, branchId, api, attempt]);
  const snapshot = read?.key === key && "data" in read ? read.data : null;
  const currentReview = review?.key === key ? review : null;
  async function confirm() {
    const selected = reviewRef.current;
    if (
      !selected ||
      selected.key !== key ||
      !confirmed ||
      busy.current ||
      locked.current
    )
      return;
    reviewRef.current = null;
    setReview(null);
    busy.current = true;
    setPhase("saving");
    setMessage("");
    const controller = new AbortController();
    writer.current = controller;
    const current = () =>
      mounted.current &&
      latest.current.key === selected.key &&
      latest.current.confirmed;
    const result = await api.update(
      branchId,
      selected.etag,
      selected.input,
      current,
      controller.signal,
    );
    if (!current()) return;
    busy.current = false;
    writer.current = null;
    if (result.outcome === "CONFIRMED") {
      setRead({ key, data: result.data });
      setFields({ ...selected.input, reason: "" });
      setPhase("saved");
      setMessage("Branch information updated.");
    } else if (result.outcome === "OUTCOME_UNKNOWN") {
      locked.current = true;
      setPhase("unknown");
      setMessage(unknownMessage);
    } else if (result.error.status === 412 || result.error.status === 428) {
      setRead(null);
      setPhase("stale");
      setMessage(
        "Version changed or unavailable. Read again, review the latest values, then confirm a new update.",
      );
    } else {
      if ([401, 403, 404].includes(result.error.status ?? 0)) {
        setRead({
          key,
          error:
            "Branch data cleared after server rejection. Revalidate session/access before continuing.",
        });
        setFields({ ...empty });
      }
      setPhase("rejected");
      setMessage(
        result.error.status === 401
          ? "Session invalid. Revalidate before continuing."
          : result.error.status === 403
            ? "Server denied this update."
            : result.error.status === 404
              ? "Update endpoint unavailable or branch not found."
              : result.error.status === 409
                ? "Update conflict. Review server state before another attempt."
                : "Update was not confirmed. Correct the input or read again.",
      );
    }
  }
  if (!confirmed)
    return <p role="alert">A confirmed unrestricted session is required.</p>;
  if (!safeId(branchId))
    return <p role="alert">Select a positive safe branch ID.</p>;
  return (
    <section aria-label="Branch information update">
      <h1>Update branch information</h1>
      <p>Branch metadata changes require server authorization.</p>
      <button
        disabled={phase === "saving"}
        onClick={() => {
          reviewRef.current = null;
          setReview(null);
          setAttempt((v) => v + 1);
        }}
      >
        Read branch
      </button>
      <button
        disabled={phase === "saving"}
        onClick={() => void actions.revalidate()}
      >
        Check session
      </button>
      {read?.key === key && "error" in read && <p role="alert">{read.error}</p>}
      {!snapshot &&
        !(read?.key === key && "error" in read) &&
        phase !== "stale" && <p role="status">Loading branch information...</p>}
      {snapshot && (
        <>
          <p>
            Branch {snapshot.branch.id}: {snapshot.branch.code}; timezone{" "}
            {snapshot.branch.timezone ?? "not provided"};{" "}
            {snapshot.branch.isActive ? "Active" : "Inactive"}
          </p>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              if (!snapshot || busy.current || locked.current) return;
              if (!validUpdate(fields)) {
                setMessage(
                  "Name/address are required. Check field limits and provide a reason of at least ten characters.",
                );
                return;
              }
              const next = { key, input: { ...fields }, etag: snapshot.etag };
              reviewRef.current = next;
              setReview(next);
              setMessage("");
            }}
          >
            {(["name", "address", "phone", "email", "reason"] as const).map(
              (field) => (
                <label key={field}>
                  {field}
                  <input
                    aria-label={field}
                    value={fields[field] ?? ""}
                    disabled={phase === "saving" || locked.current}
                    maxLength={
                      field === "address"
                        ? 4000
                        : field === "name"
                          ? 200
                          : field === "phone"
                            ? 20
                            : field === "email"
                              ? 150
                              : 500
                    }
                    onChange={(event) => {
                      reviewRef.current = null;
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
              disabled={phase === "saving" || locked.current}
            >
              Review update
            </button>
          </form>
        </>
      )}
      {currentReview && (
        <section aria-label="Review branch update">
          <p>
            Branch {branchId}: {currentReview.input.name};{" "}
            {currentReview.input.address};{" "}
            {currentReview.input.phone ?? "(clear phone)"};{" "}
            {currentReview.input.email ?? "(clear email)"}; reason{" "}
            {currentReview.input.reason}
          </p>
          <button
            type="button"
            disabled={phase === "saving" || locked.current}
            onClick={() => void confirm()}
          >
            Confirm update
          </button>
        </section>
      )}
      {phase === "saving" && <p role="status">Saving...</p>}
      {message && (
        <p role={phase === "saved" ? "status" : "alert"}>{message}</p>
      )}
    </section>
  );
}
