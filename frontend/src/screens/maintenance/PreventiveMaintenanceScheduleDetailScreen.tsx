import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { date, DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "asset",
  "interval",
  "intervalUnit",
  "nextDue",
  "assignee",
];
const workflowUCs = ["UC-061", "UC-062"];
export default function PreventiveMaintenanceScheduleDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("schedules").find((r) => r.id === id);
  return (
    <DetailPage
      id={46}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Repeat every",
                value: `${record.interval} ${record.intervalUnit}`,
              },
              { label: "Next execution", value: date(record.nextDue) },
              {
                label: "Generated work",
                value: db.maintenance.filter((r) => r.schedule === record.id)
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
          entity="schedules"
          recordId={id}
          title={t("Preventive Maintenance Schedule Detail · related activity")}
        />
      }
    />
  );
}
