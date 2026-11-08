using System.Net.Http.Json;
using PcShop.Domain;
namespace PcShop.Api.Services;
public sealed class AssemblyClient(HttpClient client)
{
 public async Task<CompatibilityResult> Check(CheckRequest request,CancellationToken ct)
 {
  try
  {
   using var response=await client.PostAsJsonAsync("api/compatibility/check",request,Json.Options,ct);
   if(!response.IsSuccessStatusCode)throw new ShopException((int)response.StatusCode>=500?503:502,"ASSEMBLY_RESPONSE","Сборщик отклонил проверку. Проверьте адрес, ключ и контракт.");
   var result=await response.Content.ReadFromJsonAsync<CompatibilityResult>(Json.Options,ct);
   if(result==null||result.Errors==null||result.Warnings==null||result.Compatible&&(result.Errors.Count>0||result.Power==null||result.Power.TotalPowerW<=0||result.Power.RequiredPsuW<=0))throw new ShopException(502,"ASSEMBLY_JSON","Некорректный результат проверки сборщика.");
   return result;
  }
  catch(HttpRequestException){throw new ShopException(503,"ASSEMBLY_UNAVAILABLE","Сервис сборки недоступен. Совместимость не подтверждена.");}
  catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new ShopException(503,"ASSEMBLY_TIMEOUT","Сборщик не ответил вовремя. Совместимость не подтверждена.");}
  catch(System.Text.Json.JsonException){throw new ShopException(502,"ASSEMBLY_JSON","Сборщик вернул неверный JSON.");}
 }
}

