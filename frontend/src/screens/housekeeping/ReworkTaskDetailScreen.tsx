import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["room", "assignee", "priority", "due"];
const workflowUCs = ["UC-035", "UC-036"];
export default function ReworkTaskDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("rework").find((r) => r.id === id);
  return (
    <DetailPage
      id={29}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Defects to fix",
                value: record.defects?.split("\n").filter(Boolean).length || 0,
              },
              {
                label: "Remediation",
                value: record.remediation ? "Notes recorded" : "Awaiting notes",
              },
              { label: "Rework stage", value: record.status },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="rework"
          recordId={id}
          title={t("Rework Task Detail · related activity")}
        />
      }
    />
  );
}
