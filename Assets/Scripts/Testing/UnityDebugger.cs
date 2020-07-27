using Evix.Testing;
using UnityEngine;

namespace Evix {

  public class UnityDebugger {
    /// <summary>
    /// If the debugger is enabled.
    /// </summary>
#if DEBUG
    public bool debugModeIsEnabled {
      get;
      private set;
    } = true;
#else
    public bool isEnabled {
      get;
      private set;
    } = false;
#endif

    /// <summary>
    /// The Timer used for time tracking
    /// </summary>
    public readonly Timer Timer 
      = new Timer();

    /// <summary>
    /// Make a new unity debugger. Override debug mode if you want
    /// </summary>
    public UnityDebugger() {}
    public UnityDebugger(bool isEnabled) {
      debugModeIsEnabled = isEnabled;
    }

    /// <summary>
    /// Log a debug message
    /// </summary>
    public void log(string debugMessage) {
      if (debugModeIsEnabled) {
        Debug.Log(debugMessage);
      }
    }

    /// <summary>
    /// Log a debug warning
    /// </summary>
    public void logWarning(string debugMessage) {
      if (debugModeIsEnabled) {
        Debug.LogWarning(debugMessage);
      }
    }

    /// <summary>
    /// Log an error. These will log even outside of debug mode
    /// </summary>
    public void logError(string debugMessage) {
      Debug.LogError(debugMessage);
    }

    /// <summary>
    /// Log an error and throw and exception of the given type with the mesage of the error.
    /// These will work outside of debug mode
    /// </summary>
    /// <exception cref="ExceptionType">Throws an exception of the provided type</exception>
    public void logAndThrowError<ExceptionType>(string debugMessage) where ExceptionType : System.Exception {
      Debug.LogError(debugMessage);
      throw (ExceptionType)typeof(ExceptionType).GetConstructor(new[] {typeof(string)}).Invoke(new object[] { debugMessage});
    }
  }
}
