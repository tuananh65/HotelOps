import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["address", "phone"];
const workflowUCs = ["UC-012", "UC-013"];
export default function BranchDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("branches").find((r) => r.id === id);
  return (
    <DetailPage
      id={14}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Rooms",
                value: db.rooms.filter((r) => r.branch === record.name).length,
              },
              {
                label: "Floors / areas",
                value: db.locations.filter((r) => r.branch === record.name)
                  .length,
              },
              {
                label: "Departments",
                value: db.departments.filter((r) => r.branch === record.name)
                  .length,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="branches"
          recordId={id}
          title={t("Branch Detail · related activity")}
        />
      }
    />
  );
}
