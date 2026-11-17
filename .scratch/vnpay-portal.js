// Read the VNPay sandbox TerminalEdit page via Playwright,
// navigating from login to terminal settings.
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Users/Login.htm');
  await page.fill('#Email', 'vuduytien20042004@gmail.com');
  await page.fill('#Password', 'Bw7sJHKYkUKz');
  // Dismiss any modal and submit via Enter key
  await page.keyboard.press('Enter');
  await page.waitForURL('**/Dashboard.htm', { timeout: 30000 });
  console.log('Logged in successfully!');

  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Account/TerminalEdit.htm');
  await page.waitForTimeout(3000);
  const html = await page.content();
  fs.writeFileSync(__dirname + '/te_pw.html', html);
  console.log('Saved terminal edit page. Length:', html.length);
  console.log('Text snippet:', html.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 1500));

  await browser.close();
})();