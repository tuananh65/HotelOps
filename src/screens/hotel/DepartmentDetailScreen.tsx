import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["branch", "description"];
const workflowUCs = ["UC-025", "UC-026"];
export default function DepartmentDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("departments").find((r) => r.id === id);
  return (
    <DetailPage
      id={22}
      overview={
        record && (
          <Summary
            items={[
              { label: "Department", value: record.name },
              {
                label: "Assigned employees",
                value: db.users.filter(
                  (r) =>
                    r.department === record.name && r.branch === record.branch,
                ).length,
              },
              {
                label: "Open tasks",
                value: db.tasks.filter(
                  (r) =>
                    r.department === record.name &&
                    r.branch === record.branch &&
                    r.status !== "Closed",
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
          entity="departments"
          recordId={id}
          title={t("Department Detail · related activity")}
        />
      }
    />
  );
}
