using FluentValidation;
using Identity.Internal.Application.DTOs;

namespace Identity.Internal.Application.Validators;

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Mã xác nhận không được để trống.")
            .Matches(@"^\s*\d{6}\s*$").WithMessage("Mã xác nhận phải gồm đúng 6 chữ số.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .MaximumLength(128).WithMessage("Mật khẩu mới không được vượt quá 128 ký tự.");
    }
}
