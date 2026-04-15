# Com.H.Threading.Scheduler

For full documentation, visit the [GitHub project page](https://github.com/H7O/Com.H.Threading.Scheduler).

An easy-to-use, feature-rich, open-source scheduling framework for building background services as Windows services, Linux daemons, or containerized microservices.

## Quick start

Define tasks in an XML configuration file with scheduling rules in `<sys>` and custom data in any other tags:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>11:00</time>
    </sys>
    <greeting_message>Good morning! It's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```

> Program.cs
```csharp
using Com.H.Threading.Scheduler;

var configPath = Path.Combine(AppContext.BaseDirectory, "scheduler.xml");
if (!File.Exists(configPath)) throw new FileNotFoundException(configPath);

var scheduler = new HTaskScheduler(configPath);
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine(e["greeting_message"]);
};

Console.WriteLine("Press <ctrl+c> to exit.");
await scheduler.StartAsync();
```

## Interval scheduling

Run every 3 seconds between 9:00 AM and 2:00 PM on specific days:

```xml
<sys>
  <time>09:00</time>
  <until_time>14:00</until_time>
  <interval>3000</interval>
  <dow>Monday,Thursday</dow>
</sys>
```

## Scheduling rules

All rules compose — every condition must be true for a task to run.

| Tag | Description | Format | Example |
|-----|-------------|--------|---------|
| `enabled` | Enable or disable the task | `true` / `false` | `true` |
| `not_before` | Earliest date/time to run | `yyyy-MM-dd HH:mm:ss` | `2026-06-01 08:00:00` |
| `not_after` | Latest date/time to run | `yyyy-MM-dd HH:mm:ss` | `2026-12-31 23:59:59` |
| `dates` | Specific dates (pipe-delimited) | `yyyy-MM-dd` | `2026-01-15\|\|2026-07-04` |
| `doy` | Days of the year | Integer 1–366 | `1,60,120..130` |
| `eom` | Last day of month | `true` / `false` | `true` |
| `bom` | First day of month | `true` / `false` | `true` |
| `dom` | Days of the month | Integer 1–31 | `1,15,28..31` |
| `dow` | Days of the week | Weekday names | `Monday,Friday` |
| `time` | Start time | `HH:mm` | `14:30` |
| `until_time` | End time | `HH:mm` | `23:00` |
| `interval` | Milliseconds between runs | Positive integer | `5000` |
| `ignore_log_on_restart` | Force re-run on restart | `true` / `false` | `true` |

## Retry on error

```xml
<sys>
  <interval>10000</interval>
  <retry_attempts_on_error>3</retry_attempts_on_error>
  <sleep_on_error>5000</sleep_on_error>
</sys>
```

## Variables

Built-in placeholders replaced at runtime:

| Variable | Example | Result |
|----------|---------|--------|
| `{now{yyyy-MM-dd}}` | `{now{yyyy-MM-dd}}` | `2026-04-15` |
| `{tomorrow{yyyy-MM-dd}}` | `{tomorrow{yyyy-MM-dd}}` | `2026-04-16` |
| `{dir{sys}}` | `{dir{sys}}\data` | `C:\app\bin\data` |
| `{dir{uri}}` | `{dir{uri}}/data` | `file:///C:/app/bin/data` |

## Repeat

Execute a task multiple times with different data per iteration. Supports XML (default), JSON, CSV, and pipe-separated formats:

```xml
<sys>
  <interval>60000</interval>
  <repeat content_type="json" delay_interval="500">
    [
      {"id": 1, "name": "alice"},
      {"id": 2, "name": "bob"}
    ]
  </repeat>
</sys>
<message>Processing {var{name}} (ID: {var{id}})</message>
```

### Repeat from external source

Chain content types to fetch and parse: `uri > json`, `uri > csv`, `uri > xml`, etc.

```xml
<repeat content_type="uri > json" delay_interval="500">
  https://api.example.com/users
</repeat>
```

File URIs are also supported:

```xml
<repeat content_type="uri > csv" delay_interval="500">
  {dir{uri}}/data/users.csv
</repeat>
```

## External settings

Fetch tag content from URLs with optional caching:

```xml
<config content_type="uri" content_cache="once_per_day">
  https://api.example.com/config
</config>
```

Cache options: `none` (default), `once_per_day` / `daily`, or milliseconds (e.g., `3600000`).

### Dynamic scheduling with URI — skip holidays

`content_type="uri"` also works on scheduling rules inside `<sys>`. Point `<enabled>` at an API — if the response contains `true` (case-insensitive), the task runs; otherwise it's skipped:

```xml
<sys>
  <time>09:00</time>
  <dow>Monday,Tuesday,Wednesday,Thursday,Friday</dow>
  <enabled content_type="uri">https://api.example.com/is-business-day?date={now{yyyy-MM-dd}}</enabled>
</sys>
```

Add `content_cache="once_per_day"` to avoid calling the API on every tick — the result is cached until midnight:

```xml
<enabled content_type="uri" content_cache="once_per_day">https://api.example.com/is-business-day?date={now{yyyy-MM-dd}}</enabled>
```

## Custom placeholder markers

Override the default `{{` / `}}` markers on any tag:

```xml
<sql open-marker="[%" close-marker="%]" null-value="N/A">
  INSERT INTO users (name, email) VALUES ('[%var{name}%]', '[%var{email}%]')
</sql>
```

## Error handling

```csharp
scheduler.TaskExecutionError += async (object sender, HTaskExecutionErrorEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Task failed: {e.Exception.Message}");
};

scheduler.TaskLoadingError += async (object sender, HErrorEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Config error: {e.Exception.Message}");
};
```

## Worker service integration

For .NET worker services, use [Com.H.Threading.Scheduler.DI](https://www.nuget.org/packages/Com.H.Threading.Scheduler.DI/) for `IServiceCollection` integration. Installing it automatically includes this library as a dependency.

