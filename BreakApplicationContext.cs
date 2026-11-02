using Microsoft.Win32;

namespace BreakReminder;

public class BreakApplicationContext : ApplicationContext
{
    private readonly BreakScheduler breakScheduler;
    private readonly KeyboardBlocker keyboardBlocker = new();
    private readonly NotifyIcon trayIcon;
    private readonly System.Windows.Forms.Timer schedulerTickTimer;
    private readonly List<BreakOverlay> visibleBreakOverlays = new();

    public BreakApplicationContext(BreakDurations breakDurations)
    {
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Exit", null, (exitMenuSender, exitMenuEventArgs) => ExitThread());
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            ContextMenuStrip = trayMenu,
            Visible = true,
        };

        CallDetector callDetector = CallDetector.FromIgnoredAppsFile(Path.Combine(AppContext.BaseDirectory, "ignored-apps.txt"));
        breakScheduler = new BreakScheduler(breakDurations, callDetector.IsCallActive, TimeProvider.System);
        breakScheduler.BreakStarted += ShowBreakOverlays;
        breakScheduler.BreakEnded += CloseBreakOverlays;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        schedulerTickTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        schedulerTickTimer.Tick += (schedulerTimerSender, schedulerTimerEventArgs) => OnSchedulerTick();
        schedulerTickTimer.Start();
        UpdateTrayText();
    }

    protected override void ExitThreadCore()
    {
        schedulerTickTimer.Stop();
        schedulerTickTimer.Dispose();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        CloseBreakOverlays();
        keyboardBlocker.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.ExitThreadCore();
    }

    private void OnSchedulerTick()
    {
        breakScheduler.Tick();
        UpdateTrayText();
    }

    private void OnSessionSwitch(object? sessionSwitchSender, SessionSwitchEventArgs sessionSwitchEventArgs)
    {
        if (sessionSwitchEventArgs.Reason == SessionSwitchReason.SessionLock)
        {
            breakScheduler.MarkAway();
        }
        else if (sessionSwitchEventArgs.Reason == SessionSwitchReason.SessionUnlock)
        {
            breakScheduler.MarkBack();
        }
    }

    private void OnPowerModeChanged(object? powerModeSender, PowerModeChangedEventArgs powerModeChangedEventArgs)
    {
        if (powerModeChangedEventArgs.Mode == PowerModes.Suspend)
        {
            breakScheduler.MarkAway();
        }
        else if (powerModeChangedEventArgs.Mode == PowerModes.Resume)
        {
            breakScheduler.MarkBack();
        }
    }

    private void ShowBreakOverlays()
    {
        Screen[] overlayScreens = Screen.AllScreens;
        Screen cancelPanelScreen = Screen.PrimaryScreen ?? overlayScreens[0];
        foreach (Screen overlayScreen in overlayScreens.OrderBy(overlayScreen => overlayScreen.DeviceName == cancelPanelScreen.DeviceName))
        {
            var breakOverlay = new BreakOverlay(overlayScreen, overlayScreen.DeviceName == cancelPanelScreen.DeviceName, () => breakScheduler.BreakTimeRemaining);
            breakOverlay.CancelClicked += breakScheduler.Cancel;
            visibleBreakOverlays.Add(breakOverlay);
            breakOverlay.Show();
        }
        keyboardBlocker.Start();
    }

    private void CloseBreakOverlays()
    {
        keyboardBlocker.Stop();
        foreach (BreakOverlay breakOverlay in visibleBreakOverlays)
        {
            breakOverlay.CloseOverlay();
        }
        visibleBreakOverlays.Clear();
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        trayIcon.Text = breakScheduler.SchedulerPhase switch
        {
            BreakSchedulerPhase.Working => $"Next break at {breakScheduler.NextBreakAt.ToLocalTime():HH:mm}",
            BreakSchedulerPhase.WaitingForCallEnd => "Break waits for the call to end",
            _ => "Break in progress",
        };
    }
}
