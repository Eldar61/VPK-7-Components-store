$ErrorActionPreference='Stop'
Write-Warning 'HTTP-проверка создаёт тестовые аккаунты, товары и покупки. Используйте отдельную локальную тестовую БД.'
Push-Location (Split-Path $PSScriptRoot -Parent)
try { & node 'scripts/integration.mjs'; if ($LASTEXITCODE -ne 0) { throw 'HTTP-проверка завершилась ошибкой.' } } finally { Pop-Location }

