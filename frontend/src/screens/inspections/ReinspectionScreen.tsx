import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, Form, PageHeader, useDB } from "../../ui";
export default function ReinspectionScreen() {
  useLanguage();
  const db = useDB();
  const params = useParams();
  const nav = useNavigate();
  const existing = db.inspections.find((i) => i.id === params.id);
  const [work, setWork] = useState("");
  if (existing?.rework) return <DetailPage id={37} />;
  return (
    <>
      <PageHeader id={37} />
      <section className="panel form-panel">
        <label>
          {t("Select submitted rework")}
          <select value={work} onChange={(e) => setWork(e.target.value)}>
            <option value="">{t("Select rework")}</option>
            {service
              .visible("rework")
              .filter((w) => w.status === "Pending Reinspection")
              .map((w) => (
                <option key={w.id} value={w.id}>
                  {t(w.name)}
                </option>
              ))}
          </select>
        </label>
        {work && (
          <Form
            fields={[
              {
                key: "inspector",
                label: "Inspector",
                type: "inspector",
                required: true,
              },
            ]}
            onSubmit={(v) => {
              const r = service.createReinspection(
                db.rework.find((w) => w.id === work)!,
                v.inspector,
              );
              nav(pathFor(37, r.id));
            }}
          />
        )}
      </section>
    </>
  );
}
