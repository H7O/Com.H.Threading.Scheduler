# Com.H.Threading.Scheduler

An easy-to-use, feature-rich, open-source scheduling framework for building background services as Windows services, Linux daemons, or containerized microservices.

## Purpose

This library lets you write background services while focusing only on the business logic. You define **when** tasks should run in a simple XML configuration file, and the scheduling engine takes care of the rest — concurrency, persistence, error recovery, retries, and hot-reloading of configuration changes at runtime.

## Installation

Install via NuGet:

```
dotnet add package Com.H.Threading.Scheduler
```

Or browse the package on [nuget.org](https://www.nuget.org/packages/Com.H.Threading.Scheduler/).

Alternatively, clone this repo, build, and reference the output DLLs directly.

### Dependency injection for worker services

For .NET worker services hosted as Windows services or Linux daemons, use the [Com.H.Threading.Scheduler.DI](https://github.com/h7o/Com.H.Threading.Scheduler.DI) extension package which provides `IServiceCollection` integration:

```
dotnet add package Com.H.Threading.Scheduler.DI
```

Installing the DI package automatically includes this base library as a dependency.

## Quick start

The scheduler reads task definitions from an XML configuration file. Each task has a `<sys>` element that defines **when** it runs, and any number of custom elements that carry **data** your code receives at runtime.

## Examples

The examples below progress from simple to advanced, gradually introducing the scheduler's features.

---

### Example 1 — Run once a day at a specific time

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

**What's happening here:**

- `<tasks_list>` is the root container — the tag name can be anything you like.
- Each `<task>` defines a scheduled unit of work.
- `<sys>` contains scheduling rules. Here, `<time>11:00</time>` means "run once per day at 11:00 AM."
- `<greeting_message>` is a **custom tag** — the engine passes it through to your code untouched.

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

**Output** (at 11:00 AM):
```
Good morning! It's 11:00 AM!
```

#### How persistence works

After the task runs, a `scheduler.xml.log` file appears alongside `scheduler.xml`. This log tracks which tasks have already executed (keyed by a SHA-256 hash of each task's XML content), so tasks don't re-run unexpectedly after an application restart.

If you modify a task's XML, its hash changes and the engine treats it as a new task with a clean execution history, making it eligible to run again.

#### Async vs fire-and-forget

`StartAsync` blocks (awaits) until cancelled. If you need the scheduler running in the background while doing other work, use `Start` instead:

```csharp
using Com.H.Threading.Scheduler;

var configPath = Path.Combine(AppContext.BaseDirectory, "scheduler.xml");
if (!File.Exists(configPath)) throw new FileNotFoundException(configPath);

var scheduler = new HTaskScheduler(configPath);
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine(e["greeting_message"]);
};

scheduler.Start(); // non-blocking — runs in the background

Console.WriteLine("Press <enter> to exit.");
Console.ReadLine();
scheduler.Stop();
```

---

### Example 2 — Run on a fixed interval

Replace `<time>` with `<interval>` (in milliseconds) to run a task repeatedly throughout the day:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <interval>3000</interval>
    </sys>
    <greeting_message>Hello there!</greeting_message>
  </task>
</tasks_list>
```

This runs every 3 seconds, all day. The C# code from Example 1 works unchanged.

---

### Example 3 — Interval within a time window

Combine `<interval>` with `<time>` and `<until_time>` to restrict when the interval is active:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>09:00</time>
      <until_time>14:00</until_time>
      <interval>3000</interval>
    </sys>
    <greeting_message>Hello there!</greeting_message>
  </task>
</tasks_list>
```

This runs every 3 seconds between 9:00 AM and 2:00 PM each day.

> **Note:** If `<time>` is omitted, the window starts at midnight. If `<until_time>` is omitted, it runs until end of day.

---

### Example 4 — Restrict to specific days of the week

Add `<dow>` to limit which days the task runs:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>09:00</time>
      <until_time>14:00</until_time>
      <interval>3000</interval>
      <dow>Monday,Thursday</dow>
    </sys>
    <greeting_message>Hello there!</greeting_message>
  </task>
</tasks_list>
```

All scheduling rules are composable — the engine requires **all** conditions to be true before running a task.

---

## Scheduling rules reference

All rules are placed inside the `<sys>` element. When multiple rules are present, **all** must be satisfied for a task to run.

| Tag | Description | Format | Example |
|-----|-------------|--------|---------|
| `enabled` | Enable or disable the task | `true` / `false` | `true` |
| `not_before` | Earliest date/time the task can run | `yyyy-MM-dd HH:mm:ss` | `2026-06-01 08:00:00` |
| `not_after` | Latest date/time the task can run | `yyyy-MM-dd HH:mm:ss` | `2026-12-31 23:59:59` |
| `dates` | Run only on specific dates (pipe-delimited) | `yyyy-MM-dd` or `MM-dd` | `2026-01-15\|\|2026-07-04` |
| `doy` | Days of the year (comma or range) | Integer 1–366 | `1,60,120..130` |
| `eom` | Run on the last day of the month | `true` / `false` | `true` |
| `bom` | Run on the first day of the month | `true` / `false` | `true` |
| `dom` | Days of the month (comma or range) | Integer 1–31 | `1,15,28..31` |
| `dow` | Days of the week (comma-separated, case-insensitive) | Weekday names | `Monday,Wednesday,Friday` |
| `time` | Time of day to start (or single daily run if no interval) | `HH:mm` or `HH:mm:ss` | `14:30` |
| `until_time` | Time of day to stop running | `HH:mm` or `HH:mm:ss` | `23:00` |
| `interval` | Milliseconds between consecutive runs | Positive integer | `5000` |
| `ignore_log_on_restart` | Clear this task's log on scheduler restart, forcing it to re-run | `true` / `false` | `true` |

**Evaluation order:** `enabled` → `not_before` → `not_after` → `dates` → `doy` → `eom`/`bom`/`dom` → `dow` → `time` → `until_time` → `interval` (or once-per-day check).

### Range syntax for `dom`, `doy`

Use `..` for ranges: `<dom>1,15,25..28</dom>` matches the 1st, 15th, and 25th through 28th of each month.

---

## Custom tags

Any XML element outside of `<sys>` is a **custom tag**. The engine passes these through to your code via the `HTaskEventArgs` indexer.

Custom tags are useful for:
- Passing configuration data to your task logic
- Identifying which task triggered the event when multiple tasks share one handler

### Example 5 — Multiple tasks with different handlers

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <name>print a message</name>
    <sys>
      <interval>3000</interval>
    </sys>
    <greeting_message>Hello there!</greeting_message>
  </task>
  <task>
    <name>calculate some numbers</name>
    <sys>
      <interval>2000</interval>
    </sys>
    <some_numbers>32,56,4,67,1</some_numbers>
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
    switch (e["name"])
    {
        case "print a message":
            Console.WriteLine(e["greeting_message"]);
            break;
        case "calculate some numbers":
            var sum = e["some_numbers"]!
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).Sum();
            Console.WriteLine($"sum of {e["some_numbers"]} = {sum}");
            break;
    }
};

Console.WriteLine("Press <ctrl+c> to exit.");
await scheduler.StartAsync();
```

### Nested custom tags

Custom tags can be nested. Access child elements using a path separator (`/`):

```xml
<task>
  <sys><interval>5000</interval></sys>
  <config>
    <database>Server=myserver;Database=mydb</database>
    <timeout>30</timeout>
  </config>
</task>
```

```csharp
string connStr = e["config/database"];  // "Server=myserver;Database=mydb"
string timeout = e["config/timeout"];   // "30"
```

You can also retrieve child items programmatically:

```csharp
IHTaskItem? configItem = e.GetItem("config");
IEnumerable<string?>? allValues = e.GetValues("config/database");
```

---

## Retry on error

The scheduler can automatically retry a task when it throws an exception, with configurable attempt limits and delay between retries.

| Tag | Description | Format |
|-----|-------------|--------|
| `retry_attempts_on_error` | Maximum number of retry attempts | Positive integer |
| `sleep_on_error` | Milliseconds to wait before retrying | Positive integer (ms) |

### Example 6 — Retry a failing task

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <name>flaky api call</name>
    <sys>
      <interval>10000</interval>
      <retry_attempts_on_error>3</retry_attempts_on_error>
      <sleep_on_error>5000</sleep_on_error>
    </sys>
    <api_url>https://api.example.com/data</api_url>
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
    // If this throws, the scheduler retries up to 3 times, waiting 5 seconds between attempts
    using var http = new HttpClient();
    var result = await http.GetStringAsync(e["api_url"], ct);
    Console.WriteLine($"Received {result.Length} chars from API");
};

// Subscribe to error events for logging
scheduler.TaskExecutionError += async (object sender, HTaskExecutionErrorEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Task error: {e.Exception.Message}");
};

Console.WriteLine("Press <ctrl+c> to exit.");
await scheduler.StartAsync();
```

When retry is enabled, exceptions are **suppressed** from the caller and the `TaskExecutionError` event is raised instead. The error count resets to zero after a successful execution.

---

## Variables

The scheduler supports placeholder variables in custom tag values. These are replaced at runtime before your code receives the data.

### Built-in variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{now{format}}` | Current date/time with a .NET format string | `{now{yyyy-MM-dd}}` → `2026-04-15` |
| `{tomorrow{format}}` | Tomorrow's date with a .NET format string | `{tomorrow{yyyy-MM-dd}}` → `2026-04-16` |
| `{dir{sys}}` | Application base directory (OS path) | `C:\app\bin` |
| `{dir{uri}}` | Application base directory (URI format) | `file:///C:/app/bin` |

### Example 7 — Using date variables in task data

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>06:00</time>
    </sys>
    <report_date>{now{yyyy-MM-dd}}</report_date>
    <output_path>{dir{sys}}\reports\report_{now{yyyyMMdd}}.csv</output_path>
  </task>
</tasks_list>
```

```csharp
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Generating report for {e["report_date"]}");
    Console.WriteLine($"Saving to {e["output_path"]}");
};
```

**Output** (on 2026-04-15):
```
Generating report for 2026-04-15
Saving to C:\app\bin\reports\report_20260415.csv
```

---

## Repeat

The `<repeat>` element (inside `<sys>`) lets a task execute multiple times per trigger, once for each child element — useful for batch processing with different parameters per iteration.

### Repeat variables

During each iteration, properties from the repeat data are accessible through `{var{property_name}}` placeholders in custom tags.

| Attribute | Description | Format |
|-----------|-------------|--------|
| `delay_interval` or `delay-interval` | Milliseconds to wait between iterations | Positive integer |

### Example 8 — Processing a batch of items

The content inside `<repeat>` can be XML, JSON, CSV, or pipe-separated values. Each child element (or row) becomes one iteration, with its properties accessible via `{var{property_name}}`.

#### XML format (default)

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <interval>60000</interval>
      <repeat delay_interval="500">
        <user>
          <id>1</id>
          <name>alice</name>
        </user>
        <user>
          <id>2</id>
          <name>bob</name>
        </user>
        <user>
          <id>3</id>
          <name>charlie</name>
        </user>
      </repeat>
    </sys>
    <message>Processing user {var{name}} (ID: {var{id}})</message>
  </task>
</tasks_list>
```

```csharp
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine(e["message"]);
};
```

**Output** (each iteration 500ms apart):
```
Processing user alice (ID: 1)
Processing user bob (ID: 2)
Processing user charlie (ID: 3)
```

#### JSON format

```xml
<repeat content_type="json" delay_interval="500">
  [
    {"id": 1, "name": "alice"},
    {"id": 2, "name": "bob"},
    {"id": 3, "name": "charlie"}
  ]
</repeat>
```

#### CSV format

```xml
<repeat content_type="csv" delay_interval="500">
  id,name
  1,alice
  2,bob
  3,charlie
</repeat>
```

#### Pipe-separated format

```xml
<repeat content_type="psv" delay_interval="500">
  id|name
  1|alice
  2|bob
  3|charlie
</repeat>
```

All four formats produce the same result — `{var{id}}` and `{var{name}}` resolve identically regardless of input format.

---

## External settings

Custom tags can fetch their content from external HTTP/HTTPS URLs at runtime using the `content_type="uri"` attribute.

| Attribute | Description |
|-----------|-------------|
| `content_type="uri"` | Fetch tag value from the URL specified in the tag text |
| `uri_referer` | Set the HTTP Referer header on the request |
| `uri_user_agent` | Set the HTTP User-Agent header on the request |

### External settings cache

To avoid fetching on every task execution, add a `content_cache` attribute:

| Value | Behavior |
|-------|----------|
| `none` | No caching (default) |
| `once_per_day` / `daily` | Cache until midnight |
| Numeric (milliseconds) | Cache for the specified duration |

### Example 9 — Fetching configuration from an API

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <interval>30000</interval>
    </sys>
    <config content_type="uri" content_cache="once_per_day">
      https://api.example.com/task-config
    </config>
  </task>
</tasks_list>
```

```csharp
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    // e["config"] contains the HTTP response body, cached for the day
    Console.WriteLine($"Config: {e["config"]}");
};
```

### Using URI to control scheduling — skip holidays

The `content_type="uri"` feature isn't limited to custom tags — it also works on scheduling rules inside `<sys>`. A practical use case is the `<enabled>` tag: you can point it at an API that returns whether today is a business day, and the engine will dynamically enable or disable the task.

The engine checks whether the API response **contains** the word `true` (case-insensitive). Any response format works — JSON, XML, plain text — as long as "true" appears somewhere in the response body.

### Example — Skip tasks on holidays

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>09:00</time>
      <interval>60000</interval>
      <dow>Monday,Tuesday,Wednesday,Thursday,Friday</dow>
      <enabled content_type="uri">https://api.example.com/is-business-day?date={now{yyyy-MM-dd}}</enabled>
    </sys>
    <name>business hours task</name>
    <message>Running business logic...</message>
  </task>
</tasks_list>
```

If the API returns `{"is_business_day": true}` or simply `true`, the task runs. On holidays, the API returns something like `{"is_business_day": false}` and the task is skipped for the day.

Note how `{now{yyyy-MM-dd}}` passes today's date to the API — built-in variables are resolved before the URI is fetched.

However, in the example above, the engine calls the API on **every** scheduling tick (default: every second). Since the holiday status doesn't change throughout the day, we can add `content_cache="once_per_day"` to call the API only once and cache the result until midnight:

```xml
<enabled content_type="uri" content_cache="once_per_day">https://api.example.com/is-business-day?date={now{yyyy-MM-dd}}</enabled>
```

This way, the first check of the day calls the API and caches the response. Every subsequent check reuses the cached result — keeping the scheduling efficient without unnecessary API calls.

> **Fail-secure behavior:** If the API call fails (network error, timeout, etc.), the engine **does not execute the task** and **does not cache the failure**. On the next tick it simply retries the call. This means a temporary API outage won't accidentally run — or permanently skip — the task. Execution only proceeds once a definitive response is received.

---

## Content type processors

Custom tags support a `content_type` attribute for parsing structured data into dynamic objects, accessible via `GetModel()` and `GetModels()`.

| `content_type` | Description |
|-----------------|-------------|
| `uri` | Fetch content from URL |
| `json` | Parse tag text as JSON |
| `xml` | Parse tag text as XML |
| `csv` | Parse tag text as comma-separated values |
| `psv` | Parse tag text as pipe-separated values |

### Chaining content types

Content types can be **chained** using `>`, `->`, `=>`, or `,` as separators. Processors execute left to right, each feeding its output to the next.

For example, `content_type="uri > json"` first fetches from the URL, then parses the response as JSON.

### Example 10 — Fetching and parsing a repeat list from an API

This combines external URI fetching with repeat iteration. The `<repeat>` element fetches a JSON array from an API, and each item becomes an iteration:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <interval>60000</interval>
      <repeat content_type="uri > json" delay_interval="500">
        https://api.example.com/users
      </repeat>
    </sys>
    <message>Processing user {var{name}} (ID: {var{id}})</message>
  </task>
</tasks_list>
```

If the API returns:
```json
[
  {"id": 1, "name": "alice"},
  {"id": 2, "name": "bob"},
  {"id": 3, "name": "charlie"}
]
```

**Output:**
```
Processing user alice (ID: 1)
Processing user bob (ID: 2)
Processing user charlie (ID: 3)
```

Other chaining combinations work the same way: `uri > csv`, `uri > xml`, `uri > psv`, etc.

### File URIs

The `uri` content type supports `file://` URIs in addition to `http://` and `https://`. This lets you load content from local files:

```xml
<repeat content_type="uri > csv" delay_interval="500">
  file:///C:/data/users.csv
</repeat>
```

You can also use the built-in `{dir{uri}}` variable to reference files relative to the application directory:

```xml
<repeat content_type="uri > csv" delay_interval="500">
  {dir{uri}}/data/users.csv
</repeat>
```

### Custom placeholder markers

By default, variable placeholders use `{{` and `}}`. You can override this with attributes on any custom tag:

| Attribute | Default | Description |
|-----------|---------|-------------|
| `open-marker` | `{{` | Opening placeholder marker |
| `close-marker` | `}}` | Closing placeholder marker |
| `null-value` | (empty) | Value to substitute when a variable is null |

### Example — Custom markers and null handling

This is useful when tag content conflicts with the default `{{` / `}}` markers (e.g., JSON templates), or when you want missing variables to show a fallback value instead of being blank:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <interval>60000</interval>
      <repeat content_type="json">
        [
          {"name": "alice", "email": "alice@example.com"},
          {"name": "bob", "email": null}
        ]
      </repeat>
    </sys>
    <sql open-marker="[%" close-marker="%]" null-value="N/A">
      INSERT INTO users (name, email) VALUES ('[%var{name}%]', '[%var{email}%]')
    </sql>
  </task>
</tasks_list>
```

```csharp
scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine(e["sql"]);
};
```

**Output:**
```
INSERT INTO users (name, email) VALUES ('alice', 'alice@example.com')
INSERT INTO users (name, email) VALUES ('bob', 'N/A')
```

---

## Error handling

The scheduler exposes three async events:

| Event | EventArgs Type | When |
|-------|---------------|------|
| `TaskIsDue` | `HTaskEventArgs` | A task meets all scheduling criteria |
| `TaskExecutionError` | `HTaskExecutionErrorEventArgs` | A task throws an exception (when retry is configured) |
| `TaskLoadingError` | `HErrorEventArgs` | XML config file fails to parse or load |

### Example 11 — Handling loading and execution errors

```csharp
scheduler.TaskLoadingError += async (object sender, HErrorEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Config error: {e.Exception.Message}");
};

scheduler.TaskExecutionError += async (object sender, HTaskExecutionErrorEventArgs e, CancellationToken ct) =>
{
    Console.WriteLine($"Task failed: {e.Exception.Message}");
    // Access the task that failed:
    Console.WriteLine($"Task name: {e.EventArgs["name"]}");
};
```

---

## Cancellation and graceful shutdown

`HTaskScheduler` implements `IDisposable`. The `CancellationToken` passed to your event handler is linked to the scheduler's lifecycle — use it for cooperative cancellation.

### Example 12 — Graceful shutdown with cancellation

```csharp
using Com.H.Threading.Scheduler;

var configPath = Path.Combine(AppContext.BaseDirectory, "scheduler.xml");
if (!File.Exists(configPath)) throw new FileNotFoundException(configPath);

using var scheduler = new HTaskScheduler(configPath);

scheduler.TaskIsDue += async (object sender, HTaskEventArgs e, CancellationToken ct) =>
{
    // Use the cancellation token in long-running operations
    using var http = new HttpClient();
    var data = await http.GetStringAsync(e["api_url"], ct);
    
    // Check cancellation between steps
    ct.ThrowIfCancellationRequested();
    
    await File.WriteAllTextAsync(e["output_path"], data, ct);
};

// Wire up ctrl+c for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

await scheduler.StartAsync(cts.Token);
Console.WriteLine("Scheduler stopped.");
```

---

## Multi-file configuration

If you pass a **directory path** to the `HTaskScheduler` constructor instead of a file path, it loads all `*.xml` files in that directory. Files are watched for changes at runtime — modified files are automatically reloaded without restarting the application.

```csharp
// Load all XML files from the "config" directory
var configDir = Path.Combine(AppContext.BaseDirectory, "config");
var scheduler = new HTaskScheduler(configDir);
```

---

## Custom task sources

For non-XML sources (databases, JSON files, APIs, etc.), implement the `IHTaskCollection` and `IHTaskTimeLogger` interfaces and pass them to the overloaded constructor:

```csharp
var tasks = new MyDatabaseTaskCollection();
var logger = new MyDatabaseTimeLogger();
var scheduler = new HTaskScheduler(tasks, logger);
```

> **Note:** When `IHTaskTimeLogger` is omitted, logging defaults to in-memory only. Task execution history is lost on restart, so all eligible tasks will re-run.
