# PC Штаб Автопилот — карта переделки FluentCleaner

## Цель

Не писать проект с нуля. Берём FluentCleaner как готовую WinUI-базу и добавляем режим автопилота.

## Что уже есть в FluentCleaner

1. WinUI desktop-приложение под Windows.
2. Главная навигация через `MainWindow.xaml` и `MainWindow.xaml.cs`.
3. Страницы: Cleaner, Terminal, Custom, Settings.
4. Уже есть headless-запуск через аргумент `/AUTO` в `App.xaml.cs`.
5. Уже есть `SilentRunner`, который может запускать очистку без окна.
6. Уже есть анализ и очистка через `CleaningService`.
7. Уже есть настройки в `%AppData%\FluentCleaner\settings.json`.

## Почему это хорошая база

Самая важная находка: в проекте уже есть скрытая основа автопилота.

`App.xaml.cs` проверяет аргумент `/AUTO` и запускает `SilentRunner.RunAsync(...)` без открытия окна.

Значит, нам не нужно изобретать автозапуск с нуля. Нужно:

- сделать нормальный экран управления автопилотом;
- добавить расписание через Планировщик заданий Windows;
- ограничить автоочистку безопасным профилем;
- добавить отчёт и понятный статус.

## Главная проблема текущего SilentRunner

Сейчас `/AUTO` берёт выбранные пользователем записи или дефолтные записи Winapp2 и чистит их.

Для PC Штаб Автопилот это опасно как первый режим, потому что там могут быть разные правила, включая registry cleanup.

Нужен отдельный безопасный режим:

`/AUTOPILOT`

Он должен чистить только заранее разрешённые безопасные зоны:

- `%TEMP%`;
- `C:\Windows\Temp`;
- кэш миниатюр — только если включено;
- корзина — только если включено отдельно.

## Файлы для правки MVP

### 1. `FluentCleaner/App.xaml.cs`

Добавить обработку аргумента:

- `/AUTOPILOT`

Логика:

```csharp
bool isAutopilot = cmdArgs.Any(a => a.Equals("/AUTOPILOT", StringComparison.OrdinalIgnoreCase));

if (isAutopilot)
{
    _ = AutopilotRunner.RunAsync();
    return;
}
```

### 2. `FluentCleaner/Services/AutopilotRunner.cs`

Новый файл.

Должен:

- проверить диск C;
- проверить RAM/CPU базово;
- проверить Defender по возможности;
- посчитать временные файлы;
- безопасно удалить временные файлы старше N дней;
- создать TXT/JSON отчёт;
- сохранить результат в настройки.

### 3. `FluentCleaner/Services/AutopilotScheduler.cs`

Новый файл.

Должен включать/выключать ежедневный запуск через Планировщик заданий Windows.

Команда запуска:

```text
FCleaner.exe /AUTOPILOT
```

### 4. `FluentCleaner/Services/AppSettings.cs`

Добавить настройки:

```csharp
public bool AutopilotEnabled { get; set; } = false;
public string AutopilotMode { get; set; } = "Observe";
public int AutopilotHour { get; set; } = 11;
public int SafeCleanOlderThanDays { get; set; } = 7;
public bool SafeCleanUserTemp { get; set; } = true;
public bool SafeCleanWindowsTemp { get; set; } = true;
public bool SafeCleanExplorerThumbCache { get; set; } = false;
public bool SafeCleanRecycleBin { get; set; } = false;
public DateTime? LastAutopilotRun { get; set; }
public long LastAutopilotFreedBytes { get; set; }
public int LastAutopilotWarnings { get; set; }
```

### 5. `FluentCleaner/Views/AutopilotPage.xaml`

Новая страница.

Экран должен быть простым:

- `ПК под контролем`;
- режим: Наблюдение / Безопасный автопилот / Ручное подтверждение;
- последняя проверка;
- следующая проверка;
- освобождено места;
- предупреждения;
- кнопка `Проверить сейчас`;
- кнопка `Включить автопилот`;
- кнопка `Выключить автопилот`;
- кнопка `Открыть отчёты`.

### 6. `FluentCleaner/ViewModels/AutopilotPageViewModel.cs`

Новый ViewModel для экрана автопилота.

Команды:

- `RunNowCommand`;
- `EnableAutopilotCommand`;
- `DisableAutopilotCommand`;
- `OpenReportsCommand`.

### 7. `FluentCleaner/MainWindow.xaml`

Добавить пункт меню:

```xml
<NavigationViewItem Tag="Autopilot" Content="Autopilot">
    <NavigationViewItem.Icon>
        <FontIcon Glyph="&#xE7C1;" />
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

### 8. `FluentCleaner/MainWindow.xaml.cs`

Добавить переход:

```csharp
case "Autopilot":
    NavFrame.Navigate(typeof(AutopilotPage), null, transition);
    break;
```

## Что не трогать в первом MVP

- Не трогать текущую ручную страницу Cleaner.
- Не удалять Terminal.
- Не менять Winapp2 parser.
- Не включать registry cleanup в автопилот.
- Не использовать `appx remove` в автопилоте.
- Не делать debloat.

## Минимальный результат

MVP готов, когда:

1. Приложение открывается.
2. В меню есть `Autopilot`.
3. Кнопка `Проверить сейчас` создаёт отчёт.
4. Кнопка `Включить автопилот` создаёт задание в Планировщике Windows.
5. Кнопка `Выключить автопилот` удаляет задание.
6. `/AUTOPILOT` работает без окна.
7. Автоочистка не трогает реестр, службы, программы, драйверы и личные файлы.

## Следующий практический шаг

Открыть репозиторий в Visual Studio 2026 Preview или другой среде, которая поддерживает `.NET 10` и `Windows App SDK 2.0.1`.

Затем сначала собрать исходный FluentCleaner без изменений. Только после успешной сборки добавлять файлы автопилота.
