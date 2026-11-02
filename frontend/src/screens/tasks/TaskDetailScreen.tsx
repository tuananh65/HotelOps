import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["room", "assignee", "priority", "due"];
const workflowUCs = [
  "UC-103",
  "UC-105",
  "UC-106",
  "UC-107",
  "UC-108",
  "UC-109",
  "UC-110",
  "UC-111",
  "UC-112",
];
export default function TaskDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("tasks").find((r) => r.id === id);
  return (
    <DetailPage
      id={67}
      overview={
        record && (
          <Summary
            items={[
              { label: "Priority", value: record.priority },
              {
                label: "Completed criteria",
                value: record.completedItems?.length || 0,
              },
              {
                label: "Recorded work intervals",
                value: db.intervals.filter((i) => i.recordId === record.id)
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
          entity="tasks"
          recordId={id}
          title={t("Task Detail · related activity")}
        />
      }
    />
  );
}
