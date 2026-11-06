const http = require('http');

// Minimal stand-in for the Ticketing module (TV4) contract, for live demo/verification only.
// GET /api/ticketing/reservations/{id}  -> active reservation (2 VIP tickets @ 125000 VND)
// GET /api/ticketing/events/{id}/tickets -> tickets seeded via POST /__set-tickets
// POST /__set-tickets                   -> replace the ticket store (demo seeding)
const customerId = '99999999-9999-9999-9999-999999999999';
const eventId = '88888888-8888-8888-8888-888888888888';
const ticketTypeId = '77777777-7777-7777-7777-777777777777';
let tickets = [];

function send(res, status, payload) {
  res.statusCode = status;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify(payload));
}

http.createServer((req, res) => {
  if (req.method === 'POST' && req.url === '/__set-tickets') {
    let body = '';
    req.on('data', (c) => (body += c));
    req.on('end', () => {
      tickets = JSON.parse(body);
      send(res, 200, { seeded: tickets.length });
    });
    return;
  }
  if (req.method === 'GET' && req.url.startsWith('/api/ticketing/reservations/')) {
    const id = req.url.split('/').pop();
    send(res, 200, {
      reservationId: id,
      customerId: customerId,
      eventId: eventId,
      items: [{ ticketTypeId, ticketTypeName: 'VIP', quantity: 2, unitPrice: 125000 }],
      expiresAtUtc: new Date(Date.now() + 10 * 60000).toISOString(),
      isActive: true,
      customerEmail: 'customer@example.com'
    });
    return;
  }
  if (req.method === 'GET' && req.url.includes('/tickets')) {
    send(res, 200, tickets);
    return;
  }
  if (req.method === 'PUT' && req.url.match(/^\/api\/ticketing\/reservations\/[^/]+\/cancel$/)) {
    send(res, 200, { cancelled: true });
    return;
  }
  send(res, 404, {});
}).listen(5003, () => console.log('mock ticketing listening on 5003'));
