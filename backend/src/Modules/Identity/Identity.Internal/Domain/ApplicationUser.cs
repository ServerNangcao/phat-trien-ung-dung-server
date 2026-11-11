using Microsoft.AspNetCore.Identity;

namespace Identity.Internal.Domain;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private ApplicationUser() { }

    public static ApplicationUser Create(string email, string fullName, string? phoneNumber = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return user;
    }
}
