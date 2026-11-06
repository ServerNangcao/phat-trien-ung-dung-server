using FluentAssertions;
using NetArchTest.Rules;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Contracts.IntegrationEvents;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure;

namespace Ticketing.Tests.Orders.Architecture;

/// <summary>
/// Enforces the modular-monolith layering invariants for the Orders module:
/// Domain depends on nothing but SharedKernel; Contracts are pure DTOs;
/// Application never touches Infrastructure or the API layer.
/// </summary>
public class ModuleBoundaryTests
{
    [Fact]
    public void OrdersDomain_DependsOnlyOnSharedKernel()
    {
        var result = Types.InAssembly(typeof(Order).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Orders.Application",
                "Ticketing.Modules.Orders.Infrastructure",
                "Ticketing.Modules.Orders.Api",
                "MediatR",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void OrdersApplication_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(CreateOrderCommand).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Orders.Infrastructure",
                "Ticketing.Modules.Orders.Api",
                "Ticketing.Infrastructure.Core")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void OrdersContracts_ArePureDtosWithoutModuleDependencies()
    {
        var result = Types.InAssembly(typeof(OrderPaidIntegrationEvent).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Ticketing.Modules.Orders.Domain",
                "Ticketing.Modules.Orders.Application",
                "Ticketing.Modules.Orders.Infrastructure",
                "Ticketing.Modules.Orders.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }

    [Fact]
    public void OrdersInfrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(OrdersDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ticketing.Modules.Orders.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: string.Join("; ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>()));
    }
}
