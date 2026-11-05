using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using global::Ticketing.Infrastructure.Core;
using global::Ticketing.SharedKernel;
using global::Ticketing.Modules.Events.Contracts;
using global::Ticketing.Modules.Ticketing.Contracts;
using global::Ticketing.Modules.Ticketing.Internal.Domain;

namespace Ticketing.Modules.Ticketing.Internal.Application;

internal sealed class TicketingService(Db db,IEventsModuleApi events,QrSigner signer) : ITicketingModuleApi
{
    internal static ReservationDto ReadReservation(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetInt32(4),r.GetDecimal(5),r.GetFieldValue<DateTimeOffset>(6),r.GetString(7));
    internal const string ReservationColumns = "id,event_id,ticket_type_id,customer_id,quantity,unit_price,expires_at,status";
    public async Task<IReadOnlyList<TicketTypeDto>> GetTypesAsync(Guid eventId,CancellationToken ct=default)
    {
        await using var c=await db.OpenAsync(ct);
        await using var cmd=Db.Command(c,null,"SELECT id,event_id,name,price,capacity,available,max_per_order FROM ticketing.ticket_types WHERE event_id=@e ORDER BY price",("e",eventId));
        await using var r=await cmd.ExecuteReaderAsync(ct);var items=new List<TicketTypeDto>();
        while(await r.ReadAsync(ct)) items.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetDecimal(3),r.GetInt32(4),r.GetInt32(5),r.GetInt32(6)));
        return items;
    }
    public async Task<ReservationDto?> GetReservationAsync(Guid id,CancellationToken ct=default)
    {
        await using var c=await db.OpenAsync(ct);await using var cmd=Db.Command(c,null,$"SELECT {ReservationColumns} FROM ticketing.reservations WHERE id=@id",("id",id));
        await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadReservation(r):null;
    }
    // [TV4 - Đồng]: Chống trùng lặp (Idempotency) - Tìm reservation theo idempotency_key
    private async Task<ReservationDto?> ByKey(Guid customerId,string key,CancellationToken ct)
    {
        await using var c=await db.OpenAsync(ct);await using var cmd=Db.Command(c,null,$"SELECT {ReservationColumns} FROM ticketing.reservations WHERE customer_id=@u AND idempotency_key=@k",("u",customerId),("k",key));
        await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadReservation(r):null;
    }

    /// <summary>
    /// [TV4 - Đồng]: Trừ kho nguyên tử & Giữ chỗ 10 phút.
    /// Đảm bảo tính nhất quán cao (high concurrency), không bao giờ bị bán âm vé (oversell).
    /// </summary>
    public async Task<ReservationDto> ReserveAsync(Guid customerId,Guid eventId,Guid ticketTypeId,int quantity,string idempotencyKey,CancellationToken ct=default)
    {
        if(!Guid.TryParse(idempotencyKey,out _)) throw new ApiException(400,"idempotency_key","Idempotency-Key phải là UUID.");
        if(quantity is <1 or >20) throw new ApiException(400,"quantity","Số vé mỗi đơn phải từ 1 đến 20.");
        
        // [TV4 - Đồng]: Chống trùng lặp - Xác thực tính toàn vẹn của payload idempotent đã xử lý trước đó
        void Validate(ReservationDto previous) { if(previous.EventId!=eventId||previous.TicketTypeId!=ticketTypeId||previous.Quantity!=quantity) throw new ApiException(409,"idempotency_conflict","Khóa yêu cầu đã dùng với nội dung khác."); }
        var prior=await ByKey(customerId,idempotencyKey,ct);if(prior is not null){Validate(prior);return prior;}
        var ev=await events.GetAsync(eventId,ct);
        if(ev is null||ev.Status!="Published"||ev.StartsAtUtc<=DateTimeOffset.UtcNow) throw new ApiException(409,"not_on_sale","Sự kiện chưa mở bán hoặc đã bắt đầu.");
        try
        {
            await using var c=await db.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
            // [TV4 - Đồng]: Trừ kho nguyên tử - Atomic UPDATE trực tiếp tại DB cấp độ câu lệnh SQL
            await using var stock=Db.Command(c,tx,"""
                UPDATE ticketing.ticket_types SET available=available-@q,version=version+1
                WHERE id=@t AND event_id=@e AND available>=@q AND max_per_order>=@q RETURNING price
                """,("q",quantity),("t",ticketTypeId),("e",eventId));
            var price=await stock.ExecuteScalarAsync(ct);
            if(price is null) throw new ApiException(409,"insufficient_stock","Không đủ vé hoặc vượt giới hạn mỗi đơn.");
            
            // [TV4 - Đồng]: Set thời gian 10 phút chờ (TTL 10 phút cho reservation)
            var item=new ReservationDto(Guid.NewGuid(),eventId,ticketTypeId,customerId,quantity,(decimal)price,DateTimeOffset.UtcNow.AddMinutes(10),"Active");
            await using var insert=Db.Command(c,tx,"""
                INSERT INTO ticketing.reservations(id,event_id,ticket_type_id,customer_id,quantity,unit_price,expires_at,status,idempotency_key)
                VALUES(@id,@e,@t,@u,@q,@p,@x,'Active',@k)
                """,("id",item.Id),("e",eventId),("t",ticketTypeId),("u",customerId),("q",quantity),("p",item.UnitPrice),("x",item.ExpiresAtUtc),("k",idempotencyKey));
            await insert.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return item;
        }
        catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.UniqueViolation)
        { var previous=await ByKey(customerId,idempotencyKey,ct);if(previous is null)throw;Validate(previous);return previous; }
        catch(ApiException ex) when(ex.Code=="insufficient_stock")
        {
            // An identical concurrent request may have taken the final available seat.
            // The failed UPDATE waited for it to commit, so retry its persisted response.
            var previous=await ByKey(customerId,idempotencyKey,ct);if(previous is null)throw;Validate(previous);return previous;
        }
    }
    public async Task MarkPaymentPendingAsync(Guid id,Guid customerId,CancellationToken ct=default)
    {
        await using var c=await db.OpenAsync(ct);await using var cmd=Db.Command(c,null,"UPDATE ticketing.reservations SET status='PaymentPending' WHERE id=@id AND customer_id=@u AND status IN ('Active','PaymentPending') AND expires_at>now()",("id",id),("u",customerId));
        if(await cmd.ExecuteNonQueryAsync(ct)==0)throw new ApiException(409,"reservation_expired","Lượt giữ vé đã hết hạn.");
    }
    public async Task ReleaseAsync(Guid id,Guid customerId,CancellationToken ct=default)
    {
        await using var c=await db.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        Guid typeId;int qty;
        await using(var cmd=Db.Command(c,tx,"UPDATE ticketing.reservations SET status='Cancelled' WHERE id=@id AND customer_id=@u AND status IN ('Active','PaymentPending') RETURNING ticket_type_id,quantity",("id",id),("u",customerId)))
        {await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)){await r.DisposeAsync();await tx.CommitAsync(ct);return;}typeId=r.GetGuid(0);qty=r.GetInt32(1);}
        await using var add=Db.Command(c,tx,"UPDATE ticketing.ticket_types SET available=available+@q,version=version+1 WHERE id=@t",("q",qty),("t",typeId));await add.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
    /// <summary>
    /// [TV4 - Đồng]: Chống trùng lặp & Soát vé QR HMAC.
    /// Kiểm tra chữ ký mật mã HMAC-SHA256, trừ trạng thái 'Valid' -> 'Used' nguyên tử để chống quay vòng vé (double-redemption).
    /// </summary>
    public async Task<RedeemResult> RedeemTicketAsync(string qrPayload,Guid eventId,Guid staffId,CancellationToken ct=default)
    {
        if(!signer.TryVerify(qrPayload,eventId,out var id))return new("Invalid",null,null,null);
        var ev=await events.GetAsync(eventId,ct);
        if(ev is null||ev.Status!="Published"||ev.EndsAtUtc<DateTimeOffset.UtcNow)return new("Invalid",id,null,null);
        await using var c=await db.OpenAsync(ct);
        await using var tx=await c.BeginTransactionAsync(ct);
        RedeemResult? success=null;
        // [TV4 - Đồng]: Chống trùng lặp - Chỉ cho phép đổi vé duy nhất 1 lần khi status đang là 'Valid'
        await using(var cmd=Db.Command(c,tx,"UPDATE ticketing.tickets SET status='Used',used_at=now(),used_by=@s WHERE id=@id AND event_id=@e AND status='Valid' RETURNING ticket_type_name,used_at",("s",staffId),("id",id),("e",eventId)))
        {await using var r=await cmd.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct))success=new("Valid",id,r.GetString(0),r.GetFieldValue<DateTimeOffset>(1));}
        if(success is not null)
        {
            await Db.EnqueueAsync(c,tx,"ticketing","ticket.redeemed",new{ticketId=id,eventId,staffId,usedAtUtc=success.UsedAtUtc},ct);
            await tx.CommitAsync(ct);return success;
        }
        await tx.CommitAsync(ct);
        // [TV4 - Đồng]: Nếu vé đã được quét trước đó, trả về trạng thái AlreadyUsed để cảnh báo
        await using var find=Db.Command(c,null,"SELECT status,ticket_type_name,used_at FROM ticketing.tickets WHERE id=@id AND event_id=@e",("id",id),("e",eventId));
        await using var prior=await find.ExecuteReaderAsync(ct);if(!await prior.ReadAsync(ct))return new("Invalid",null,null,null);
        return new(prior.GetString(0)=="Used"?"AlreadyUsed":"Invalid",id,prior.GetString(1),prior.IsDBNull(2)?null:prior.GetFieldValue<DateTimeOffset>(2));
    }
    public async Task<int> GetIssuedCountAsync(Guid eventId,CancellationToken ct=default)
    {await using var c=await db.OpenAsync(ct);await using var cmd=Db.Command(c,null,"SELECT count(*)::int FROM ticketing.tickets WHERE event_id=@e",("e",eventId));return (int)(await cmd.ExecuteScalarAsync(ct))!;}
    internal async Task<IReadOnlyList<TicketDto>> GetMineAsync(Guid userId,CancellationToken ct)
    {
        await using var c=await db.OpenAsync(ct);await using var cmd=Db.Command(c,null,"SELECT id,event_id,order_id,customer_id,event_title,ticket_type_name,status,qr_payload,used_at FROM ticketing.tickets WHERE customer_id=@u ORDER BY order_id,sequence",("u",userId));
        await using var r=await cmd.ExecuteReaderAsync(ct);var items=new List<TicketDto>();while(await r.ReadAsync(ct))items.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.IsDBNull(8)?null:r.GetFieldValue<DateTimeOffset>(8)));return items;
    }
}

/// <summary>
/// [TV4 - Đồng]: Trả vé hết hạn (Periodic Reservation Expiry Worker).
/// Chạy nền mỗi 5 giây, quét các đơn giữ chỗ đã quá hạn 10 phút để tự động hoàn trả số lượng vé về kho sẵn có.
/// </summary>
internal sealed class ReservationExpiryWorker(IServiceScopeFactory scopes,ILogger<ReservationExpiryWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<Db>();
                await using var c=await db.OpenAsync(stoppingToken);await using var tx=await c.BeginTransactionAsync(stoppingToken);
                var rows=new List<(Guid Id,Guid Type,int Qty)>();
                // [TV4 - Đồng]: Trả vé hết hạn - Khóa các hàng hết hạn an toàn với SKIP LOCKED
                await using(var cmd=Db.Command(c,tx,"SELECT id,ticket_type_id,quantity FROM ticketing.reservations WHERE status IN ('Active','PaymentPending') AND expires_at<=now() ORDER BY expires_at LIMIT 100 FOR UPDATE SKIP LOCKED"))
                {await using var r=await cmd.ExecuteReaderAsync(stoppingToken);while(await r.ReadAsync(stoppingToken))rows.Add((r.GetGuid(0),r.GetGuid(1),r.GetInt32(2)));}
                foreach(var row in rows)
                {
                    // [TV4 - Đồng]: Cập nhật trạng thái 'Expired' và cộng trả lại kho vé available
                    await using var cmd=Db.Command(c,tx,"UPDATE ticketing.reservations SET status='Expired' WHERE id=@id; UPDATE ticketing.ticket_types SET available=available+@q,version=version+1 WHERE id=@t",("id",row.Id),("q",row.Qty),("t",row.Type));
                    await cmd.ExecuteNonQueryAsync(stoppingToken);await Db.EnqueueAsync(c,tx,"ticketing","reservation.expired",new{reservationId=row.Id},stoppingToken);
                }
                await tx.CommitAsync(stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Reservation expiry failed; next tick will retry.");}
        }
    }
}
