import { Link, useSearchParams } from "react-router-dom";
import { PageHeader } from "../../ui";
export default function CreateCleaningTaskScreen() {
  const [params] = useSearchParams();
  const branch = params.get("branchId") || "";
  return (
    <>
      <PageHeader id={25} />
      <section className="panel form-panel">
        <h2>Tạo công việc vệ sinh</h2>
        <p role="status">Chưa thể tạo công việc trên máy chủ.</p>
        <p>
          API tạo công việc và hợp đồng cập nhật trạng thái phòng chưa được phê
          duyệt. Chức năng sẽ được mở khi có quy tắc chống trùng, trạng thái
          phòng và audit trong cùng giao dịch.
        </p>
        <button className="primary" disabled>
          Tạo công việc
        </button>
        <p>
          Không lưu dữ liệu minh họa hoặc báo thành công khi chưa có API thật.
        </p>
        <Link
          to={`/cleaning/tasks${branch ? `?branchId=${encodeURIComponent(branch)}` : ""}`}
        >
          Về Cleaning Monitor
        </Link>
      </section>
    </>
  );
}
