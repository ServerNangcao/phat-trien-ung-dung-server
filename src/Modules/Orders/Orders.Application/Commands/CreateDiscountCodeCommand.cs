using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Organizer/Admin creates a discount code (FR-16). The code is stored normalized, so
/// lookups from order creation are case-insensitive.
/// </summary>
public sealed record CreateDiscountCodeCommand(string Code, string Type, decimal Value,
    int? MaxRedemptions, DateTime? ExpiresAtUtc) : IRequest<Result<DiscountCodeDto>>;

public sealed record DiscountCodeDto(Guid Id, string Code, string Type, decimal Value,
    int? MaxRedemptions, int TimesUsed, DateTime? ExpiresAtUtc, bool IsActive);

public sealed class CreateDiscountCodeCommandHandler : IRequestHandler<CreateDiscountCodeCommand, Result<DiscountCodeDto>>
{
    private readonly IDiscountCodeRepository _discountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDiscountCodeCommandHandler(IDiscountCodeRepository discountRepository, IUnitOfWork unitOfWork)
    {
        _discountRepository = discountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DiscountCodeDto>> Handle(CreateDiscountCodeCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DiscountCode.DiscountType>(request.Type, ignoreCase: true, out var type))
            return Result<DiscountCodeDto>.Failure(Error.Validation(
                $"Discount type '{request.Type}' is invalid. Use '{nameof(DiscountCode.DiscountType.Percentage)}' or '{nameof(DiscountCode.DiscountType.FixedVnd)}'."));

        var existing = await _discountRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
            return Result<DiscountCodeDto>.Failure(Error.Conflict($"Discount code '{request.Code}' already exists."));

        var created = DiscountCode.Create(request.Code, type, request.Value, request.MaxRedemptions, request.ExpiresAtUtc);
        if (created.IsFailure)
            return Result<DiscountCodeDto>.Failure(created.Error);

        await _discountRepository.AddAsync(created.Value, cancellationToken);
        var commit = await _unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
            return Result<DiscountCodeDto>.Failure(commit.Error);

        var code = created.Value;
        return Result<DiscountCodeDto>.Success(new DiscountCodeDto(
            code.Id, code.Code, code.Type.ToString(), code.Value,
            code.MaxRedemptions, code.TimesUsed, code.ExpiresAtUtc, code.IsActive));
    }
}
