namespace Evix.Events {

  /// <summary>
  /// Helper interface for records from IRecorded
  /// </summary>
  public static class RecordedInterfaceHelper {

    /// <summary>
    /// Get the given records formatted in markdown
    /// </summary>
    /// <param name="records"></param>
    /// <returns></returns>
    public static string FormatRecordsMarkdown((string, string)[] records) {
      string fullMessage = "";
      foreach ((string timestamp, string eventMessage) in records) {
        fullMessage += $"{FormatMarkdownTimestamp(timestamp)} {eventMessage}\n";
      }

      return fullMessage;
    }

    /// <summary>
    /// Format a timestamp in markdown
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    static string FormatMarkdownTimestamp(string timestamp) {
      return $"<color=green>[{timestamp}]::</color>";
    }
  }
}
