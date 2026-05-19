# Современные технологии разработки программного обеспечения

**Вариант №17 — «Транспортное средство»**  
**Выполнил:** Чукарев Михаил, группа 6511

## Лабораторная работа №4 — «Переход на облачную инфраструктуру»

### Описание

Все сервисы перенесены в Yandex Cloud. Клиент размещён в объектном хранилище, сервисы генерации и обработки файлов развёрнуты как Cloud Functions, очередь сообщений заменена на Yandex Message Queue, а маршрутизация реализована через Yandex API Gateway.

### Что реализовано

<details>
<summary>Клиент (Object Storage)</summary>
</details>

<details>
<summary>Брокер сообщений (LocalStack SQS → Yandex Message Queue)</summary>
<br>

- Создана стандартная очередь `vehicles`

<br>
</details>

<details>
<summary>Объектное хранилище (MinIO → Object Storage)</summary>
<br>

- Создан приватный бакет `vehicles-storage`
- Minio SDK заменён на AWS S3 SDK с endpoint `storage.yandexcloud.net`
- JSON-файлы транспортных средств сохраняются автоматически при каждом запросе

<br>
</details>

<details>
<summary>Сервис генерации (VehicleApi → Cloud Function)</summary>
<br>

- Генерация данных через `VehicleGenerator` с детерминированным seed по `id`

<br>
</details>

<details>
<summary>Файловый сервис (FileService → Cloud Function + триггер YMQ)</summary>
<br>

- JSON сохраняется в бакет `vehicles-storage` через AWS S3 SDK

<br>
</details>

<details>
<summary>API Gateway (Ocelot → Yandex API Gateway)</summary>
<br>

- Создан шлюз `vehicle-gateway` на основе OpenAPI-спецификации
- Маршрут `GET /api/vehicles?id={id}` проксирует запросы в Cloud Function `vehicle-api`

<br>
</details>


## Лабораторная работа №3 — «Интеграционное тестирование»

### Описание

Реализован файловый сервис, сохраняющий данные о транспортных средствах в объектное хранилище через брокер сообщений, а также интеграционные тесты, проверяющие корректность работы всего бэкенда.

### Что реализовано

<details>
<summary>Брокер сообщений (Amazon SQS + LocalStack)</summary>
</details>

<details>
<summary>Файловый сервис (FileService)</summary>
<br>

- `MinioStorageService` — сохраняет JSON каждого сообщения как отдельный файл в MinIO
- Эндпоинт `GET /files` — список всех файлов в bucket

<br>
</details>

<details>
<summary>Объектное хранилище (MinIO)</summary>
<br>

- Bucket `vehicles` создаётся автоматически при старте FileService
- Хранение JSON-файлов с данными транспортных средств

<br>
</details>

<details>
<summary>Интеграционные тесты (xUnit + Aspire Testing)</summary>
<br>

- Поднимают весь AppHost целиком (Redis, LocalStack, MinIO, все сервисы)
- `GetVehicle_ValidId_ReturnsOk` — gateway возвращает 200
- `GetVehicle_ValidId_ReturnsExpectedFields` — ответ содержит все поля
- `GetVehicle_InvalidId_ReturnsBadRequest` — валидация id=0
- `GetVehicle_NegativeId_ReturnsBadRequest` — валидация отрицательных id
- `GetVehicle_SameId_ReturnsCachedData` — одинаковый id возвращает одинаковые данные
- `GetVehicle_DifferentIds_ReturnDifferentData` — разные id возвращают разные данные
- `GetVehicle_FileAppearsInMinio` — файл появляется в MinIO после запроса (30 сек таймаут)
- `FileService_HealthCheck_ReturnsOk` — health check FileService
- `Gateway_HealthCheck_ReturnsOk` — health check ApiGateway

<br>
</details>

## Лабораторная работа №2 — «Балансировка нагрузки»

### Описание

Реализован ApiGateway с балансировкой нагрузки между тремя репликами сервиса генерации данных о транспортных средствах с оркестрацией через .NET Aspire.

### Что реализовано

<details>
<summary>Балансировка нагрузки (Ocelot + WeightedRandom)</summary>
<br>

- ApiGateway на базе Ocelot
- Кастомный балансировщик `WeightedRandomLoadBalancer`
- Веса реплик настраиваются через `appsettings.json` (`WeightedRandom:Weights`)
- Три реплики сервиса генерации

<br>
</details>

<details>
<summary>CORS</summary>
<br>

- Настроен на уровне ApiGateway (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`)

<br>
</details>

<details>
<summary>Оркестрация (.NET Aspire)</summary>
<br>

- Redis
- Три реплики VehicleApi
- ApiGateway
- Клиент

<br>
</details>

## Лабораторная работа №1 — «Кэширование»

**Вариант №17 — «Транспортное средство»**  
**Выполнил:** Чукарев Михаил, группа 6511

### Описание

Реализован сервис генерации данных о транспортных средствах с кэшированием ответов в Redis и оркестрацией через .NET Aspire.

### Что реализовано

<details>
<summary>Генерация данных (Bogus)</summary>
<br>

- Класс `VehicleGenerator` с `RuleFor` для каждого поля
- Поля: VIN, производитель, модель, год выпуска, тип кузова, тип топлива, цвет, пробег, дата последнего ТО

<br>
</details>

<details>
<summary>Кэширование (Redis + IDistributedCache)</summary>
<br>

- Сервис `VehicleService` инкапсулирует логику работы с кэшем

<br>
</details>

<details>
<summary>Структурное логирование</summary>
<br>

- Логирование через `ILogger<T>`

<br>
</details>

<details>
<summary>CORS</summary>
<br>

- Доверенные origins вынесены в `appsettings.json` (`Cors:AllowedOrigins`)
- `AllowAnyMethod`, `AllowAnyHeader`

<br>
</details>

<details>
<summary>Оркестрация (.NET Aspire)</summary>
<br>

- Redis
- API сервис
- Клиент WASM

<br>
</details>

<details>
<summary>API</summary>
<br>

- Единственный эндпоинт: `GET /api/vehicles?id={id}`

<br>
</details>

<details>
<summary>Тесты (xUnit)</summary>
<br>

- `Generate_SameId_ReturnsSameData` — детерминированность генерации
- `Generate_DifferentIds_ReturnsDifferentData` — уникальность по `id`
- `Generate_YearConstraint_IsValid` — год в диапазоне [1990, текущий]
- `Generate_MileageConstraint_IsValid` — пробег в диапазоне [0, 500 000]
- `Generate_LastServiceDateConstraint_IsValid` — дата ТО не раньше года выпуска и не в будущем

<br>
</details>

