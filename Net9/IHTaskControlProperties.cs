using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public interface IHTaskControlProperties
    {
        /// <summary>
        /// Whether or not this task is permitted to run.
        /// The first value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        bool Enabled { get; }
        /// <summary>
        /// Don't run before this date / time
        /// The second value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        DateTime? NotBefore { get; }
        /// <summary>
        /// Don't run after this date / time
        /// 3rd value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        DateTime? NotAfter { get; }
        /// <summary>
        /// 4th value to be checked by the engine to determine whether or not to run the task.
        /// Permitted to run only at these exact dates pipe | delimited.
        /// It can be exact dates yyyy-MM-dd HH:mm:ss, dates without time yyyy-MM-dd or alternatively dd MMM, yyyy,
        /// or dates without year MM-dd or alternatively dd MMM.
        /// If the year is omitted the scheduler uses the current year.
        /// </summary>
        IEnumerable<DateTime>? Dates { get; }
        /// <summary>
        /// The task is permitted to run only within specific days of the year
        /// e.g. 56,75..100 <= permit the task to run on day 56,75, and between 75 and 200.
        /// 5th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        IEnumerable<int>? DaysOfYear { get; }
        /// <summary>
        /// Permitted to run only on last day of the month.
        /// 6th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        bool? LastDayOfMonth { get; }

        /// <summary>
        /// The task is permitted to run only within specific days of the month
        /// e.g. 10,15,25..28 <= permit the task to run on the 10th,15th, and between 25th and 28th of the month.
        /// 6th value to be checked by the engine to determine whether or to not run the task.
        /// </summary>

        IEnumerable<int>? DaysOfMonth { get; }
        /// <summary>
        /// The task is permitted to run only within specific days of the week
        /// e.g. monday,thursday <= permit the task to run only on Monday and Thursday.
        /// 7th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        IEnumerable<string>? DaysOfWeek { get; }
        /// <summary>
        /// Time during the day from when the task is permitted run.
        /// 8th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        TimeSpan? Time { get; }
        /// <summary>
        /// Time during the day untill when the task is permitted run.
        /// 9th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        TimeSpan? UntilTime { get; }
        /// <summary>
        /// Defines the time interval in miliseconds the task will repeat running during the day.
        /// 10th value to be checked by the engine to determine whether or not to run the task.
        /// </summary>
        int? Interval { get; }
        /// <summary>
        /// If set to true, on task startup after a shutdown, the task schedular would ignore the log it keeps
        /// that tells it whether or not this particular task already ran during the day.
        /// So tasks that already ran during the day that have restrictive conditions such as run once a day at a specific time, would run again when starting up from a shutdown.
        /// </summary>
        bool IgnoreLogOnRestart { get; }
        /// <summary>
        /// If a task throws an exception, and it has its RetryInMilisecAfterError set, 
        /// the task schedular will attempt to re-run the task again after the n miliseconds
        /// defined in RetryInMilisecAfterError
        /// Since the task exception would be suppressed (prevented from bubbling up and crashing the runtime), if this option is set, 
        /// the thrown exception would be captured and passed to the schedular OnError event so it can be handled 
        /// by the application if needed.
        /// </summary>
        int? RetryInMilisecAfterError { get; }
        /// <summary>
        /// If a task throws an exception, and it has its RetryAttemptsAfterError set, 
        /// the task schedular will attempt to re-run the task again either on every time interval defined in RetryInMilisecAfterError
        /// or on task schedular check timer (default 1000 miliseconds) if RetryInMilisecAfterError is not defined.
        /// Since the task exception would be suppressed (prevented from bubbling up and crashing the runtime), if this option is set, 
        /// the thrown exception would be captured and passed to the schedular OnError event so it can be handled 
        /// by the application if needed.
        /// </summary>
        int? RetryAttemptsAfterError { get; }
        ///// <summary>
        ///// Retry attempts count after an exception.
        ///// </summary>
        //int? CurrentRetryAttemptsAfterError { get; }

        /// <summary>
        /// A custom DateTime to override DateTime.Now when filling custom placeholders for 
        /// {now{dd MMM, yyyy}} 
        /// </summary>
        DateTime Now { get; }

        DateTime Today { get; }
        DateTime Tomorrow { get; }

        /// <summary>
        /// Loops execution of the task by the number of items in the IEnumerable.
        /// Properties within the dynamic object are replaced in all tags using
        /// the following var format: {var{property_name}}
        /// </summary>
        public object? Repeat { get; }
        /// <summary>
        /// Used in conjunction with Repeat. Introduces a delay interval among iterative repeats.
        /// </summary>
        public int? RepeatDelayInterval { get; }

    }
}
