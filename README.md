# Com.H.Threading.Scheduler
An easy to use, simple, yet feature-rich open-source framework for writing middlewares in the form of low resource footnote Windows services and/or Linux Daemons. Written in .NET starndard 2.0 to offer the convenience of using it under both older .NET framework 4.8 and newer .NET 5 and beyond.
 
## What's the purpose of this library?
This library helps build solutions that require some of their time sensitive logic to conditinally run as a background process without user input.

And although most solutions might not have the need for such time-sensitive background process, the ones that do would hopefully benefit from what this library offers.

One of the goals this library aims to achieve is help developers write background services while focusing only on what matters, which is the logic that needs to run in the background, and leaving the process scheduling details conveniently hidden behind an easy to use, feature-rich, and efficient scheduling engine tucked away in this small library.

## How to install
Perhaps the easiest way is to install the package for this library is done via NuGet package manager [https://www.nuget.org/packages/Com.H.Threading.Scheduler/](https://www.nuget.org/packages/Com.H.Threading.Scheduler/)
 
Alternatively, you can clone / download the library from this github repo, compile, then reference the output dll in your project.


## Examples

The following examples are orginized to cover scenarious in the order from simple scheduling requirements to comprehensive scheduling that showcase the depth of the feature-set this library offers.

---

### **Example 1 - Running a sample code once a day at a specific time.**

This simple example demonstrate how to use the library to build a console application that runs some logic once a day at a specific time.

The first thing we need to do before writing code is prepare a simple configuration file that tells the library of the rules that it's little engine needs to follow when scheduling services (note: `services` might also be referred to in this document as `tasks` or `processes`, all intended to mean the same thing, which is simply a block of code that needs to be executed). 

We'll discuss the configuration file format in details later-on, but for now, let's build a simple config file to serve as a sample of what the configuration file structure looks like. Later-on, we can reveal more scheduling control features available to setup in the config file as we go through more examples.

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- the first root tag is the container of tasks (aka services / aka prcocesses), and the root tag name itself can be anything we like -->
<!-- let's call it <tasks_list> in this example -->
<tasks_list>
<!-- inside <tasks_list> we have our individual tasks (could be one or many tasks)-->
<!-- let's simply call <task> the tag that identifies individual tasks within the <tasks_list> tag  -->
  <task>
	<!-- the scheduling engine within the library looks for the <sys> tag inside each <task> to get the rules it needs to follow when execute a task -->
    <sys>
	  <!-- this task example has only one rule, which is the <time> rule, this rule tells the engine to run this task only once a day at 11:00 AM -->
      <time>11:00</time>
    </sys>
	<!-- a custom tag that the we'd like the library to pass to our code when it gets triggers  -->
    <greeting_message>Good morning! it's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```

The configuration above tells the scheduler engine to run our desired code once every day at 11:00 AM and passes it the information we placed under the tag `<greeting_message>`.

What's left is writing our desired code that we want to get called by the engine at 11:00 AM.

> Program.cs
```c#
using System;
using System.IO;
using Com.H.Threading.Scheduler;

namespace scheduler_tester
{
    class Program
    {
        static void Main(string[] args)
        {
			// path to our config file
            var configPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "scheduler.xml";
			// throw error if config file not found in current folder
            if (File.Exists(configPath)==false) throw new FileNotFoundException(configPath);
			// instantiate the scheduler engine
            var scheduler = new ServiceScheduler(configPath);
			// listen for the event that the engine triggers based on our config <sys> tag conditions
            scheduler.ServiceIsDue += HandleTask;
			// start the scheduler engine
            scheduler.Start();
			// wait for <enter> input from screen to stop the scheduler engine
            System.Console.WriteLine("press <enter> to exit.");
            Console.ReadLine();
            scheduler.Stop();
            System.Console.WriteLine("done.");
        }
		
		// handles the event that is triggered by the engine
        static void HandleTask(object sender, ServiceSchedulerEventArgs e)
        {
			// print the content stored in <greeting_message> tag
            System.Console.WriteLine(e["greeting_message"]);
        }
    }
}
```
> output

```bash
Good morning! it's 11:00 AM!
press <enter> to exit.
```
Once the task runs and our message is printed, you'll notice a new file is generated by the name of `scheduler.xml.log` under the same folder where `scheduler.xml` resides. This is the log file the engine uses to keep track of which tasks ran successfully so it could mark them as completed in order to maintain persistency if we were to restart the application. This prevents tasks from running again on restarts if they were not meant to do so.

Aside from maintaining persistency during restarts, the engine also detects changes made to the config file during both runtime and even between restarts prompting it to re-evaluate the modified tasks `<sys>` tag run conditions.

The engine achieves this through generating and keeping both in memory and in the persistant log file a sha256 sum of each task defined in the config file so that any modification made to the config file can be followed by a sha256 integrity check to all tasks to determine which were the ones modified.

The modified tasks then gets treated as entirely new tasks having a clean run status history making them eligible for running in accordance with their `<sys>` tag run conditions.

---
### **Example 2 - Running a sample code on an interval (every n seconds) during the day.**

This example demonstrate running the same code we've written in **Example 1** every 3 seconds instead of once per day.

To achieve this, all we need to do is modify the config file and set `<interval>` run condition while removing `<time>` condition.

The new config file would look like the following:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- the first root tag is the container of tasks (aka services / aka prcocesses), and the root tag name itself can be anything we like -->
<!-- let's call it <tasks_list> in this example -->
<tasks_list>
<!-- inside <tasks_list> we have our individual tasks (could be one or many tasks)-->
<!-- let's simply call <task> the tag that identifies individual tasks within the <tasks_list> tag  -->
  <task>
	<!-- the scheduling engine within the library looks for the <sys> tag inside each <task> to get the rules it needs to follow when execute a task -->
    <sys>
	  <!-- this task example has only one rule, which is the <interval> rule, this rule tells the engine to run this task every 3,000 miliseconds (i.e 3 seconds) all throughout the day -->
      <interval>3000</interval>
    </sys>
	<!-- a custom tag that the we'd like the library to pass to our code when it gets triggers  -->
    <greeting_message>Good morning! it's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```
The `<interval>` tag expects miliseconds and instruct the engine to trigger the task every so often on a fixed interval based on the defined miliseconds.

---
### **Example 3 - Running a sample code on an interval (every n seconds) during specific time of the day.**

This example demonstrate running the same code in **Example 1 & 2** but on every 3 seconds between 9:00 AM and 2:00 PM every day.

This can be achieved by combining `<interval>` with `<time>` and a new tag called `<until_time>`

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- the first root tag is the container of tasks (aka services / aka prcocesses), and the root tag name itself can be anything we like -->
<!-- let's call it <tasks_list> in this example -->
<tasks_list>
<!-- inside <tasks_list> we have our individual tasks (could be one or many tasks)-->
<!-- let's simply call <task> the tag that identifies individual tasks within the <tasks_list> tag  -->
  <task>
	<!-- the scheduling engine within the library looks for the <sys> tag inside each <task> to get the rules it needs to follow when execute a task -->
    <sys>
	  <!-- this task example has 3 rules, <interval> rule to determine how often it runs, the <time> to determine from what time it start running, and <time_end> to determine until what time it runs -->
	  <time>09:00</time>
	  <until_time>14:00</until_time>
      <interval>3000</interval>
    </sys>
	<!-- a custom tag that the we'd like the library to pass to our code when it gets triggers  -->
    <greeting_message>Good morning! it's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```
The `<interval>` tag expects miliseconds and instruct the engine to trigger the task every so often on a fixed interval based on the defined miliseconds.

The `<time>` tag expects a time format of HH:mm instructing the engine when to start running the task.

The `<until_time>` tag expects a time format of HH:mm instructing the engine until what time the task should continue to run.

> Note: if no `<time>` tag is defined, the engine would start running the task at 00:00 hour until `<until_time>`. Similarily, if no `<until_time>` tag is defined, the engine would start running the task from `<time>` until end of the day.

---
### **Example 4 - Running a sample code on an interval (every n seconds) during specific time and on specific days of the week.**

This example demonstrate running the same code in **Example 1 & 2 & 3** every 3 seconds between 9:00 AM and 2:00 PM only on Monday and Thursday.

This is achieved by adding **days of the week** rule `<dow>`

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- the first root tag is the container of tasks (aka services / aka prcocesses), and the root tag name itself can be anything we like -->
<!-- let's call it <tasks_list> in this example -->
<tasks_list>
<!-- inside <tasks_list> we have our individual tasks (could be one or many tasks)-->
<!-- let's simply call <task> the tag that identifies individual tasks within the <tasks_list> tag  -->
  <task>
	<!-- the scheduling engine within the library looks for the <sys> tag inside each <task> to get the rules it needs to follow when execute a task -->
    <sys>
	  <!-- this task example has 4 rules:
	  <interval> rule to determine how often it runs 
	  <time> rule to determine from what time to start running 
	  <time_end> to determine until what time to run 
	  <dow> to determine on what days of the week it's allowed to run.
	  -->
	  <time>09:00</time>
	  <until_time>14:00</until_time>
      <interval>3000</interval>
	  <dow>Monday,Thursday</dow>
    </sys>
	<!-- a custom tag that the we'd like the library to pass to our code when it gets triggers  -->
    <greeting_message>Good morning! it's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```
<br>
---
| Tag        | Function                                  | Format                        | Example         |
|------------|-------------------------------------------|-------------------------------|-----------------|
| interval   | How often it runs in miliseconds          | miliseconds                   | 3000            |
| time       | From what time to start running           | HH:mm                         | 14:32           |
| until_time | Until what time to run                    | HH:mm                         | 23:15           |
| dow        | What days of the week allowed to run      | Comma separated weekday names | Monday,Thursday |
| dom        | What days of the month allowed to run     | Days of the month             | 1,5,23          |
| eom        | End of month (i.e. last day of the month) | true or false                 | true            |
| doy        | What days of the year allowed to run      | days of the year              | 53,250,300      |
| date       | On specific date                          | yyyy-MM-dd                    | 2077-01-23      |
| enabled    | enables or disables the task              | true or false                 | true            |

<br>

---
## Special conditional tags
Documentation in progress. Stay tuned.

---
### Retry on error
Documentation in progress. Stay tuned.

#### Retry attempts
Documentation in progress. Stay tuned.

#### Retry interval
Documentation in progress. Stay tuned.

---
## Variables
Documentation in progress. Stay tuned.

---
## External settings
Documentation in progress. Stay tuned.

### External settings cache
Documentation in progress. Stay tuned.

---
## Repeat
Documentation in progress. Stay tuned.

### Repeat variables
Documentation in progress. Stay tuned.

---
## Miscellaneous
Documentation in progress. Stay tuned.

---
## Putting it all together
Documentation in progress. Stay tuned.

---
## Future roadmap
Documentation in progress. Stay tuned.
