using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Modules.Orders.Infrastructure.Outbox;
using Ticketing.Modules.Orders.Infrastructure.Data;
using Ticketing.Modules.Orders.Infrastructure.Payment;
using Ticketing.Modules.Orders.Infrastructure.Ports;
using Ticketing.Modules.Orders.Infrastructure.Repositories;
using Ticketing.SharedKernel;
using MediatR;

namespace Ticketing.Modules.Orders.Api;

public static class OrdersModuleRegistration
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));

        // Public module API consumed by other modules (Events' revenue dashboard, FR-23).
        services.AddScoped<Contracts.IOrdersModuleApi, OrdersModuleApi>();

        // DbContext registration lives in the composition root (Program.cs / TV1's host),
        // which chooses the provider (Npgsql or InMemory for dev/test).

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutbox, OutboxRepository>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddHttpClient<ITicketingModuleApi, HttpTicketingModuleApi>(client =>
        {
            var baseUrl = configuration.GetValue<string>("Ticketing:BaseUrl");
            client.BaseAddress = new Uri(baseUrl ?? "http://localhost:5003");
        });

        services.Configure<VnPayOptions>(configuration.GetSection("Payment:VnPay"));
        services.AddScoped<VnPayPaymentService>();

        // MoMo credentials are validated lazily on first use, so machines that never pay
        // through MoMo can still start and run the VNPay flow.
        services.Configure<MoMoOptions>(configuration.GetSection("Payment:MoMo"));
        services.AddHttpClient();
        services.AddScoped<MoMoPaymentService>();

        return services;
    }
}
