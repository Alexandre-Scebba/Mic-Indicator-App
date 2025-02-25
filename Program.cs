using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Media;
using NAudio.CoreAudioApi;
using Microsoft.Win32; // For registry access
using WinFormsTimer = System.Windows.Forms.Timer;

public class MuteIndicator : Form
{
    private WinFormsTimer mouseTracker;
    private WinFormsTimer muteChecker;
    private WinFormsTimer pulseTimer;
    private bool isMuted;
    private double pulsePhase = 0.0;

    // Tray icon fields
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private bool soundEnabled = true;
    private bool startupEnabled = false;

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
        // Enabled double buffering to reduce flicker
        this.DoubleBuffered = true;  

        // Read the registry to set the startupEnabled flag
        startupEnabled = IsStartupEnabled();

        // Smaller overall form size: 60×18
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.Width = 55;
        this.Height = 18;
        // We'll paint everything in OnPaint.
        this.BackColor = Color.Black;
        this.TransparencyKey = Color.Magenta;
        this.ShowInTaskbar = false;

        // Round out the region to the form size
        this.Region = new Region(new Rectangle(0, 0, this.Width, this.Height));

        // Initialize mute state
        isMuted = GetMicMuteState();

        // Timer to update the position of the indicator
        mouseTracker = new WinFormsTimer();
        mouseTracker.Interval = 10;
        mouseTracker.Tick += UpdatePosition;
        mouseTracker.Start();

        // Timer to check the mic mute state
        muteChecker = new WinFormsTimer();
        muteChecker.Interval = 500;
        muteChecker.Tick += CheckMuteState;
        muteChecker.Start();

        // Timer for pulse effect when unmuted
        pulseTimer = new WinFormsTimer();
        pulseTimer.Interval = 50;
        pulseTimer.Tick += PulseTimer_Tick;
        pulseTimer.Start();

        // Set up tray icon and context menu
        trayMenu = new ContextMenuStrip();
        // First menu item: Mute/Unmute toggle (we'll update text in UpdateTrayIcon)
        trayMenu.Items.Add("", null, ToggleMute_Click);
        // Sound toggle
        var soundItem = new ToolStripMenuItem("Sound (On)") { CheckOnClick = true, Checked = soundEnabled };
        soundItem.Click += ToggleSound_Click;
        trayMenu.Items.Add(soundItem);
        //  startup toggle
        var startupItem = new ToolStripMenuItem("Launch on Startup (Off)") { CheckOnClick = true, Checked = startupEnabled };
        startupItem.Click += ToggleStartup_Click;
        trayMenu.Items.Add(startupItem);
        //Transparancy submenu item
        var transparencyItem = new ToolStripMenuItem("Transparency");
        transparencyItem.DropDownItems.Add("100%", null, (s, e) => { this.Opacity = 1.0; });
        transparencyItem.DropDownItems.Add("75%", null, (s, e) => { this.Opacity = 0.75; });
        transparencyItem.DropDownItems.Add("50%", null, (s, e) => { this.Opacity = 0.5; });
        transparencyItem.DropDownItems.Add("25%", null, (s, e) => { this.Opacity = 0.35; });

        trayMenu.Items.Add(transparencyItem);
        // mouse indicator toggle:
        var indicatorToggle = new ToolStripMenuItem("Hide Mouse Indicator");
        indicatorToggle.Click += (s, e) =>
        {
            this.Visible = !this.Visible;
            indicatorToggle.Text = this.Visible ? "Hide Mouse Indicator" : "Show Mouse Indicator";
        };
        trayMenu.Items.Add(indicatorToggle);
        // Exit
        trayMenu.Items.Add("Exit", null, Exit_Click);

        trayIcon = new NotifyIcon();
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        // Left-click tray icon => toggle mute
        trayIcon.MouseUp += TrayIcon_MouseUp;

        UpdateTrayIcon();
    }

    private void UpdatePosition(object? sender, EventArgs e)
    {
        if (GetCursorPos(out POINT cursor))
        {
            this.Left = cursor.X + 10;
            this.Top = cursor.Y + 10;
        }
    }

    private void CheckMuteState(object? sender, EventArgs e)
    {
        bool newMuteState = GetMicMuteState();
        if (newMuteState != isMuted)
        {
            if (newMuteState)
            {
                if (soundEnabled)
                    SystemSounds.Hand.Play(); // click for muting
            }
            else
            {
                if (soundEnabled)
                    SystemSounds.Beep.Play(); // beep for unmuting
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
        // Pulse in both states
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

        // Define a 16×16 indicator box at left with small margin
        int margin = 1;
        // Using your original rectangle: (this.Height - 37)/2, 16,16
        Rectangle boxRect = new Rectangle(margin, (this.Height - 37) / 2, 16, 16);

        if (!isMuted)
        {
            // Unmuted: fill the box with a pulsing red color.
            int greenValue = (int)(80 + 175 * ((Math.Sin(pulsePhase) + 1) / 2));
            Color fillColor = Color.FromArgb(0, greenValue, 0);
            using (SolidBrush brush = new SolidBrush(fillColor))
            {
                g.FillRectangle(brush, boxRect);
            }

            // Draw small text "UNMUTED" flush with the box
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
            // Muted: fill the box with a pulsing green color.
            int redValue = (int)(80 + 175 * ((Math.Sin(pulsePhase) + 1) / 2));
            Color fillColor = Color.FromArgb(redValue, 0, 0);
            using (SolidBrush bgBrush = new SolidBrush(fillColor))
            {
                g.FillRectangle(bgBrush, boxRect);
            }
            // Draw small text "MUTED" flush with the box.
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

        // Optional: subtle white border around entire form
        using (Pen borderPen = new Pen(Color.White, 1))
        {
            g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
        }
    }

    // --- Tray Icon & Menu Handlers ---

    // Left-click on tray icon toggles mute
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
        // muteChecker will pick up the new state.
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

    private void UpdateTrayIcon()
    {
        trayMenu.Items[0].Text = isMuted ? "Muted (Toggle)" : "Unmuted (Toggle)";
        trayIcon.Icon = GenerateTrayIcon(isMuted);
        trayIcon.Text = isMuted ? "Muted" : "Unmuted";
    }

    private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        // Top-left arc.
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 70);
        // Top edge.
        path.AddLine(rect.X + radius, rect.Y, rect.Right - radius, rect.Y);
        // Top-right arc.
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        // Right edge.
        path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom - radius);
        // Bottom-right arc.
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 20, 150);
        // Bottom edge.
        path.AddLine(rect.Right - radius, rect.Bottom, rect.X + radius, rect.Bottom);
        // Bottom-left arc.
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 40);
        // Left edge.
        path.AddLine(rect.X, rect.Bottom - radius, rect.X, rect.Y + radius);
        path.CloseFigure();
        return path;
    }


    private Icon GenerateTrayIcon(bool muted)
    {
        // 16x16 icon, with a short rectangle top + stand + base
        Bitmap bmp = new Bitmap(16, 16);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            // draws the mic color in black if muted, white if unmuted
            Color micColor = muted ? Color.Silver : Color.White;
            using (SolidBrush brush = new SolidBrush(micColor))
            {
                // Short rectangle top - a 6x5 rectangle for the mic head
                // at (5,2) so it's fairly centered horizontally
                Rectangle headRect = new Rectangle(4, 0, 8, 12);
                using (GraphicsPath roundedHead = CreateRoundedRectanglePath(headRect, 1))
                    g.FillPath(brush, roundedHead);
                // some lines to make it look like a mic stand
                // thin rectangle from y=7 down to y=10
                // (, , , , +V-length )
                Rectangle standRect = new Rectangle(7, 7, 1, 9);
                g.FillRectangle(brush, standRect);

                // a base - a small horizontal ellipse at y=12
                // e.g. (3,12,10,3)
                //(-left, -height , +length, +thick)
                using (GraphicsPath basePath = new GraphicsPath())
                {
                    basePath.AddEllipse(new Rectangle(5, 14, 6, 2));
                    g.FillPath(brush, basePath);
                }

                using (GraphicsPath basetopPath = new GraphicsPath())
                {
                    basetopPath.AddEllipse(new Rectangle(2, 12, 12, 2));
                    g.FillPath(brush, basetopPath);
                }

                //(-left, -height , +length, +thick)
                using (GraphicsPath vleftPath = new GraphicsPath())
                {
                    vleftPath.AddEllipse(new Rectangle(1, 8, 2, 5));
                    g.FillPath(brush, vleftPath);
                }

                using (GraphicsPath vrightPath = new GraphicsPath())
                {
                    vrightPath.AddEllipse(new Rectangle(12, 8, 2, 5));
                    g.FillPath(brush, vrightPath);
                }
            }
            // If muted, adds a red slash
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

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MuteIndicator());
    }
}
