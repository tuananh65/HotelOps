import { InspectionDirectory } from "./InspectionDirectory";
import { reads, type Severity } from "./reads";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, EvidenceList, Results, useOperation } from "./shared";
import {
  finishIssues,
  InspectionError,
  parseId,
  uploadFiles,
  type Execution,
  type ResultInput,
  type Upload,
} from "./api";

export function InspectionEntry() {
  const [branch, setBranch] = useState(""),
    [id, setId] = useState(""),
    [error, setError] = useState("");
  const navigate = useNavigate();
  return (
    <main>
      <h1>Perform Room Inspection</h1>
      <InspectionDirectory />
      <p>
        Open the inspection assigned to you using its branch and inspection
        identity. The server verifies your assignment.
      </p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          try {
            navigate(
              `/inspections/${parseId(branch)}/${parseId(id)}/execution`,
            );
          } catch {
            setError("Enter positive branch and inspection IDs.");
          }
        }}
      >
        <label>
          Branch ID
          <input
            required
            inputMode="numeric"
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
          />
        </label>
        <label>
          Inspection ID
          <input
            required
            inputMode="numeric"
            value={id}
            onChange={(e) => setId(e.target.value)}
          />
        </label>
        <button>Open assigned inspection</button>
      </form>
      {error && <p role="alert">{error}</p>}
    </main>
  );
}
export function ExecutionPage() {
  const p = useParams();
  return (
    <ExecutionEditor
      key={`${p.branch}/${p.id}`}
      branchText={p.branch ?? ""}
      idText={p.id ?? ""}
    />
  );
}
function ExecutionEditor({
  branchText,
  idText,
}: {
  branchText: string;
  idText: string;
}) {
  const { run, busy, error, setError } = useOperation();
  const [source, setSource] = useState<{ data: Execution; tag: string } | null>(
    null,
  );
  const [severityError, setSeverityError] = useState("");
  const [severities, setSeverities] = useState<Severity[]>([]);
  const [draft, setDraft] = useState<ResultInput[]>([]);
  const [blocked, setBlocked] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [notice, setNotice] = useState("");
  const [reason, setReason] = useState("");
  const [criterion, setCriterion] = useState(""),
    [severity, setSeverity] = useState(""),
    [description, setDescription] = useState(""),
    [location, setLocation] = useState(""),
    [photos, setPhotos] = useState<Upload[]>([]);
  const [fileKey, setFileKey] = useState(0),
    [reading, setReading] = useState(false);
  let branch = 0,
    id = 0;
  try {
    branch = parseId(branchText);
    id = parseId(idText);
  } catch {
    /* Invalid identity never sends requests. */
  }
  const adopt = (v: { data: Execution; tag: string }) => {
    setSource(v);
    setDraft(
      v.data.criteria.map((c) => ({
        itemId: c.itemId,
        outcome: c.outcome ?? "",
        note: c.note,
      })),
    );
    setDirty(false);
    setBlocked(false);
    setPhotos([]);
    setFileKey((k) => k + 1);
  };
  const load = () => {
    setNotice("");
    return run(
      async (s) => {
        const v = await api.execution(branch, id, s);
        try {
          return {
            v,
            values: await reads.severities(branch, s),
            lookupError: "",
          };
        } catch (e) {
          if (
            s.aborted ||
            (e instanceof InspectionError &&
              (e.status === 401 || e.status === 403))
          )
            throw e;
          return {
            v,
            values: [] as Severity[],
            lookupError:
              "Severity lookup unavailable. Reload before recording a defect.",
          };
        }
      },
      ({ v, values, lookupError }) => {
        setSeverityError(lookupError);
        adopt(v);
        setSeverities(values);
        setSeverity("");
      },
      () => {
        setSource(null);
        setBlocked(true);
      },
    );
  };
  useEffect(() => {
    let alive = true;
    queueMicrotask(() => {
      if (alive && branch && id) void load();
    });
    return () => {
      alive = false;
    };
  }, []);
  const write = (
    action: "start" | "results" | "finish" | "defects",
    body: unknown,
  ) => {
    if (!source || blocked || busy || reading) return;
    setNotice("");
    void run(
      (s) => api.write(branch, id, action, body, source.tag, s),
      (v) => {
        adopt(v);
        setNotice(`${action} saved. State: ${v.data.state}`);
      },
      (e) => {
        if (
          !(e instanceof InspectionError) ||
          [0, 401, 403, 404, 409, 412, 502, 503].includes(e.status)
        ) {
          setBlocked(true);
        }
      },
    );
  };
  if (!branch || !id)
    return (
      <main>
        <h1>Invalid inspection identity</h1>
        <Link to="/inspections">Choose inspection</Link>
      </main>
    );
  const data = source?.data;
  const inspecting = data?.state === "INSPECTING";
  const defectPhase = inspecting || data?.state === "PENDING_CONCLUSION";
  const issues = data ? finishIssues(data.criteria, data.defects) : [];
  return (
    <main>
      <h1>Room Inspection #{id}</h1>
      <p>Branch #{branch}</p>
      <button
        disabled={busy || reading}
        onClick={() => {
          if (!dirty || window.confirm("Discard unsaved results and reload?"))
            void load();
        }}
      >
        Reload inspection
      </button>
      {busy && <p role="status">Loading / saving…</p>}
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {blocked && (
        <p role="alert">
          Further writes are blocked. Reload the persisted inspection; no
          operation is automatically retried.
        </p>
      )}
      {data && (
        <>
          <p>
            Room #{data.roomId} · Cleaning task #{data.cleaningTaskId} ·
            Inspector #{data.inspectorId}
          </p>
          <p>
            Assigned checklist version ID: {data.versionId} · State:{" "}
            <strong>{data.state}</strong> · Due: {data.dueAt ?? "Not set"}
          </p>
          <Link to={`/inspections/history/${branch}/${id}`}>
            History and authorized evidence
          </Link>
          <label>
            Operation reason
            <input
              value={reason}
              required
              maxLength={500}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          {data.state === "ASSIGNED" && (
            <button
              disabled={busy || blocked || !reason.trim()}
              onClick={() => write("start", { reason })}
            >
              Start Inspection
            </button>
          )}
          <h2>Assigned checklist</h2>
          {!data.criteria.length && <p>No criteria in assigned version.</p>}
          {data.criteria.map((c, index) => (
            <fieldset
              key={c.itemId}
              disabled={!inspecting || busy || blocked || reading}
            >
              <legend>
                {c.criterion} {c.mandatory ? "(Mandatory)" : ""}
              </legend>
              <p>
                {c.group} {c.description}
              </p>
              <p>Pass condition: {c.passCondition ?? "Not specified"}</p>
              <label>
                Outcome: {c.criterion}
                <select
                  value={draft[index]?.outcome ?? ""}
                  onChange={(e) => {
                    setDraft((rows) =>
                      rows.map((r) =>
                        r.itemId === c.itemId
                          ? { ...r, outcome: e.target.value }
                          : r,
                      ),
                    );
                    setDirty(true);
                  }}
                >
                  <option value="">Not recorded</option>
                  <option
                    value="PASS"
                    disabled={data.defects.some((d) => d.itemId === c.itemId)}
                  >
                    PASS
                  </option>
                  <option value="FAIL">FAIL</option>
                </select>
              </label>
              <label>
                Note: {c.criterion}
                <textarea
                  maxLength={4000}
                  value={draft[index]?.note ?? ""}
                  onChange={(e) => {
                    setDraft((rows) =>
                      rows.map((r) =>
                        r.itemId === c.itemId
                          ? { ...r, note: e.target.value }
                          : r,
                      ),
                    );
                    setDirty(true);
                  }}
                />
              </label>
              <label>
                Criterion evidence: {c.criterion}
                <input
                  key={`${fileKey}-${c.itemId}`}
                  type="file"
                  multiple
                  accept="image/png,image/jpeg,application/pdf"
                  onChange={async (e) => {
                    const files = e.target.files;
                    setReading(true);
                    try {
                      const uploads = await uploadFiles(files);
                      setDraft((rows) =>
                        rows.map((r) =>
                          r.itemId === c.itemId ? { ...r, photos: uploads } : r,
                        ),
                      );
                      setDirty(true);
                    } catch (err) {
                      setError(
                        err instanceof Error ? err.message : "Invalid evidence",
                      );
                    } finally {
                      setReading(false);
                    }
                  }}
                />
              </label>
            </fieldset>
          ))}
          {inspecting && (
            <>
              <button
                disabled={busy || blocked || reading || !reason.trim()}
                onClick={() => {
                  const results = draft.filter((r) => r.outcome);
                  if (
                    results.reduce((n, r) => n + (r.photos?.length ?? 0), 0) > 5
                  ) {
                    setError("A save supports at most five evidence files.");
                    return;
                  }
                  write("results", { results, reason });
                }}
              >
                Save Draft
              </button>
              <button
                disabled={
                  busy ||
                  blocked ||
                  reading ||
                  dirty ||
                  !!issues.length ||
                  !data.criteria.length ||
                  !reason.trim()
                }
                onClick={() => write("finish", { reason })}
              >
                Finish Inspection
              </button>
              {dirty && (
                <p>Save Draft before finishing or recording a defect.</p>
              )}
              {issues.map((x) => (
                <p key={x}>{x}</p>
              ))}
            </>
          )}
          {data.state === "PENDING_CONCLUSION" && (
            <p>
              Pending Conclusion. No approval/rejection action is provided here.
            </p>
          )}
          {defectPhase && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                try {
                  write("defects", {
                    itemId: parseId(criterion),
                    severityId: parseId(severity),
                    description,
                    location,
                    photos,
                  });
                } catch {
                  setError(
                    "Select a saved FAIL criterion and a valid severity reference ID.",
                  );
                }
              }}
            >
              <fieldset disabled={busy || blocked || dirty || reading}>
                <legend>Record Inspection Defect</legend>
                <label>
                  Failed criterion
                  <select
                    required
                    value={criterion}
                    onChange={(e) => setCriterion(e.target.value)}
                  >
                    <option value="">Choose saved FAIL</option>
                    {data.criteria
                      .filter((c) => c.outcome === "FAIL")
                      .map((c) => (
                        <option key={c.itemId} value={c.itemId}>
                          {c.criterion}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Severity
                  <select
                    aria-label="Severity"
                    required
                    value={severity}
                    onChange={(e) => setSeverity(e.target.value)}
                  >
                    <option value="">Choose severity</option>
                    {severities.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.name} ({v.code})
                      </option>
                    ))}
                  </select>
                </label>
                {severityError && <p role="alert">{severityError}</p>}
                {!severities.length && (
                  <p role="alert">
                    No active Inspection severity is available. Reload or
                    contact your administrator.
                  </p>
                )}
                <label>
                  Description
                  <textarea
                    required
                    maxLength={4000}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                  />
                </label>
                <label>
                  Location in room
                  <input
                    required
                    maxLength={250}
                    value={location}
                    onChange={(e) => setLocation(e.target.value)}
                  />
                </label>
                <label>
                  Defect evidence
                  <input
                    key={`defect-${fileKey}`}
                    type="file"
                    multiple
                    accept="image/png,image/jpeg,application/pdf"
                    onChange={async (e) => {
                      const files = e.target.files;
                      setReading(true);
                      try {
                        setPhotos(await uploadFiles(files));
                      } catch (err) {
                        setError(
                          err instanceof Error
                            ? err.message
                            : "Invalid evidence",
                        );
                      } finally {
                        setReading(false);
                      }
                    }}
                  />
                </label>
                <button
                  disabled={
                    !criterion ||
                    !severities.some((v) => String(v.id) === severity) ||
                    !description.trim() ||
                    !location.trim()
                  }
                >
                  Record defect
                </button>
              </fieldset>
            </form>
          )}
          <Results criteria={data.criteria} defects={data.defects} />
          <EvidenceList items={data.evidence} branch={branch} id={id} />
        </>
      )}
    </main>
  );
}
