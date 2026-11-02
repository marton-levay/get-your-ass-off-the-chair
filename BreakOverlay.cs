using System.Runtime.InteropServices;

namespace BreakReminder;

public class BreakOverlay : Form
{
    private static readonly IntPtr TopmostWindowHandle = new(-1);
    private const uint NoSizeFlag = 0x0001;
    private const uint NoMoveFlag = 0x0002;
    private const uint NoActivateFlag = 0x0010;

    private readonly Screen overlayScreen;
    private readonly bool hasCancelPanel;
    private readonly Func<TimeSpan> breakTimeRemaining;
    private readonly Label? countdownLabel;
    private readonly System.Windows.Forms.Timer keepOnTopTimer;
    private bool isCloseAllowed;

    public event Action? CancelClicked;

    public BreakOverlay(Screen overlayScreen, bool hasCancelPanel, Func<TimeSpan> breakTimeRemaining)
    {
        this.overlayScreen = overlayScreen;
        this.hasCancelPanel = hasCancelPanel;
        this.breakTimeRemaining = breakTimeRemaining;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = overlayScreen.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.FromArgb(40, 40, 40);
        Opacity = 0.85;

        if (hasCancelPanel)
        {
            countdownLabel = CreateLabel(string.Empty, 48, FontStyle.Bold);
            Controls.Add(CreateCancelPanel(countdownLabel));
        }

        keepOnTopTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        keepOnTopTimer.Tick += (keepOnTopTimerSender, keepOnTopTimerEventArgs) => KeepOnTop();
    }

    protected override bool ShowWithoutActivation => !hasCancelPanel;

    public void CloseOverlay()
    {
        isCloseAllowed = true;
        keepOnTopTimer.Stop();
        Close();
    }

    protected override void OnShown(EventArgs shownEventArgs)
    {
        base.OnShown(shownEventArgs);
        ActiveControl = null;
        KeepOnTop();
        keepOnTopTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs formClosingEventArgs)
    {
        if (!isCloseAllowed && formClosingEventArgs.CloseReason == CloseReason.UserClosing)
        {
            formClosingEventArgs.Cancel = true;
        }
        base.OnFormClosing(formClosingEventArgs);
    }

    protected override bool ProcessCmdKey(ref Message keyMessage, Keys pressedKeys)
    {
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            keepOnTopTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private TableLayoutPanel CreateCancelPanel(Label breakCountdownLabel)
    {
        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            TabStop = false,
            Anchor = AnchorStyles.None,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 16, FontStyle.Regular),
            Padding = new Padding(24, 8, 24, 8),
            Margin = new Padding(0, 32, 0, 0),
        };
        cancelButton.Click += (cancelButtonSender, cancelButtonEventArgs) => CancelClicked?.Invoke();

        var cancelPanelLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        cancelPanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cancelPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cancelPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cancelPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cancelPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cancelPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cancelPanelLayout.Controls.Add(CreateLabel("Get your ass off the chair. Go walk.", 32, FontStyle.Bold), 0, 1);
        cancelPanelLayout.Controls.Add(breakCountdownLabel, 0, 2);
        cancelPanelLayout.Controls.Add(cancelButton, 0, 3);
        return cancelPanelLayout;
    }

    private static Label CreateLabel(string labelText, float fontSize, FontStyle fontStyle)
    {
        return new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", fontSize, fontStyle),
        };
    }

    private void KeepOnTop()
    {
        if (Bounds != overlayScreen.Bounds)
        {
            Bounds = overlayScreen.Bounds;
        }
        if (WindowState != FormWindowState.Normal)
        {
            WindowState = FormWindowState.Normal;
        }

        SetWindowPos(Handle, TopmostWindowHandle, 0, 0, 0, 0, NoSizeFlag | NoMoveFlag | NoActivateFlag);

        if (countdownLabel != null)
        {
            TimeSpan countdownTime = breakTimeRemaining();
            countdownLabel.Text = $"{(int)countdownTime.TotalMinutes:00}:{countdownTime.Seconds:00}";
        }
        if (hasCancelPanel)
        {
            BringToForeground();
        }
    }

    private void BringToForeground()
    {
        IntPtr foregroundWindowHandle = GetForegroundWindow();
        if (foregroundWindowHandle == Handle)
        {
            return;
        }

        uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindowHandle, out _);
        uint overlayThreadId = GetCurrentThreadId();
        bool isInputAttached = foregroundThreadId != 0
            && foregroundThreadId != overlayThreadId
            && AttachThreadInput(foregroundThreadId, overlayThreadId, true);
        SetForegroundWindow(Handle);
        Activate();
        if (isInputAttached)
        {
            AttachThreadInput(foregroundThreadId, overlayThreadId, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfterWindowHandle, int x, int y, int width, int height, uint positionFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachingThreadId, uint attachedToThreadId, bool isAttaching);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
