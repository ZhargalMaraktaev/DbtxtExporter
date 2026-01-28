Табло (DbtxtExporter)
Что это такое?
DbtxtExporter — это фоновое консольное приложение (.NET 9), которое каждую минуту собирает производственные показатели с нескольких SQL Server баз данных и записывает их в простые текстовые файлы (.txt) в определённую сетевую папку.
Эти текстовые файлы, используются для отображения текущих показателей на производственных табло / экранах / scoreboards в цеху.
Основные показатели, которые считаются и записываются:
•	план на 12-часовую смену
•	фактическое количество труб за день / за ночь
•	простои (в часах)
•	брак
•	план и факт по каждой из четырёх подсмен (A, Б, В, Г)
Где физически находится и где запускается?
•	Исходный код — в репозитории проекта
•	Скомпилированное приложение — обычно на сервере автоматизации / сервере ASUTP / промышленном ПК
•	Запускается как обычное консольное приложение
•	Пишет файлы в сетевую папку: \\ITPZVWS01\Scoreboards
Внутри этой папки создаются подпапки по линиям:
o	NKT12
o	NOTPAK
o	NC9
o	URZBIL
o	UOSTIZM
В каждой подпапке лежит файл вида report_название.txt (например report_nkt12.txt, report_nc9.txt и т.д.)
Кому это нужно / кто пользуется?
Кто	Для чего использует
Табло в цеху	Показывают текущие цифры по плану/факту/простоям/браку
Мастера смены	Видят, насколько отстают/опережают план
Начальник участка / цеха	Быстрый обзор ситуации по всем линиям
Технологи / ОТК	Контроль брака и простоев в реальном времени
IT / АСУ ТП	Поддерживают работу самого приложения и подключение к БД
Фактически конечный потребитель — производственный персонал цеха, который смотрит на большие экраны.
Как работает (очень высокоуровнево)
1.	Приложение запускается и сразу делает первый отчёт
2.	Каждые 60 секунд повторяет цикл:
o	Подключается к 4 базам данных:
	NKT_REP
	NOT_REP_DB
	TPA140TREND
	N0T (база простоя)
o	Читает последние данные по трубам, планам, простоям и браку
o	Считает показатели за:
	текущий день (00:00 – сейчас)
	текущую ночь / день
	весь месяц (по подсменам A,Б,В,Г)
o	Формирует одну строку вида: 168;45;1.2;2;168;38;0.8;1;42;45;42;38;42;40;42;41
o	Записывает эту строку в соответствующий файл report_....txt
3.	Если база недоступна → в логах будет ошибка, но приложение продолжит пытаться дальше
Основные источники данных
Линия	Таблица репортных данных	Таблица задержек (простои, брак)	План на час (примерно)
nkt12	NKT_12_REP	NEW_NKT12	14 шт/час
notpak	NOT_PAK_REP	— (не используется)	6 шт/час
nc9	TPA140_NC9_REP	NEW_NC9	?
urzbil	URZ_BIL_REP	NEW_BIL	?
uostizm	UOST_IzmLin_REP	NEW_UOST_IzmLin	?
Очень кратко — что происходит каждую минуту
1.	Определяется, какая сейчас смена и когда она началась
2.	Считаются факты за день и ночь (кол-во записей в таблицах репорт)
3.	Считаются простои и брак из таблиц NEW_*
4.	Считается план на месяц с учётом простоев (очень хитрая логика с накоплением эффективного времени до 10.54 ч)
5.	Формируется строка из 16 чисел
6.	Записывается в файл

 Введение

DbtxtExporter — это консольное приложение на базе .NET 9.0, предназначенное для периодического экспорта данных из нескольких SQL Server баз данных в текстовые файлы. Приложение работает как фоновый сервис (BackgroundService), который каждую минуту генерирует отчёты на основе данных о производстве (трубы, простои, планы смен и т.д.). Отчёты экспортируются в указанную директорию в формате TXT и используются, для табло или мониторинга (scoreboard).

 Основные функции:
- Подключение к нескольким базам данных (NKT, NOT, TPA140, N0T).
- Извлечение данных о репортных записях (например, Nkt12Rep, NotPakRep) и задержках (NewNkt12Delay и аналогичные).
- Расчёт показателей за смены: план, факт, простои, брак, эффективность.
- Генерация текстовых отчётов для различных линий производства (nkt12, notpak, nc9, urzbil, uostizm).
- Периодический запуск (каждую минуту) с логированием.

Приложение использует Entity Framework Core для доступа к данным, Microsoft.Extensions.Hosting для хостинга сервиса и конфигурацию через appsettings.json.

 Требования:
- .NET 9.0 SDK.
- SQL Server базы данных с указанными таблицами.
- Доступ к сетевой директории для экспорта (например, \\ITPZVWS01\Scoreboards).

 Архитектура

 Компоненты:
1. Модели (Models): Классы, представляющие сущности из баз данных (например, Nkt12Rep, NewNkt12Delay).
2. Контексты баз данных (DbContexts): Классы для подключения к каждой БД (NktDbContext, NotPakDbContext и т.д.).
3. Сервис (Services): ExportHostedService — основной фоновый сервис, который выполняет логику экспорта.
4. Конфигурация: appsettings.json для строк подключения и настроек экспорта.
5. Программа запуска (Program.cs): Настраивает DI (Dependency Injection), регистрирует сервисы и запускает хост.

 Поток выполнения:
- При запуске: создаётся хост, регистрируются DbContexts и сервис.
- В ExportHostedService: Таймер на 1 минуту. При каждом тике вызывается GenerateAllReports, который извлекает данные и генерирует TXT-файлы.
- Логика отчётов: В методе GenerateReport (приватный в ExportHostedService) для каждого типа линии (prefix).

 Базы данных:
- NKT: Таблица NKT_12_REP (Nkt12Rep).
- NOT: Таблица NOT_PAK_REP (NotPakRep).
- TPA140: Таблицы TPA140_NC9_REP (Tpa140Nc9Rep), URZ_BIL_REP (UrzBilRep), UOST_IzmLin_REP (UostIzmLinRep).
- N0T: Таблицы задержек (NEW_NKT12, NEW_NKT3, NEW_NC9, NEW_BIL, NEW_UOST_IzmLin).

 Конфигурация (appsettings.json)

Файл appsettings.json содержит строки подключения и настройки экспорта. Он встраивается в сборку как EmbeddedResource и копируется в output-директорию.

 Структура:
```json
{
  "ConnectionStrings": {
    "NKT": "Data Source=192.168.11.215,1433;Initial Catalog=NKT_REP;User ID=UserNKTTREND;Password=NKTTREND;TrustServerCertificate=True",
    "NOT": "Data Source=192.168.11.222,1433;Initial Catalog=NOT_REP_DB;User ID=UserNotTrend;Password=NotTrend;TrustServerCertificate=True",
    "TPA140": "Data Source=192.168.11.104,1433;Initial Catalog=TPA140TREND;User ID=UserTPA140;Password=TPA140;TrustServerCertificate=True",
    "N0T": "Data Source=192.168.50.24;Initial Catalog=N0T;User ID=UserASUTP;Password=ASUTP;TrustServerCertificate=true"
  },
  "Export": {
    "OutputPath": "\\\\ITPZVWS01\\Scoreboards",
    "FilePrefix": "orders"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- ConnectionStrings: Строки подключения к SQL Server. Используют аутентификацию по логину/паролю и TrustServerCertificate=True для игнорирования ошибок сертификата.
- Export:
  - OutputPath: Директория для экспорта TXT-файлов (сетевая папка).
  - FilePrefix: Префикс для имён файлов (используется как "orders", но в коде переопределяется для каждого отчёта, например "nkt12").
- Logging: Уровень логирования по умолчанию — Information.

В Program.cs конфигурация загружается с помощью `builder.Configuration.AddJsonFile("appsettings.json")`, а настройки экспорта биндятся к классу ExportSettings с помощью `builder.Services.Configure<ExportSettings>(...)`.

 Модели (Models)

Все модели находятся в пространстве имён DbtxtExporter.Models. Они представляют таблицы баз данных и используются EF Core для маппинга.

 Общие свойства:
- Id: Первичный ключ (int).
- DateTime / DateFrom / DateTo: Временные метки (DateTime).
- Smena: Номер смены (int?, 1-4, где 1/2 — день, 3/4 — ночь).
- Plan: План (int?).
- DelayTimePrevHour / DelayTimeThisHour: Задержки за предыдущий/текущий час (int?).
- Pipes: Количество труб (int, для задержек).
- Bad: Брак (int?).
- PlanA: План A (int, для задержек).
- DelayTime: Общая задержка (int).
- Veracity: Точность/достоверность (decimal).
- AvgCycle: Средний цикл (float?).

 Конкретные модели:

1. ExportSettings (не модель БД, а настройки):
   - OutputPath: string (путь экспорта).
   - FilePrefix: string (префикс файла, по умолчанию "data").

2. Nkt12Rep:
   - Id, DateTime, Smena, Plan, DelayTimePrevHour, DelayTimeThisHour.
   - Таблица: NKT_12_REP.

3. NotPakRep (в коде иногда NotPakMp6Reps, но класс NotPakRep):
   - Id, DateTime, Smena, Plan, DelayTimePrevHour, DelayTimeThisHour.
   - Таблица: NOT_PAK_REP.

4. Tpa140Nc9Rep:
   - Id, DateTime, Smena, Plan, DelayTimePrevHour, DelayTimeThisHour.
   - Таблица: TPA140_NC9_REP.

5. UrzBilRep:
   - Id, DateTime, Smena, Plan, DelayTimePrevHour, DelayTimeThisHour.
   - Таблица: URZ_BIL_REP.

6. UostIzmLinRep:
   - id (маленькая 'i'), DateTime, Smena, Plan, DelayTimePrevHour, DelayTimeThisHour.
   - Таблица: UOST_IzmLin_REP.
   - Обратите внимание: id с маленькой буквы, что необычно для C.

7. NewNkt12Delay:
   - Id, DateFrom, DateTo, Pipes, Bad, PlanA, DelayTime, Smena, Veracity (decimal(6,2)), AvgCycle.
   - Таблица: NEW_NKT12.

8. NewNkt3Delay:
   - Аналогично NewNkt12Delay.
   - Таблица: NEW_NKT3.

9. NewNc9Delay:
   - Аналогично NewNkt12Delay.
   - Таблица: NEW_NC9.

10. NewUrzBilDelay:
    - Аналогично NewNkt12Delay.
    - Таблица: NEW_BIL.

11. NewUostIzmLinDelay:
    - Аналогично NewNkt12Delay.
    - Таблица: NEW_UOST_IzmLin.

 Контексты баз данных (DbContexts)

Каждый DbContext наследует от DbContext и настраивается в OnModelCreating для маппинга таблиц.

1. NktDbContext:
   - DbSet<Nkt12Rep> Nkt12Reps.
   - Таблица: TPA140_NC9_REP, схема dbo, ключ Id.

2. NotPakDbContext:
   - DbSet<NotPakRep> NotPakMp6Reps.
   - Таблица: NOT_PAK_REP, схема dbo, ключ Id.

3. Tpa140DbContext:
   - DbSet<Tpa140Nc9Rep> Tpa140Nc9Reps.
   - Таблица: TPA140_NC9_REP, схема dbo, ключ Id.

4. UrzBilDbContext:
   - DbSet<UrzBilRep> UrzBilReps.
   - Таблица: URZ_BIL_REP, схема dbo, ключ Id.

5. UostIzmLinDbContext:
   - DbSet<UostIzmLinRep> UostIzmLinReps.
   - Таблица: UOST_IzmLin_REP, схема dbo, ключ id (маленькая 'i').

6. DelayDbContext:
   - DbSets для всех NewDelay: NewNkt12Delay, NewNkt3Delay, NewNc9Delay, NewUrzBilDelay, NewUostIzmLinDelay.
   - Таблицы: NEW_NKT12, NEW_NKT3, NEW_NC9, NEW_BIL, NEW_UOST_IzmLin (схема dbo).
   - Ключ: Id для всех.
   - Veracity: decimal(6,2).

В Program.cs контексты регистрируются с UseSqlServer и строками подключения.

 Сервис: ExportHostedService

Это основной класс, наследующий BackgroundService. Он отвечает за периодический экспорт.

 Поля:
- _timer: PeriodicTimer (интервал 1 минута).
- _sp: IServiceProvider (для создания scopes).
- _settings: ExportSettings (из конфигурации).
- _logger: ILogger<ExportHostedService>.

 Конструктор:
- Инициализирует поля.
- Создаёт директорию OutputPath, если не существует.

 Метод ExecuteAsync (override):
- Логирует запуск и путь экспорта.
- Вызывает GenerateAllReports сразу (первый запуск).
- В цикле: ждёт следующий тик таймера, затем вызывает GenerateAllReports.

 Метод GenerateAllReports (private async):
- Создаёт scope для DI.
- Получает DbContexts: dbNkt, dbNot, dbTpa, dbUrz, dbUost (для репортных данных).
- Получает текущую дату/время: now = DateTime.Now, today = DateTime.Today.
- Определяет начало текущей смены (currentShiftStart):
  - Если 8:00-20:00: today + 8 часов (день).
  - Если <8:00: yesterday + 20 часов (ночь, началась вчера).
  - Если >=20:00: today + 20 часов (ночь, началась сегодня).
- Вызывает GenerateReport для каждого типа линии с параметрами:
  - prefix: "nkt12", "notpak", "nc9", "urzbil", "uostizm".
  - db: Соответствующий DbContext (dbNkt для nkt12 и т.д.).
  - delayTable: Соответствующий DbSet из DelayDbContext (NewNkt12Delay и т.д.).
  - planPerHour: Фиксированный план в час (например, 14 для nkt12, 6 для notpak).
  - useDelayDb: bool, true для некоторых (nkt12, nc9 и т.д.), false для других.
  - shiftPlan12h: План на 12-часовую смену (planPerHour  12).
  - currentShiftStart: Начало смены.

 Метод GenerateReport (private async, с параметрами prefix, db, delayTable, planPerHour, useDelayDb, shiftPlan12h, currentShiftStart):
Эта логика генерирует отчёт для одной линии.

 Шаги:
1. Определение периодов:
   - todayStart: Начало дня (00:00).
   - shiftEnd: Конец смены (currentShiftStart + 12 часов).
   - monthlyStart: Начало месяца (1 число, 00:00).

2. Извлечение данных:
   - monthly: Записи за месяц из db.Set<T>(), где DateTime >= monthlyStart.
   - todayRecords: Записи за день (DateTime >= todayStart).
   - Если useDelayDb: Извлекает задержки из delayTable (NewDelay), где DateFrom >= monthlyStart.

3. Расчёты за день/ночь/месяц:
   - dayFact / nightFact: Количество записей за день/ночь (фильтр по времени DateTime).
   - dayBad / nightBad: Брак (Bad) из задержек, если useDelayDb (сумма Bad за день/ночь).
   - dayDowntime / nightDowntime: Простои (DelayTime) из задержек (сумма DelayTime / 60 для часов).

4. Расчёты по сменам (A, Б, В, Г):
   - Группировка monthly по Smena (1-4).
   - Для каждой смены: Расчёт плана с учётом эффективности (veracity? Нет, логика на основе простоев).
   - Сортировка простоев (delays) по DateFrom.
   - running: Накопленные часы.
   - Для каждого простоя: рассчитывается duration (часы между DateFrom текущего и следующего).
   - eff: Эффективные часы (до лимита 10.54 часа — это константа).
   - planBySmena[smena] += planPerHour  eff (округляется).
   - factDict: Факт по сменам (количество записей).

   Детальная логика расчёта плана по сменам:
   - planBySmena: Dictionary<int, decimal> для смен 1-4.
   - delays: Задержки за месяц, сгруппированные по Smena.
   - Для каждой группы (по Smena):
     - sorted: Сортировка по DateFrom.
     - running = 0m.
     - Для i от 0 до sorted.Count-1:
       - curr = sorted[i].
       - next = sorted[i+1].DateFrom или shiftEnd.
       - duration = (next - curr.DateFrom).TotalHours.
       - prev = running.
       - running += duration.
       - eff = if prev >= 10.54m then 0m else if running <= 10.54m then duration else 10.54m - prev.
     - planBySmena[smena] += planPerHourForSmena  eff (planPerHourForSmena = planPerHour, кроме TPA140? В коде фиксировано).
   - Затем: planA/B/V/G = Round(planBySmena[1/2/3/4]).
   - factA/B/V/G = factDict.GetValueOrDefault(1/2/3/4, 0).

   Особенности:
   - Для TPA140 (nc9): используется фиксированный план.
   - Константа 10.54m: норма рабочих часов в смене (12 – перерывы).
   - Veracity и AvgCycle: не используются в расчётах.

5. Формирование строки отчёта:
   - line = $"{shiftPlan12h};{dayFact};{dayDowntime};{dayBad};{shiftPlan12h};{nightFact};{nightDowntime};{nightBad};{planA};{factA};{planB};{factB};{planV};{factV};{planG};{factG}".
   - Формат: Разделитель ';', значения int.

6. Запись файла:
   - folderPath = OutputPath + prefix.ToUpper().
   - path = folderPath + $"report_{prefix}.txt".
   - WriteAllTextAsync(line + NewLine).
   - Логирует [OK] с prefix и line.

 Вспомогательные методы (private static):
1. GetId<T>(T obj): возвращает значение свойства "Id" (int).
2. GetIntProperty<T>(T obj, string propertyName): Возвращает int? свойства по имени.

 Логика смен и времени

- Смены: 1/2 — день (A/B?), 3/4 — ночь (V/G?).
- День/Ночь: День: 8:00-20:00, Ночь: 20:00-8:00.
- Начало смены: Логика в GenerateAllReports обеспечивает правильный расчёт для текущей смены.
- Простоев: учитываются только до 10.54 часов эффективности на смену.
- Брак и простои: только если useDelayDb=true (для линий с задержками).

 Развёртывание и запуск

- Собрать проект: dotnet build.
- Запустить: dotnet run.
- Файлы экспорта: В OutputPath / PREFIX_UPPER / report_prefix.txt.
- Логи: В консоль (Information уровень).

