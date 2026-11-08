using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Domain;
namespace PcShop.Api.Services;
public record Credentials(string Login,string Password);
public record UserView(Guid Id,string Login,string Role,CustomerType CustomerType);
public record AuthResponse(string Token,DateTimeOffset ExpiresAt,UserView User);
public sealed class AuthService(ShopDb db,IHttpContextAccessor accessor,TimeProvider time)
{
 public static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
 public async Task<AuthResponse> Authenticate(Credentials input,bool register,CancellationToken ct)
 {
  var login=(input.Login??"").Trim().ToLowerInvariant();var password=input.Password??"";
  if(login.Length is <3 or >80||password.Length is <8 or >200)throw new ShopException(400,"CREDENTIALS_INVALID","Логин 3–80 символов; пароль 8–200 символов.");
  var user=await db.Users.SingleOrDefaultAsync(x=>x.Login==login,ct);var hasher=new PasswordHasher<UserAccount>();
  if(register) {if(user!=null)throw new ShopException(409,"LOGIN_EXISTS","Логин уже занят.");user=new(){Login=login};user.PasswordHash=hasher.HashPassword(user,password);db.Users.Add(user);}
  else if(user==null||hasher.VerifyHashedPassword(user,user.PasswordHash,password)==PasswordVerificationResult.Failed)throw new ShopException(401,"LOGIN_FAILED","Неверный логин или пароль.");
  var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var expires=time.GetUtcNow().AddHours(12);
  db.Sessions.Add(new(){TokenHash=Hash(token),UserId=user!.Id,ExpiresAt=expires});await db.SaveChangesAsync(ct);return new(token,expires,View(user));
 }
 public async Task<UserAccount> Require(CancellationToken ct)
 {
  var header=accessor.HttpContext!.Request.Headers.Authorization.ToString();
  if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)||header.Length>200)throw new ShopException(401,"SESSION_REQUIRED","Войдите в аккаунт магазина.");
  var hash=Hash(header[7..]);var session=await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash&&x.ExpiresAt>time.GetUtcNow(),ct);
  return session==null?throw new ShopException(401,"SESSION_EXPIRED","Сессия завершена. Войдите снова."):await db.Users.SingleAsync(x=>x.Id==session.UserId,ct);
 }
 public async Task<UserAccount> Admin(CancellationToken ct) {var u=await Require(ct);if(u.Role!="ADMIN")throw new ShopException(403,"ADMIN_REQUIRED","Доступ только администратору.");return u;}
 public async Task Logout(CancellationToken ct) {await Require(ct);var token=accessor.HttpContext!.Request.Headers.Authorization.ToString()[7..];await db.Sessions.Where(x=>x.TokenHash==Hash(token)).ExecuteDeleteAsync(ct);}
 public static UserView View(UserAccount u)=>new(u.Id,u.Login,u.Role,u.CustomerType);
}

