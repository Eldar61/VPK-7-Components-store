param([string]$BaseUrl='http://localhost:5080',[string]$ShopUrl='http://localhost:5081')
$ErrorActionPreference='Stop'
function Assert($condition,[string]$message){if(-not $condition){throw $message}}
function Request([string]$path,[string]$method='GET',$body=$null,$headers=@{}){
    $params=@{Uri="$BaseUrl$path";Method=$method;Headers=$headers;UseBasicParsing=$true}
    if($null-ne $body){$params.ContentType='application/json; charset=utf-8';$params.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 20))}
    try{$response=Invoke-WebRequest @params}catch{
        if($_.Exception.Response){$reader=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream());$detail=$reader.ReadToEnd();Write-Host "HTTP failure at $method $path : $detail"}
        throw
    }
    if($response.Content){$parsed=$response.Content|ConvertFrom-Json; foreach($entry in $parsed){Write-Output $entry}}
}
function ExpectStatus([int]$status,[scriptblock]$action){try{& $action|Out-Null;throw "Expected HTTP $status"}catch{if($_.Exception.Response){Assert ([int]$_.Exception.Response.StatusCode-eq $status) "Expected $status, got $($_.Exception.Response.StatusCode)"}else{throw}}}
$credentials=@{login="smoke_$([Guid]::NewGuid().ToString('N').Substring(0,12))";password='Test-password-123!'}
$auth=Request '/api/auth/register' 'POST' $credentials
$headers=@{Authorization="Bearer $($auth.token)"}
$products=@(Request '/api/products')
$items=@($products|Select-Object -First 7|ForEach-Object{@{productId=$_.id;category=$_.category;quantity=$(if($_.category-eq 'RAM'){2}else{1})}})
$before=@(Request '/api/statistics/price-buckets')|Measure-Object -Property count -Sum
$c=Request '/api/configurations' 'POST' @{name='Smoke build';budgetRub=120000;items=$items} $headers
$e=Request "/api/configurations/$($c.id)/evaluate" 'POST' $null $headers
Assert $e.technical.compatible 'Expected compatible build'
Assert ($e.technical.power.totalPowerW-eq 307) 'Unexpected power total'
Assert ($e.technical.power.requiredPsuW-eq 384) 'Unexpected PSU margin'
Assert ($e.available-and $e.withinBudget) 'Quote should be available and in budget'
$null=Request "/api/configurations/$($c.id)/save" 'POST' $null $headers
$null=Request "/api/configurations/$($c.id)/save" 'POST' $null $headers
$after=@(Request '/api/statistics/price-buckets')|Measure-Object -Property count -Sum
Assert ($after.Sum-eq ($before.Sum+1)) 'Repeated save must count once'
ExpectStatus 409 {Request "/api/configurations/$($c.id)" 'PUT' @{name='Mutation';budgetRub=100;items=$items;version=2} $headers}
$clone=Request "/api/configurations/$($c.id)/clone" 'POST' $null $headers
$newItems=@($items|ForEach-Object{$_.Clone()})
$newItems[0].productId=$products[7].id
$null=Request "/api/configurations/$($clone.id)" 'PUT' @{name='Bad socket';budgetRub=120000;items=$newItems;version=$clone.version} $headers
$bad=Request "/api/configurations/$($clone.id)/evaluate" 'POST' $null $headers
Assert (-not $bad.technical.compatible-and ($bad.technical.errors.code-contains 'SOCKET_MISMATCH')) 'Socket mismatch not detected'
$recommendations=@(Request "/api/configurations/$($clone.id)/recommendations" 'GET' $null $headers)
Assert ($recommendations.Count-gt 0) 'Expected a fully checked replacement for the incompatible CPU'
$cart=Request "/api/configurations/$($c.id)/cart" 'POST' $null $headers
$reserved=Request "/api/configurations/$($c.id)/cart/reserve" 'POST' @{quoteId=$cart.quote.id;operationKey='reserve-smoke'} $headers
$repeat=Request "/api/configurations/$($c.id)/cart/reserve" 'POST' @{quoteId=$cart.quote.id;operationKey='reserve-smoke'} $headers
Assert ($reserved.reservationId-eq $repeat.reservationId) 'Reservation must be idempotent'
$confirmed=Request "/api/configurations/$($c.id)/cart/confirm" 'POST' @{quoteId=$cart.quote.id;operationKey='confirm-smoke'} $headers
Assert ($confirmed.status-eq 'CONFIRMED') 'Expected confirmed cart'
$other=Request '/api/auth/register' 'POST' @{login="other_$([Guid]::NewGuid().ToString('N').Substring(0,12))";password='Other-password-123!'}
ExpectStatus 404 {Request "/api/configurations/$($c.id)" 'GET' $null @{Authorization="Bearer $($other.token)"}}
$fresh=Request '/api/configurations' 'POST' @{name='Outage draft';budgetRub=120000;items=$items} $headers
$serviceHeaders=@{'X-Service-Key'='development-only-service-key'}
try{
    Invoke-WebRequest -Uri "$ShopUrl/testing/unavailable/true" -Method POST -Headers $serviceHeaders -UseBasicParsing|Out-Null
    ExpectStatus 503 {Request "/api/configurations/$($fresh.id)/evaluate" 'POST' $null $headers}
    $kept=Request "/api/configurations/$($fresh.id)" 'GET' $null $headers
    Assert ($kept.status-eq 'DRAFT') 'Shop outage must preserve draft status'
    $pure=Request '/api/compatibility/check' 'POST' @{items=$items;products=$products} $serviceHeaders
    Assert $pure.compatible 'Pure compatibility endpoint must work while shop is down'
}finally{Invoke-WebRequest -Uri "$ShopUrl/testing/unavailable/false" -Method POST -Headers $serviceHeaders -UseBasicParsing|Out-Null}
$openapi=Request '/swagger/v1/swagger.json'
Assert (@($openapi.paths.PSObject.Properties).Count-gt 10) 'OpenAPI paths missing'
Write-Host 'PASS: registration, PostgreSQL persistence, validation, immutable save, histogram, ownership, checkout, idempotency, shop outage and pure API.'
