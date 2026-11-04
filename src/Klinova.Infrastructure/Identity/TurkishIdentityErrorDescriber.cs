using Microsoft.AspNetCore.Identity;

namespace Klinova.Infrastructure.Identity;

/// <summary>Turkish messages for ASP.NET Core Identity validation errors.</summary>
public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Beklenmeyen bir hata oluştu.");

    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Kayıt başka bir kullanıcı tarafından değiştirildi. Lütfen sayfayı yenileyin.");

    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Mevcut şifre hatalı.");

    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Geçersiz işlem anahtarı.");

    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"'{userName}' geçersiz bir kullanıcı adı. Yalnızca harf, rakam ve . _ - @ kullanılabilir.");

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"'{email}' geçersiz bir e-posta adresi.");

    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"'{userName}' kullanıcı adı zaten kullanılıyor.");

    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"'{email}' e-posta adresi zaten kullanılıyor.");

    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), $"'{role}' rolü zaten var.");

    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), $"Kullanıcı zaten '{role}' rolünde.");

    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), $"Kullanıcı '{role}' rolünde değil.");

    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Şifre en az {length} karakter olmalıdır.");

    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Şifre en az bir rakam içermelidir.");

    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Şifre en az bir küçük harf içermelidir.");

    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Şifre en az bir büyük harf içermelidir.");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Şifre en az bir özel karakter içermelidir.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Şifre en az {uniqueChars} farklı karakter içermelidir.");

    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "Bu kullanıcı için hesap kilitleme etkin değil.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
