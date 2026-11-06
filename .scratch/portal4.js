const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Users/Login.htm');
  await page.fill('#Email', 'vuduytien20042004@gmail.com');
  await page.fill('#Password', 'Bw7sJHKYkUKz');
  await page.keyboard.press('Enter');
  await page.waitForURL('**/Dashboard.htm', { timeout: 30000 });
  console.log('Logged in!');

  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Account/TerminalEdit.htm');
  await page.waitForTimeout(3000);
  
  // Click Test call IPN
  const testBtn = await page.$('#testIpn');
  if (testBtn) {
    console.log('Clicking Test IPN...');
    const [response] = await Promise.all([
      page.waitForResponse(r => r.url().includes('TestIPN') || r.url().includes('testIpn'), { timeout: 10000 }).catch(() => null),
      testBtn.click()
    ]);
    if (response) {
      console.log('Test IPN response:', response.status(), response.url());
      const body = await response.text().catch(() => 'no body');
      console.log('Response body:', body.slice(0, 1000));
    } else {
      console.log('No Test IPN response captured');
      await page.waitForTimeout(3000);
      const html = await page.content();
      console.log('Page after click:', html.slice(-2000));
    }
  } else {
    console.log('Test IPN button not found');
  }

  await browser.close();
})();