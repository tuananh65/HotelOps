import { referenceLabel } from "../../i18n/referenceLabel";
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
  if (!owner) return <p role="status">Đang kiểm tra phiên đăng nhập...</p>;
  return (
    <main>
      <h1>Lần kiểm tra ban đầu</h1>
      <Link to={`/reinspections/${branch}/${id}/execution`}>
        Quay lại kiểm tra lại
      </Link>
      {error && (
        <p role="alert">
          Kiểm tra ban đầu chưa khả dụng hoặc bạn không còn quyền.
        </p>
      )}
      {!data && !error && <p role="status">Đang tải lần kiểm tra ban đầu...</p>}
      {data && (
        <>
          <p>
            Kiểm tra {referenceLabel(data, "id")} — Không đạt. Người kiểm tra {referenceLabel(data, "inspectorId")}.
            Checklist version {referenceLabel(data, "versionId")}.
          </p>
          <p>{data.summary}</p>
          <section aria-label="Kết quả kiểm tra ban đầu">
            <h2>Kết quả kiểm tra</h2>
            {data.criteria.map((c) => (
              <section key={c.itemId}>
                <h3>{c.criterion}</h3>
                <p>{c.description}</p>
                <p>Kết quả: {c.outcome ?? "Chưa ghi nhận"}</p>
                <p>{c.note}</p>
              </section>
            ))}
          </section>
          <section aria-label="Các lỗi ban đầu">
            <h2>Các lỗi</h2>
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
                      Lỗi {referenceLabel(d, "id")} ảnh #{file}
                    </a>
                  </p>
                ))}
              </section>
            ))}
          </section>
          <section aria-label="Bằng chứng kiểm tra ban đầu">
            <h2>Bằng chứng kiểm tra</h2>
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
