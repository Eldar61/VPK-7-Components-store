using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PcAssembly.Api.Data;
namespace PcAssembly.Api.Services;

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class SessionService(AssemblyDb db, IHttpContextAccessor accessor)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<UserAccount> RequireUser(CancellationToken ct)
    {
        var header = accessor.HttpContext!.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) throw new ApiException(401, "AUTH_REQUIRED", "Войдите в аккаунт.");
        var tokenHash = Hash(header[7..]);
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == tokenHash && s.ExpiresAt > DateTimeOffset.UtcNow, ct);
        if (session is null) throw new ApiException(401, "SESSION_EXPIRED", "Сессия истекла. Войдите снова.");
        return await db.Users.FindAsync([session.UserId], ct) ?? throw new ApiException(401, "AUTH_REQUIRED", "Аккаунт не найден.");
    }
    public async Task<AuthResponse> Authenticate(Credentials input, bool register, CancellationToken ct)
    {
        var login = (input.Login ?? "").Trim().ToLowerInvariant();
        if (login.Length is < 3 or > 80 || string.IsNullOrEmpty(input.Password) || input.Password.Length is < 10 or > 128)
            throw new ApiException(400, "CREDENTIALS_INVALID", "Логин: 3–80 символов. Пароль: 10–128 символов.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login, ct);
        var hasher = new PasswordHasher<UserAccount>();
        if (register)
        {
            if (user is not null) throw new ApiException(409, "LOGIN_EXISTS", "Этот логин уже занят.");
            user = new() { Login = login };
            user.PasswordHash = hasher.HashPassword(user, input.Password);
            db.Users.Add(user);
        }
        else if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            throw new ApiException(401, "CREDENTIALS_INVALID", "Неверный логин или пароль.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = DateTimeOffset.UtcNow.AddHours(12);
        db.Sessions.Add(new() { TokenHash = Hash(token), UserId = user!.Id, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        return new(token, expires, new(user.Id, user.Login));
    }
}
public record Credentials(string Login, string Password);
public record UserView(Guid Id, string Login);
public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserView User);
