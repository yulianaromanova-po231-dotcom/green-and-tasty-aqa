# Green & Tasty: e2e-тести групового pre-order

Домашнє завдання #8 (Test Automation) Марафону IT-технологій 2026. Виконала **Романова Юліана**.

| | |
|---|---|
| **Напрям** | **Напрям 1. Груповий pre-order.** Тестується власна реалізація: бекенд з ДЗ5 і фронтенд з ДЗ6, інтегровані й розгорнуті в AWS |
| **Підхід** | **BDD + Screenplay**, наявний фреймворк EPAM (Reqnroll + TUnit + Playwright for .NET), розширений під групове замовлення |
| **Рівні** | 1, 2 і 3, позитивні й негативні сценарії, UI та API |
| **Об'єкт тестування** | фронтенд https://d31xoqdbs6rjnl.cloudfront.net, API `https://vx8vlt4fii.execute-api.eu-north-1.amazonaws.com/develop` |

Тести лежать в [`aqa/restaurant-aqa-framework`](aqa/restaurant-aqa-framework). Пайплайни в [`.github/workflows`](.github/workflows). Поруч лежить код, який тестується: [`backend`](backend) (ДЗ5) і [`frontend`](frontend) (ДЗ6). Інструкція з розгортання: [DEPLOYMENT.md](DEPLOYMENT.md).

---

## Що покрито

38 сценаріїв (з урахуванням рядків `Examples`): 29 API і 9 UI.

| Рівень | Feature | UI (екран Owner / офіціанта) | API (авторизація, валідація) |
|---|---|---|---|
| **1. Перегляд учасників** | [`GroupPreOrderReview.feature`](aqa/restaurant-aqa-framework/Features/GroupPreOrderReview.feature) | Owner бачить гостей, їхні страви й підсумки, консолідоване замовлення й Grand Total; оновлення після зміни кошика (Refresh); порожній стан і заблокований Submit; інший клієнт не відкриває чужий екран | учасники, страви, кількості, суми; загальна сума = сума по учасниках і змінюється разом із кошиком; непідтверджений кошик і гість без страв є в списку; порожня група (`participants: []`, `total: 0`); доступ для іншого клієнта, офіціанта, гостя й неавторизованого; неіснуюча група |
| **2. Підтвердження** | [`GroupPreOrderSubmit.feature`](aqa/restaurant-aqa-framework/Features/GroupPreOrderSubmit.feature) | Owner підтверджує на екрані: «Sent to Restaurant», консолідація лише підтверджених кошиків; офіціант бачить замовлення в інтерфейсі офіціанта; Submit недоступний, поки ніхто не підтвердив кошик | однакові страви об'єднуються, непідтверджені кошики не враховуються, замовлення стає `SubmittedByCustomer`, група стає `Submitted`; офіціант бачить склад; повторний submit; не-Owner; кожна умова, за якої підтвердити неможливо, окремим сценарієм |
| **3. Видалення учасника** | [`GroupPreOrderRemoveParticipant.feature`](aqa/restaurant-aqa-framework/Features/GroupPreOrderRemoveParticipant.feature) | видалення гостя на екрані з перерахунком сум; **наскрізний сценарій**: гості додають страви → Owner переглядає → видаляє одного → підтверджує → офіціант бачить замовлення без видаленого | видалення одного з кількох; гостя, який уже підтвердив кошик; останнього учасника; учасник іншої групи (400); неіснуючий (404); не-Owner; власна сесія Owner; група вже підтверджена |

**Піраміда тестів.** UI-сценарії перевіряють те, що бачить користувач. API-сценарії перевіряють авторизацію й валідацію. Підготовка даних іде через API: ресторан, офіціант, бронювання, гості та їхні кошики. Гість у застосунку не має акаунта, тому його кошик теж готується через API. UI ж використовується для дій Owner і офіціанта.

### Поведінка, яку ДЗ не визначає (обрано й зафіксовано)

| Випадок | Поведінка | Де перевіряється |
|---|---|---|
| Запит не від Owner, але від авторизованого клієнта чи офіціанта | `403` | Outline «Somebody other than the owner…» у кожному рівні |
| Запит без входу як клієнт (неавторизований відвідувач або гість лише з guest-токеном) | `401` від Cognito authorizer API Gateway | ті самі Outline |
| Повторний submit уже підтвердженої групи | `400` «Group reservation was submitted.», склад замовлення не змінюється | «The owner cannot submit the same group pre-order twice» |
| Видалення останнього учасника | `200`, `participants: []`, `total: 0` | «The owner removes the last participant» |
| Owner видаляє власну сесію | `400` «The owner's own session cannot be removed from the group.» | «The owner cannot remove their own place in the group» |
| Видалення після submit | `400` «Group reservation was submitted.» | «Nobody can be removed once the pre-order has been submitted» |
| Група без учасників | `participants: []`, `total: 0` (як вимагає ДЗ) | «Nobody has joined the group yet» |
| «У підтверджених кошиках немає жодної страви» | через застосунок недосяжно: бекенд не дає підтвердити порожній кошик («The cart is empty»). Тому тест перевіряє саме цю гарантію | «A guest cannot confirm a cart without dishes» |
| Немає order (Owner не робив власний pre-order) | `400`, «… was not found.» | «The owner has not pre-ordered for the booking» |

### Знайдений і виправлений дефект

UI-тести на реальному API знайшли баг у фронтенді ДЗ6. Інтерсептор не додавав токен Owner до запитів `/guest/groups/{id}/sessions` і `/status`, бо вважав усі `/guest/*` гостьовими. Через це екран Owner на реальному бекенді показував «Couldn't load». На моках цього не було видно. Виправлено в `frontend/.../auth/token.interceptor.ts`, додано юніт-тести і перерозгорнуто фронтенд.

---

## Як налаштувати й запустити

Потрібні .NET 10 SDK і доступ до розгорнутого застосунку. Браузер Playwright фреймворк встановлює сам.

```bash
cd aqa/restaurant-aqa-framework
cp .env.example .env      # вписати UI_URL і API_URL свого розгортання
dotnet build
dotnet run --no-build -- --report-trx
```

У `.env` достатньо двох значень, `UI_URL` і `API_URL`. Облікові записи не потрібні: кожен сценарій сам реєструє Owner, іншого клієнта й офіціанта. Офіціанта він ставить у список офіціантів свого ресторану, бо самостійною реєстрацією офіціанта не створиш. `.env` у `.gitignore`.

### Підмножина сценаріїв (за тегом)

```bash
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level1]"   # рівень 1 (level2, level3)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=api]"      # лише API (ui — лише UI)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"    # швидка перевірка
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=negative]" # негативні
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=e2e]"      # наскрізний сценарій
```

Теги: `@level1`, `@level2`, `@level3`, `@group-pre-order`, `@api`, `@ui`, `@smoke`, `@negative`, `@e2e`.

### Де звіти

Усе потрапляє в `aqa/restaurant-aqa-framework/TestResults/`:
- `*-report.html` і `*.trx`: звіт TUnit;
- `reqnroll/reqnroll_report.html`: звіт Reqnroll з кроками Gherkin;
- `traces/*.zip`: Playwright trace для кожного впалого UI-сценарію (DOM, мережа, консоль, скриншоти). Відкрити: `npx playwright show-trace <file>.zip`.

---

## CI (GitHub Actions)

| Workflow | Коли | Що робить |
|---|---|---|
| [`aqa-quality.yml`](.github/workflows/aqa-quality.yml) | кожен push і PR у `aqa/**` | немає згенерованих файлів, `dotnet format`, збірка (warnings = errors), Gherkin lint, список тестів |
| [`aqa-tests.yml`](.github/workflows/aqa-tests.yml) | push у `aqa/**`, щоночі й вручну (Actions → AQA Tests → Run workflow, з вибором `all / ui / api / smoke / level1-3`) | запускає сценарії на розгорнутому застосунку й публікує артефакти: звіт TUnit і TRX, звіт Reqnroll, Playwright traces; підсумок у Job Summary |

Впалий тест не зупиняє пайплайн. Крок тестів має `continue-on-error`, а звіти й артефакти публікуються завжди (`if: always()`). Наприкінці job позначається червоним, щоб падіння було видно.

Адреси задаються змінними репозиторію (Settings → Secrets and variables → Actions → Variables): `AQA_UI_URL`, `AQA_API_URL`. Паролів і секретів не потрібно.

---

## Тестові дані

- Кожен сценарій створює власний ресторан: локацію, офіціанта, стіл і страви з префіксом `AQA-Sample-`. Також він створює власних клієнтів (`aqa.sample.<guid>@example.com`) і бронювання. Тож сценарії незалежні й виконуються паралельно, до 4 одночасно.
- Час бронювання рахується динамічно: `now + 3 дні`, різні години для кількох бронювань.
- Після сценарію `CleanupScope` прибирає все створене у зворотному порядку: скасовує бронювання, знімає офіціанта зі списку, видаляє страви й локацію разом зі столами. Акаунти Cognito API не видаляє, тому на кожен запуск беруться унікальні адреси.

## Архітектура тестів

```
Features/           Gherkin (бізнес-мова, персонажі: owner, guest, another customer, waiter, visitor)
StepDefinitions/    тонкі кроки: одна й та сама фраза виконується через API у @api і через екран у @ui ([Scope])
Screenplay/Tasks/   дії: OpenTheRestaurant, BookAGroupTable, JoinTheGroup, AddToTheCart, ConfirmTheCart,
                    ReviewTheGroupPreOrder, SubmitTheGroupPreOrder, RemoveTheParticipant, … і UI-дії екрана Owner/офіціанта
Screenplay/Questions/  що бачить актор: TheGroupPreOrderShown, TheConsolidatedPreOrder, TheReservationOrder,
                    TheGroupStatus, TheGroupOrderPage, TheWaiterPreOrderShown, TheOutcome
Ui/                 page objects: GroupOrderPage, ParticipantCardComponent, WaiterReservationsPage, WaiterPreOrderPage
Api/                клієнти: GroupOrdersApi, WaitersApi (+ наявні Auth/Locations/Tables/Dishes/Reservations/Orders)
```

Локатори обираються за пріоритетом: роль + доступне ім'я (`Remove Olena`, `Submit Pre-Order`, heading картки), потім CSS-класи компонентів. Очікування роблять Playwright `Expect`, без `sleep`. Відповідність «бізнес-результат ↔ HTTP-статус» описана в одному місці, в `OutcomeStepDefinitions`.

## Використання AI

Під час роботи використовувався AI-асистент (Claude): для аналізу завдання, написання коду тестів і цього README. Результат перевірено запуском усіх сценаріїв на розгорнутому застосунку: 29/29 API, 9/9 UI.
