import { useNavigate, useParams } from "react-router-dom";
import { pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DataTable, PageHeader, date, useDB } from "../../ui";
export default function AuditLogDetailScreen() {
  useLanguage();
  const detail: boolean = true;
  const db = useDB();
  const params = useParams();
  const nav = useNavigate();
  const rows = service.visible("audit");
  const r = rows.find((r) => r.id === params.id);
  return (
    <>
      <PageHeader id={detail ? 80 : 79} />
      {detail ? (
        r ? (
          <section className="panel">
            <h2>{t(r.name)}</h2>
            <p>
              {r.actor} {t("·")}
              {date(r.createdAt)} {t("·")}
              {r.entity} {t("·")}
              {r.recordId}
            </p>
            <div className="before-after">
              <div>
                <h3>{t("Before")}</h3>
                <pre>{JSON.stringify(r.before, null, 2)}</pre>
              </div>
              <div>
                <h3>{t("After")}</h3>
                <pre>{JSON.stringify(r.after, null, 2)}</pre>
              </div>
            </div>
          </section>
        ) : (
          <p>{t("Audit record not found or outside scope.")}</p>
        )
      ) : (
        <DataTable
          rows={rows}
          columns={[
            {
              key: "createdAt",
              label: "Time",
              render: (r) => date(r.createdAt),
            },
            { key: "actor", label: "Actor" },
            { key: "action", label: "Action" },
            { key: "entity", label: "Entity" },
            { key: "name", label: "Summary" },
          ]}
          onOpen={(r) => nav(pathFor(80, r.id))}
        />
      )}
    </>
  );
}
