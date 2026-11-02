import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["room", "assignee", "priority", "due"];
const workflowUCs = ["UC-044", "UC-045"];
export default function InspectionResultScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("inspections").find((r) => r.id === id);
  return (
    <DetailPage
      id={35}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Passed criteria",
                value:
                  record.results?.filter(
                    (r: { result: string }) => r.result === "Pass",
                  ).length || 0,
              },
              {
                label: "Failed criteria",
                value:
                  record.results?.filter(
                    (r: { result: string }) => r.result === "Fail",
                  ).length || 0,
              },
              { label: "Room conclusion", value: record.status },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="inspections"
          recordId={id}
          title={t("Inspection Result · related activity")}
        />
      }
    />
  );
}
