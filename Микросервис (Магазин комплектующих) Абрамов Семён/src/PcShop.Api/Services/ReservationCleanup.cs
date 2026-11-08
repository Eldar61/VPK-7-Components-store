using PcShop.Api.Data;
namespace PcShop.Api.Services;
public sealed class ReservationCleanup(IServiceScopeFactory scopes,ILogger<ReservationCleanup> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
  try {while(await timer.WaitForNextTickAsync(stoppingToken)){try {await using var scope=scopes.CreateAsyncScope();var tx=scope.ServiceProvider.GetRequiredService<Transactions>();var commerce=scope.ServiceProvider.GetRequiredService<CommerceService>();await tx.Run(async()=>{await commerce.Expire(stoppingToken);return true;},stoppingToken);}catch(Exception e)when(!stoppingToken.IsCancellationRequested){logger.LogWarning(e,"Reservation cleanup failed; availability still excludes expired reservations");}}}
  catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){}
 }
}

