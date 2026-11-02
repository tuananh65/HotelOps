import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["minutes", "checklist"];
const workflowUCs = ["UC-018", "UC-019"];
export default function RoomTypeDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("roomTypes").find((r) => r.id === id);
  return (
    <DetailPage
      id={18}
      overview={
        record && (
          <Summary
            items={[
              { label: "Room category", value: record.name },
              {
                label: "Expected cleaning time",
                value: `${record.minutes || 0} min`,
              },
              {
                label: "Rooms of this type",
                value: db.rooms.filter((r) => r.roomType === record.id).length,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="roomTypes"
          recordId={id}
          title={t("Room Type Detail · related activity")}
        />
      }
    />
  );
}
