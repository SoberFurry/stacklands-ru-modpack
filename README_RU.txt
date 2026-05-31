====================================================================
  STACKLANDS — Stable RU Mod Pack
====================================================================

СОДЕРЖИМОЕ ПАКЕТА:
------------------
1. BetterSideBar (исправленный)
   - Закрепление идей (Pin/Unpin) левым кликом
   - Фильтры: Pinned / Quick / New
   - Быстрый поиск рецептов средней кнопкой мыши
   - Поиск на русском и английском языках
   - ИСПРАВЛЕНО: MissingMethodException (searchKnowledge→KnowledgeMatchesSearch)

2. RecipeInspector (новый)
   - Ctrl+клик по любой карте → боковая панель с рецептами
   - Вкладки: "Создать" / "Используется" / "Закреплённые" / "Открытые"
   - Иконки карт, локализованные названия
   - Только открытые в текущем сейве рецепты
   - Кеш строится один раз при загрузке мира

УСТАНОВКА:
----------
1. Запустите Scripts\Install_All_Mods.ps1 (ПКМ → Запустить в PowerShell)
   ИЛИ вручную:
   - Скопируйте BetterSideBar\ в:
     C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Mods\BetterSideBar\
   - Скопируйте RecipeInspector\ в:
     C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Mods\RecipeInspector\

ОТКАТ:
------
   Запустите Scripts\Rollback_All_Mods.ps1

БЭКАП СОХРАНЕНИЙ:
-----------------
   Запустите Scripts\Stacklands_Backup.ps1
   ZIP создаётся на Рабочем столе.

УСТРАНЕНИЕ ПРОБЛЕМ:
-------------------
- Нет кириллицы? → Убедитесь что Russian Language (3034167106) включён в Steam Workshop
- Мод не загружается? → Проверьте Player.log:
  C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Player.log
- BetterSideBar не показывает рецепты? → Перезапустите игру после включения мода

УПРАВЛЕНИЕ:
-----------
  Средняя кнопка мыши на карте  → Quick Search (поиск в Ideas)
  Ctrl + ЛКМ на карте           → Recipe Inspector (боковая панель)
  Клик на идею в Ideas          → Pin/Unpin
  Кнопки "Pinned/Quick/New"     → Фильтры

СОВМЕСТИМОСТЬ:
--------------
Протестировано с: Card Zones, Compact Storage, BetterInfo,
Cardopedia Fix, Faster End Of Months, Fix Grid, Russian Language,
New Language Loader.
====================================================================
