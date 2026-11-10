import { expect, test } from "@playwright/test";

test("customer discovers an event, reserves tickets, completes checkout and opens QR tickets", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".demo-ribbon")).toHaveCount(0);
  await page.getByRole("link", { name: "Xem sự kiện Midnight Jazz" }).click();
  await expect(page.getByRole("heading", { name: /MIDNIGHT JAZZ/ }).first()).toBeVisible();
  await page.getByRole("button", { name: /Giữ vé & tiếp tục/ }).click();

  await expect(page.getByRole("heading", { name: /Hoàn tất đặt vé/ })).toBeVisible();
  await page.getByLabel("Họ và tên").fill("Nguyễn Minh Anh");
  await page.getByLabel("Email nhận vé").fill("minhanh@example.com");
  await page.getByLabel("Số điện thoại").fill("0912345678");
  await page.getByLabel(/Tôi đồng ý/).check();
  await page.getByRole("button", { name: /Tiếp tục thanh toán/ }).click();

  await expect(page.getByRole("heading", { name: /Một trải nghiệm hay/ })).toBeVisible();
  await page.getByRole("link", { name: /Mở vé của tôi/ }).click();
  await expect(page.getByRole("heading", { name: "Vé của bạn." })).toBeVisible();
  await expect(page.locator(".digital-ticket-qr svg")).toBeVisible();
  await expect(page.getByText(/DEMO-/)).toBeVisible();
});

test("event catalog filters by city and category", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Ẩm thực", exact: true }).last().click();
  await page.getByLabel("Địa điểm").selectOption("TP. Hồ Chí Minh");
  await expect(page.getByRole("heading", { name: "Saigon Street Food Night" }).last()).toBeVisible();
  await expect(page.getByRole("heading", { name: "The Sound of Spring" })).toHaveCount(0);
});

test("each category has exactly ten demo events", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".event-count")).toHaveText("50 SỰ KIỆN");
  for (const category of ["Âm nhạc", "Nghệ thuật", "Hội thảo", "Ẩm thực", "Thể thao"]) {
    await page.locator(".category-pills").getByRole("button", { name: category, exact: true }).click();
    await expect(page.locator(".event-count")).toHaveText("10 SỰ KIỆN");
  }
});

test("weekly hot area shows four selectable featured events", async ({ page }) => {
  await page.goto("/");
  const hotEvents = page.getByRole("button", { name: /Xem sự kiện nổi bật:/ });
  await expect(hotEvents).toHaveCount(4);
  await hotEvents.nth(3).click();
  await expect(page.locator(".hero-art-bottom h2")).toHaveText("Saigon Street Food Night");
  await expect(page.getByRole("link", { name: "Xem sự kiện Saigon Street Food Night" }))
    .toHaveAttribute("href", "/events/street-food-night");
});

test("staff route is role guarded and displays invalid scan feedback", async ({ page }) => {
  await page.goto("/staff/check-in");
  await expect(page).toHaveURL(/\/login$/);
  await page.getByRole("button", { name: /Nhân viên check-in/ }).click();
  await page.getByRole("button", { name: "Tiếp tục" }).click();
  await expect(page.getByRole("heading", { name: /Soát vé nhanh gọn/ })).toBeVisible();
  await expect(page.getByText(/CHẾ ĐỘ DEMO · vé mẫu/)).toHaveCount(0);
  await page.getByLabel("Mã vé").fill("NOT-A-REAL-TICKET");
  await page.getByRole("button", { name: "Xác thực mã" }).click();
  await expect(page.getByText("KHÔNG TÌM THẤY VÉ")).toBeVisible();
});

test("customer can register and receives a customer account", async ({ page }) => {
  await page.goto("/login");
  await page.getByRole("tab", { name: "Đăng ký" }).click();
  await expect(page.getByRole("heading", { name: /Tạo tài khoản Ticketing/ })).toBeVisible();
  await page.getByLabel("Họ và tên").fill("Nguyễn An");
  await page.getByLabel("Email").fill("an@example.com");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("secure-pass-123");
  await page.getByLabel("Xác nhận mật khẩu").fill("secure-pass-123");
  await page.getByRole("button", { name: "Tạo tài khoản" }).click();
  await expect(page).toHaveURL(/\/my-tickets$/);
  await expect(page.getByText("Nguyễn An")).toBeVisible();
});
