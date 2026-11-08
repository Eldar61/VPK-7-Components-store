# HTTP-контракт сервиса сборки ПК

Версия начального контракта: 1. Все тела — JSON, имена полей camelCase, enum — строки `CPU`, `MOTHERBOARD`, `RAM`, `GPU`, `PSU`, `CASE`, `STORAGE`. ID — UUID; деньги — decimal RUB с двумя знаками; длины — целые миллиметры, мощности — ватты; время — ISO 8601 с UTC offset. Не передавайте числовые enum.

Схемы входов/выходов генерируются API в `/swagger/v1/swagger.json`. В `openapi.json` находится снимок, полученный с работающего сервиса. Общий формат ошибок: `{"code":"SO...","message":"Понятное сообщение"}`. Технические ошибки внутри CompatibilityResult содержат `code`, `message`, `productIds`.

## Внешний пользователь API сборки

| Метод и маршрут | Вход | Выход / правила |
|---|---|---|
| POST /api/auth/register | login, password | token, expiresAt, user; новый аккаунт |
| POST /api/auth/login | login, password | такой же ответ |
| POST /api/auth/logout | Bearer token | 204, сессия удалена |
| GET /api/auth/me | Bearer token | id, login |
| GET /api/products | — | товары от API магазина |
| GET /api/configurations | Bearer token | только конфигурации владельца |
| POST /api/configurations | name, budgetRub, items | 201, новый черновик |
| GET /api/configurations/{id} | Bearer token | ConfigurationView |
| PUT /api/configurations/{id} | name, budgetRub, items, version | обновлённый черновик; 409 при конфликте версии или SAVED |
| POST /api/configurations/{id}/evaluate | — | Evaluation |
| POST /api/configurations/{id}/save | — | неизменяемая ConfigurationView; повтор не дублирует статистику |
| POST /api/configurations/{id}/clone | — | новый черновик с составом исходного |
| GET /api/configurations/{id}/recommendations | — | до 3 проверенных замен одной детали |
| POST /api/configurations/{id}/cart | — | создать/восстановить связанную корзину магазина |
| GET /api/configurations/{id}/cart | — | актуальное состояние магазинной корзины |
| POST /api/configurations/{id}/cart/{action} | quoteId, operationKey | action: quote, reserve, confirm, cancel |
| GET /api/statistics/price-buckets | — | публичные fromRub, toRub, count |

Все операции над конфигурациями требуют `Authorization: Bearer <token>`. Токен хранится в sessionStorage сайта, сервер хранит его SHA-256. Пользователь не может задавать userId. Чужой ID возвращает 404. Для аккаунтов действует ограничение 15 запросов регистрации/входа в минуту на IP. Максимальное тело запроса — 256 КБ.

```json
{
  "name": "Моя сборка",
  "budgetRub": 120000,
  "version": 0,
  "items": [
    {"productId":"00000000-0000-0000-0000-000000000001","category":"CPU","quantity":1}
  ]
}
```

`ConfigurationView`: `id`, `name`, `budgetRub`, `items`, `status`, `version`, `createdAt`, `updatedAt`, `savedAt`, `savedPriceRub`, `cartId`, `evaluation`, `products`. Последние продукты — снимок полученных при проверке характеристик. До проверки `evaluation` и `products` могут быть null. `Evaluation` содержит `technical`, `quote`, nullable `available`, nullable `withinBudget`, `externalError`, `checkedAt`. Отсутствующая цена не заменяется нулём.

## Чистая проверка для магазина Семёна

`POST /api/compatibility/check` требует `X-Service-Key`, принимает `{items, products}` и возвращает `{compatible, errors, warnings, power}`. Метод не вызывает сеть и не записывает конфигурации/статистику. Это полная проверка ПК; неполный набор возвращает `CATEGORY_REQUIRED` — магазин может показать, что для полного ПК не хватает деталей. Техническая несовместимость — HTTP 200 с `compatible:false`; ошибки формата — 400; неверный межсервисный ключ — 401.

`power`: `totalPowerW`, `requiredPsuW`, `slices[{productId,name,watts,percent}]`. Если мощности достоверно рассчитать нельзя, `power:null`. Ошибки: PRODUCT_NOT_FOUND, PRODUCT_INACTIVE, CATEGORY_MISMATCH, CATEGORY_REQUIRED, SINGLE_COMPONENT_REQUIRED, INVALID_QUANTITY, DUPLICATE_ITEM, SPEC_MISSING, SOCKET_MISMATCH, RAM_TYPE_MISMATCH, RAM_SLOTS_EXCEEDED, RAM_CAPACITY_EXCEEDED, GPU_SLOT_MISSING, GPU_TOO_LONG, GPU_POWER_CONNECTORS_MISSING, BOARD_CASE_MISMATCH, PSU_CASE_MISMATCH, STORAGE_PORTS_EXCEEDED, DRIVE_BAYS_EXCEEDED, STORAGE_SPEC_INVALID, PSU_INSUFFICIENT.

## Требуемый контракт API магазина

Все следующие запросы отправляются сервером сборщика с `X-Service-Key`. Внешнему покупателю не выдаётся ключ.

| Метод и маршрут | Вход | Выход |
|---|---|---|
| GET /api/products | необязательные `ids` через запятую | массив Product; неизвестные ID пропускаются |
| POST /api/quotes | `{items}` | Quote, без изменения складских остатков |
| POST /api/carts | `{externalConfigurationId,items,operationKey}` | ShopCart; повтор ключа возвращает ту же корзину |
| GET /api/carts/{id} | — | ShopCart |
| POST /api/carts/{id}/quote | CartAction | ShopCart с новым Quote |
| POST /api/carts/{id}/reserve | CartAction | ShopCart, RESERVED; all-or-nothing |
| POST /api/carts/{id}/confirm | CartAction | ShopCart, CONFIRMED |
| POST /api/carts/{id}/cancel | CartAction | ShopCart, CANCELLED |

`CartAction = {quoteId, operationKey}`. Срок Quote и резерва — 15 минут. Повтор одного action и operationKey не создаёт повторную операцию; на новом действии пользователь передаёт новый ключ. `ShopCart = {id,status,quote,reservationId,expiresAt}`. Состояния: DRAFT, RESERVED, CONFIRMED, CANCELLED, EXPIRED. 409 означает конфликт цены, остатка, состояния или срока. После 409 клиент запрашивает состояние/новое предложение и получает согласие покупателя на новую цену. После тайм-аута сначала запрашивается состояние, затем при необходимости повторяется тот же ключ операции.

`Quote = {id, expiresAt, lines, totalRub, available}`. Каждая линия: `{productId, quantity, unitPriceRub, discountPercent, totalRub, available}`. Применяется одна наибольшая скидка; для гостевой внешней корзины тип STANDARD. `totalRub` линии = round(unitPriceRub × quantity × (1 − discountPercent/100), 2), midpoint rounding away from zero. Общая сумма равна сумме линий. Сборщик проверяет состав и арифметику ответа; отрицательные суммы, некорректные скидки, несовпадение позиций и истёкшие ответы отклоняются.

`Product = {id,name,category,active,priceRub,availableQuantity,specs}`. `availableQuantity` — доступный, а не физический остаток. Сборщик не записывает его в собственный склад.

| Категория | Обязательные specs |
|---|---|
| CPU | socket, estimatedPowerW |
| MOTHERBOARD | socket, memoryType, memorySlots, maxMemoryGb, pcieX16Slots, formFactor, sataPorts, m2Slots, estimatedPowerW |
| RAM | memoryType, moduleCapacityGb, estimatedPowerW |
| GPU | gpuLengthMm, requiredPowerConnectors, estimatedPowerW |
| PSU | ratedPowerW, formFactor, powerConnectors |
| CASE | supportedMotherboardForms[], supportedPsuForms[], maxGpuLengthMm, driveBays, estimatedPowerW (мощность вентиляторов, допустим 0) |
| STORAGE | storageInterface (SATA/M2), driveForm (2.5/3.5/M2), estimatedPowerW |

Словари разъёмов: `{"PCIE_8PIN":2,"12V_2X6":1}`. Нужны отдельные одинаковые ключи на GPU/PSU; переходники автоматически не предполагаются. Словарь мест накопителей: `{"2.5":2,"3.5":2}`; отсутствующий ключ означает 0 мест. M.2 не занимает место 2.5/3.5 в корпусе. Один товар RAM — **один модуль**, quantity — число модулей. Поля, неприменимые к категории, могут быть null. Socket/formFactor/memoryType сравниваются без учёта регистра и внешних пробелов. Конкретные строки enum интерфейса накопителя и ключи словарей — как в таблице.

Текущая модель не рассчитывает пропускную способность PCIe, BIOS-поддержку CPU, разгону RAM, охлаждение CPU и взаимное отключение M.2/SATA портов. Эти дополнительные аппаратные правила требуют расширения характеристик и могут стать экзаменационной доработкой. Граница гарантии текущего алгоритма — явно перечисленные свойства.
