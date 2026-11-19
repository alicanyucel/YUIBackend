using FluentValidation;
using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Customers;

public sealed record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt);

public sealed record GetCustomersQuery(int PageNumber = 1, int PageSize = 20, Guid? CustomerId = null)
    : IQuery<PagedResult<CustomerDto>>;

public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetCustomersQueryHandler(
    ICustomerRepository repository,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    public async Task<Result<PagedResult<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var result = await cache.GetOrCreateAsync("customers", new { request, tenantId }, async () =>
        {
            var page = request.CustomerId is Guid customerId
                ? await repository.GetPageByIdAsync(customerId, request.PageNumber, request.PageSize, cancellationToken)
                : await repository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
            return new PagedResult<CustomerDto>(page.Items.Select(ToDto).ToList(),
                page.TotalCount, page.PageNumber, page.PageSize);
        }, cancellationToken);
        return Result.Success(result);
    }

    internal static CustomerDto ToDto(Customer customer) => new(
        customer.Id, customer.FirstName, customer.LastName, customer.Email,
        customer.PhoneNumber, customer.IsActive, customer.CreatedAt);
}

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto>;

internal sealed class GetCustomerByIdQueryHandler(
    ICustomerRepository repository,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetCustomerByIdQuery, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var page = await cache.GetOrCreateAsync("customers", new { request.Id, tenantId }, async () =>
        {
            var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
            var items = customer is null
                ? Array.Empty<CustomerDto>()
                : [GetCustomersQueryHandler.ToDto(customer)];
            return new PagedResult<CustomerDto>(items, items.Length, 1, 1);
        }, cancellationToken);
        return page.Items.Count == 0
            ? Result.Failure<CustomerDto>(Error.NotFound(nameof(Customer)))
            : Result.Success(page.Items[0]);
    }
}

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber) : ICommand<Guid>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
    }
}

internal sealed class CreateCustomerCommandHandler(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
            return Result.Failure<Guid>(Error.Conflict(nameof(Customer)));

        var customer = new Customer(request.FirstName.Trim(), request.LastName.Trim(),
            email, request.PhoneNumber?.Trim());
        await repository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success(customer.Id);
    }
}

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool? IsActive = null) : ICommand;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
    }
}

internal sealed class UpdateCustomerCommandHandler(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<UpdateCustomerCommand>
{
    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return Result.Failure(Error.NotFound(nameof(Customer)));

        var email = request.Email.Trim();
        var existing = await repository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null && existing.Id != customer.Id)
            return Result.Failure(Error.Conflict(nameof(Customer)));

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.PhoneNumber = request.PhoneNumber?.Trim();
        if (request.IsActive is bool isActive)
            customer.IsActive = isActive;
        customer.MarkUpdated();
        repository.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteCustomerCommand(Guid Id) : ICommand;

internal sealed class DeleteCustomerCommandHandler(
    ICustomerRepository repository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<DeleteCustomerCommand>
{
    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return Result.Failure(Error.NotFound(nameof(Customer)));

        if ((await accountRepository.GetByCustomerIdAsync(customer.Id, cancellationToken)).Any())
            return Result.Failure(Error.Validation(nameof(Customer), "Customer with accounts cannot be deleted."));

        customer.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success();
    }
}
