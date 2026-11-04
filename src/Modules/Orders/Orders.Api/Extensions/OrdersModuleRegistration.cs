using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core.EventBus;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Modules.Orders.Infrastructure.Payment;
using Ticketing.Modules.Orders.Infrastructure.Repositories;
using Ticketing.Modules.Orders.Contracts;
using MediatR;

namespace Ticketing.Modules.Orders.Api;

public static class OrdersModuleRegistration
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IEventBus, EventBus>();
        services.Configure<VnPayOptions>(configuration.GetSection("Payment:VnPay"));
        services.AddScoped<VnPayPaymentService>();

        return services;
    }
}