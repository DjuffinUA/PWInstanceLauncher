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
