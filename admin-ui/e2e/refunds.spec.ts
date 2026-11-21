import { expect, test } from "@playwright/test";
import { seed } from "./seed-info";

test("admin approves the seeded refund; the audit note is shown and the order becomes Refunded", async ({ page, request }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Refunds" }).click();
  const refundRow = page.getByRole("row", { name: /E2E: cannot attend/ });
  await expect(refundRow).toBeVisible();

  await page.getByRole("textbox", { name: /Decision note/ }).fill("E2E approval note");
  await refundRow.getByRole("button", { name: "Approve" }).click();
  await expect(page.getByText(/approved\./)).toBeVisible();

  await page.getByRole("button", { name: "All", exact: true }).click();
  const processedRow = page.getByRole("row", { name: /E2E: cannot attend/ });
  await expect(processedRow).toContainText("Processed");
  await expect(processedRow).toContainText("E2E approval note");

  const order = await (await request.get(`http://localhost:5001/api/orders/${seed().refundRequestedOrderId}`)).json();
  expect(order.status).toBe("Refunded");
});
