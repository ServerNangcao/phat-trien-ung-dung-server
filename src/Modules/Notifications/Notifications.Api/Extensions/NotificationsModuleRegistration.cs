using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Ticketing.Modules.Notifications.Application.Handlers;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Modules.Notifications.Infrastructure.Repositories;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Api;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TicketIssuedHandler).Assembly));

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IEmailRepository, EmailRepository>();
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}