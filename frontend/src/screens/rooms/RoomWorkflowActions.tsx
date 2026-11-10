import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { cleaningActionHref } from "../../api/dev1/roomActions";
import { createError } from "../../api/dev1/cleaningCreate";

export default function RoomWorkflowActions({
  branch,
  room,
}: {
  branch: string;
  room: string;
}) {
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    href?: string;
    error?: string;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    cleaningActionHref(branch, room, abort.signal)
      .then((href) => {
        if (!abort.signal.aborted) setResult({ href });
      })
      .catch((error) => {
        if (!abort.signal.aborted) setResult({ error: createError(error) });
      });
    return () => abort.abort();
  }, [branch, room, retry]);
  return (
    <section aria-label="Hành động tiếp theo">
      <h3>Hành động tiếp theo</h3>
      {result?.href ? (
        <Link to={result.href}>Tạo công việc vệ sinh</Link>
      ) : (
        <>
          <button disabled>Tạo công việc vệ sinh</button>
          <p>{result?.error || "Đang kiểm tra quyền và phòng…"}</p>
          {result?.error && (
            <button
              onClick={() => {
                setResult(null);
                setRetry((v) => v + 1);
              }}
            >
              Kiểm tra lại hành động
            </button>
          )}
        </>
      )}
      <p>
        <button disabled>Phân công kiểm tra phòng</button> Chức năng chưa sẵn
        sàng.
      </p>
      <p>
        <button disabled>Báo cáo sự cố</button> Chức năng chưa sẵn sàng.
      </p>
      <p>
        Chức năng đích kiểm tra lại quyền và điều kiện nghiệp vụ trước khi thực
        hiện.
      </p>
    </section>
  );
}
