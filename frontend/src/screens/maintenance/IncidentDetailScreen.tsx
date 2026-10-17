import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "room",
  "cleaningType",
  "assignee",
  "priority",
  "severity",
  "asset",
  "due",
  "detectedAt",
];
const workflowUCs = [
  "UC-050",
  "UC-051",
  "UC-052",
  "UC-056",
  "UC-057",
  "UC-059",
  "UC-060",
];
export default function IncidentDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("incidents").find((r) => r.id === id);
  return (
    <DetailPage
      id={41}
      overview={
        record && (
          <Summary
            items={[
              { label: "Severity", value: record.severity },
              { label: "Triage priority", value: record.priority },
              { label: "Resolution stage", value: record.status },
              {
                label: "Room blocks",
                value: db.blocks.filter(
                  (b) => b.source === record.id && b.active,
                ).length,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="incidents"
          recordId={id}
          title={t("Incident Detail · related activity")}
        />
      }
    />
  );
}
