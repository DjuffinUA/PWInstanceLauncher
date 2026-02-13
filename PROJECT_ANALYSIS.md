# Аналіз коду та корекція відповідно до Етапів 1–4

## 1) Що перевірено
- Логіка конфігу (`ConfigService` + стартова перевірка шляху гри).
- Керування профілями (`Add/Edit/Remove`, валідація введення, шифрування пароля).
- Pipeline запуску (`LaunchCharacter`, пошук існуючого процесу, запуск, очікування handle).
- Desktop-логіка (`DesktopService`, switch/move/activate для існуючого і нового процесу).

---

## 2) Виявлені неточності до виправлення
1. У `MainViewModel` був надмірно громіздкий `LaunchCharacter` з дублюванням MessageBox-поведінки.
2. Для `LaunchMode.CurrentDesktop` існуючий процес лише активувався, але не переміщувався в поточний desktop.
3. `EnsureGamePath` використовував exception як основний UX-flow при скасуванні вибору файлу.
4. У `DesktopService` не було валідації login для desktop mapping.
5. У `ProcessService` залишався невикористаний метод `IsGameExecutableValid` (дублювання відповідальності).

---

## 3) Внесені виправлення і спрощення

### MainViewModel
- Розбито запуск на логічні блоки:
  - `ValidateLaunchInput(...)`
  - `FocusExistingCharacter(...)`
  - `LaunchNewCharacter(...)`
- Додано `TryEnsureGamePath()` (без exception у runtime-launch гілці).
- Уніфіковано повідомлення через `ShowWarning(...)` / `ShowError(...)`.
- Для вже запущеного процесу в режимі `CurrentDesktop` тепер викликається `MoveWindowToCurrentDesktop(...)`.

### DesktopService
- Додана валідація login у desktop mapping (`GetOrCreateDesktop`, `TrySwitchToCharacterDesktop`).
- Логіка switch-by-window стала явнішою (`return false`, якщо desktop не знайдено).

### ProcessService
- Прибрано невикористаний метод `IsGameExecutableValid(...)` для спрощення і уникнення дублювання.

---

## 4) Повторний аналіз після змін (Етапи 1–4)

### Етап 1 — База
- JSON/fallback/нормалізація: є.
- Перевірка GamePath: є.
- Стан: **закрито** (із технічним нюансом, що при першому старті і відмові від вибору exe застосунок не продовжує роботу).

### Етап 2 — Профілі
- Add/Edit/Remove: є.
- DPAPI шифрування: є.
- Редагування без перезапису пароля порожнім значенням: є.
- Стан: **закрито**.

### Етап 3 — Процес запуску
- Перевірки перед запуском: є.
- Duplicate-check по логіну: є.
- Запуск + wait main window handle + error handling: є.
- Стан: **закрито** (з відомим ризиком доступності WMI для command line).

### Етап 4 — Desktop
- Для existing process: switch/activate реалізовано.
- Для new process: move на окремий desktop або current desktop залежно від режиму.
- Стан: **закрито функціонально**.

---

## 5) Залишкові ризики (без втрати функціональності)
- Визначення existing process за login залежить від доступу до `Win32_Process.CommandLine` (WMI).
- Desktop mapping `login -> VirtualDesktop` зберігається в пам'яті процесу (після рестарту відновлення через fallback switch-by-window).
