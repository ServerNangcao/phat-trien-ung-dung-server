const http = require('http');

// Minimal stand-in for the Ticketing module (TV4) contract, for live demo/verification only.
const customerId = '99999999-9999-9999-9999-999999999999';
const eventId = '88888888-8888-8888-8888-888888888888';
const ticketTypeId = '77777777-7777-7777-7777-777777777777';

http.createServer((req, res) => {
  res.setHeader('Content-Type', 'application/json');
  if (req.url.startsWith('/api/ticketing/reservations/')) {
    const id = req.url.split('/').pop();
    res.end(JSON.stringify({
      reservationId: id,
      customerId: customerId,
      eventId: eventId,
      items: [{ ticketTypeId, ticketTypeName: 'VIP', quantity: 2, unitPrice: 125000 }],
      expiresAtUtc: new Date(Date.now() + 10 * 60000).toISOString(),
      isActive: true
    }));
    return;
  }
  if (req.url.includes('/tickets')) {
    res.end(JSON.stringify([]));
    return;
  }
  res.statusCode = 404;
  res.end('{}');
}).listen(5003, () => console.log('mock ticketing listening on 5003'));
