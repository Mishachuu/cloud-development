# Современные технологии разработки программного обеспечения

**Вариант №17 — «Транспортное средство»**  
**Выполнил:** Чукарев Михаил, группа 6511

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

