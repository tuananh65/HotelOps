import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { domainFor, pathFor } from "../../domain";
import { useLanguage } from "../../i18n";
import * as service from "../../services";
import { EntityForm, Form, PageHeader, notify } from "../../ui";
export default function CreateCleaningTaskScreen() {
  useLanguage();
  const id: number = 25;
  const d = domainFor(id);
  const nav = useNavigate();
  const [params] = useSearchParams();
  const inspection = service
    .snapshot()
    .inspections.find((i) => i.id === useParams().id);
  return (
    <>
      <PageHeader id={id} />
      <section className="panel form-panel">
        {id === 36 ? (
          <Form
            fields={d.fields}
            initial={{
              name: "Rework · " + inspection?.name,
              branch: inspection?.branch,
              department: "Housekeeping",
              room: inspection?.room,
              inspection: inspection?.id,
              priority: "High",
              criteria: inspection?.criteria,
              description: "Fix defects from the failed inspection.",
              defects: inspection?.results
                ?.filter((v: any) => v.result === "Fail")
                .map((v: any) => v.defect)
                .join("\n"),
            }}
            label="Request rework"
            onSubmit={async (v) => {
              const r = await service.save(d, v);
              nav(pathFor(29, r.id));
              notify("Rework requested.");
            }}
          />
        ) : (
          <EntityForm
            d={d}
            onDone={(r) => {
              nav(pathFor(d.detail, r.id));
            }}
            onCancel={() => nav(pathFor(d.list))}
          />
        )}
      </section>
    </>
  );
}
