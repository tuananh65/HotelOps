import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, name, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "room",
  "assignee",
  "asset",
  "incident",
  "schedule",
  "due",
];
const workflowUCs = ["UC-054", "UC-055", "UC-058", "UC-062"];
export default function MaintenanceTaskDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record =
    service.visible("maintenance").find((r) => r.id === id) ||
    service.visible("incidents").find((r) => r.id === id);
  return (
    <DetailPage
      id={44}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Work source",
                value: record.incident
                  ? "Incident repair"
                  : "Preventive maintenance",
              },
              {
                label: "Completed checks",
                value: record.completedItems?.length || 0,
              },
              { label: "Technician", value: name("users", record.assignee) },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="maintenance"
          recordId={id}
          title={t("Maintenance Task Detail · related activity")}
        />
      }
    />
  );
}
