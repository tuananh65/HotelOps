import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "department",
  "receivingDepartment",
  "priority",
  "due",
];
const workflowUCs = [
  "UC-115",
  "UC-116",
  "UC-117",
  "UC-118",
  "UC-119",
  "UC-120",
  "UC-121",
  "UC-122",
  "UC-123",
];
export default function InternalRequestDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("requests").find((r) => r.id === id);
  return (
    <DetailPage
      id={77}
      overview={
        record && (
          <Summary
            items={[
              { label: "Requesting department", value: record.department },
              {
                label: "Receiving department",
                value: record.receivingDepartment,
              },
              { label: "Linked work", value: record.links?.length || 0 },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="requests"
          recordId={id}
          title={t("Internal Request Detail · related activity")}
        />
      }
    />
  );
}
