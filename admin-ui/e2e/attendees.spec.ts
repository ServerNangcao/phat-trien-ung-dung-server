import { expect, test } from "@playwright/test";
import { seed } from "./seed-info";

test("FR-22: attendee list shows paid-order tickets and filters out pending ones", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Attendees" }).click();
  await page.getByRole("textbox", { name: "Event ID (GUID)" }).fill(seed().eventId);
  await page.getByRole("button", { name: "Load attendees" }).click();

  await expect(page.getByRole("row", { name: new RegExp(seed().paidOrderId.slice(0, 8)) })).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(seed().pendingOrderId.slice(0, 8)) })).toHaveCount(0);
});
