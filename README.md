# Com.H.Threading.Scheduler
An easy to use, simple, yet feature-rich open-source framework for writing middlewares in the form of low resource footnote Windows services and/or Linux Daemons.
 
## What's the purpose of this library?
This library helps build solutions that require some of their time sensitive logic to conditinally run as a background process without user input.

And although most solutions might not have the need for such time-sensitive background process logic, the ones that do would hopefully benefit from what this library offers.

One of the goals of this library is to help developers write background services while focusing only on what matters, which is the logic that needs to run in the background, and leaving the process scheduling details conveniently hidden behind an easy to use, feature-rich, efficient and hopefully reliable scheduling engine tucked away in this small library.

## How to install
Perhaps the easiest way is to install the package for this library is done via NuGet package manager [https://www.nuget.org/packages/Com.H.Threading.Scheduler/](https://www.nuget.org/packages/Com.H.Threading.Scheduler/)
 
Alternatively, you can clone / download the library from this github repo, compile, then reference the output dlls in your project.


## Examples

The following examples are orginized to cover scenarious in the order from simple scheduling requirements to comprehensive scheduling that showcase the depth of the feature-set this library offers.

---

### **Example 1 - Running a sample code once a day at a specific time.**

This simple example demonstrate how to use the library to build a console application that runs some logic once a day at a specific time.

The first thing we need to do before writing code is prepare a simple configuration file that tells the library of the rules it's scheduling engine needs to follow when scheduling tasks (note: `tasks` might also be referred to in this document as `services` or `processes`, all intended to mean the same thing, which is simply a block of code that needs to be executed). 

We'll discuss the configuration file format in details later-on, but for now, let's build a simple config file to serve as a sample of what the configuration file structure looks like. Later-on, we can reveal more scheduling control features available to setup in the config file as we go through more examples.

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
      <time>11:00</time>
    </sys>
    <greeting_message>Good morning! it's 11:00 AM!</greeting_message>
  </task>
</tasks_list>
```
---
> the same scheduler.xml file above but with comments describing the configuration tags
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


The configuration above tells the scheduler engine to run our desired code once every day at 11:00 AM and passes it the information we placed under the custom tag `<greeting_message>`.

> **Note 1:** You can add as many custom tags (including nested custom tags) to the configuration and the scheduler engine would pass them all to your code at runtime when it gets called for execution. More details on that later-on in **`custom tags`** section

> **Note 2:** You can add as many tasks as you want with different or same run conditions and the engine would run them all concurrently.


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
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "scheduler.xml");
			// throw error if config file not found in current folder
            if (File.Exists(configPath)==false) throw new FileNotFoundException(configPath);
			// instantiate the scheduler engine
            var scheduler = new HTaskScheduler(configPath);
			// listen for the event that the engine triggers based on our config <sys> tag conditions
            scheduler.TaskIsDue += HandleTask;
			// start the scheduler engine
            scheduler.Start();
			// wait for <enter> input from screen to stop the scheduler engine
            System.Console.WriteLine("press <enter> to exit.");
            Console.ReadLine();
            scheduler.Stop();
            System.Console.WriteLine("done.");
        }
		
		// the code we want the engine to call at runtime
        static void HandleTask(object sender, HTaskSchedulerEventArgs e)
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

The engine achieves this through generating and keeping both in memory and in the persistant log file a sha256 sum of each task defined in the config file so that any modification made to the config file can be followed by a sha256 integrity check on all tasks to determine which were the ones modified.

The modified tasks then gets treated as entirely new tasks having a clean run status history making them eligible for running in accordance with their `<sys>` tag run conditions.

---
### **Example 2 - Running a sample code on an interval (every n seconds) during the day.**

This example demonstrate running the same code we've written in **Example 1** every 3 seconds instead of once per day.

To achieve this, all we need to do is modify the config file and set `<interval>` run condition while removing `<time>` condition.

The new config file would look like the following:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <sys>
	  <!-- this task example has only one rule, which is the <interval> rule, 
	  this rule tells the engine to run this task every 3,000 miliseconds 
	  (i.e 3 seconds) all throughout the day -->
      <interval>3000</interval>
    </sys>
    <greeting_message>Hello there!</greeting_message>
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
<tasks_list>
  <task>
    <sys>
	  <!-- this task example has 3 rules: 
	  <interval> to determine how often it runs
	  <time> to determine from what time it start running
	  <time_end> to determine until what time it runs 
	  -->
		<time>09:00</time>
		<until_time>14:00</until_time>
		<interval>3000</interval>
    </sys>
    <greeting_message>Hello there!</greeting_message>
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
<tasks_list>
  <task>
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
    <greeting_message>Hello there!</greeting_message>
  </task>
</tasks_list>
```

---
## General conditional tags
Here is the list of all `general conditional tags` (i.e. run rules) available.

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
| not_before | Date & time not to run before             | yyyy-MM-dd HH:mm:ss           | 2077-01-23 14:23|
| not_after  | Date & time not to run after              | yyyy-MM-dd HH:mm:ss           | 2077-01-23 14:23|



---
## Custom tags
Custom tags are treated as configuration information that the scheduler engine just passes to your code when executing a task. You can put as much as you want information inside those tags, and have as many custom tags as you want for each task.

In previous examples, we've seen how we used the custom tag `<greeting_message>` to pass information to our task to print a message on screen.

We can come up with many different examples of information that we might want to pass to a task, as custom tags not only help us pass information about a specific task, they also help us identify which task is calling our code if we have defined multiple tasks in our config file.

In a large application that has different multiple types of tasks and different ways of handling each particular task, custom tags come handy in identifying which task called our code so we can route the handling of that task to the appropriate logic in our application.

For example, giving the task a `<name>` custom tag is a good practice that helps our code know which task issued the call.

The following example showcase how to make use of a custom tag to help identify what logic to run when we have multiple tasks.

### **Example 5 - Running two different tasks.**

This exapmle demonstrate running two different tasks, each requires us to handle it differently than the other. One task is to print a message on screen, the other is to calculate the sum of random numbers then prints the result on screen.

To build such logic, let's first write our configuration file:

> scheduler.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<tasks_list>
  <task>
    <name>print a message</name>
    <sys>
	    <!-- this task is set to run every 3 seconds -->
		  <interval>3000</interval>
    </sys>
    
    <greeting_message>Hello there!</greeting_message>
  </task>
  <task>
    <name>calculate some numbers</name>
    <sys>
	    <!-- this task is set to run every 2 seconds -->
		  <interval>2000</interval>
    </sys>
    <some_numbers>32,56,4,67,1</some_numbers>
  </task>
</tasks_list>
```

Now let's build our solution that handles those two tasks.
> Program.cs
```c#
using System;
using System.IO;
using System.Linq;
using Com.H.Threading.Scheduler;

namespace scheduler_tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "scheduler.xml");
            if (File.Exists(configPath)==false) throw new FileNotFoundException(configPath);
            var scheduler = new HTaskScheduler(configPath);
            scheduler.TaskIsDue += HandleTask;
            scheduler.Start();
            System.Console.WriteLine("press <enter> to exit.");
            Console.ReadLine();
            scheduler.Stop();
            System.Console.WriteLine("done.");
        }
        static void HandleTask(object sender, HTaskSchedulerEventArgs e)
        {
            switch(e["name"] as string)
            {
                case "print a message": ProcessPrintMessageTask(e);
                break;
                case "calculate some numbers": ProcessCountNumbersTask(e);
                break;
                default:System.Console.WriteLine("unknown task");
                break;
            }
        }

        static void ProcessPrintMessageTask(HTaskSchedulerEventArgs e)
        {
            System.Console.WriteLine(e["greeting_message"]);
        }
        static void ProcessCountNumbersTask(HTaskSchedulerEventArgs e)
        {
            int sum = e["some_numbers"]
                .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(num=>int.Parse(num)).Sum();
            System.Console.WriteLine($"sum of {e["some_numbers"]} = {sum}");    
        }
    }
}
```

> **NOTE**: You could use `<id>` or any other custom tag beside `<name>` to identify tasks. It's entirely up to you and how you'd like to choose your best practices for your own workflow. This is just one example on how one could come about to do so.

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
## Cancellation Tokens and exiting gracefully
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
