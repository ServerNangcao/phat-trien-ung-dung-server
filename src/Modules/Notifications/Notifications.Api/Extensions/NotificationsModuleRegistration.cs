using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Data;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Modules.Notifications.Infrastructure.Inbox;
using Ticketing.Modules.Notifications.Infrastructure.Repositories;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Api;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext registration lives in the composition root (Program.cs / TV1's host),
        // which chooses the provider (Npgsql or InMemory for dev/test).

        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<IInbox, InboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<EmailDispatcher>();
        services.AddScoped<IEmailDispatcher>(sp => sp.GetRequiredService<EmailDispatcher>());
        services.AddScoped<TicketEmailProcessor>();
        services.AddScoped<RefundEmailProcessor>();
        services.AddScoped<EmailRetryProcessor>();
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
