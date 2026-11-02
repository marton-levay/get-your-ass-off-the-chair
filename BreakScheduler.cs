using System.Globalization;

namespace BreakReminder;

public enum BreakSchedulerPhase
{
    Working,
    WaitingForCallEnd,
    BreakShowing,
}

public record BreakDurations(TimeSpan WorkDuration, TimeSpan BreakDuration, TimeSpan SnoozeDuration, TimeSpan CallEndGraceDuration)
{
    public static BreakDurations FromCommandLine(string[] commandLineArguments)
    {
        return new BreakDurations(
            TimeSpan.FromMinutes(PositiveNumberArgument(commandLineArguments, 0, 60)),
            TimeSpan.FromMinutes(PositiveNumberArgument(commandLineArguments, 1, 10)),
            TimeSpan.FromMinutes(PositiveNumberArgument(commandLineArguments, 2, 5)),
            TimeSpan.FromSeconds(PositiveNumberArgument(commandLineArguments, 3, 30)));
    }

    private static double PositiveNumberArgument(string[] commandLineArguments, int argumentPosition, double defaultNumber)
    {
        if (commandLineArguments.Length <= argumentPosition)
        {
            return defaultNumber;
        }

        bool isNumber = double.TryParse(commandLineArguments[argumentPosition], NumberStyles.Float, CultureInfo.InvariantCulture, out double argumentNumber);
        return isNumber && argumentNumber > 0 ? argumentNumber : defaultNumber;
    }
}

public class BreakScheduler
{
    private readonly BreakDurations breakDurations;
    private readonly Func<bool> isCallActive;
    private readonly TimeProvider timeProvider;
    private BreakSchedulerPhase schedulerPhase = BreakSchedulerPhase.Working;
    private DateTimeOffset nextBreakAt;
    private DateTimeOffset breakStartedAt;
    private DateTimeOffset callLastSeenAt;
    private DateTimeOffset? awayStartedAt;

    public event Action? BreakStarted;
    public event Action? BreakEnded;

    public BreakScheduler(BreakDurations breakDurations, Func<bool> isCallActive, TimeProvider timeProvider)
    {
        this.breakDurations = breakDurations;
        this.isCallActive = isCallActive;
        this.timeProvider = timeProvider;
        nextBreakAt = timeProvider.GetUtcNow() + breakDurations.WorkDuration;
    }

    public BreakSchedulerPhase SchedulerPhase => schedulerPhase;

    public DateTimeOffset NextBreakAt => nextBreakAt;

    public TimeSpan BreakTimeRemaining
    {
        get
        {
            TimeSpan breakTimeRemaining = breakStartedAt + breakDurations.BreakDuration - timeProvider.GetUtcNow();
            return breakTimeRemaining > TimeSpan.Zero ? breakTimeRemaining : TimeSpan.Zero;
        }
    }

    public void Tick()
    {
        if (awayStartedAt != null)
        {
            return;
        }

        DateTimeOffset currentUtcTime = timeProvider.GetUtcNow();
        switch (schedulerPhase)
        {
            case BreakSchedulerPhase.Working:
                if (currentUtcTime < nextBreakAt)
                {
                    return;
                }
                if (isCallActive())
                {
                    callLastSeenAt = currentUtcTime;
                    schedulerPhase = BreakSchedulerPhase.WaitingForCallEnd;
                    return;
                }
                StartBreak(currentUtcTime);
                return;

            case BreakSchedulerPhase.WaitingForCallEnd:
                if (isCallActive())
                {
                    callLastSeenAt = currentUtcTime;
                    return;
                }
                if (currentUtcTime - callLastSeenAt >= breakDurations.CallEndGraceDuration)
                {
                    StartBreak(currentUtcTime);
                }
                return;

            case BreakSchedulerPhase.BreakShowing:
                if (currentUtcTime - breakStartedAt >= breakDurations.BreakDuration)
                {
                    EndBreak(currentUtcTime + breakDurations.WorkDuration);
                }
                return;
        }
    }

    public void Cancel()
    {
        if (schedulerPhase != BreakSchedulerPhase.BreakShowing)
        {
            return;
        }

        EndBreak(timeProvider.GetUtcNow() + breakDurations.SnoozeDuration);
    }

    public void MarkAway()
    {
        awayStartedAt ??= timeProvider.GetUtcNow();
    }

    public void MarkBack()
    {
        if (awayStartedAt == null)
        {
            return;
        }

        DateTimeOffset currentUtcTime = timeProvider.GetUtcNow();
        TimeSpan awayDuration = currentUtcTime - awayStartedAt.Value;
        awayStartedAt = null;
        if (awayDuration < breakDurations.BreakDuration)
        {
            return;
        }

        if (schedulerPhase == BreakSchedulerPhase.BreakShowing)
        {
            EndBreak(currentUtcTime + breakDurations.WorkDuration);
            return;
        }

        schedulerPhase = BreakSchedulerPhase.Working;
        nextBreakAt = currentUtcTime + breakDurations.WorkDuration;
    }

    private void StartBreak(DateTimeOffset currentUtcTime)
    {
        breakStartedAt = currentUtcTime;
        schedulerPhase = BreakSchedulerPhase.BreakShowing;
        BreakStarted?.Invoke();
    }

    private void EndBreak(DateTimeOffset upcomingBreakAt)
    {
        nextBreakAt = upcomingBreakAt;
        schedulerPhase = BreakSchedulerPhase.Working;
        BreakEnded?.Invoke();
    }
}
