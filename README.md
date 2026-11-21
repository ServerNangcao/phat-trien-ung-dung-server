# Hệ thống Quản Lý Sự Kiện và Bán Vé (Ticketing Platform)

> Dự án xây dựng nền tảng quản lý sự kiện và bán vé trực tuyến theo kiến trúc **Modular Monolith** trên nền tảng **.NET 10**, đóng gói **Docker Compose**, phân lập cơ sở dữ liệu **PostgreSQL Multi-Schema**, và tích hợp CI/CD tự động.

---

## 1. Tổng quan dự án

Hệ thống cung cấp giải pháp toàn diện cho việc tổ chức sự kiện và thương mại điện tử vé số:
- **Khách hàng (Customer):** Khám phá sự kiện, tìm kiếm, lọc theo giá/địa điểm, giữ vé và thanh toán trực tuyến, nhận vé điện tử QR qua email.
- **Ban tổ chức (Organizer):** Khởi tạo sự kiện, thiết lập các hạng vé (VIP, Standard...), theo dõi báo cáo doanh thu và tiến độ bán vé thời gian thực.
- **Nhân viên soát vé (Staff):** Ứng dụng quét mã QR tại cổng sự kiện để check-in tức thì, chống vé giả và vé quay vòng.
- **Quản trị viên (Admin):** Kiểm duyệt sự kiện, quản lý người dùng, xử lý yêu cầu hoàn tiền.

---

## 2. Kiến trúc hệ thống (System Architecture)

Dự án áp dụng mô hình kiến trúc **Modular Monolith (In-process Modules)** với nguyên tắc phân rã ranh giới nghiệp vụ (Bounded Contexts) chặt chẽ:

```
                                  ┌──────────────────────────┐
                                  │      Ticketing.Api       │
                                  │   (Thin Host / Program)  │
                                  └─────────────┬────────────┘
                                                │ Nạp DI & Route
        ┌───────────────────┬───────────────────┼───────────────────┬───────────────────┐
        ▼                   ▼                   ▼                   ▼                   ▼
  ┌───────────┐       ┌───────────┐       ┌───────────┐       ┌───────────┐       ┌───────────┐
  │ Identity  │       │  Events   │       │ Ticketing │       │  Orders   │       │  CheckIn  │
  └───────────┘       └───────────┘       └─────┬─────┘       └─────┬─────┘       └───────────┘
                                                │                   │
                                                │ (Outbox/Inbox)    │ (Outbox/Inbox)
                                                ▼                   ▼
                                          ┌───────────────────────────────┐
                                          │         Notifications         │
                                          │ (Chỉ lắng nghe sự kiện ngầm) │
                                          └───────────────────────────────┘
```

### 2.1 Cấu trúc 2 Project cho mỗi Module
Mỗi module độc lập gồm đúng 2 project .NET nhằm tối ưu thời gian build và bảo vệ ranh giới đóng gói:
1. **`{Module}.Contracts` (Public):** Chỉ chứa interface `I{Module}ModuleApi`, DTOs công khai và Integration Events. Đây là thành phần duy nhất các module khác được phép tham chiếu.
2. **`{Module}.Internal` (Internal):** Chứa toàn bộ mã nguồn xử lý bao gồm 4 thư mục:
   - `Domain/`: Thực thể (Entity), Value Object, Domain Events (thuần C#, không phụ thuộc EF Core).
   - `Application/`: Use Cases, Handlers, FluentValidators nội bộ.
   - `Infrastructure/`: `DbContext` riêng biệt kế thừa `BaseDbContext`, Repositories, Migrations.
   - `Api/`: Minimal API Endpoints nội bộ; chỉ duy nhất class `{Module}Module` là `public` để Host nạp DI và map route.

### 2.2 Phân lập dữ liệu (Schema Isolation)
- Hệ thống dùng chung một cụm cơ sở dữ liệu PostgreSQL nhưng **mỗi module sở hữu một Postgres Schema riêng biệt** (`identity`, `events`, `ticketing`, `orders`, `checkin`, `notifications`).
- Mỗi schema sở hữu một bảng lịch sử di trú độc lập (`__EFMigrationsHistory`), cho phép di trú DB (migration) độc lập không gây xung đột khóa bảng.
- **Ranh giới:** Nghiêm cấm foreign key và join bảng chéo schema giữa các module. Mọi truy xuất liên module phải thông qua Contract API hoặc sự kiện.

### 2.3 Giao tiếp sự kiện: Outbox & Inbox Pattern
- **Giao tiếp đồng bộ:** Dùng cho các tác vụ cần kết quả tức thì (ví dụ: `Orders` gọi `Ticketing.Contracts` để giữ vé tạm thời).
- **Giao tiếp bất đồng bộ:** Áp dụng **Outbox Pattern** kết hợp `SELECT ... FOR UPDATE SKIP LOCKED` để đảm bảo sự kiện phát hành vé (`TicketIssued`), cập nhật hạng vé (`TicketTypesChanged`) không bao giờ bị mất ngay cả khi mạng hoặc dịch vụ lỗi.
- **Idempotency (Chống trùng lặp):** Module nhận sự kiện (`Notifications`, `Events`) áp dụng **Inbox Pattern** với ràng buộc duy nhất `(EventId, Handler)` nhằm triệt tiêu hoàn toàn rủi ro xử lý lặp sự kiện.

### 2.4 Bốn luật kiểm tra ranh giới (Architecture Tests)
Tự động xác thực qua thư viện `NetArchTest` trong CI/CD:
1. `Internal` module A **không bao giờ** được tham chiếu `Internal` module B.
2. `Contracts` module A **không bao giờ** tham chiếu `Contracts` module B.
3. Thư mục `Domain` **không phụ thuộc** vào `Application`, `Infrastructure` hay `Microsoft.EntityFrameworkCore`.
4. Assembly `Internal` chỉ công khai duy nhất class `{Module}Module` ra bên ngoài.

---

## 3. Các phân hệ chức năng (Core Modules)

| Phân hệ | Schema DB | Trách nhiệm & Nghiệp vụ cốt lõi |
|---|---|---|
| **Identity** | `identity` | • Đăng ký, đăng nhập email/mật khẩu, xác thực JWT + Refresh Token.<br>• Phân quyền theo vai trò (Customer, Organizer, Staff, Admin). |
| **Events** | `events` | • Quản lý danh mục, địa điểm, sự kiện (Nháp → Chờ duyệt → Đang bán → Kết thúc).<br>• Tải ảnh bìa poster lên Cloudflare R2 / MinIO.<br>• Bộ lọc sự kiện đa tiêu chí (danh mục, khoảng giá, ngày tổ chức).<br>• Dashboard thống kê doanh thu và lượng vé bán cho Organizer. |
| **Ticketing** | `ticketing` | • Thiết lập các hạng vé (giá, tổng số lượng mở bán, giới hạn mua mỗi đơn).<br>• **Giữ vé tạm thời (Reservation):** Tạm giữ vé trong 10 phút, tự động hoàn trả tồn kho nếu quá hạn.<br>• **Chống bán vượt vé (Over-selling):** Trừ kho nguyên tử bằng concurrency token / SQL atomic updates.<br>• Phát hành vé điện tử với mã QR ký số bảo mật `HMAC-SHA256`. |
| **Orders** | `orders` | • Khởi tạo đơn hàng từ vé đang giữ.<br>• Tích hợp cổng thanh toán sandbox (VNPay / MoMo).<br>• Nhận và xử lý Webhook thanh toán an toàn, có cơ chế đối soát thanh toán trễ (`PaidAfterExpiry`) và tự động tạo `RefundRequest`. |
| **CheckIn** | `checkin` | • Quét mã QR tại cổng kiểm soát, xác thực tính toàn vẹn chữ ký HMAC.<br>• Chặn tuyệt đối vé đã sử dụng (chống quay vòng vé).<br>• Báo cáo thời gian thực số lượng người tham dự đã vào cổng. |
| **Notifications** | `notifications` | • Lắng nghe sự kiện `TicketIssued` qua Inbox Message.<br>• Gửi email thông báo đơn hàng kèm vé điện tử và mã QR đến khách hàng.<br>• Tự động thử lại (retry) khi gửi mail gặp sự cố. |

---

## 4. Công nghệ sử dụng (Tech Stack)

### Backend
- **Nền tảng:** .NET 10 (C# 14), ASP.NET Core Minimal APIs.
- **ORM & Database:** Entity Framework Core 10, PostgreSQL 18 (Npgsql).
- **Bộ nhớ đệm & Khóa:** Redis 7 (Cache, Distributed Lock, Rate Limiting).
- **Validation & Mapping:** FluentValidation.
- **Bảo mật & Ký số:** ASP.NET Identity, JWT Bearer, HMAC-SHA256 (QR Signature).
- **Tài liệu API:** OpenAPI / Swagger, Scalar UI.

### Frontend (Định hướng)
- React 19, TypeScript, Vite.
- Tailwind CSS, shadcn/ui.
- TanStack Query (State & Caching), React Hook Form + Zod.
- Thư viện quét mã: `html5-qrcode`.

### Hạ tầng & DevOps
- **Container hóa:** Docker, Docker Compose (Multi-stage build tối ưu kích thước image).
- **Lưu trữ tệp:** Cloudflare R2 (Production) / MinIO (Local Dev).
- **CI/CD:** GitHub Actions (tự động hóa build, test kiến trúc và deploy).

---

## 5. Cấu trúc thư mục

```
├── .github/workflows/          # Quy trình CI/CD tự động
├── backend/
│   ├── Ticketing.slnx          # Solution file .NET
│   ├── Directory.Build.props   # Cấu hình chung version, compiler, analyzer
│   ├── Directory.Packages.props# Central Package Management (CPM)
│   ├── Dockerfile              # Multi-stage Dockerfile cho Backend
│   ├── src/
│   │   ├── Api/
│   │   │   └── Ticketing.Api/  # Host: nạp middleware, DI và endpoint của module
│   │   ├── BuildingBlocks/
│   │   │   ├── Ticketing.SharedKernel/        # BaseEntity, Result<T>, Events, ErrorCodes
│   │   │   └── Ticketing.Infrastructure.Core/ # BaseDbContext, Outbox/Inbox Workers
│   │   └── Modules/
│   │       ├── Identity/       # Identity.Contracts & Identity.Internal
│   │       ├── Events/         # Events.Contracts & Events.Internal
│   │       ├── Ticketing/      # Ticketing.Contracts & Ticketing.Internal
│   │       ├── Orders/         # Orders.Contracts & Orders.Internal
│   │       ├── CheckIn/        # CheckIn.Contracts & CheckIn.Internal
│   │       └── Notifications/  # Notifications.Contracts & Notifications.Internal
│   └── tests/
│       ├── Architecture.Tests/ # Kiểm tra ranh giới kiến trúc (NetArchTest)
│       └── Integration.Tests/  # Kiểm tra tích hợp Outbox/Inbox và DbContext
├── frontend/                   # Ứng dụng giao diện người dùng
├── docker-compose.yml          # Môi trường chạy cục bộ (PostgreSQL, Redis, MinIO)
├── PHAN_CONG_CONG_VIEC.md      # Kế hoạch phân chia công việc chi tiết 5 thành viên
└── README.md
```

---

## 6. Hướng dẫn khởi chạy cục bộ (Getting Started)

### Yêu cầu tiên quyết
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/) & Docker Compose
- [Node.js 20+](https://nodejs.org/) (cho phần Frontend)

---

### Cách 1: Khởi chạy nhanh toàn bộ (1 lệnh duy nhất trên Windows)

Sử dụng script PowerShell [`start-dev.ps1`](start-dev.ps1) tại thư mục gốc:

```powershell
.\start-dev.ps1
```

Script sẽ tự động:
1. Khởi động hạ tầng PostgreSQL 18 (port `5433`) và Redis 7 (port `6379`) qua Docker.
2. Mở một cửa sổ Terminal mới chạy **Backend API** với tính năng Hot-Reload (`dotnet watch`) tại `http://localhost:8080`.
3. Mở một cửa sổ Terminal mới chạy **Frontend Client** (`npm run dev`) tại `http://localhost:5173`.

---

### Cách 2: Khởi chạy thủ công từng dịch vụ (Theo dõi log chi tiết)

#### Bước 1: Khởi động cơ sở dữ liệu & Cache (Docker)
```bash
docker compose up -d ticketing-db ticketing-redis
```

#### Bước 2: Khởi chạy Backend API (.NET 10)
```bash
dotnet watch --project backend/src/Api/Ticketing.Api
```
*(Backend sẽ lắng nghe tại `http://localhost:8080`, hỗ trợ tự động reload khi sửa mã nguồn)*.

#### Bước 3: Cấu hình và Khởi chạy Frontend (React 19)
1. Đảm bảo cấu hình file `frontend/.env.development`:
   - Chế độ kết nối API thật:
     ```env
     VITE_DATA_MODE=api
     VITE_API_URL=http://localhost:8080
     ```
   - Chế độ dữ liệu mẫu demo (không cần backend):
     ```env
     VITE_DATA_MODE=demo
     ```
2. Cài đặt dependencies (lần đầu) và khởi chạy:
   ```bash
   cd frontend
   npm install    # chỉ chạy lần đầu
   npm run dev
   ```
*(Frontend sẽ lắng nghe tại `http://localhost:5173`)*.

---

### 🌐 Bảng các địa chỉ truy cập hệ thống

| Dịch vụ | URL | Mô tả |
|---|---|---|
| **Frontend Web App** | [http://localhost:5173](http://localhost:5173) | Khám phá sự kiện, đặt giữ vé, thanh toán |
| **Trang Đăng nhập / Đăng ký** | [http://localhost:5173/login](http://localhost:5173/login) | Xác thực tài khoản Khách hàng / Soát vé |
| **Giao diện Soát vé QR (Staff)** | [http://localhost:5173/staff/check-in](http://localhost:5173/staff/check-in) | Quét mã vé điện tử tại cổng sự kiện |
| **Backend API Host** | [http://localhost:8080](http://localhost:8080) | Cổng Minimal APIs chính của backend |
| **OpenAPI Specification** | [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json) | Tài liệu OpenAPI JSON tự động sinh |
| **Health Check** | [http://localhost:8080/health](http://localhost:8080/health) | Kiểm tra trạng thái máy chủ Backend |

---

### Các lệnh kiểm thử hệ thống

- **Kiểm tra ranh giới kiến trúc & tích hợp Backend:**
  ```bash
  dotnet test backend/Ticketing.slnx
  ```
- **Kiểm tra Typecheck Frontend:**
  ```bash
  cd frontend && npm run typecheck
  ```
- **Chạy kiểm thử E2E tự động (Playwright):**
  ```bash
  cd frontend && npm run test:e2e
  ```

---

## 7. Tài liệu dự án liên quan

- [Bảng phân công nhiệm vụ nhóm 5 thành viên (Bản rút gọn)](BANG_PHAN_CONG_NHIEM_VU.md)
- [Bảng phân chia công việc chi tiết (Kế hoạch 12 tuần & RACI)](PHAN_CONG_CONG_VIEC.md)
- [Đặc tả kiến trúc chi tiết (ARCHITECTURE.md)](ARCHITECTURE.md)
- [Kế hoạch khởi tạo kiến trúc Modular Monolith](Kế%20hoạch%20khởi%20tạo%20kiến%20trúc%20Modular%20Monolith%202%20project.md)
- [Báo cáo Audit kiến trúc & Danh sách lỗi (note.md)](note.md)


---
