import fs from "node:fs";
import path from "node:path";

export interface SeedInfo {
  customerId: string;
  eventId: string;
  paidOrderId: string;
  pendingOrderId: string;
  refundRequestedOrderId: string;
}

const seedPath = () => path.join(process.cwd(), "e2e", ".seed.json");

export function seed(): SeedInfo {
  return JSON.parse(fs.readFileSync(seedPath(), "utf8")) as SeedInfo;
}
