import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["floor", "area", "roomType"];
const workflowUCs = ["UC-021", "UC-022", "UC-023"];
export default function RoomDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("rooms").find((r) => r.id === id);
  return (
    <DetailPage
      id={20}
      overview={
        record && (
          <Summary
            items={[
              { label: "Readiness", value: record.status },
              {
                label: "Open cleaning cycles",
                value: db.cleaning.filter(
                  (r) =>
                    r.room === record.id &&
                    !["Completed", "Cancelled"].includes(r.status),
                ).length,
              },
              {
                label: "Open incidents",
                value: db.incidents.filter(
                  (r) =>
                    r.room === record.id &&
                    !["Closed", "Cancelled"].includes(r.status),
                ).length,
              },
              {
                label: "Inspections",
                value: db.inspections.filter((r) => r.room === record.id)
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
          entity="rooms"
          recordId={id}
          title={t("Room Detail · related activity")}
        />
      }
    />
  );
}
