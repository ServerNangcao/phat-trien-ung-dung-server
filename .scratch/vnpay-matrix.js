// Matrix test against live VNPay sandbox: transport-encoding x hash algorithm x extras.
// Reports HTTP status + redirect Location for each variant.
const crypto = require('crypto');

const TMNCODE = 'IAS2CKBJ';
const SECRET = 'BQDZMOPKOJTZMKJZDJBBDETJRCYXEJXH';
const PAY_URL = 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html';

const now = new Date(Date.now() + 7 * 3600 * 1000);
const pad = n => String(n).padStart(2, '0');
const fmt = d => `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}`;

function makeData(tag) {
  return {
    vnp_Version: '2.1.0',
    vnp_Command: 'pay',
    vnp_TmnCode: TMNCODE,
    vnp_Locale: 'vn',
    vnp_CurrCode: 'VND',
    vnp_Amount: '100000',
    vnp_TxnRef: tag + Date.now(),
    vnp_OrderInfo: 'Test matrix ' + tag,
    vnp_OrderType: 'other',
    vnp_ReturnUrl: 'http://localhost:5001/api/payments/vnpay/return',
    vnp_IpAddr: '127.0.0.1',
    vnp_ExpireDate: fmt(new Date(now.getTime() + 15 * 60000)),
    vnp_CreateDate: fmt(now),
  };
}

const hmac = (secret, s, alg) => crypto.createHmac(alg, secret).update(s).digest('hex');

// transport encoders
const encPlus = k => k + '=' + encodeURIComponent('').slice(0, 0) + ''; // placeholder unused
function qsPlus(data) {
  const sp = new URLSearchParams();
  Object.keys(data).sort().forEach(k => sp.append(k, data[k]));
  return sp.toString();
}
function qsPct(data) { // %20 style, colon/slash encoded
  return Object.keys(data).sort().map(k => `${encodeURIComponent(k)}=${encodeURIComponent(data[k])}`).join('&');
}
function rawInput(data) {
  return Object.keys(data).sort().map(k => `${k}=${data[k]}`).join('&');
}

const variants = [];
const tags = ['C', 'D', 'E', 'F', 'G', 'H', 'I'];
let i = 0;
function add(name, data, hashInput, alg, extraQuery = '') {
  const sig = hmac(SECRET, hashInput, alg);
  const transport = qsPlus(data);
  const url = `${PAY_URL}?${transport}${extraQuery ? '&' + extraQuery : ''}&vnp_SecureHash=${sig}`;
  variants.push({ name, url });
}

const mk = tag => makeData(tag);

// C: encoded(+) + sha256
let d = mk('C'); add('C enc+sha256', d, qsPlus(d), 'sha256');
// D: raw + sha256
d = mk('D'); add('D raw+sha256', d, rawInput(d), 'sha256');
// E: pct20 + sha512 (sign over %20-style query)
d = mk('E'); add('E pct20+sha512', d, qsPct(d), 'sha512');
// F: pct20 + sha256
d = mk('F'); add('F pct20+sha256', d, qsPct(d), 'sha256');
// G: encoded(+) + sha512 + vnp_SecureHashType=SHA512 unsigned
d = mk('G'); add('G enc+sha512+SecureHashType', d, qsPlus(d), 'sha512', 'vnp_SecureHashType=SHA512');
// H: pct20 input but + transport, sha512, sign over pct string
d = mk('H'); { const s = qsPct(d); const sig = hmac(SECRET, s, 'sha512'); const url = `${PAY_URL}?${qsPlus(d)}&vnp_SecureHash=${sig}`; variants.push({ name: 'H pct-input+sha512', url }); }
// I: encoded(+) + sha512 but EXCLUDE vnp_ExpireDate from signature
d = mk('I'); { const subset = { ...d }; delete subset.vnp_ExpireDate; const sig = hmac(SECRET, qsPlus(subset), 'sha512'); const url = `${PAY_URL}?${qsPlus(d)}&vnp_SecureHash=${sig}`; variants.push({ name: 'I enc+sha512 exclExpire', url }); }

(async () => {
  for (const v of variants) {
    try {
      const r = await fetch(v.url, { redirect: 'manual', headers: { 'User-Agent': 'Mozilla/5.0' } });
      const loc = r.headers.get('location') || '-';
      let marker = '';
      if (r.status === 200) {
        const body = await r.text();
        marker = body.includes('Invalid signature') ? '[Invalid signature]' : (body.includes('payment-method') || body.includes('Payment Method') || body.includes('NCB') ? '[PAYMENT PAGE?]' : '[html ' + body.length + ']');
      }
      console.log(`${v.name.padEnd(34)} status=${r.status} loc=${loc} ${marker}`);
    } catch (e) {
      console.log(`${v.name.padEnd(34)} ERROR ${e.message}`);
    }
  }
})();
