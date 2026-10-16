import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["checklistVersion", "criteria"];
const workflowUCs = ["UC-039", "UC-040"];
export default function InspectionChecklistDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("checklists").find((r) => r.id === id);
  return (
    <DetailPage
      id={32}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Checklist version",
                value: record.checklistVersion || 1,
              },
              {
                label: "Criteria",
                value: record.criteria?.split("\n").filter(Boolean).length || 0,
              },
              {
                label: "Linked room types",
                value: db.roomTypes.filter((r) => r.checklist === record.id)
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
          entity="checklists"
          recordId={id}
          title={t("Inspection Checklist Detail · related activity")}
        />
      }
    />
  );
}
