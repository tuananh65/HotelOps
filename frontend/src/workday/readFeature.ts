/** Build-time opt-in only. Missing, false, and all other values remain OFF. */
export const workdayReadEnabled =
  import.meta.env?.VITE_FE12_READ_ENABLED === "true";
