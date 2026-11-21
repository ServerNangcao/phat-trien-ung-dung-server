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

  // Go directly to TerminalEdit
  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Account/TerminalEdit.htm');
  await page.waitForTimeout(3000);
  const html = await page.content();
  fs.writeFileSync('C:\\Users\\tien2004\\.zcode\\workspace\\draft-18af59a6-b6a9-4585-b2bf-ee294e93e91f\\backend\\.scratch\\te3.html', html);
  console.log('TerminalEdit saved');

  // Look for ALL form fields
  const fields = await page.$$eval('input, select, textarea', els => els.map(e => ({
    name: e.name,
    id: e.id,
    type: e.type,
    value: e.value,
    placeholder: e.placeholder,
    disabled: e.disabled,
    readonly: e.readOnly,
    style: e.style.cssText
  })));
  console.log('Fields:', JSON.stringify(fields, null, 2));

  // Check if there's a hidden secretkey field
  const secretField = await page.$('[name="secretkey"], [name="HashSecret"], [name="hashSecret"]');
  console.log('Secret field found:', !!secretField);

  await browser.close();
})();