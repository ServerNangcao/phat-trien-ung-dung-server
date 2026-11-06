// Credential oracle: VNPay merchant_webapi querydr.
// checksum data = values joined by "|" (NOT query-string form).
// A valid signature + nonexistent TxnRef => vnp_ResponseCode 91 (txn not found).
// An invalid signature => 97. So this discriminates wrong-secret vs wrong-format.
const crypto = require('crypto');

const TMNCODE = 'IAS2CKBJ';
const SECRET = 'BQDZMOPKOJTZMKJZDJBBDETJRCYXEJXH';
const URL = 'https://sandbox.vnpayment.vn/merchant_webapi/api/transaction';

const now = new Date(Date.now() + 7 * 3600 * 1000);
const pad = n => String(n).padStart(2, '0');
const fmt = d => `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}`;

function attempt(label, secret, alg) {
  const reqId = 'QR' + crypto.randomBytes(9).toString('hex').toUpperCase();
  const txnRef = 'NOEXIST' + Date.now();
  const tDate = fmt(now);
  const cDate = fmt(now);
  const data = [reqId, '2.1.0', 'querydr', TMNCODE, txnRef, tDate, cDate, '127.0.0.1', 'Kiem tra ket noi'].join('|');
  const hash = crypto.createHmac(alg, secret).update(data).digest('hex');
  const body = {
    vnp_RequestId: reqId,
    vnp_Version: '2.1.0',
    vnp_Command: 'querydr',
    vnp_TmnCode: TMNCODE,
    vnp_TxnRef: txnRef,
    vnp_OrderInfo: 'Kiem tra ket noi',
    vnp_TransactionDate: tDate,
    vnp_CreateDate: cDate,
    vnp_IpAddr: '127.0.0.1',
    vnp_SecureHash: hash,
  };
  return fetch(URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }).then(r => r.text()).then(t => {
    console.log(`--- ${label} ---`);
    console.log(t.slice(0, 600));
    console.log();
  }).catch(e => console.log(label, 'ERROR', e.message));
}

(async () => {
  await attempt('REAL secret + sha512', SECRET, 'sha512');
  await attempt('REAL secret + sha256', SECRET, 'sha256');
  await attempt('WRONG secret + sha512 (control)', 'WRONGSECRETWRONGSECRETWRONGSECRET', 'sha512');
})();
