# 01: Ticketing (TV4) — phát hành TicketIssued kèm giá vé

**What to build:** Khi đơn hàng chuyển sang `Paid` và module Ticketing phát hành vé, sự kiện
`TicketIssued` gửi sang module Notifications phải mang theo giá trị hóa đơn: `TotalAmount`
(tổng tiền của đơn) và `Items` (mỗi hạng vé: tên, số lượng, đơn giá). Nhờ vậy email vé tự
động có bảng invoice — tính năng thuộc scope TV2, đã chờ sẵn: phần invoice chỉ render khi
event có giá, payload cũ không điền giá vẫn hợp lệ (email bỏ qua invoice, không lỗi).

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

Shape của contract (đã định nghĩa trong `Notifications.Contracts`, event do TV4 publish —
điền giá trị khi phát hành):

```
TicketIssuedIntegrationEvent {
  ..., TotalAmount: decimal, Items: [ { TicketTypeName, Quantity, UnitPrice } ]
}
```

- [ ] `TicketIssued` được publish sau khi đơn `Paid` chứa `TotalAmount` khớp tổng tiền đơn
- [ ] `Items` chứa đúng mỗi hạng vé trong đơn (số lượng × đơn giá = subtotal của hạng đó)
- [ ] Backward compat: payload không điền giá vẫn được Notifications xử lý bình thường
      (email không có invoice, không lỗi)
- [ ] Demo trên sandbox: một đơn thật qua VNPay → email vé hiển thị bảng invoice đúng số tiền
