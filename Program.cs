namespace BreakReminder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] commandLineArguments)
    {
        using var singleInstanceMutex = new Mutex(true, "BreakReminder.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new BreakApplicationContext(BreakDurations.FromCommandLine(commandLineArguments)));
    }
}
