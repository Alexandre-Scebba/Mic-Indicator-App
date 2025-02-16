using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Media;
using NAudio.CoreAudioApi;
using Microsoft.Win32; // For accessing the registry
using WinFormsTimer = System.Windows.Forms.Timer;

public class MuteIndicator : Form
{
    private WinFormsTimer mouseTracker;
    private WinFormsTimer muteChecker;
    private WinFormsTimer pulseTimer;
    private bool isMuted;
    private double pulsePhase = 0.0;

    // Tray icon and menu fields
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private bool soundEnabled = true;
    private bool startupEnabled = false; // Tracks if the app is set to launch on startup

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public MuteIndicator()
    {
        // Initialize the startup flag from the registry.
        startupEnabled = IsStartupEnabled();

        // Configure the main form (the floating indicator).
        // Form dimensions: 55x18 (designed to be unobtrusive)
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.Width = 55;
        this.Height = 18;
        this.BackColor = Color.Black;           // Background color (not painted over)
        this.TransparencyKey = Color.Magenta;     // Transparent color to hide unused areas
        this.ShowInTaskbar = false;
        this.Region = new Region(new Rectangle(0, 0, this.Width, this.Height));

        // Initialize the current mute state from the system.
        isMuted = GetMicMuteState();

        // Timer that updates the window's position relative to the mouse cursor.
        mouseTracker = new WinFormsTimer();
        mouseTracker.Interval = 10;
        mouseTracker.Tick += UpdatePosition;
        mouseTracker.Start();

        // Timer that polls the mic mute state every 500ms.
        muteChecker = new WinFormsTimer();
        muteChecker.Interval = 500;
        muteChecker.Tick += CheckMuteState;
        muteChecker.Start();

        // Timer that drives the pulsing effect for the indicator.
        pulseTimer = new WinFormsTimer();
        pulseTimer.Interval = 50;
        pulseTimer.Tick += PulseTimer_Tick;
        pulseTimer.Start();

        // Set up the system tray icon and context menu.
        trayMenu = new ContextMenuStrip();
        // First menu item: Toggle mute state. Text will be updated in UpdateTrayIcon().
        trayMenu.Items.Add("", null, ToggleMute_Click);
        // Second item: Sound toggle (checkable).
        var soundItem = new ToolStripMenuItem("Sound (On)") { CheckOnClick = true, Checked = soundEnabled };
        soundItem.Click += ToggleSound_Click;
        trayMenu.Items.Add(soundItem);
        // Third item: Launch on Startup toggle (checkable). Reads the current setting.
        var startupItem = new ToolStripMenuItem("Launch on Startup (Off)") { CheckOnClick = true, Checked = startupEnabled };
        startupItem.Click += ToggleStartup_Click;
        trayMenu.Items.Add(startupItem);
        // Fourth item: Toggle the mouse indicator visibility.
        var indicatorToggle = new ToolStripMenuItem("Hide Mouse Indicator");
        indicatorToggle.Click += (s, e) =>
        {
            this.Visible = !this.Visible;
            indicatorToggle.Text = this.Visible ? "Hide Mouse Indicator" : "Show Mouse Indicator";
        };
        trayMenu.Items.Add(indicatorToggle);
        // Final item: Exit the application.
        trayMenu.Items.Add("Exit", null, Exit_Click);

        trayIcon = new NotifyIcon();
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        // Enable left-click on the tray icon to toggle mute.
        trayIcon.MouseUp += TrayIcon_MouseUp;

        UpdateTrayIcon();
    }

    private void UpdatePosition(object? sender, EventArgs e)
    {
        // Update the position of the floating window to follow the cursor.
        if (GetCursorPos(out POINT cursor))
        {
            this.Left = cursor.X + 10;
            this.Top = cursor.Y + 10;
        }
    }

    private void CheckMuteState(object? sender, EventArgs e)
    {
        // Poll the system's mute state.
        bool newMuteState = GetMicMuteState();
        if (newMuteState != isMuted)
        {
            // Play a sound if enabled when the state changes.
            if (newMuteState)
            {
                if (soundEnabled)
                    SystemSounds.Hand.Play(); // Sound for muting
            }
            else
            {
                if (soundEnabled)
                    SystemSounds.Beep.Play(); // Sound for unmuting
            }
            isMuted = newMuteState;
            UpdateTrayIcon();
            this.Invalidate();
        }
    }

    private bool GetMicMuteState()
    {
        try
        {
            // Retrieve the default capture device and return its mute state.
            using (var enumerator = new MMDeviceEnumerator())
            {
                MMDevice device;
                try
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                }
                catch
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }
                return device.AudioEndpointVolume.Mute;
            }
        }
        catch
        {
            return false;
        }
    }

    private void PulseTimer_Tick(object? sender, EventArgs e)
    {
        // Update the pulse phase to create an animated effect.
        pulsePhase += 0.2;
        if (pulsePhase > Math.PI * 2)
            pulsePhase -= Math.PI * 2;
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Define a 16x16 box to serve as the indicator.
        int margin = 1;
        Rectangle boxRect = new Rectangle(margin, (this.Height - 37) / 2, 16, 16);

        if (!isMuted)
        {
            // When unmuted, fill the box with a pulsing red color.
            int redValue = (int)(80 + 175 * ((Math.Sin(pulsePhase) + 1) / 2));
            Color fillColor = Color.FromArgb(redValue, 0, 0);
            using (SolidBrush brush = new SolidBrush(fillColor))
            {
                g.FillRectangle(brush, boxRect);
            }
            // Draw the text "UNMUTED" next to the indicator.
            string text = "UNMUTED";
            using (Font font = new Font("Segoe UI", 7, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                SizeF textSize = g.MeasureString(text, font);
                float textX = boxRect.Right - 15;
                float textY = (this.Height - textSize.Height) / 2 - 10;
                g.DrawString(text, font, textBrush, textX, textY);
            }
        }
        else
        {
            // When muted, fill the box with a pulsing grey (silver) color.
            int redValue = (int)(80 + 175 * ((Math.Sin(pulsePhase) + 1) / 2));
            Color fillColor = Color.FromArgb(redValue, 0, 0); // We'll use redValue only for the pulsing effect calculation.
            // For muted, override with Silver.
            fillColor = Color.Silver;
            using (SolidBrush bgBrush = new SolidBrush(fillColor))
            {
                g.FillRectangle(bgBrush, boxRect);
            }
            // Draw the text "MUTED" next to the indicator.
            string text = "MUTED";
            using (Font font = new Font("Segoe UI", 7, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                SizeF textSize = g.MeasureString(text, font);
                float textX = boxRect.Right - 1;
                float textY = (this.Height - textSize.Height) / 2 - 10;
                g.DrawString(text, font, textBrush, textX, textY);
            }
        }

        // Draw a subtle white border around the entire form.
        using (Pen borderPen = new Pen(Color.White, 1))
        {
            g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
        }
    }

    // --- Tray Icon & Menu Handlers ---

    // When the user left-clicks the tray icon, toggle the mute state.
    private void TrayIcon_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleMute();
        }
    }

    private void ToggleMute_Click(object sender, EventArgs e)
    {
        ToggleMute();
    }

    private void ToggleMute()
    {
        try
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                MMDevice device;
                try
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                }
                catch
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }
                device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
            }
        }
        catch { }
        // The muteChecker timer will detect and update the new state.
    }

    private void ToggleSound_Click(object sender, EventArgs e)
    {
        soundEnabled = !soundEnabled;
        trayMenu.Items[1].Text = soundEnabled ? "Sound (On)" : "Sound (Off)";
    }

    private void ToggleStartup_Click(object? sender, EventArgs e)
    {
        startupEnabled = !startupEnabled;
        SetStartup(startupEnabled);
        trayMenu.Items[2].Text = startupEnabled ? "Launch on Startup (On)" : "Launch on Startup (Off)";
    }

    private void Exit_Click(object sender, EventArgs e)
    {
        trayIcon.Visible = false;
        Application.Exit();
    }

    // Update the tray icon and menu text to reflect the current state.
    private void UpdateTrayIcon()
    {
        trayMenu.Items[0].Text = isMuted ? "Unmute" : "Mute";
        trayIcon.Icon = GenerateTrayIcon(isMuted);
        trayIcon.Text = isMuted ? "Muted" : "Unmuted";
    }

    private Icon GenerateTrayIcon(bool muted)
    {
        // Generate a 16x16 tray icon using your specified dimensions.
        Bitmap bmp = new Bitmap(16, 16);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            // Use white for unmuted; use Silver for muted.
            Color micColor = muted ? Color.DimGray : Color.White;
            using (SolidBrush brush = new SolidBrush(micColor))
            {
                // Draw the mic head as a short rectangle.
                // (Coordinates chosen to center the mic design.)
                Rectangle headRect = new Rectangle(5, 2, 6, 4);
                g.FillRectangle(brush, headRect);

                // Draw the mic stand as a thin vertical line.
                Rectangle bodyRect = new Rectangle(8, 6, 1, 4);
                g.FillRectangle(brush, bodyRect);

                // Draw the base as a horizontal ellipse.
                g.FillEllipse(brush, new Rectangle(4, 10, 8, 3));
            }
            // If muted, add a red diagonal slash.
            if (muted)
            {
                using (Pen redPen = new Pen(Color.Red, 2))
                {
                    g.DrawLine(redPen, 2, 2, 14, 14);
                }
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    // --- Startup Registry Helpers ---

    private bool IsStartupEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
        {
            return key?.GetValue("MuteIndicator") != null;
        }
    }

    private void SetStartup(bool enable)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
        {
            if (enable)
                key.SetValue("MuteIndicator", Application.ExecutablePath);
            else
                key.DeleteValue("MuteIndicator", false);
        }
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MuteIndicator());
    }
}
