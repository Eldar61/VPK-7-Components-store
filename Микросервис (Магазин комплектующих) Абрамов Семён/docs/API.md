# Контракт магазина версия 1

JSON camelCase; enum строками CPU/MOTHERBOARD/RAM/GPU/PSU/CASE/STORAGE; UUID; decimal RUB; даты UTC ISO 8601.

Межсервисные маршруты X-Service-Key совместимы со сборщиком: GET /api/products?ids=..., POST /api/quotes {items}, POST /api/carts {externalConfigurationId,items,operationKey}, GET /api/carts/{id}, POST /api/carts/{id}/{quote|reserve|confirm|cancel} {quoteId,operationKey}.

Product {id,name,category,active,priceRub,availableQuantity,specs}; Quote {id,expiresAt,lines,totalRub,available}; line {productId,quantity,unitPriceRub,discountPercent,totalRub,available}; ShopCart {id,status,quote,reservationId,expiresAt}.

Public: GET /api/catalog, /api/catalog/{id}, /api/catalog/{id}/alternatives; POST /api/auth/register,/login; POST /api/auth/logout; GET /api/auth/me. User Bearer: POST /api/customer/quotes {items}, POST /api/customer/carts {items,operationKey}, GET /api/customer/carts, GET /api/customer/carts/{id}, POST /api/customer/carts/{id}/{action}. Admin Bearer: GET /api/admin/products, POST/PUT /api/admin/products[/{id}], POST /api/admin/products/{id}/archive, GET /api/admin/users, PUT /api/admin/users/{id}/customer-type, GET /api/admin/analytics?from=&to=&group=day|week|month&category=. POST /api/recommendations/builds {budgetRub} calls pure assembly API for each candidate.

Quotes and reservations 15 minutes. Highest single discount, rounding away from zero. Atomic all-or-none reserve; price comparison by individual lines. Idempotency key binds exact request and identity; replay returns original stored response. Errors {code,message}: 400 input,401 auth,403 role/key,404 ownership/not found,409 conflict,502 invalid partner response,503 unavailable.

Analytics uses confirmed immutable sale lines only, UTC [from,to), Moscow calendar groups, distinct orders in category scope, zero-filled intervals, previous period of equal absolute duration. Frontend never calculates aggregates.

