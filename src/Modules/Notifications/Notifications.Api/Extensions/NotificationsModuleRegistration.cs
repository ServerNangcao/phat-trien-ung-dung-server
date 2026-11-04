using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Modules.Notifications.Application.Handlers;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Modules.Notifications.Infrastructure.Inbox;
using Ticketing.Modules.Notifications.Infrastructure.Repositories;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Api;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TicketIssuedHandler).Assembly));

        // DbContext registration lives in the composition root (Program.cs / TV1's host),
        // which chooses the provider (Npgsql or InMemory for dev/test).

        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<IInbox, InboxRepository>();
        services.AddScoped<EmailRetryProcessor>();
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
