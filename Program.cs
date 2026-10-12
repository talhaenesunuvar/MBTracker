using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace InternetSayaci
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool created;
            using (new Mutex(true, "InternetSayaci_tekil", out created))
            {
                if (!created) return;
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Widget());
            }
        }
    }

    static class Fmt
    {
        public static readonly CultureInfo Tr = new CultureInfo("tr-TR");
        const double MB = 1024.0 * 1024.0;
        public const double GB = MB * 1024.0;

        public static string Bytes(long b)
        {
            if (b < MB) return (b / 1024.0).ToString("0", Tr) + " KB";
            if (b < 100 * MB) return (b / MB).ToString("0.0", Tr) + " MB";
            if (b < GB) return (b / MB).ToString("0", Tr) + " MB";
            return (b / GB).ToString("0.00", Tr) + " GB";
        }

        public static string Speed(double bytesPerSec)
        {
            return Bytes((long)bytesPerSec) + "/sn";
        }
    }

    static class Paths
    {
        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InternetSayaci");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void WriteLines(string file, IEnumerable<string> lines)
        {
            string path = Path.Combine(Dir, file);
            string tmp = path + ".tmp";
            File.WriteAllLines(tmp, lines);
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
    }

    class Settings
    {
        public double QuotaGB = 0;      // 0 = kota takibi kapalı
        public int ResetDay = 1;        // kotanın yenilendiği gün
        public int BurstMB = 50;        // 1 dakikada bu kadar MB harcanırsa uyar
        public int X = int.MinValue, Y = int.MinValue;
        public string AlertPeriod = "";
        public int AlertLevel = 0;

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string path = Path.Combine(Paths.Dir, "ayarlar.txt");
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "QuotaGB") s.QuotaGB = double.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "ResetDay") s.ResetDay = int.Parse(v);
                    else if (k == "BurstMB") s.BurstMB = int.Parse(v);
                    else if (k == "X") s.X = int.Parse(v);
                    else if (k == "Y") s.Y = int.Parse(v);
                    else if (k == "AlertPeriod") s.AlertPeriod = v;
                    else if (k == "AlertLevel") s.AlertLevel = int.Parse(v);
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Paths.WriteLines("ayarlar.txt", new string[] {
                    "QuotaGB=" + QuotaGB.ToString(CultureInfo.InvariantCulture),
                    "ResetDay=" + ResetDay,
                    "BurstMB=" + BurstMB,
                    "X=" + X,
                    "Y=" + Y,
                    "AlertPeriod=" + AlertPeriod,
                    "AlertLevel=" + AlertLevel
                });
            }
            catch { }
        }

        public DateTime PeriodStart()
        {
            DateTime t = DateTime.Today;
            DateTime start = new DateTime(t.Year, t.Month, Math.Min(ResetDay, DateTime.DaysInMonth(t.Year, t.Month)));
            if (start > t)
            {
                DateTime p = t.AddMonths(-1);
                start = new DateTime(p.Year, p.Month, Math.Min(ResetDay, DateTime.DaysInMonth(p.Year, p.Month)));
            }
            return start;
        }
    }

    // Gün gün toplam (indirme + yükleme) kullanımı diskte tutar.
    class UsageStore
    {
        readonly Dictionary<string, long[]> days = new Dictionary<string, long[]>();

        static string Key(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        public UsageStore()
        {
            try
            {
                string path = Path.Combine(Paths.Dir, "kullanim.txt");
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] p = line.Split(';');
                    if (p.Length == 3) days[p[0]] = new long[] { long.Parse(p[1]), long.Parse(p[2]) };
                }
            }
            catch { }
        }

        public void Add(long rx, long tx)
        {
            string k = Key(DateTime.Today);
            long[] v;
            if (!days.TryGetValue(k, out v)) { v = new long[2]; days[k] = v; }
            v[0] += rx;
            v[1] += tx;
        }

        public long Today
        {
            get
            {
                long[] v;
                return days.TryGetValue(Key(DateTime.Today), out v) ? v[0] + v[1] : 0;
            }
        }

        public long Since(DateTime start)
        {
            string from = Key(start);
            long sum = 0;
            foreach (KeyValuePair<string, long[]> kv in days)
                if (string.CompareOrdinal(kv.Key, from) >= 0) sum += kv.Value[0] + kv.Value[1];
            return sum;
        }

        public void Save()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, long[]> kv in days)
                    lines.Add(kv.Key + ";" + kv.Value[0] + ";" + kv.Value[1]);
                lines.Sort(string.CompareOrdinal);
                Paths.WriteLines("kullanim.txt", lines);
            }
            catch { }
        }
    }

    // İnternete çıkan adaptörlerin bayt sayaçlarını okur, iki okuma arasındaki farkı verir.
    class NetMeter
    {
        readonly Dictionary<string, long[]> last = new Dictionary<string, long[]>();

        static bool HasGateway(NetworkInterface ni)
        {
            foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses)
                if (!g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any)) return true;
            return false;
        }

        public void Sample(out long rx, out long tx)
        {
            rx = 0; tx = 0;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (!HasGateway(ni)) continue;

                IPInterfaceStatistics st = ni.GetIPStatistics();
                long[] cur = new long[] { st.BytesReceived, st.BytesSent };
                long[] prev;
                if (last.TryGetValue(ni.Id, out prev))
                {
                    // Sayaç geri gittiyse adaptör yeniden bağlanmıştır; o tur sayılmaz.
                    if (cur[0] >= prev[0]) rx += cur[0] - prev[0];
                    if (cur[1] >= prev[1]) tx += cur[1] - prev[1];
                }
                last[ni.Id] = cur;
            }
        }
    }

    class AppRow
    {
        public string Name;
        public long LastMinute;
        public long Today;
    }

    // Windows'un "Veri kullanımı" kayıtlarından uygulama bazında kullanımı çeker.
    class AppTracker
    {
        readonly object gate = new object();
        readonly Queue<KeyValuePair<DateTime, Dictionary<string, long>>> history =
            new Queue<KeyValuePair<DateTime, Dictionary<string, long>>>();
        Dictionary<string, long> current = new Dictionary<string, long>();
        DateTime day;
        string profileName = "";

        public void Start()
        {
            Thread t = new Thread(Loop);
            t.IsBackground = true;
            t.Start();
        }

        void Loop()
        {
            while (true)
            {
                try { Poll(); } catch { }
                Thread.Sleep(5000);
            }
        }

        void Poll()
        {
            Windows.Networking.Connectivity.ConnectionProfile profile =
                Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            if (profile == null) return;

            DateTime now = DateTime.Now;
            var op = profile.GetAttributedNetworkUsageAsync(
                new DateTimeOffset(now.Date), new DateTimeOffset(now),
                new Windows.Networking.Connectivity.NetworkUsageStates());
            for (int i = 0; i < 300 && op.Status == Windows.Foundation.AsyncStatus.Started; i++) Thread.Sleep(50);
            if (op.Status != Windows.Foundation.AsyncStatus.Completed) return;

            Dictionary<string, long> snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (Windows.Networking.Connectivity.AttributedNetworkUsage u in op.GetResults())
            {
                string id = u.AttributionId ?? "";
                long v;
                snap.TryGetValue(id, out v);
                snap[id] = v + (long)u.BytesReceived + (long)u.BytesSent;
            }

            string name = profile.ProfileName ?? "";
            lock (gate)
            {
                if (now.Date != day || name != profileName)
                {
                    history.Clear();
                    day = now.Date;
                    profileName = name;
                }
                current = snap;
                history.Enqueue(new KeyValuePair<DateTime, Dictionary<string, long>>(now, snap));
                while (history.Count > 1 && (now - history.Peek().Key).TotalSeconds > 65) history.Dequeue();
            }
        }

        static string NiceName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Sistem / Windows";
            int slash = id.LastIndexOf('\\');
            if (slash >= 0) return id.Substring(slash + 1);
            int us = id.IndexOf('_');
            return us > 0 ? id.Substring(0, us) : id;
        }

        public List<AppRow> Rows()
        {
            List<AppRow> rows = new List<AppRow>();
            lock (gate)
            {
                Dictionary<string, long> old = history.Count > 0 ? history.Peek().Value : null;
                foreach (KeyValuePair<string, long> kv in current)
                {
                    long before = 0;
                    if (old != null) old.TryGetValue(kv.Key, out before);
                    AppRow r = new AppRow();
                    r.Name = NiceName(kv.Key);
                    r.Today = kv.Value;
                    r.LastMinute = Math.Max(0, kv.Value - before);
                    rows.Add(r);
                }
            }
            rows.Sort(delegate(AppRow a, AppRow b)
            {
                int c = b.LastMinute.CompareTo(a.LastMinute);
                return c != 0 ? c : b.Today.CompareTo(a.Today);
            });
            return rows;
        }
    }

    class Widget : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "InternetSayaci";

        readonly Settings settings = Settings.Load();
        readonly UsageStore store = new UsageStore();
        readonly NetMeter meter = new NetMeter();
        readonly AppTracker tracker = new AppTracker();
        readonly Queue<long> lastMinute = new Queue<long>();
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Font bigFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        readonly Font smallFont = new Font("Segoe UI", 8f);
        readonly float scale;

        DetailsForm details;
        ToolStripMenuItem showItem, startupItem;
        long minuteSum;
        double downSpeed, upSpeed;
        DateTime lastBurstAlert = DateTime.MinValue;
        bool menuOpen, dragging, moved;
        Point dragStart;
        int ticks;

        public Widget()
        {
            using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;

            Text = "İnternet Sayacı";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(27, 29, 34);
            Size = new Size(S(216), S(48));
            Icon = MakeIcon();
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), S(9)))
                Region = new Region(path);
            PlaceOnScreen();

            BuildMenu();
            ContextMenuStrip = menu;

            tray.Icon = Icon;
            tray.Text = "İnternet Sayacı";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleWidget(); };
            tray.BalloonTipClicked += delegate { ShowDetails(); };

            long rx, tx;
            meter.Sample(out rx, out tx);
            tracker.Start();

            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        int S(int v) { return (int)Math.Round(v * scale); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: Alt+Tab'da görünmesin
                return cp;
            }
        }

        void PlaceOnScreen()
        {
            Rectangle wanted = new Rectangle(settings.X, settings.Y, Width, Height);
            bool visible = false;
            if (settings.X != int.MinValue)
                foreach (Screen s in Screen.AllScreens)
                    if (s.Bounds.IntersectsWith(wanted)) visible = true;

            if (visible) { Location = wanted.Location; return; }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(8), wa.Bottom - Height - S(8));
        }

        void BuildMenu()
        {
            menu.Items.Add("Uygulama bazında kullanım", null, delegate { ShowDetails(); });
            showItem = new ToolStripMenuItem("Göstergeyi gizle", null, delegate { ToggleWidget(); });
            menu.Items.Add(showItem);
            menu.Items.Add("Ayarlar...", null, delegate { OpenSettings(); });
            startupItem = new ToolStripMenuItem("Windows açılışında başlat", null, delegate { ToggleStartup(); });
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Çıkış", null, delegate { Close(); });
            menu.Opening += delegate
            {
                menuOpen = true;
                showItem.Text = Visible ? "Göstergeyi gizle" : "Göstergeyi göster";
                startupItem.Checked = StartupEnabled();
            };
            menu.Closed += delegate { menuOpen = false; };
        }

        static bool StartupEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        void ToggleStartup()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (k.GetValue(RunName) != null) k.DeleteValue(RunName);
                else k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            }
        }

        void ToggleWidget()
        {
            Visible = !Visible;
        }

        void ShowDetails()
        {
            if (details == null || details.IsDisposed) details = new DetailsForm(tracker);
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(Left + Width - details.Width, wa.Right - details.Width));
            int y = Top - details.Height - S(6);
            if (y < wa.Top) y = Math.Min(Bottom + S(6), wa.Bottom - details.Height);
            details.Location = new Point(x, y);
            details.Show();
            details.Activate();
        }

        void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(settings))
            {
                if (f.ShowDialog() != DialogResult.OK) return;
                settings.Save();
                Invalidate();
            }
        }

        void Tick()
        {
            long rx, tx;
            try { meter.Sample(out rx, out tx); }
            catch { return; }

            double secs = Math.Max(0.2, clock.Elapsed.TotalSeconds);
            clock.Restart();
            downSpeed = rx / secs;
            upSpeed = tx / secs;

            if (rx + tx > 0) store.Add(rx, tx);
            lastMinute.Enqueue(rx + tx);
            minuteSum += rx + tx;
            while (lastMinute.Count > 60) minuteSum -= lastMinute.Dequeue();

            CheckBurst();
            CheckQuota();

            string tip = "↓ " + Fmt.Speed(downSpeed) + "  ↑ " + Fmt.Speed(upSpeed) + "\nBugün " + Fmt.Bytes(store.Today);
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            // Görev çubuğunun üstüne sürüklendiyse arkada kalmasın.
            if (Visible && !menuOpen) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
            if (++ticks % 30 == 0) store.Save();
            Invalidate();
        }

        bool BurstActive { get { return minuteSum >= settings.BurstMB * 1024L * 1024L; } }

        void CheckBurst()
        {
            if (settings.BurstMB <= 0 || !BurstActive) return;
            if ((DateTime.Now - lastBurstAlert).TotalMinutes < 5) return;
            lastBurstAlert = DateTime.Now;

            string text = "Son 1 dakikada " + Fmt.Bytes(minuteSum) + " veri harcandı.";
            List<AppRow> rows = tracker.Rows();
            if (rows.Count > 0 && rows[0].LastMinute > 1024 * 1024)
                text += "\nEn çok kullanan: " + rows[0].Name + " (" + Fmt.Bytes(rows[0].LastMinute) + ")";
            tray.ShowBalloonTip(10000, "Yoğun internet kullanımı", text, ToolTipIcon.Warning);
        }

        void CheckQuota()
        {
            if (settings.QuotaGB <= 0) return;
            string period = settings.PeriodStart().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (settings.AlertPeriod != period) { settings.AlertPeriod = period; settings.AlertLevel = 0; }

            long used = store.Since(settings.PeriodStart());
            double pct = used * 100.0 / (settings.QuotaGB * Fmt.GB);
            int level = pct >= 100 ? 100 : pct >= 80 ? 80 : pct >= 50 ? 50 : 0;
            if (level <= settings.AlertLevel) return;

            settings.AlertLevel = level;
            settings.Save();
            string title = level >= 100 ? "Kota doldu" : "Kotanın %" + level + " kadarı kullanıldı";
            tray.ShowBalloonTip(10000, title,
                "Bu dönem bu bilgisayardan " + Fmt.Bytes(used) + " / " + settings.QuotaGB.ToString("0.##", Fmt.Tr) + " GB harcandı.",
                level >= 100 ? ToolTipIcon.Error : ToolTipIcon.Warning);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (GraphicsPath border = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), S(9)))
            using (Pen pen = new Pen(Color.FromArgb(58, 62, 70)))
                g.DrawPath(pen, border);

            Color down = Color.FromArgb(230, 230, 230);
            if (BurstActive && settings.BurstMB > 0) down = Color.FromArgb(248, 113, 113);
            else if (downSpeed >= 1024 * 1024) down = Color.FromArgb(251, 191, 36);
            else if (downSpeed >= 50 * 1024) down = Color.FromArgb(74, 222, 128);

            int pad = S(11), y1 = S(5), y2 = S(25);
            using (Brush b = new SolidBrush(down))
                g.DrawString("↓ " + Fmt.Speed(downSpeed), bigFont, b, pad, y1);
            using (Brush dim = new SolidBrush(Color.FromArgb(160, 165, 175)))
            {
                DrawRight(g, "↑ " + Fmt.Speed(upSpeed), bigFont, dim, y1);
                g.DrawString("Bugün " + Fmt.Bytes(store.Today), smallFont, dim, pad, y2);

                long used = store.Since(settings.PeriodStart());
                string right = "Dönem " + Fmt.Bytes(used);
                if (settings.QuotaGB > 0)
                    right = Fmt.Bytes(used) + " / " + settings.QuotaGB.ToString("0.##", Fmt.Tr) + " GB";
                DrawRight(g, right, smallFont, dim, y2);

                if (settings.QuotaGB > 0)
                {
                    double ratio = Math.Min(1.0, used / (settings.QuotaGB * Fmt.GB));
                    Color bar = ratio >= 0.8 ? Color.FromArgb(248, 113, 113) : Color.FromArgb(96, 165, 250);
                    using (Brush bb = new SolidBrush(bar))
                        g.FillRectangle(bb, 0, Height - S(3), (int)(Width * ratio), S(3));
                }
            }
        }

        void DrawRight(Graphics g, string text, Font font, Brush brush, int y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, Width - S(11) - size.Width, y);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath bg = Rounded(new Rectangle(1, 1, 30, 30), 7))
                    using (Brush b = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        g.FillPath(b, bg);
                    using (Pen p = new Pen(Color.White, 4f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        g.DrawLine(p, 16, 7, 16, 23);
                        g.DrawLines(p, new Point[] { new Point(9, 17), new Point(16, 24), new Point(23, 17) });
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; moved = false; dragStart = e.Location; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                int dx = e.X - dragStart.X, dy = e.Y - dragStart.Y;
                if (moved || Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
                {
                    moved = true;
                    Location = new Point(Left + dx, Top + dy);
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
            {
                dragging = false;
                if (moved) { settings.X = Left; settings.Y = Top; settings.Save(); }
                else ShowDetails();
            }
            base.OnMouseUp(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            store.Save();
            settings.Save();
            tray.Visible = false;
            base.OnFormClosing(e);
        }
    }

    class DetailsForm : Form
    {
        readonly AppTracker tracker;
        readonly ListView list = new ListView();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

        public DetailsForm(AppTracker tracker)
        {
            this.tracker = tracker;
            Text = "Uygulama bazında kullanım";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 9f);

            Label note = new Label();
            note.Text = "Windows'un veri kullanımı kayıtlarından okunur, birkaç saniye gecikebilir.";
            note.Dock = DockStyle.Bottom;
            note.Height = 22;
            note.ForeColor = SystemColors.GrayText;
            note.TextAlign = ContentAlignment.MiddleLeft;

            list.View = View.Details;
            list.FullRowSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.Dock = DockStyle.Fill;
            list.Columns.Add("Uygulama", 230);
            list.Columns.Add("Son 1 dk", 90, HorizontalAlignment.Right);
            list.Columns.Add("Bugün", 90, HorizontalAlignment.Right);

            SuspendLayout();
            Controls.Add(list);
            Controls.Add(note);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(432, 330);
            ResumeLayout(false);

            timer.Interval = 2000;
            timer.Tick += delegate { Reload(); };
            VisibleChanged += delegate
            {
                timer.Enabled = Visible;
                if (Visible) Reload();
            };
        }

        void Reload()
        {
            List<AppRow> rows = tracker.Rows();
            list.BeginUpdate();
            list.Items.Clear();
            for (int i = 0; i < rows.Count && i < 20; i++)
            {
                ListViewItem item = new ListViewItem(rows[i].Name);
                item.SubItems.Add(rows[i].LastMinute > 0 ? Fmt.Bytes(rows[i].LastMinute) : "-");
                item.SubItems.Add(Fmt.Bytes(rows[i].Today));
                list.Items.Add(item);
            }
            list.EndUpdate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }
    }

    class SettingsForm : Form
    {
        readonly Settings settings;
        readonly NumericUpDown quota = new NumericUpDown();
        readonly NumericUpDown resetDay = new NumericUpDown();
        readonly NumericUpDown burst = new NumericUpDown();

        public SettingsForm(Settings settings)
        {
            this.settings = settings;
            Text = "İnternet Sayacı - Ayarlar";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            SuspendLayout();
            quota.DecimalPlaces = 1;
            quota.Maximum = 1000;
            quota.Value = (decimal)Math.Min(1000, Math.Max(0, settings.QuotaGB));
            AddRow("Aylık kota (GB, 0 = takip etme)", quota, 16);

            resetDay.Minimum = 1;
            resetDay.Maximum = 31;
            resetDay.Value = Math.Min(31, Math.Max(1, settings.ResetDay));
            AddRow("Kotanın yenilendiği gün", resetDay, 52);

            burst.Maximum = 5000;
            burst.Increment = 10;
            burst.Value = Math.Min(5000, Math.Max(0, settings.BurstMB));
            AddRow("1 dakikada şu kadar MB harcanırsa uyar (0 = kapalı)", burst, 88);

            Button ok = new Button();
            ok.Text = "Kaydet";
            ok.SetBounds(248, 132, 84, 28);
            ok.Click += delegate { Apply(); };
            Button cancel = new Button();
            cancel.Text = "Vazgeç";
            cancel.SetBounds(340, 132, 84, 28);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(440, 176);
            ResumeLayout(false);
        }

        void AddRow(string text, NumericUpDown input, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(16, y + 3, 320, 20);
            input.SetBounds(344, y, 80, 24);
            Controls.Add(label);
            Controls.Add(input);
        }

        void Apply()
        {
            settings.QuotaGB = (double)quota.Value;
            settings.ResetDay = (int)resetDay.Value;
            settings.BurstMB = (int)burst.Value;
            DialogResult = DialogResult.OK;
        }
    }
}
