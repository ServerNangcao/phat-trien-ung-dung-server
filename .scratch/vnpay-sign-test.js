// Discriminating test: same params, two signing variants, against the live VNPay sandbox.
// A) hash over URL-encoded query string (URLSearchParams rules, space as '+')  <- current code
// B) hash over raw key=value pairs                                            <- official merchant sample style
const crypto = require('crypto');

const TMNCODE = 'IAS2CKBJ';
const SECRET = 'BQDZMOPKOJTZMKJZDJBBDETJRCYXEJXH';
const PAY_URL = 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html';

const now = new Date(Date.now() + 7 * 3600 * 1000); // Vietnam GMT+7
const pad = n => String(n).padStart(2, '0');
const fmt = d => `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}`;

const data = {
  vnp_Version: '2.1.0',
  vnp_Command: 'pay',
  vnp_TmnCode: TMNCODE,
  vnp_Locale: 'vn',
  vnp_CurrCode: 'VND',
  vnp_Amount: '100000',
  vnp_TxnRef: 'TEST' + Date.now(),
  vnp_OrderInfo: 'Test order signing',
  vnp_OrderType: 'other',
  vnp_ReturnUrl: 'http://localhost:5001/api/payments/vnpay/return',
  vnp_IpAddr: '127.0.0.1',
  vnp_ExpireDate: fmt(new Date(now.getTime() + 15 * 60000)),
  vnp_CreateDate: fmt(now),
};

const hmac = (secret, s) => crypto.createHmac('sha512', secret).update(s).digest('hex');
const sortedKeys = Object.keys(data).sort();

// A: encoded
const sp = new URLSearchParams();
sortedKeys.forEach(k => sp.append(k, data[k]));
const encodedQs = sp.toString();
const sigEncoded = hmac(SECRET, encodedQs);

// B: raw
const rawQs = sortedKeys.map(k => `${k}=${data[k]}`).join('&');
const sigRaw = hmac(SECRET, rawQs);
const sp2 = new URLSearchParams();
sortedKeys.forEach(k => sp2.append(k, data[k]));

console.log('TXNREF=' + data.vnp_TxnRef);
console.log('QS=' + encodedQs);
console.log('SIG_ENCODED=' + sigEncoded);
console.log('SIG_RAW=' + sigRaw);
console.log('URL_A=' + `${PAY_URL}?${encodedQs}&vnp_SecureHash=${sigEncoded}`);
console.log('URL_B=' + `${PAY_URL}?${sp2.toString()}&vnp_SecureHash=${sigRaw}`);
