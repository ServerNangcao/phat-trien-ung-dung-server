# Idempotency Architecture & Core Flows — Event Ticketing System (TV2 Orders & Notifications)

## 1. System Idempotency 4-Layer Protection Architecture

```mermaid
flowchart TD
    subgraph L1 [Layer 1: Idempotency Keys]
        direction LR
        A1[ReservationId<br/>Orders] --> A2[vnp_TxnRef<br/>Payments] --> A3[EventId<br/>Inbox Dedup] --> A4[DiscountCode<br/>Redemption]
    end

    subgraph L2 [Layer 2: Guarded State Machine]
        direction LR
        B1[Order: Pending -> Paid] --> B2[Ticket: Valid -> Used] --> B3[Email: Pending -> Sent]
    end

    subgraph L3 [Layer 3: Database Concurrency]
        direction LR
        C1[UNIQUE INDEX<br/>on ReservationId & EventId] --> C2[xmin rowversion<br/>optimistic locking] --> C3[Atomic UPDATE ... WHERE status = Pending]
    end

    subgraph L4 [Layer 4: Outbox / Inbox Exactly-Once]
        direction LR
        D1[Outbox Table<br/>FOR UPDATE SKIP LOCKED] --> D2[Inbox Table<br/>UNIQUE EventId + Handler] --> D3[At-Least-Once Publisher + At-Most-Once Consumer]
    end

    L1 --> L2 --> L3 --> L4
```

## 2. Order Creation & VNPay IPN Processing Flow

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant API as Orders API
    participant DB as Orders DB (PostgreSQL)
    participant Ticketing as Ticketing Module API
    participant VNPay as VNPay Gateway
    participant Inbox as Notifications Inbox

    Client->>API: POST /api/orders {customerId, reservationId}
    API->>Ticketing: GET /reservations/{reservationId} (Contract)
    Ticketing-->>API: Return Pricing & Items
    
    API->>DB: Check existing Order by ReservationId
    alt Order already exists
        DB-->>API: Return existing Order (Idempotent 200 OK)
        API-->>Client: Return existing order & paymentUrl
    else New Reservation
        API->>DB: INSERT Order (Pending) [UNIQUE INDEX guard]
        DB-->>API: Success
        API-->>Client: Return new orderId & paymentUrl
    end

    Note over Client,VNPay: User pays on VNPay sandbox
    VNPay->>API: POST /api/payments/vnpay/ipn (Callback)
    
    API->>API: Verify HMAC-SHA512 Signature & Cross-check Amount
    API->>DB: SELECT FOR UPDATE Order by txnRef
    
    alt Order already Paid / Refunded
        API-->>VNPay: RspCode 00 (Idempotent replay ack)
    else Order is Pending
        API->>DB: UPDATE Order SET Status = Paid (Guarded State Machine)
        API->>DB: INSERT OutboxMessage (OrderPaidIntegrationEvent)
        DB-->>API: Commit Transaction
        API-->>VNPay: RspCode 00 Success
    end

    Note over DB,Inbox: Async Event Delivery
    DB->>Inbox: Deliver OrderPaid / TicketIssued Event
    Inbox->>Inbox: Check UNIQUE(EventId, Handler)
    alt Event already processed
        Inbox-->>API: Return {duplicated: true} (At-Most-Once)
    else New Event
        Inbox->>Inbox: Stage Event & Send Email Atomically
    end
```

## 3. Race Condition Scenarios & Resolution Tree

```mermaid
flowchart TD
    Start([Concurrent Request Arrives]) --> Type{Scenario Type?}

    Type -->|Dual Order Creation| A1[Two threads POST same ReservationId]
    A1 --> A2[Thread A inserts row first]
    A2 --> A3[Thread B hits UNIQUE INDEX violation on ReservationId]
    A3 --> A4[Thread B catches exception and queries winner]
    A4 --> A5[Both threads return identical order details successfully]

    Type -->|Parallel IPN Callbacks| B1[VNPay retries IPN simultaneously]
    B1 --> B2[Thread A acquires lock via SELECT FOR UPDATE]
    B2 --> B3[Thread A updates status Pending to Paid and writes outbox]
    B3 --> B4[Thread B inspects status and sees it is already Paid]
    B4 --> B5[Thread B skips state change and immediately returns RspCode 00]

    Type -->|Message Broker Redelivery| C1[Outbox publisher delivers same EventId twice]
    C1 --> C2[Inbox middleware checks UNIQUE constraint on EventId + Handler]
    C2 --> C3[Second delivery hits unique constraint error]
    C3 --> C4[System catches duplicate, logs warning, and responds with duplicated: true]
```
