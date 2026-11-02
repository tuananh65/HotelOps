import { visible } from "../services";
import type {
  Asset,
  CleaningTask,
  Incident,
  InventoryItem,
  OperationalTask,
  Room,
  StockTransaction,
  User,
} from "../types/entities";

/** Typed read views over the scoped API cache. */
export const readModels = {
  users: () => visible("users") as User[],
  rooms: () => visible("rooms") as Room[],
  incidents: () => visible("incidents") as Incident[],
  assets: () => visible("assets") as Asset[],
  items: () => visible("items") as InventoryItem[],
  cleaning: () => visible("cleaning") as CleaningTask[],
  tasks: () => visible("tasks") as OperationalTask[],
  transactions: () => visible("transactions") as StockTransaction[],
};
