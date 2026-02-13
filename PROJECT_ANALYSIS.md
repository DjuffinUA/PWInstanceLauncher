# Аналіз проєкту після реалізації Етапу 4

## Початковий аналіз перед змінами

### Стан до доробок
- Етап 1: стабільний конфіг (JSON fallback + нормалізація) був реалізований.
- Етап 2: профілі (add/edit/remove) та DPAPI були реалізовані.
- Етап 3: launch pipeline (валідація -> duplicate-check -> launch -> wait handle) був реалізований.
- Етап 4: **не був завершений**, бо `DesktopService` не інтегрувався у `LaunchCharacter` (створення/switch desktop не викликались у runtime-потоці).

---

## Внесені зміни для завершення Етапу 4

### 1) DesktopService розширено для runtime-сценарію
Додано:
- in-memory мапу `login -> VirtualDesktop`;
- `PlaceWindowOnCharacterDesktop(login, windowHandle)`:
  - створює/повторно використовує desktop персонажа,
  - переміщує вікно,
  - перемикає desktop,
  - активує вікно;
- `MoveWindowToCurrentDesktop(windowHandle)`;
- `SwitchToDesktopWithWindow(windowHandle)` (fallback через desktop вікна);
- `TrySwitchToCharacterDesktop(login, windowHandle)`;
- `ActivateWindow(windowHandle)` з WinAPI (`ShowWindow`, `SetForegroundWindow`).

### 2) MainViewModel: інтегровано Етап 4 у launch-алгоритм
- Додано `DesktopService` у VM.
- `LaunchCharacter` тепер має дві окремі гілки:
  - `FocusExistingCharacter(...)`:
    - для вже запущеного персонажа: отримання handle,
    - switch на desktop персонажа (або fallback switch по desktop вікна),
    - активація вікна.
  - `LaunchNewCharacter(...)`:
    - запуск нового процесу,
    - очікування `MainWindowHandle`,
    - якщо `SeparateDesktop` -> переміщення на desktop персонажа,
    - якщо `CurrentDesktop` -> переміщення на поточний desktop.

Результат: цільовий алгоритм Етапу 4 працює в runtime-потоці натискання кнопки Launch.

---

## Повторний аналіз логіки після змін

### Що перевірено
1. Для вже запущеного персонажа:
   - немає повторного запуску;
   - є спроба перейти на desktop цього вікна і активувати його.
2. Для нового запуску:
   - є запуск, очікування handle, переміщення на desktop згідно режиму.
3. Для `LaunchMode`:
   - `SeparateDesktop` і `CurrentDesktop` реально впливають на потік виконання.

### Неточності / технічні ризики, що залишилися
- Мапа `login -> desktop` in-memory, тобто після рестарту застосунку відновлення виконується через fallback (`SwitchToDesktopWithWindow`), а не через persisted mapping.
- `EnsureGamePath()` все ще кидає exception при cancel (це UX-компроміс, не функціональна помилка).
- Пошук existing process як і раніше залежить від WMI `CommandLine` доступу.

---

## Готовність по етапах (оновлено)

- **Етап 1:** 90% (стабільний конфіг + валідація є).
- **Етап 2:** 90% (керування профілями + безпечне оновлення пароля є).
- **Етап 3:** 85% (надійний launch pipeline реалізовано).
- **Етап 4:** 80% (desktop switch/move інтегровано у launch-потік).
- **Етап 5:** 10% (моніторинг процесів та cleanup стану ще не додані).
- **Етап 6:** 20% (логування/статуси/глибока обробка винятків ще потребують доробки).

---

## Висновок
Етап 4 функціонально інтегровано: для існуючих процесів є switch/activate, для нових — створення/переміщення вікна на desktop відповідно до режиму. Архітектура готова до переходу на Етап 5 (моніторинг стану і cleanup).
# Аналіз коду та готовність до переходу на Етап 4

## Що зроблено для завершення Етапу 3 (запуск процесу)

### ProcessService
- Додано перевірку валідності executable: `IsGameExecutableValid(gamePath)`.
- Додано пошук уже запущеного клієнта саме по логіну: `TryFindRunningByLogin(login)` через аналіз `CommandLine` процесу.
- `Launch(...)` тепер повертає `Process` для подальшої роботи з вікном.
- Додано `WaitForMainWindowHandle(process, timeout)` для очікування готовності вікна.
- Додано базовий safe-build аргументів (`user:` / `pwd:`) перед запуском.

### MainViewModel
- `LaunchCharacter(...)` переписано на явний покроковий pipeline:
  1) перевірка `GamePath`;
  2) перевірка логіну;
  3) перевірка дубля процесу по логіну;
  4) дешифрування пароля;
  5) запуск процесу;
  6) очікування `MainWindowHandle`.
- Додано обробку помилок та повідомлення користувачу для всіх критичних гілок.

---

## Поточний статус проти вашої моделі

### 1) Архітектурні модулі
- **ConfigService:** JSON читання/запис є; базова перевірка `GamePath` виконується у `MainViewModel`.
- **CredentialService:** DPAPI шифрування/дешифрування реалізоване.
- **ProcessService:** закрито ключові вимоги Етапу 3 (валідація, запуск, duplicate-check, очікування вікна).
- **DesktopService:** бібліотека інтегрована, створення та переміщення реалізовані.
- **ViewModel:** колекція + launch/add команди реалізовані; remove команда ще відсутня.

### 2) Логіка запуску
Реалізовано більшість етапу 3. Готова основа для етапу 4, тому що після запуску вже є:
- `Process`;
- `MainWindowHandle` (або контрольований timeout).

---

## Що залишилось для Етапу 4 (Desktop)

### Мінімальний технічний план переходу
1. Додати у `DesktopService` методи:
   - `CreateDesktopForLogin(login)`;
   - `SwitchToDesktop(...)`;
   - `MoveWindowToDesktop(windowHandle, desktop)`.
2. Додати in-memory мапу в `MainViewModel`:
   - `login -> desktopId`;
   - `login -> processId`.
3. Розширити `LaunchCharacter`:
   - якщо процес існує -> переключитись на desktop персонажа + активувати вікно;
   - якщо процес новий -> створити desktop, дочекатися handle, перемістити вікно.
4. Додати очищення стану (база Етапу 5):
   - таймер моніторингу;
   - якщо процес завершився, прибрати зв'язок login/process/desktop.

### Ризики перед стартом Етапу 4
- `CommandLine` процесу може бути недоступний у деяких середовищах (WMI обмеження).
- Для надійної активації вікна можуть знадобитись WinAPI виклики (`SetForegroundWindow`, `ShowWindow`).

---

## Висновок
Етап 3 доведено до робочого стану для production-пайплайну запуску без `.bat` і з контрольованою обробкою помилок. Проєкт технічно готовий переходити до Етапу 4 (ізоляція персонажів по Desktop) без переробки базового launch-flow.
