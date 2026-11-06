import { expect, test } from "@playwright/test";
import { seed } from "./seed-info";

test("FR-13: customer search lists seeded orders and the detail shows items", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("textbox", { name: "Customer ID (GUID)" }).fill(seed().customerId);
  await page.getByRole("button", { name: "Search orders" }).click();

  const paidRow = page.getByRole("row", { name: new RegExp(seed().paidOrderId.slice(0, 8)) });
  await expect(paidRow).toBeVisible();
  await expect(paidRow).toContainText("Paid");

  const pendingRow = page.getByRole("row", { name: new RegExp(seed().pendingOrderId.slice(0, 8)) });
  await expect(pendingRow).toContainText("Pending");

  await paidRow.getByRole("button", { name: "Detail" }).click();
  await expect(page.getByRole("heading", { name: `Order ${seed().paidOrderId}` })).toBeVisible();
  // Scope to the detail panel: the list table above also contains 250,000 cells
  await expect(page.locator(".detail").getByRole("cell", { name: "250,000" })).toHaveCount(1); // line subtotal
  await expect(page.locator(".detail").getByRole("cell", { name: "125,000" })).toHaveCount(1); // unit price
  await expect(page.locator(".detail").getByText("250,000 VND")).toBeVisible(); // order total
});
