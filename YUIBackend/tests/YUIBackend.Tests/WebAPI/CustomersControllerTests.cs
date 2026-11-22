using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyProject.Domain.Abstractions;
using MyProject.WebAPI.Authorization;
using MyProject.WebAPI.Controllers;
using NSubstitute;
using YUIBackend.Application.Features.Customers.UpdateCustomers;

namespace MyProject.Tests.WebAPI;

public sealed class CustomersControllerTests
{
    [Fact]
    public async Task CustomerCanUpdateOwnProfileButCannotChangeActivityStatus()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UpdateCustomerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var resourceAccess = Substitute.For<ICustomerResourceAccess>();
        resourceAccess.IsAdministratorAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(false);
        resourceAccess.CanAccessCustomerAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var controller = CreateController(sender, resourceAccess);
        var customerId = Guid.NewGuid();
        var request = new UpdateCustomerCommand(
            customerId, "Ada", "Yilmaz", "ada@example.test", null, IsActive: false);

        var result = await controller.Update(customerId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await sender.Received(1).Send(
            Arg.Is<UpdateCustomerCommand>(command =>
                command.Id == customerId && command.FirstName == "Ada" && command.IsActive == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CustomerCannotUpdateAnotherCustomersProfile()
    {
        var sender = Substitute.For<ISender>();
        var resourceAccess = Substitute.For<ICustomerResourceAccess>();
        resourceAccess.IsAdministratorAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(false);
        resourceAccess.CanAccessCustomerAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var controller = CreateController(sender, resourceAccess);
        var customerId = Guid.NewGuid();
        var request = new UpdateCustomerCommand(
            customerId, "Ada", "Yilmaz", "ada@example.test", null);

        var result = await controller.Update(customerId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await sender.DidNotReceive().Send(
            Arg.Any<UpdateCustomerCommand>(), Arg.Any<CancellationToken>());
    }

    private static CustomersController CreateController(ISender sender, ICustomerResourceAccess resourceAccess)
    {
        var controller = new CustomersController(sender, resourceAccess);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("Customer"))
            }
        };
        return controller;
    }
}
