using FluentAssertions;
using NetArchTest.Rules;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Infrastructure;

namespace Ticketing.Tests.Notifications.Architecture;

/// <summary>
/// Enforces the modular-monolith layering invariants for the Notifications module.
/// </summary>
public class ModuleBoundaryTests
{
    [Fact]
    public void NotificationsDomain_DependsOnlyOnSharedKernel()
    {
        var result = Types.InAssembly(typeof(EmailHistory).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Notifications.Application",
                "Ticketing.Modules.Notifications.Infrastructure",
                "Ticketing.Modules.Notifications.Api",
                "MediatR",
                "Microsoft.EntityFrameworkCore",
                "MailKit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void NotificationsApplication_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(TicketEmailProcessor).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Notifications.Infrastructure",
                "Ticketing.Modules.Notifications.Api",
                "Ticketing.Infrastructure.Core",
                "MailKit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void NotificationsContracts_ArePureDtosWithoutModuleDependencies()
    {
        var result = Types.InAssembly(typeof(EmailHistoryDto).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Notifications.Domain",
                "Ticketing.Modules.Notifications.Application",
                "Ticketing.Modules.Notifications.Infrastructure",
                "Ticketing.Modules.Notifications.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void NotificationsInfrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(NotificationsDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ticketing.Modules.Notifications.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }
}
