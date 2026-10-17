import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["room", "assignee", "priority", "due"];
const workflowUCs = ["UC-042", "UC-043"];
export default function RoomInspectionScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("inspections").find((r) => r.id === id);
  return (
    <DetailPage
      id={34}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Required criteria",
                value: record.criteria?.split("\n").filter(Boolean).length || 0,
              },
              { label: "Recorded results", value: record.results?.length || 0 },
              {
                label: "Failed criteria",
                value:
                  record.results?.filter(
                    (r: { result: string }) => r.result === "Fail",
                  ).length || 0,
              },
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
          title={t("Room Inspection · related activity")}
        />
      }
    />
  );
}
