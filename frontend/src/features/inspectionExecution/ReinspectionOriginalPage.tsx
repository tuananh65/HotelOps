import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readReinspectionOriginal,
  reinspectionOriginalEvidenceUrl,
  type ReinspectionOriginal,
} from "../../api/dev1/reinspectionOriginal";

export function ReinspectionOriginalPage() {
  const { branch = "", id = "" } = useParams();
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    branch: string;
    id: string;
    data: ReinspectionOriginal;
  } | null>(null);
  const [error, setError] = useState(false);
  useEffect(() => {
    setLoaded(null);
    setError(false);
    if (!owner) return;
    const abort = new AbortController();
    void readReinspectionOriginal(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted && current.current === owner)
          setLoaded({ owner, branch, id, data });
      })
      .catch((reason) => {
        if (!abort.signal.aborted && current.current === owner) {
          setError(true);
          if (reason instanceof ApiError && reason.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => abort.abort();
  }, [owner, branch, id, actions]);
  const data =
    owner &&
    loaded?.owner === owner &&
    loaded.branch === branch &&
    loaded.id === id
      ? loaded.data
      : null;
  if (!owner) return <p role="status">Checking session...</p>;
  return (
    <main>
      <h1>Original inspection</h1>
      <Link to={`/reinspections/${branch}/${id}/execution`}>
        Return to reinspection
      </Link>
      {error && (
        <p role="alert">
          The original inspection is unavailable or you no longer have access.
        </p>
      )}
      {!data && !error && <p role="status">Loading original inspection...</p>}
      {data && (
        <>
          <p>
            Inspection #{data.id} — Failed. Inspector #{data.inspectorId}.
            Checklist version #{data.versionId}.
          </p>
          <p>{data.summary}</p>
          <section aria-label="Original checklist results">
            <h2>Checklist results</h2>
            {data.criteria.map((c) => (
              <section key={c.itemId}>
                <h3>{c.criterion}</h3>
                <p>{c.description}</p>
                <p>Result: {c.outcome ?? "Not recorded"}</p>
                <p>{c.note}</p>
              </section>
            ))}
          </section>
          <section aria-label="Original defects">
            <h2>Defects</h2>
            {data.defects.map((d) => (
              <section key={d.id}>
                <h3>{d.description}</h3>
                <p>{d.location}</p>
                {d.evidenceIds.map((file) => (
                  <p key={file}>
                    <a
                      href={reinspectionOriginalEvidenceUrl(branch, id, file)}
                      target="_blank"
                      rel="noreferrer"
                    >
                      Defect #{d.id} photo #{file}
                    </a>
                  </p>
                ))}
              </section>
            ))}
          </section>
          <section aria-label="Original inspection evidence">
            <h2>Inspection evidence</h2>
            {data.evidence.map((e) => (
              <p key={e.id}>
                <a
                  href={reinspectionOriginalEvidenceUrl(branch, id, e.id)}
                  target="_blank"
                  rel="noreferrer"
                >
                  {e.name}
                </a>
              </p>
            ))}
          </section>
        </>
      )}
    </main>
  );
}
