using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MainGameDesktopDisplay;

sealed class DesktopTestCard : Form
{
    readonly string output;
    string phase = "A";
    readonly Timer timer = new Timer { Interval = 100 };
    DesktopTestCard(string output)
    {
        this.output = output;
        var monitor = DesktopCapture.Enumerate()[0];
        Text = "Desktop capture smoke test — actual Windows window";
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(monitor.Rect.Left + 35, monitor.Rect.Top + 55, 760, 440);
        TopMost = true; DoubleBuffered = true; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        Shown += (s,e) =>
        {
            var point = PointToScreen(new Point(30, 300));
            File.WriteAllText(Path.Combine(output, "card.json"), "{\"device\":\"" + monitor.Device.Replace("\\", "\\\\") + "\",\"x\":" + point.X + ",\"y\":" + point.Y + "}");
        };
        timer.Tick += (s,e) =>
        {
            if (File.Exists(Path.Combine(output,"close-card"))) { Close(); return; }
            string next = File.Exists(Path.Combine(output,"phase-b")) ? "B" : "A";
            if (next != phase) { phase = next; Invalidate(); }
        };
        timer.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(phase == "A" ? Color.FromArgb(32,180,90) : Color.FromArgb(240,110,30));
        using(var title = new Font("Segoe UI", 32, FontStyle.Bold))
        using(var normal = new Font("Segoe UI", 17))
        using(var phaseFont = new Font("Segoe UI", 60, FontStyle.Bold))
        {
            e.Graphics.DrawString("LIVE DESKTOP", title, Brushes.Black, 28, 22);
            e.Graphics.DrawString("Windows capture -> Unity texture -> 3D screen", normal, Brushes.Black, 30, 85);
            e.Graphics.DrawString("PHASE " + phase, phaseFont, Brushes.White, 155, 160);
            e.Graphics.DrawString("This is a real window on your desktop.", normal, Brushes.Black, 30, 350);
        }
    }
    protected override void Dispose(bool disposing) { if(disposing) timer.Dispose(); base.Dispose(disposing); }
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args)
    {
        SetProcessDPIAware(); Application.EnableVisualStyles();
        Application.Run(new DesktopTestCard(args[0]));
    }
}
