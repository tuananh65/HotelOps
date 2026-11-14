import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["kind", "branch"];
const workflowUCs = ["UC-015", "UC-016"];
export default function FloorAreaDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("locations").find((r) => r.id === id);
  return (
    <DetailPage
      id={16}
      overview={
        record && (
          <Summary
            items={[
              { label: "Location type", value: record.kind },
              {
                label: "Rooms in location",
                value: db.rooms.filter(
                  (r) => r.floor === record.id || r.area === record.id,
                ).length,
              },
              { label: "Branch", value: record.branch },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="locations"
          recordId={id}
          title={t("Floor/Area Detail · related activity")}
        />
      }
    />
  );
}
