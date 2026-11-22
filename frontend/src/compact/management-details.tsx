import { useEffect, useState } from "react";
import { api, type Page } from "./api";
import { useAuth } from "./auth";
import { readCatalogRows } from "./use-catalog-rows";

type Details = {
  hotels: { name: string; rooms: number }[];
};
export default function useManagementDetails(revision: number) {
  const { canGlobal } = useAuth();
  const structure = canGlobal("MASTER_DATA_MANAGE");
  const [retry, bump] = useState(0);
  const [query, setQuery] = useState<{
    loading: boolean;
    data?: Details;
    error?: unknown;
  }>({ loading: true });
  useEffect(() => {
    const controller = new AbortController();
    setQuery({ loading: true });
    (async () => {
      const hotels = structure
        ? await readCatalogRows("branches", 0, controller.signal)
        : [];
      const hotelStats: Details["hotels"] = [];
      // Keep requests bounded to one hotel at a time for larger organizations.
      for (const hotel of hotels) {
        const rooms = await api<Page>(
          `catalog/rooms?branchId=${hotel.branchId}&pageSize=1`,
          undefined,
          "GET",
          controller.signal,
        );
        hotelStats.push({
          name: hotel.name,
          rooms: rooms.total,
        });
      }
      if (!controller.signal.aborted)
        setQuery({
          loading: false,
          data: {
            hotels: hotelStats,
          },
        });
    })().catch((error) => {
      if (!controller.signal.aborted) setQuery({ loading: false, error });
    });
    return () => controller.abort();
  }, [structure, revision, retry]);
  return { ...query, enabled: structure, retry: () => bump((n) => n + 1) };
}
