# Com.H.Threading.Scheduler
Kindly visit the project's github page for full documentation [https://github.com/H7O/Com.H.Threading.Scheduler](https://github.com/H7O/Com.H.Threading.Scheduler)

Below is a cut down version of the documentation.

# What is this library

An easy-to-use, feature-rich, open-source framework for creating middlewares as low-resource Windows services, Linux daemons, and microservices.

## Purpose

The library simplifies building solutions that require time-sensitive logic to conditionally run as background processes without user interaction.

While many projects may not require such time-sensitive operations, those that do will find the tools provided by this library especially useful. The goal is to allow developers to focus solely on their business logic while the library handles task scheduling with an efficient and reliable scheduler.

## Installation

The easiest way to install this library is through this NuGet package manager

Alternatively, you can clone/download the library from the GitHub repository, compile it, and add the resulting DLLs to your project.

If you want to use this library in a worker process hosted as a Windows service, follow the instructions here: [Com.H.Threading.Scheduler.DI](https://github.com/h7o/Com.H.Threading.Scheduler.DI). This guide explains how to integrate using an extension for `IServiceCollection` for .NET Core worker services.

You can find the NuGet package for the `IServiceCollection` extension at [Com.H.Threading.Scheduler.DI](https://www.nuget.org/packages/Com.H.Threading.Scheduler.DI/). Installing the extension package will automatically include this base scheduler library as a dependency.

## Examples

Below are examples, organized from basic scheduling requirements to more comprehensive setups showcasing the full feature set.

### Example 1: Run Code Once a Day at a Specific Time

This example demonstrates running code once per day at a specific time using the library in a console application.

First, create a configuration file to specify scheduling rules:

> **scheduler.xml**

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

- `<tasks_list>`: Container for tasks (or services/processes).
- `<task>`: Individual task within the list.
- `<sys>`: The scheduling engine reads this tag for task execution rules.
- `<time>`: Specifies the time of day (11:00 AM).
- `<greeting_message>`: Custom tag passed to the code when the task is executed.

The configuration above instructs the scheduler engine to run the specified code daily at 11:00 AM.

Next, write the code to handle the task:

> **Program.cs**

```csharp
using System.Threading.Tasks;
using Com.H.Threading.Scheduler;

namespace Com.H.Threading.Scheduler_Samples
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "scheduler.xml");
            if (!File.Exists(configPath)) throw new FileNotFoundException(configPath);

            var scheduler = new HTaskScheduler(configPath);
            scheduler.TaskIsDue += Scheduler_TaskIsDue;

            Console.WriteLine("Press <ctrl> + c to exit.");
            await scheduler.StartAsync();
        }

        private static async Task Scheduler_TaskIsDue(object sender, HTaskEventArgs e, CancellationToken cancellationToken = default)
        {
            Console.WriteLine(e["greeting_message"]);
        }
    }
}
```

**Output:**

```bash
Good morning! It's 11:00 AM!
Press <ctrl> + c to exit.
```

A log file (`scheduler.xml.log`) is generated to track tasks, ensuring persistence across application restarts and preventing re-execution unless intended. The scheduler also detects changes made to the configuration file and re-evaluates task conditions accordingly.

### Example 2: Run Code at Regular Intervals

Modify the configuration to run the code every 3 seconds throughout the day:

> **scheduler.xml**

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

- `<interval>`: Defines how often to run the task (in milliseconds).

### Example 3: Interval Scheduling Within a Specific Time Range

To run the code every 3 seconds between 9:00 AM and 2:00 PM daily, modify the configuration as follows:

> **scheduler.xml**

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

- `<time>`: Specifies when to start.
- `<until_time>`: Specifies when to stop.

### Example 4: Interval Scheduling on Specific Days of the Week

Run the code every 3 seconds between 9:00 AM and 2:00 PM only on Mondays and Thursdays:

> **scheduler.xml**

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

## General Conditional Tags

| Tag        | Description                          | Format                        | Example         |
|------------|--------------------------------------|-------------------------------|-----------------|
| `interval` | Run frequency (milliseconds)         | milliseconds                  | 3000            |
| `time`     | Start time                           | HH:mm                         | 14:32           |
| `until_time`| End time                            | HH:mm                         | 23:15           |
| `dow`      | Days of the week allowed to run      | Comma-separated weekday names | Monday,Thursday |
| `dom`      | Days of the month allowed to run     | Days of the month             | 1,5,23          |
| `eom`      | End of month                         | true or false                 | true            |
| `doy`      | Days of the year allowed to run      | Days of the year              | 53,250,300      |
| `dates`    | Specific dates (pipe delimited)      | yyyy-MM-dd                    | 2077-01-23      |
| `enabled`  | Enable or disable the task           | true or false                 | true            |
| `not_before`| Don't run before a specific time    | yyyy-MM-dd HH:mm:ss           | 2077-01-23 14:23|
| `not_after` | Don't run after a specific time     | yyyy-MM-dd HH:mm:ss           | 2077-01-23 14:23|

## Custom Tags

Custom tags are configuration details that the scheduler engine passes to your code when executing a task. You can add as many as you need to pass information about each task.

For instance, in the previous examples, we used the `<greeting_message>` tag to pass a message to be printed. You can also use custom tags to identify tasks in large applications, routing logic based on task names or IDs.

### Example 5: Running Multiple Tasks

Run two different tasks: one to print a message and another to calculate a sum.

> **scheduler.xml**

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

> **Program.cs**

```csharp
using Com.H.Threading.Scheduler;

class Program
{
    static async Task Main(string[] args)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "scheduler.xml");
        if (!File.Exists(configPath)) throw new FileNotFoundException(configPath);

        var scheduler = new HTaskScheduler(configPath);
        scheduler.TaskIsDue += Scheduler_TaskIsDue;

        Console.WriteLine("Press <ctrl> + c to exit.");
        await scheduler.StartAsync();
    }

    private static async Task Scheduler_TaskIsDue(object sender, HTaskEventArgs e, CancellationToken cancellationToken = default)
    {
        switch (e["name"] as string)
        {
            case "print a message":
                ProcessPrintMessageTask(e);
                break;
            case "calculate some numbers":
                ProcessCountNumbersTask(e);
                break;
            default:
                Console.WriteLine("Unknown task");
                break;
        }
    }

    static void ProcessPrintMessageTask(HTaskEventArgs e)
    {
        Console.WriteLine(e["greeting_message"]);
    }

    static void ProcessCountNumbersTask(HTaskEventArgs e)
    {
        int sum = e["some_numbers"].Split(',').Select(int.Parse).Sum();
        Console.WriteLine($"Sum of {e["some_numbers"]} = {sum}");
    }
}
```

> **Note:** You can use tags like `<id>` or `<name>` to identify tasks, as per your workflow.

## Documentation In Progress

- **Special conditional tags**
- **Retry on error**
  - Retry attempts
  - Retry interval
- **Variables**
- **External settings**
  - External settings cache
- **Repeat**
  - Repeat variables
- **Cancellation Tokens and exiting gracefully**

Stay tuned for more updates.

---

## Future Roadmap

Documentation in progress. Stay tuned.

