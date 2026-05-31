# Stacklands RU ModPack — Handoff Document
**Дата:** 2026-05-31  
**Статус:** В разработке — нужна доработка

---

## ПУТИ НА СИСТЕМЕ ПОЛЬЗОВАТЕЛЯ

```
Игра:         H:\SteamLibrary\steamapps\common\Stacklands
Workshop:     H:\SteamLibrary\steamapps\workshop\content\1948280
Моды (игра):  C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Mods
Исходники:    D:\Пользователи\Пользовател\Desktop\Mod\
Логи:         C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Player.log
GameScripts:  H:\SteamLibrary\steamapps\common\Stacklands\Stacklands_Data\Managed\GameScripts.dll
```

---

## СТРУКТУРА ПРОЕКТА

```
Desktop\Mod\
├── BetterSideBar\           ← Исходники мода BetterSideBar (исправленный)
│   ├── Mod.cs
│   ├── BlueprintDB.cs
│   ├── SidebarDisplayControl.cs   ← ОСНОВНОЙ файл — вкладки, поиск, фильтры
│   ├── PinIdeaMod.cs
│   ├── AdvancedQuickSearchMod.cs
│   ├── HideUnhoveredCoroutine.cs
│   ├── RuSearchIndex.cs           ← Русский поиск через TSV
│   ├── BetterSideBar.csproj
│   ├── manifest.json              ← Id: "better_sidebar"
│   └── Icons\                     ← 40x40px PNG иконки
│       ├── icon-all.png           ← 3 горизонтальные полосы
│       ├── icon-pin.png           ← Гвоздь
│       ├── icon-quick.png         ← Молния
│       ├── icon-new.png           ← Звезда
│       └── icon-reset.png         ← Круговая стрелка
│
├── RecipeInspector\         ← Новый мод — панель рецептов
│   ├── Mod.cs               ← Harmony патчи, R-key на IdeaElement
│   ├── RecipeCache.cs       ← Кэш рецептов + имена карт
│   ├── RecipePanel.cs       ← UI панели (вкладки, перетаскивание, настройки)
│   ├── RecipeSettings.cs    ← PlayerPrefs-настройки (платформонезависимые)
│   ├── RecipeInspector.csproj
│   └── manifest.json        ← Id: "recipe_inspector"
│
├── FasterEndOfMonths\       ← Исправленный мод (заморозка при смене дня)
│   ├── Plugin.cs            ← ФИКС: KillVillagerCoroutine теперь 4 параметра
│   └── FasterEndOfMonths.csproj
│
└── Scripts\
    ├── Stacklands_Backup.ps1
    ├── Install_All_Mods.ps1
    ├── Rollback_All_Mods.ps1
    └── Generate_Icons.ps1
```

---

## КАК СОБИРАТЬ

```powershell
# BetterSideBar
cd "D:\Пользователи\Пользовател\Desktop\Mod\BetterSideBar"
dotnet build -c Release
# → автоматически копирует в Mods\BetterSideBar\

# RecipeInspector
cd "D:\Пользователи\Пользовател\Desktop\Mod\RecipeInspector"
dotnet build -c Release
# → автоматически копирует в Mods\RecipeInspector\

# FasterEndOfMonths
cd "D:\Пользователи\Пользовател\Desktop\Mod\FasterEndOfMonths"
dotnet build -c Release
# → автоматически копирует в Workshop\3012089421\
```

---

## КЛЮЧЕВЫЕ ТЕХНИЧЕСКИЕ ФАКТЫ

### Переименованные методы в GameScripts.dll (текущая версия игры):
| Старое имя | Новое имя | Видимость | Примечание |
|---|---|---|---|
| `searchKnowledge` | `KnowledgeMatchesSearch` | private | Вызов через AccessTools.Method |
| `hasFoundKnowledge` | `HasFoundKnowledge` | private | inline через FoundCardIds.Contains |
| `KillVillagerCoroutine(C,A,A)` | `KillVillagerCoroutine(C,A,A,bool)` | public | Добавлен 4й параметр |
| `GetFoodToUseUp` | то же | private | Через AccessTools |
| `TryCreatePoop` | то же | private | Через AccessTools |
| `SetStarvingHumanStatus` | то же | private | Через AccessTools |

### IdeasButton баг:
- `IdeasButton.gameObject` клон содержит компонент `SokTermText` который каждый кадр перезаписывает текст кнопки обратно на "Идеи"
- ФИКС: уничтожать `SokTermText` после клонирования + принудительно ставить текст в Update каждый кадр
- Код в `SidebarDisplayControl.UpdatePatch.Prefix()` — устанавливает текст КАЖДЫЙ КАДР

### Инициализация кэша:
- `WorldManager.Awake` → только инвалидирует кэш (SokLoc ещё не готов!)
- `GameScreen.InitIdeaElements` Postfix → строит кэш (всё готово)
- `UpdateIdeasLog` Prefix → пересобирает при смене языка

### Поиск по-русски:
- `RuSearchIndex.cs` читает `localization.tsv` напрямую из Workshop папки
- Определяет папку через `ModManager.LoadedMods` → ищет мод с колонкой "Russian"
- ё=е нормализация, регистронезависимо, несколько слов = AND

---

## ИЗВЕСТНЫЕ БАГИ И ЧТО НУЖНО ДОДЕЛАТЬ

### RecipeInspector — критические баги:

1. **Иконки карт кривые** — частично исправлено (AspectRatioFitter), но ещё проверить в игре
2. **Жители деревни (Villager) с профессией** — у них другой тип (Worker, Farmer и т.д.), иконки могут не находиться через обычный `CardData.Icon`. Нужно проверить тип карты.
3. **Садоводство и похожие** — 90 subprints. Уже ограничено MaxVariants (default=20), но нужно добавить дедупликацию по составу ингредиентов
4. **Кнопка "Закреп"** — проверить что реально сохраняет в PinIdeaMod. Сейчас `slot.Pinned = true` но это только локальный флаг панели, не связан с BetterSideBar PinIdeaMod
5. **Автоматическое скрытие при крафте** — `RecipeSettings.KeepOnCraft` добавлена настройка, но логика не реализована (нужен патч на Blueprint.Complete или аналог)
6. **Настройки** — `DarkTheme` и `AutoHide` и `KeepOnCraft` добавлены в RecipeSettings.cs но UI для них ещё не добавлен в панель настроек

### BetterSideBar — баги:

7. **Вкладки BetterSideBar** — текст кнопок иногда показывает "Идеи" при первом открытии. Исправлено установкой текста каждый кадр в Update, но нужно протестировать
8. **SokTermText компонент** — деструкция через GetComponentsInChildren работает с задержкой (Destroy не мгновенный). Может требоваться DestroyImmediate

### Общие:

9. **No column exists for Russian** в Player.log — это сообщение из NewLanguageLoader для некоторых модов без русской колонки. Не баг, но раздражает
10. **FontManager NullReferenceException** — pre-existing баг в самой игре с русским шрифтом. Не наш баг
11. **Black Market ошибка пути** — `C:\Stacklands\blackmarket\Icons\Special1.PNG` — баг самого Black Market мода, не наш

---

## НАСТРОЙКИ RecipeInspector (RecipeSettings.cs)

Все хранятся через PlayerPrefs — работает на любом ПК.

| Ключ | Тип | Default | Описание |
|---|---|---|---|
| ShowIcons | bool | true | Показывать иконки карт |
| FontSizeIdx | int 0-2 | 1 | 0=малый, 1=нормальный, 2=крупный |
| MaxVariants | int | 20 | Макс subprints на рецепт |
| OnlyFound | bool | false | Только открытые рецепты |
| DarkTheme | bool | false | Тёмная тема (UI не реализован) |
| OpacityIdx | int 0-2 | 2 | Прозрачность 70/86/98% |
| AutoHide | bool | false | Авто-скрыть (логика не реализована) |
| KeepOnCraft | bool | false | Не убирать при крафте (логика не реализована) |
| ShowResultRow | bool | true | Показывать "→ результат" |
| SavedPos | Vector2 | (-8,0) | Позиция панели (сохраняется) |

---

## КАК РАБОТАЕТ RecipePanel (архитектура)

```
RecipePanel (MonoBehaviour, DontDestroyOnLoad)
├── _panel (RectTransform)  — anchor=right, pivot=right, vertically centered
│   ├── TitleBar             — PanelDragHandler, draggable
│   │   ├── "Рецепты" text
│   │   ├── ▼/▲ collapse
│   │   ├── ⚙ settings
│   │   └── × close
│   ├── _contentRoot         — скрывается при collapse
│   │   ├── Divider
│   │   ├── _tabBar          — горизонтальные вкладки
│   │   ├── Divider
│   │   └── ScrollRect → _content (список рецептов)
│
├── _settingsOverlay (отдельный GameObject)
│   — появляется поверх, пауза через WorldManager.SpeedUp=0

Slot (класс):
├── BpId          — ID Blueprint карты
├── ResultId      — ID карты-результата (для иконки и имени)
├── Label         — усечённое имя для вкладки
├── FullName      — полное имя (для tooltip)
├── SprintIdx     — текущий subprint (0-based)
└── Pinned        — локальный флаг закрепления
```

---

## УПРАВЛЕНИЕ

| Действие | Горячая клавиша |
|---|---|
| Добавить рецепт в панель | R (при наведении на рецепт в Ideas) |
| Убрать рецепт из панели | R (при наведении на вкладку в панели) ИЛИ × |
| Закрыть панель | × в заголовке |
| Свернуть панель | ▼ в заголовке |
| Настройки | ⚙ в заголовке (пауза игры) |
| Перетащить панель | Тащить за заголовок |
| Quick Search (BetterSideBar) | Средняя кнопка мыши по карте |

---

## ЧТО ЕЩЁ НУЖНО СДЕЛАТЬ (приоритет)

### Высокий приоритет:
- [ ] Дедупликация subprints с одинаковым составом ингредиентов
- [ ] Реализовать "Удалить при крафте" (нужен патч на Blueprint.CompleteBlueprint или Timer.OnComplete)
- [ ] Связать кнопку "Закреп" в панели с реальным PinIdeaMod (через `PinIdeaMod.IsFidea` и `GroupFNumMap`)
- [ ] UI для DarkTheme, AutoHide, Opacity в настройках
- [ ] Проверить иконки жителей с профессиями (Worker, Farmer и т.д.)

### Средний приоритет:
- [ ] Добавить поиск внутри открытых рецептов в панели
- [ ] Показывать количество открытых vs всего для Blueprint
- [ ] Кнопка "Показать в Ideas" — прокрутить левый список до этого рецепта
- [ ] Поддержка модовых Blueprint'ов (уже работает, но нужно протестировать)

### Низкий приоритет:
- [ ] Анимация появления/исчезания панели
- [ ] Звуковой эффект при добавлении вкладки
- [ ] Экспорт списка рецептов в текстовый файл

---

## УСТАНОВЛЕННЫЕ МОДЫ ПОЛЬЗОВАТЕЛЯ

| Мод | Workshop ID | Статус |
|---|---|---|
| Better SideBar | 3018736719 | ИСПРАВЛЕН нами |
| Recipe Inspector | — | НОВЫЙ наш мод |
| Russian Language | 3034167106 | Работает |
| New Language Loader | 3022323444 | Работает |
| FasterEndOfMonths | 3012089421 | ИСПРАВЛЕН нами |
| Card Zones | 2998472012 | Совместим |
| Compact Storage | 3012122691 | Совместим |
| BetterInfo | 2997317807 | Совместим |
| Cardopedia Fix | — | Совместим |
| Fix Grid | 3012092960 | Совместим |
| Black Market | — | БАГИ (не наши) |

---

## НАЧНИ НОВЫЙ ЧАТ С ЭТИМ СООБЩЕНИЕМ:

```
Продолжи доработку мода RecipeInspector для Stacklands.
Прочитай файл HANDOFF.md на рабочем столе:
D:\Пользователи\Пользовател\Desktop\Mod\HANDOFF.md

Там полная документация: пути, архитектура, известные баги, что нужно сделать.
Исходники: D:\Пользователи\Пользовател\Desktop\Mod\
Сначала прочитай HANDOFF.md, потом Player.log, потом спроси что делать первым.
```
