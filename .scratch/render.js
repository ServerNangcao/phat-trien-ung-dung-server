const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const http = require('http');

(async () => {
  const excalidrawPath = 'C:\\Users\\tien2004\\.zcode\\workspace\\draft-18af59a6-b6a9-4585-b2bf-ee294e93e91f\\Idempotency Patterns in Ticketing System (Orders + Notifications).excalidraw';
  const outputPath = 'C:\\Users\\tien2004\\.zcode\\workspace\\draft-18af59a6-b6a9-4585-b2bf-ee294e93e91f\\backend\\idempotency-overview.png';

  const data = JSON.parse(fs.readFileSync(excalidrawPath, 'utf8'));
  const dataStr = JSON.stringify(data);

  // Serve the UMD builds locally so no network is needed. React globals first.
  const nm = 'C:\\Users\\tien2004\\AppData\\Local\\Temp\\excalidraw-render\\node_modules\\';
  const reactUmd = fs.readFileSync(nm + 'react\\umd\\react.production.min.js', 'utf8');
  const reactDomUmd = fs.readFileSync(nm + 'react-dom\\umd\\react-dom.production.min.js', 'utf8');
  const umdContent = fs.readFileSync(nm + '@excalidraw\\excalidraw-umd\\excalidraw.production.min.js', 'utf8');

  const html = `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<script>${reactUmd}</script>
<script>${reactDomUmd}</script>
<script>${umdContent}</script>
</head><body></body></html>`;

  const server = http.createServer((req, res) => {
    res.writeHead(200, { 'Content-Type': 'text/html' });
    res.end(html);
  });
  await new Promise(r => server.listen(0, r));
  const url = `http://localhost:${server.address().port}/`;

  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 2200, height: 2400 } });

  const errors = [];
  page.on('pageerror', err => errors.push(`PAGE ERROR: ${err.message}`));

  await page.goto(url, { waitUntil: 'load' });

  const svgHtml = await page.evaluate(async (dataStr) => {
    const scene = JSON.parse(dataStr);
    // Sequence-flow arrows were saved parked at x>=3800; snap each into its row gap.
    const arrowPos = {
      's1-client-api': [280, 210], 's1-api-ticketing': [520, 210], 's1-ticketing-repo': [740, 210],
      's1-repo-db': [960, 210], 's1-db-outbox': [1180, 210],
      's2-vnpay-api': [280, 360], 's2-api-handler': [520, 360], 's2-handler-repo': [760, 360],
      's2-repo-domain': [980, 360], 's2-domain-db': [1220, 360],
      's3-notif-endpoint': [280, 510], 's3-endpoint-inbox': [520, 510], 's3-inbox-db': [740, 510],
      's3-db-email': [960, 510], 's3-email-retry': [1180, 510],
    };
    for (const el of scene.elements) {
      if (arrowPos[el.id]) { el.x = arrowPos[el.id][0]; el.y = arrowPos[el.id][1]; }
    }
    scene.elements = scene.elements.filter(el => !el.isDeleted);
    const { restore, exportToSvg } = window.ExcalidrawLib || window.Excalidraw;
    const restored = restore(scene, null, null);
    const svg = await exportToSvg({
      elements: restored.elements,
      appState: { ...restored.appState, exportBackground: true, viewBackgroundColor: '#ffffff' },
      files: scene.files || null,
      exportPadding: 24,
    });
    return new XMLSerializer().serializeToString(svg);
  }, dataStr).catch(e => { errors.push('EXPORT: ' + e.message); return null; });

  if (!svgHtml) {
    console.error('SVG export failed\n' + errors.join('\n'));
    await browser.close();
    server.close();
    process.exit(1);
  }

  fs.writeFileSync('C:\\Users\\tien2004\\.zcode\\workspace\\draft-18af59a6-b6a9-4585-b2bf-ee294e93e91f\\backend\\idempotency-overview.svg', svgHtml);

  const svgPage = await browser.newPage();
  await svgPage.setContent(`<body style="margin:0;background:#ffffff">${svgHtml}</body>`, { waitUntil: 'load' });
  await svgPage.screenshot({ path: outputPath, fullPage: true });
  console.log('PNG saved:', outputPath);
  await browser.close();
  server.close();
})();