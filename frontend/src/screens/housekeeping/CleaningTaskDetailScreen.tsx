import { useParams, useSearchParams } from "react-router-dom";
import CleaningProgressPanel from "./CleaningProgressPanel";
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
const workflowUCs = ["UC-031", "UC-032", "UC-033", "UC-034", "UC-037"];
export default function CleaningTaskDetailScreen() {
  const { id } = useParams();
  const [params] = useSearchParams();
  if (params.has("branchId")) {
    const branch = params.get("branchId") || "";
    return (
      <CleaningProgressPanel
        key={`${branch}:${id}`}
        branch={branch}
        task={id || ""}
      />
    );
  }
  return (
    <>
      <p>
        Dữ liệu minh họa. Tiến độ máy chủ cần mã công việc và chi nhánh hợp lệ.
      </p>
      <PrototypeCleaningTaskDetail />
    </>
  );
}
function PrototypeCleaningTaskDetail() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("cleaning").find((r) => r.id === id);
  return (
    <DetailPage
      id={26}
      overview={
        record && (
          <Summary
            items={[
              { label: "Cleaning type", value: record.cleaningType },
              {
                label: "Completed checks",
                value: record.completedItems?.length || 0,
              },
              {
                label: "Required checks",
                value: record.criteria?.split("\n").filter(Boolean).length || 0,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="cleaning"
          recordId={id}
          title={t("Cleaning Task Detail · related activity")}
        />
      }
    />
  );
}
