using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Evix.Testing {
  public class Timer {

    /// <summary>
    /// The timers and each recorded value stored by name
    /// </summary>
    readonly ConcurrentDictionary<string, (long startTimeStamp, bool recordMin, bool recordMax)> timers
      = new ConcurrentDictionary<string, (long startTimeStamp, bool recordMin, bool recordMax)>();

    /// <summary>
    /// The results of each timer
    /// </summary>
    readonly ConcurrentDictionary<string, List<Result>> timerResults
      = new ConcurrentDictionary<string, List<Result>>();

    /// <summary>
    /// The max result times for each timer
    /// </summary>
    readonly ConcurrentDictionary<string, Result> timerMaximums
      = new ConcurrentDictionary<string, Result>();

    /// <summary>
    /// The min result times for each timer
    /// </summary>
    readonly ConcurrentDictionary<string, Result> timerMinimums
      = new ConcurrentDictionary<string, Result>();

    /// <summary>
    /// Start a timer with the given name
    /// </summary>
    /// <param name="timerName"></param>
    /// <param name="recordMin">If we should record which is the min result for this timer</param>
    /// <param name="recordMax">If we should record which is the max result for this timer</param>
    public void start(string timerName, bool recordMin = true, bool recordMax = true) {
      long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
      if (timers.TryGetValue(timerName, out var oldTimerSettings)) {
        /// if the timer exists, update the start time
        timers.TryUpdate(
          timerName, 
          (startTime, recordMin, recordMax), 
          oldTimerSettings
        );

        return;
      }

      /// if this a new timer we need to set it up
      timers.TryAdd(timerName, (startTime, recordMin, recordMax));
      timerResults.TryAdd(timerName, new List<Result>());
      if (recordMin) {
        timerMinimums.TryAdd(timerName, new Result() { comment = "unset", elapsedMilliseconds = long.MinValue });
      }
      if (recordMax) {
        timerMaximums.TryAdd(timerName, new Result() { comment = "unset", elapsedMilliseconds = long.MaxValue });
      }
    }

    /// <summary>
    /// Record a delta timestamp and optional comment for a started timer.
    /// </summary>
    public void record(string timerName, string comment = "") {
      long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

      /// if the timer exists, record the result from when it was started
      if (timers.TryGetValue(timerName, out var timerSettings)) {
        long elapsedTime = currentTime - timerSettings.startTimeStamp;
        if (timerResults.TryGetValue(timerName, out List<Result> results)) {
          Result result = new Result() { elapsedMilliseconds = elapsedTime, comment = comment };
          results.Add(result);

          /// if we're storing the max, check it
          if (timerSettings.recordMax) {
            if (timerMaximums.TryGetValue(timerName, out Result currentMaximum)) {
              if (elapsedTime > currentMaximum.elapsedMilliseconds) {
                timerMaximums.TryUpdate(timerName, result, currentMaximum);
              } 
            }
          }
          
          /// if we're storing the min, check it
          if (timerSettings.recordMin) {
            if (timerMinimums.TryGetValue(timerName, out Result currentMinimum)) {
              if (elapsedTime < currentMinimum.elapsedMilliseconds) {
                timerMinimums.TryUpdate(timerName, result, currentMinimum);
              } 
            }
          }
        }
      } else {
        World.Debug.logError($"Trying to record for timer that was never started: {timerName}");
      }
    }

    /// <summary>
    /// Get the results as a log
    /// </summary>
    /// <param name="timerName"></param>
    /// <returns></returns>
    public string getResultsLog(string timerName) {
      /// starter text
      string timerResultText = $"Results for Timer: {timerName}:\n ===== \n";
      if (timers.TryGetValue(timerName, out var timerSettings)) {
        if (timerResults.TryGetValue(timerName, out List<Result> results)) {
          bool addedMaxOrMin = false;
          /// add min info
          if (timerSettings.recordMin) {
            if (timerMinimums.TryGetValue(timerName, out Result minimumResult)) {
              timerResultText += $"Min Result: {minimumResult.elapsedMilliseconds}{(minimumResult.comment != "" ? $" * {minimumResult.comment}" : "")}\n";
              addedMaxOrMin = true;
            }
          }

          /// add max info
          if (timerSettings.recordMax) {
            if (timerMaximums.TryGetValue(timerName, out Result maximumResult)) {
              timerResultText += $"Min Result: {maximumResult.elapsedMilliseconds}{(maximumResult.comment != "" ? $" * {maximumResult.comment}" : "")}\n";
              addedMaxOrMin = true;
            }
          }
          if (addedMaxOrMin) {
            timerResultText += " ===== \n";
          }

          /// List the results out
          for (int index = 0; index < results.Count; index++) {
            Result result = results[index];
            timerResultText += $"\t+{result.elapsedMilliseconds}{(result.comment != "" ? $" * {result.comment}" : "")}\n";
          }
        }
      } else {
        timerResultText += "No Recorded Results";
      }

      return timerResultText;
    }

    /// <summary>
    /// A result from a timer
    /// </summary>
    struct Result {
      /// <summary>
      /// How many milliseconds have passed since start() was called for the timer
      /// </summary>
      public long elapsedMilliseconds;

      /// <summary>
      /// An optional comment on this recorded result
      /// </summary>
      public string comment;
    }
  }
}
