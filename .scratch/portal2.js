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

  // Navigate to AccountDetail
  await page.goto('https://sandbox.vnpayment.vn/merchantv2/Account/AccountDetail.htm');
  await page.waitForTimeout(2000);
  const accHtml = await page.content();
  fs.writeFileSync('C:\\Users\\tien2004\\.zcode\\workspace\\draft-18af59a6-b6a9-4585-b2bf-ee294e93e91f\\backend\\.scratch\\acc2.html', accHtml);
  console.log('Account page saved');

  // Look for any links related to terminal/secret management
  const links = await page.$$eval('a', as => as.map(a => ({href: a.href, text: a.innerText.trim()})));
  console.log('Links:', JSON.stringify(links.filter(l => l.text && (l.text.includes('mã') || l.text.includes('bí') || l.text.includes('secret') || l.text.includes('Terminal') || l.text.includes('cấu hình'))), null, 2));

  await browser.close();
})();