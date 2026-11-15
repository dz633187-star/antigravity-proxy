using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityProxyApp
{
    public class ProxyItem
    {
        public string name { get; set; }
        public string host { get; set; }
        public string port { get; set; }
        public string user { get; set; }
        public string pass { get; set; }

        public ProxyItem()
        {
            name = "Прокси";
            host = "";
            port = "8000";
            user = "";
            pass = "";
        }

        public ProxyItem Clone()
        {
            return new ProxyItem
            {
                name = this.name,
                host = this.host,
                port = this.port,
                user = this.user,
                pass = this.pass
            };
        }
    }

    public class AppSettings
    {
        public string theme { get; set; }
        public int activeProxyIndex { get; set; }
        public List<ProxyItem> proxies { get; set; }

        public AppSettings()
        {
            theme = "dark";
            activeProxyIndex = 0;
            proxies = new List<ProxyItem>();
        }
    }

    public class Palette
    {
        public Color bg, card, ink, muted, border, accent, accentHover, accentPressed;
        public Color ok, warn, err;
        public Color secondaryFill, secondaryHover, secondaryPressed, secondaryBorder, secondaryHoverBorder, secondaryText;

        public static Palette Get(string theme)
        {
            if (theme == "dark")
            {
                return new Palette
                {
                    bg = Color.FromArgb(20, 22, 27),
                    card = Color.FromArgb(28, 32, 40),
                    ink = Color.FromArgb(240, 243, 246),
                    muted = Color.FromArgb(140, 149, 159),
                    border = Color.FromArgb(48, 54, 61),
                    accent = Color.FromArgb(37, 99, 235),
                    accentHover = Color.FromArgb(59, 130, 246),
                    accentPressed = Color.FromArgb(29, 78, 216),
                    ok = Color.FromArgb(46, 160, 67),
                    warn = Color.FromArgb(210, 153, 34),
                    err = Color.FromArgb(248, 81, 73),
                    secondaryFill = Color.FromArgb(33, 38, 47),
                    secondaryHover = Color.FromArgb(46, 53, 65),
                    secondaryPressed = Color.FromArgb(26, 30, 38),
                    secondaryBorder = Color.FromArgb(56, 63, 76),
                    secondaryHoverBorder = Color.FromArgb(99, 110, 130),
                    secondaryText = Color.FromArgb(226, 232, 240)
                };
            }
            return new Palette
            {
                bg = Color.FromArgb(246, 248, 250),
                card = Color.White,
                ink = Color.FromArgb(24, 32, 44),
                muted = Color.FromArgb(100, 116, 139),
                border = Color.FromArgb(226, 232, 240),
                accent = Color.FromArgb(37, 99, 235),
                accentHover = Color.FromArgb(29, 78, 216),
                accentPressed = Color.FromArgb(30, 64, 175),
                ok = Color.FromArgb(16, 185, 129),
                warn = Color.FromArgb(217, 119, 6),
                err = Color.FromArgb(239, 68, 68),
                secondaryFill = Color.FromArgb(248, 250, 252),
                secondaryHover = Color.FromArgb(241, 245, 249),
                secondaryPressed = Color.FromArgb(226, 232, 240),
                secondaryBorder = Color.FromArgb(203, 213, 225),
                secondaryHoverBorder = Color.FromArgb(148, 163, 184),
                secondaryText = Color.FromArgb(30, 41, 59)
            };
        }
    }

    public class UiButton : Button
    {
        public int Radius { get; set; }
        private Color _fill = Color.FromArgb(37, 99, 235);
        public Color Fill { get { return _fill; } set { _fill = value; BackColor = value; Invalidate(); } }
        private Color _hover = Color.FromArgb(59, 130, 246);
        public Color Hover { get { return _hover; } set { _hover = value; Invalidate(); } }
        private Color _pressed = Color.FromArgb(29, 78, 216);
        public Color Pressed { get { return _pressed; } set { _pressed = value; Invalidate(); } }
        private Color _border = Color.FromArgb(56, 63, 76);
        public Color BorderColor { get { return _border; } set { _border = value; Invalidate(); } }
        private Color _hoverBorder = Color.FromArgb(99, 110, 130);
        public Color HoverBorderColor { get { return _hoverBorder; } set { _hoverBorder = value; Invalidate(); } }
        public int BorderWidth { get; set; }

        private bool _isHover, _isDown;

        public UiButton()
        {
            Radius = 7;
            BorderWidth = 1;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = _fill;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHover = false; _isDown = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _isDown = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isDown = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var bg = _isDown ? _pressed : (_isHover ? _hover : _fill);
            var bc = _isHover ? _hoverBorder : _border;

            if (Parent != null)
            {
                using (var pb = new SolidBrush(Parent.BackColor))
                {
                    g.FillRectangle(pb, ClientRectangle);
                }
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = GetRoundPath(rect, Radius))
            {
                using (var brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                if (BorderWidth > 0 && bc.A > 0)
                {
                    using (var pen = new Pen(bc, BorderWidth))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
        }

        private static GraphicsPath GetRoundPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            if (r <= 0) { path.AddRectangle(rect); return path; }
            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class UiComboBox : ComboBox
    {
        public Color ItemBackColor { get; set; }
        public Color ItemForeColor { get; set; }
        public Color SelectedItemBackColor { get; set; }
        public Color SelectedItemForeColor { get; set; }

        public UiComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 22;
            ItemBackColor = Color.FromArgb(33, 38, 47);
            ItemForeColor = Color.FromArgb(240, 243, 246);
            SelectedItemBackColor = Color.FromArgb(37, 99, 235);
            SelectedItemForeColor = Color.White;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) != 0;
            Color bg = isSelected ? SelectedItemBackColor : ItemBackColor;
            Color fg = isSelected ? SelectedItemForeColor : ItemForeColor;

            using (var b = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            string text = Items[e.Index] != null ? Items[e.Index].ToString() : "";
            using (var bText = new SolidBrush(fg))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
            {
                float x = (float)(e.Bounds.X + 6);
                float y = (float)e.Bounds.Y;
                float w = (float)Math.Max(0, e.Bounds.Width - 6);
                float h = (float)e.Bounds.Height;
                var rect = new RectangleF(x, y, w, h);
                e.Graphics.DrawString(text, Font, bText, rect, sf);
            }
        }
    }

    public class SpeedResult
    {
        public bool Ok { get; set; }
        public double Mbps { get; set; }
        public int PingMs { get; set; }
        public string Error { get; set; }
    }

    public class DirectInfoResult
    {
        public bool Ok { get; set; }
        public string Ip { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public int PingMs { get; set; }
        public string Error { get; set; }
    }

    public class AppCore
    {
        public static string AppRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityProxy");
        public static string XrayDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XrayNL");
        public static string XrayCfg = Path.Combine(XrayDir, "config.json");
        public static string XrayExe = Path.Combine(XrayDir, "xray.exe");
        public static string SettingsFile = Path.Combine(AppRoot, "settings.json");
        public static string BrowserProfile = Path.Combine(AppRoot, "BrowserProfile");
        public static string LogFile = Path.Combine(Path.GetTempPath(), "antigravity-proxy.log");

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogFile, string.Format("{0:HH:mm:ss} {1}\r\n", DateTime.Now, msg), Encoding.UTF8);
            }
            catch { }
        }

        public static AppSettings LoadSettings()
        {
            var s = new AppSettings();
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile, Encoding.UTF8);
                    var ser = new JavaScriptSerializer();
                    s = ser.Deserialize<AppSettings>(json);
                }
                catch (Exception ex) { Log("LoadSettings: " + ex.Message); }
            }

            if (s.proxies == null || s.proxies.Count == 0)
            {
                var cur = GetCurrentXrayServer();
                if (cur != null && !string.IsNullOrEmpty(cur.host))
                {
                    s.proxies = new List<ProxyItem> { cur };
                }
                else
                {
                    s.proxies = new List<ProxyItem>
                    {
                        new ProxyItem { name = "Мой прокси", host = "", port = "8000", user = "", pass = "" }
                    };
                }
            }

            if (s.activeProxyIndex < 0 || s.activeProxyIndex >= s.proxies.Count)
            {
                s.activeProxyIndex = 0;
            }

            if (string.IsNullOrEmpty(s.theme)) s.theme = "dark";
            return s;
        }

        public static void SaveSettings(AppSettings s)
        {
            try
            {
                if (!Directory.Exists(AppRoot)) Directory.CreateDirectory(AppRoot);
                var ser = new JavaScriptSerializer();
                string json = ser.Serialize(s);
                File.WriteAllText(SettingsFile, json, Encoding.UTF8);
            }
            catch (Exception ex) { Log("SaveSettings: " + ex.Message); }
        }

        public static ProxyItem GetCurrentXrayServer()
        {
            if (!File.Exists(XrayCfg)) return null;
            try
            {
                string json = File.ReadAllText(XrayCfg, Encoding.UTF8);
                var ser = new JavaScriptSerializer();
                var dict = ser.Deserialize<Dictionary<string, object>>(json);
                if (!dict.ContainsKey("outbounds")) return null;
                var outbounds = dict["outbounds"] as object[];
                if (outbounds == null) return null;
                foreach (Dictionary<string, object> ob in outbounds)
                {
                    if (ob.ContainsKey("protocol") && ob["protocol"].ToString() == "http" && ob.ContainsKey("settings"))
                    {
                        var settings = ob["settings"] as Dictionary<string, object>;
                        if (settings != null && settings.ContainsKey("servers"))
                        {
                            var servers = settings["servers"] as object[];
                            if (servers != null && servers.Length > 0)
                            {
                                var srv = servers[0] as Dictionary<string, object>;
                                string u = "", p = "";
                                if (srv.ContainsKey("users"))
                                {
                                    var users = srv["users"] as object[];
                                    if (users != null && users.Length > 0)
                                    {
                                        var usr = users[0] as Dictionary<string, object>;
                                        if (usr.ContainsKey("user")) u = usr["user"].ToString();
                                        if (usr.ContainsKey("pass")) p = usr["pass"].ToString();
                                    }
                                }
                                return new ProxyItem
                                {
                                    name = "Основной",
                                    host = srv.ContainsKey("address") ? srv["address"].ToString() : "",
                                    port = srv.ContainsKey("port") ? srv["port"].ToString() : "8000",
                                    user = u,
                                    pass = p
                                };
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void WriteXrayConfig(string host, string port, string user, string pass)
        {
            int pt = 8000;
            int.TryParse(port, out pt);
            var usersList = new List<object>();
            if (!string.IsNullOrEmpty(user))
            {
                usersList.Add(new Dictionary<string, object> { { "user", user }, { "pass", pass ?? "" } });
            }

            var server = new Dictionary<string, object>
            {
                { "address", host },
                { "port", pt },
                { "users", usersList }
            };

            var outbounds = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "tag", "proxy" },
                    { "protocol", "http" },
                    { "settings", new Dictionary<string, object> { { "servers", new object[] { server } } } }
                },
                new Dictionary<string, object>
                {
                    { "tag", "direct" },
                    { "protocol", "freedom" }
                }
            };

            var inbounds = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "tag", "socks" },
                    { "listen", "127.0.0.1" },
                    { "port", 10808 },
                    { "protocol", "socks" },
                    { "settings", new Dictionary<string, object> { { "udp", true } } }
                },
                new Dictionary<string, object>
                {
                    { "tag", "http" },
                    { "listen", "127.0.0.1" },
                    { "port", 10809 },
                    { "protocol", "http" }
                }
            };

            var root = new Dictionary<string, object>
            {
                { "log", new Dictionary<string, object> { { "loglevel", "warning" } } },
                { "inbounds", inbounds },
                { "outbounds", outbounds }
            };

            var ser = new JavaScriptSerializer();
            string json = ser.Serialize(root);
            try
            {
                if (!Directory.Exists(XrayDir)) Directory.CreateDirectory(XrayDir);
                if (File.Exists(XrayCfg))
                {
                    try
                    {
                        var fi = new FileInfo(XrayCfg);
                        if (fi.IsReadOnly) fi.IsReadOnly = false;
                        fi.Attributes = FileAttributes.Normal;
                    }
                    catch { }
                }
                File.WriteAllText(XrayCfg, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log("WriteXrayConfig primary write failed: " + ex.Message);
                try
                {
                    string tmp = XrayCfg + ".tmp";
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
                    File.Copy(tmp, XrayCfg, true);
                    File.Delete(tmp);
                }
                catch (Exception ex2)
                {
                    Log("WriteXrayConfig fallback failed: " + ex2.Message);
                    throw;
                }
            }
        }

        public static bool TestPort(int port = 10809)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect("127.0.0.1", port, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(400);
                    bool conn = ok && client.Connected;
                    return conn;
                }
            }
            catch { return false; }
        }

        public static bool RestartXray()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("xray"))
                {
                    try { p.Kill(); } catch { }
                }
                System.Threading.Thread.Sleep(500);

                if (!File.Exists(XrayExe)) return false;
                var psi = new ProcessStartInfo
                {
                    FileName = XrayExe,
                    Arguments = "run -c \"" + XrayCfg + "\"",
                    WorkingDirectory = XrayDir,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                for (int i = 0; i < 20; i++)
                {
                    System.Threading.Thread.Sleep(250);
                    if (TestPort(10809)) return true;
                }
            }
            catch (Exception ex) { Log("RestartXray: " + ex.Message); }
            return false;
        }

        private static DirectInfoResult QueryDirectProxyEndpoint(string url, IWebProxy proxy, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Proxy = proxy;
            req.KeepAlive = false;
            req.Headers.Add("Cache-Control", "no-cache");
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;

            var sw = Stopwatch.StartNew();
            using (var resp = req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                sw.Stop();
                string text = reader.ReadToEnd();
                var ser = new JavaScriptSerializer();
                var d = ser.Deserialize<Dictionary<string, object>>(text);
                string ip = "";
                if (d != null)
                {
                    if (d.ContainsKey("ip") && d["ip"] != null) ip = d["ip"].ToString();
                    else if (d.ContainsKey("query") && d["query"] != null) ip = d["query"].ToString();

                    string country = "";
                    if (d.ContainsKey("country") && d["country"] != null) country = d["country"].ToString();
                    else if (d.ContainsKey("countryCode") && d["countryCode"] != null) country = d["countryCode"].ToString();

                    string city = "";
                    if (d.ContainsKey("city") && d["city"] != null) city = d["city"].ToString();

                    return new DirectInfoResult
                    {
                        Ok = true,
                        Ip = ip,
                        Country = country,
                        City = city,
                        PingMs = (int)sw.ElapsedMilliseconds
                    };
                }
                return new DirectInfoResult { Ok = false, Error = "Empty response" };
            }
        }

        public static DirectInfoResult CheckDirectProxyInfo(string host, string port, string user, string pass)
        {
            try
            {
                int pt = 8000;
                int.TryParse(port, out pt);
                var proxy = new WebProxy(host, pt);
                if (!string.IsNullOrEmpty(user))
                {
                    proxy.Credentials = new NetworkCredential(user, pass ?? "");
                }

                // 1. ipinfo.io (HTTPS)
                try
                {
                    return QueryDirectProxyEndpoint("https://ipinfo.io/json", proxy, 9000);
                }
                catch (Exception ex1)
                {
                    Log("DirectInfo ipinfo.io: " + ex1.Message);
                    // 2. ip-api.com (HTTP - reliable fallback)
                    try
                    {
                        return QueryDirectProxyEndpoint("http://ip-api.com/json", proxy, 9000);
                    }
                    catch (Exception ex2)
                    {
                        Log("DirectInfo ip-api.com: " + ex2.Message);
                        // 3. api.ipify.org
                        return QueryDirectProxyEndpoint("https://api.ipify.org?format=json", proxy, 9000);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new DirectInfoResult { Ok = false, Error = msg };
            }
        }

        public static SpeedResult MeasureSpeed(string host = null, string port = null, string user = null, string pass = null)
        {
            try
            {
                IWebProxy proxy = null;
                if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(port))
                {
                    int pt = 8000;
                    int.TryParse(port, out pt);
                    var wp = new WebProxy(host, pt);
                    if (!string.IsNullOrEmpty(user))
                    {
                        wp.Credentials = new NetworkCredential(user, pass ?? "");
                    }
                    proxy = wp;
                }
                else
                {
                    proxy = new WebProxy("127.0.0.1", 10809);
                }

                string nocache = Guid.NewGuid().ToString("N");
                // 1 MB target with rolling 3-4s measurement limit
                string url = "https://speed.cloudflare.com/__down?bytes=1048576&nocache=" + nocache;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Proxy = proxy;
                req.KeepAlive = false;
                req.Headers.Add("Cache-Control", "no-cache, no-store");
                req.Headers.Add("Pragma", "no-cache");
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;

                var swPing = Stopwatch.StartNew();
                using (var resp = req.GetResponse())
                {
                    swPing.Stop();
                    int ping = (int)swPing.ElapsedMilliseconds;

                    var swDl = Stopwatch.StartNew();
                    using (var stream = resp.GetResponseStream())
                    {
                        byte[] buf = new byte[32768];
                        int total = 0;
                        int read = 0;
                        // Stream read: max 3.5 seconds or 1MB, whichever comes first
                        while (swDl.ElapsedMilliseconds < 3500 && (read = stream.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += read;
                        }
                        swDl.Stop();
                        double sec = swDl.Elapsed.TotalSeconds;
                        if (sec <= 0.01) sec = 0.01;
                        double mbps = Math.Round(((total * 8.0) / 1000000.0) / sec, 2);
                        return new SpeedResult { Ok = true, Mbps = mbps, PingMs = ping };
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new SpeedResult { Ok = false, Error = msg };
            }
        }

        public static bool TestSystemProxy()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key == null) return false;
                    var enable = key.GetValue("ProxyEnable");
                    var server = key.GetValue("ProxyServer") as string;
                    return (enable is int && (int)enable == 1) && (server != null && server.Contains("10809"));
                }
            }
            catch { return false; }
        }

        public static void SetSystemProxy(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key == null) return;
                    if (on)
                    {
                        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                        key.SetValue("ProxyServer", "127.0.0.1:10809", RegistryValueKind.String);
                        key.SetValue("ProxyOverride", "<local>;localhost;127.0.0.1;::1;" + Environment.MachineName, RegistryValueKind.String);
                    }
                    else
                    {
                        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        public static string FindAntigravityApp()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = {
                Path.Combine(local, @"Programs\antigravity\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity 2.0\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity IDE\Antigravity IDE.exe")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return "";
        }

        public static string FindLanguageServer(string appPath)
        {
            if (string.IsNullOrEmpty(appPath)) return "";
            string dir = Path.GetDirectoryName(appPath);
            string resDir = Path.Combine(dir, "resources");
            if (Directory.Exists(resDir))
            {
                try
                {
                    var files = Directory.GetFiles(resDir, "language_server.exe", SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0];
                }
                catch { }
            }
            return "";
        }

        public static bool? TestPatched(string lsPath)
        {
            if (string.IsNullOrEmpty(lsPath) || !File.Exists(lsPath)) return null;
            try
            {
                using (var fs = File.OpenRead(lsPath))
                {
                    byte[] buf = new byte[4 * 1024 * 1024];
                    string carry = "";
                    int n = 0;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        string chunk = carry + Encoding.ASCII.GetString(buf, 0, n);
                        if (chunk.Contains("inexigible")) return true;
                        if (chunk.Contains("ineligible")) return false;
                        carry = chunk.Length > 32 ? chunk.Substring(chunk.Length - 32) : chunk;
                    }
                }
                return false;
            }
            catch { return null; }
        }

        public static string FindBrowser()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] c = {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
            };
            foreach (var b in c) { if (File.Exists(b)) return b; }
            return "";
        }

        public static string FindOpenCodeGui()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] c = {
                Path.Combine(local, @"Programs\@opencode-aidesktop\OpenCode.exe"),
                Path.Combine(local, @"Programs\OpenCode\OpenCode.exe")
            };
            foreach (var b in c) { if (File.Exists(b)) return b; }
            return "";
        }

        public static string FindOpenCodeCli()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] c = {
                Path.Combine(local, @"Programs\@opencode-aidesktop\resources\opencode-cli.exe"),
                Path.Combine(local, @"Programs\@opencode-aidesktop\resources\opencode.exe"),
                Path.Combine(local, @"Programs\opencode\opencode.exe")
            };
            foreach (var b in c) { if (File.Exists(b)) return b; }
            return "";
        }

        public static string FindPatcher()
        {
            string u = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] dirs = { Path.Combine(u, "Downloads"), AppDomain.CurrentDomain.BaseDirectory };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                try
                {
                    var files = Directory.GetFiles(d, "Open_AG_Patcher*.exe");
                    if (files.Length > 0)
                    {
                        Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
                        return files[0];
                    }
                }
                catch { }
            }
            return "";
        }
    }

    public class SettingsDialog : Form
    {
        private AppSettings _settings;
        private List<ProxyItem> _localProxies;
        private int _localActiveIdx;
        private int _currentIdx;
        private Palette _p;

        private UiComboBox cbList;
        private UiButton btnAdd, btnDel, btnActive, btnTest, btnSave, btnCancel;
        private TextBox tbName, tbH, tbPt, tbU, tbPw;
        private RadioButton rbDark, rbLight;
        private Label lblNote;
        private bool _updatingCb = false;

        public bool SettingsChanged { get; private set; }

        public SettingsDialog(AppSettings settings, Palette palette, Icon icon)
        {
            _settings = settings;
            _p = palette;
            if (icon != null) Icon = icon;

            _localProxies = new List<ProxyItem>();
            foreach (var px in settings.proxies) _localProxies.Add(px.Clone());
            if (_localProxies.Count == 0) _localProxies.Add(new ProxyItem());
            _localActiveIdx = settings.activeProxyIndex;
            if (_localActiveIdx < 0 || _localActiveIdx >= _localProxies.Count) _localActiveIdx = 0;
            _currentIdx = _localActiveIdx;

            Text = "Настройки";
            ClientSize = new Size(460, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = _p.bg;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;

            BuildControls();
            RefreshList(_currentIdx);
            LoadFields(_currentIdx);
        }

        private void BuildControls()
        {
            var lblTitle = new Label { Text = "Настройки прокси", Location = new Point(18, 14), Size = new Size(200, 22), ForeColor = _p.ink, BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 10.5f) };
            Controls.Add(lblTitle);

            cbList = new UiComboBox { Location = new Point(18, 42), Size = new Size(236, 28), Font = new Font("Segoe UI", 9f), BackColor = _p.secondaryFill, ForeColor = _p.ink, ItemBackColor = _p.secondaryFill, ItemForeColor = _p.ink, SelectedItemBackColor = _p.accent, SelectedItemForeColor = Color.White };
            Controls.Add(cbList);

            btnAdd = new UiButton { Text = "+ Добавить", Location = new Point(262, 41), Size = new Size(96, 28), Radius = 6, Fill = _p.secondaryFill, Hover = _p.secondaryHover, Pressed = _p.secondaryPressed, BorderColor = _p.secondaryBorder, HoverBorderColor = _p.accent, BorderWidth = 1, ForeColor = _p.secondaryText };
            Controls.Add(btnAdd);

            btnDel = new UiButton { Text = "Удалить", Location = new Point(366, 41), Size = new Size(76, 28), Radius = 6, Fill = _p.secondaryFill, Hover = _p.secondaryHover, Pressed = _p.secondaryPressed, BorderColor = _p.secondaryBorder, HoverBorderColor = _p.err, BorderWidth = 1, ForeColor = _p.secondaryText };
            Controls.Add(btnDel);

            Controls.Add(new Label { Text = "Имя профиля:", Location = new Point(18, 76), Size = new Size(200, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbName = new TextBox { Location = new Point(18, 94), Size = new Size(424, 24), BackColor = _p.card, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(tbName);

            Controls.Add(new Label { Text = "Сервер / Хост:", Location = new Point(18, 124), Size = new Size(120, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbH = new TextBox { Location = new Point(18, 142), Size = new Size(260, 24), BackColor = _p.card, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(tbH);

            Controls.Add(new Label { Text = "Порт:", Location = new Point(290, 124), Size = new Size(60, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbPt = new TextBox { Location = new Point(290, 142), Size = new Size(152, 24), BackColor = _p.card, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(tbPt);

            Controls.Add(new Label { Text = "Логин:", Location = new Point(18, 172), Size = new Size(120, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbU = new TextBox { Location = new Point(18, 190), Size = new Size(260, 24), BackColor = _p.card, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(tbU);

            Controls.Add(new Label { Text = "Пароль:", Location = new Point(290, 172), Size = new Size(60, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbPw = new TextBox { Location = new Point(290, 190), Size = new Size(152, 24), UseSystemPasswordChar = true, BackColor = _p.card, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(tbPw);

            btnActive = new UiButton { Text = "Сделать активным", Location = new Point(18, 226), Size = new Size(200, 32), Radius = 6 };
            Controls.Add(btnActive);

            btnTest = new UiButton { Text = "↻ Проверить этот прокси", Location = new Point(226, 226), Size = new Size(216, 32), Radius = 6, Fill = _p.secondaryFill, Hover = _p.secondaryHover, Pressed = _p.secondaryPressed, BorderColor = _p.secondaryBorder, HoverBorderColor = _p.secondaryHoverBorder, BorderWidth = 1, ForeColor = _p.secondaryText };
            Controls.Add(btnTest);

            Controls.Add(new Label { Text = "Тема оформления", Location = new Point(18, 270), Size = new Size(200, 20), ForeColor = _p.ink, BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 9.5f) });
            rbDark = new RadioButton { Text = "Тёмная", Location = new Point(20, 296), Size = new Size(100, 22), ForeColor = _p.ink, BackColor = Color.Transparent };
            rbLight = new RadioButton { Text = "Светлая", Location = new Point(130, 296), Size = new Size(100, 22), ForeColor = _p.ink, BackColor = Color.Transparent };
            if (_settings.theme == "dark") rbDark.Checked = true; else rbLight.Checked = true;
            Controls.Add(rbDark); Controls.Add(rbLight);

            lblNote = new Label { Text = "Проверка прокси не влияет на активное соединение Xray.", Location = new Point(18, 330), Size = new Size(424, 44), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) };
            Controls.Add(lblNote);

            btnSave = new UiButton { Text = "Сохранить", Location = new Point(18, 444), Size = new Size(130, 38), Radius = 7, Fill = _p.accent, Hover = _p.accentHover, Pressed = _p.accentPressed, BorderWidth = 0, ForeColor = Color.White };
            Controls.Add(btnSave);

            btnCancel = new UiButton { Text = "Отмена", Location = new Point(158, 444), Size = new Size(110, 38), Radius = 7, Fill = _p.secondaryFill, Hover = _p.secondaryHover, Pressed = _p.secondaryPressed, BorderColor = _p.secondaryBorder, HoverBorderColor = _p.secondaryHoverBorder, BorderWidth = 1, ForeColor = _p.secondaryText };
            Controls.Add(btnCancel);

            cbList.SelectedIndexChanged += (s, e) =>
            {
                if (!_updatingCb && cbList.SelectedIndex >= 0)
                {
                    SaveFields();
                    _currentIdx = cbList.SelectedIndex;
                    LoadFields(_currentIdx);
                }
            };

            btnAdd.Click += (s, e) =>
            {
                SaveFields();
                var newItem = new ProxyItem { name = "Прокси " + (_localProxies.Count + 1), host = "", port = "8000" };
                _localProxies.Add(newItem);
                _currentIdx = _localProxies.Count - 1;
                RefreshList(_currentIdx);
                LoadFields(_currentIdx);
                tbName.Focus();
                tbName.SelectAll();
                lblNote.Text = "Добавлен новый профиль. Заполните реквизиты и нажмите «Сохранить».";
                lblNote.ForeColor = _p.muted;
            };

            btnDel.Click += (s, e) =>
            {
                if (_localProxies.Count <= 1)
                {
                    MessageBox.Show("Нельзя удалить единственный прокси.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int rem = _currentIdx;
                _localProxies.RemoveAt(rem);
                if (_localActiveIdx == rem) _localActiveIdx = 0;
                else if (_localActiveIdx > rem) _localActiveIdx--;
                if (_currentIdx >= _localProxies.Count) _currentIdx = _localProxies.Count - 1;
                RefreshList(_currentIdx);
                LoadFields(_currentIdx);
                lblNote.Text = "Профиль удалён.";
                lblNote.ForeColor = _p.muted;
            };

            btnActive.Click += (s, e) =>
            {
                SaveFields();
                _localActiveIdx = _currentIdx;
                RefreshList(_currentIdx);
                UpdateActiveButton(_currentIdx);
                lblNote.Text = string.Format("Прокси «{0}» выбран активным (нажмите «Сохранить» для применения).", _localProxies[_currentIdx].name);
                lblNote.ForeColor = _p.accent;
            };

            btnTest.Click += async (s, e) =>
            {
                SaveFields();
                var cur = _localProxies[_currentIdx];
                if (string.IsNullOrEmpty(cur.host))
                {
                    lblNote.Text = "Укажите адрес сервера перед проверкой.";
                    lblNote.ForeColor = _p.err;
                    return;
                }
                btnTest.Enabled = false;
                btnTest.Text = "Проверка…";
                lblNote.Text = string.Format("Проверяю {0} ({1}:{2}) напрямую…", cur.name, cur.host, cur.port);
                lblNote.ForeColor = _p.warn;

                try
                {
                    // DIRECT CHECK without touching Xray
                    var info = await Task.Run(() => AppCore.CheckDirectProxyInfo(cur.host, cur.port, cur.user, cur.pass));
                    if (info.Ok)
                    {
                        lblNote.Text = string.Format("✓ {0} ({1}, {2}) · пинг {3} мс · замер скорости…", info.Ip, info.Country, info.City, info.PingMs);
                        lblNote.ForeColor = _p.ok;

                        var sp = await Task.Run(() => AppCore.MeasureSpeed(cur.host, cur.port, cur.user, cur.pass));
                        string spStr = sp.Ok ? string.Format(" · {0} Мбит/с", sp.Mbps) : "";
                        lblNote.Text = string.Format("✓ {0} ({1}, {2}) · пинг {3} мс{4}", info.Ip, info.Country, info.City, info.PingMs, spStr);
                        lblNote.ForeColor = _p.ok;
                    }
                    else
                    {
                        lblNote.Text = "Ошибка подключения: " + info.Error;
                        lblNote.ForeColor = _p.err;
                    }
                }
                catch (Exception ex)
                {
                    lblNote.Text = "Ошибка: " + ex.Message;
                    lblNote.ForeColor = _p.err;
                }
                finally
                {
                    btnTest.Enabled = true;
                    btnTest.Text = "↻ Проверить этот прокси";
                }
            };

            btnSave.Click += (s, e) =>
            {
                SaveFields();
                bool hasHost = false;
                foreach (var px in _localProxies)
                {
                    if (!string.IsNullOrEmpty(px.host)) { hasHost = true; break; }
                }
                if (!hasHost)
                {
                    lblNote.Text = "Укажите хост/сервер хотя бы для одного прокси.";
                    lblNote.ForeColor = _p.err;
                    return;
                }

                _settings.proxies = _localProxies;
                _settings.activeProxyIndex = _localActiveIdx;
                _settings.theme = rbDark.Checked ? "dark" : "light";
                AppCore.SaveSettings(_settings);

                SettingsChanged = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
        }

        private void SaveFields()
        {
            if (_currentIdx >= 0 && _currentIdx < _localProxies.Count)
            {
                var cur = _localProxies[_currentIdx];
                cur.name = tbName.Text.Trim();
                cur.host = tbH.Text.Trim();
                cur.port = tbPt.Text.Trim();
                cur.user = tbU.Text.Trim();
                cur.pass = tbPw.Text;
            }
        }

        private void LoadFields(int idx)
        {
            if (idx >= 0 && idx < _localProxies.Count)
            {
                var cur = _localProxies[idx];
                tbName.Text = cur.name ?? "";
                tbH.Text = cur.host ?? "";
                tbPt.Text = cur.port ?? "";
                tbU.Text = cur.user ?? "";
                tbPw.Text = cur.pass ?? "";
                UpdateActiveButton(idx);
            }
        }

        private void UpdateActiveButton(int idx)
        {
            if (idx == _localActiveIdx)
            {
                btnActive.Text = "✓ Текущий активный";
                btnActive.Fill = _p.accent;
                btnActive.Hover = _p.accentHover;
                btnActive.Pressed = _p.accentPressed;
                btnActive.BorderWidth = 0;
                btnActive.ForeColor = Color.White;
            }
            else
            {
                btnActive.Text = "Сделать активным";
                btnActive.Fill = _p.secondaryFill;
                btnActive.Hover = _p.secondaryHover;
                btnActive.Pressed = _p.secondaryPressed;
                btnActive.BorderColor = _p.secondaryBorder;
                btnActive.HoverBorderColor = _p.secondaryHoverBorder;
                btnActive.BorderWidth = 1;
                btnActive.ForeColor = _p.secondaryText;
            }
            btnActive.Invalidate();
        }

        private void RefreshList(int selIdx)
        {
            _updatingCb = true;
            try
            {
                cbList.Items.Clear();
                for (int i = 0; i < _localProxies.Count; i++)
                {
                    var item = _localProxies[i];
                    string star = (i == _localActiveIdx) ? "★ " : "";
                    string n = !string.IsNullOrEmpty(item.name) ? item.name : ("Прокси " + (i + 1));
                    string h = !string.IsNullOrEmpty(item.host) ? string.Format("{0}:{1}", item.host, item.port) : "пусто";
                    cbList.Items.Add(string.Format("{0}{1} ({2})", star, n, h));
                }
                if (selIdx >= 0 && selIdx < _localProxies.Count)
                    cbList.SelectedIndex = selIdx;
                else if (_localProxies.Count > 0)
                    cbList.SelectedIndex = 0;
            }
            finally
            {
                _updatingCb = false;
            }
        }
    }

    public class MainForm : Form
    {
        private AppSettings _settings;
        private Palette _p;
        private bool _isExiting = false;
        private bool _updatingProxyCb = false;

        private Label lblTitle, lblSub, lblStatus, lblDetail, lblPatch, lblApp, lblFoot;
        private UiButton btnSettings, btnTheme, btnStatus, btnSpeed;
        private UiComboBox cbActiveProxy;
        private UiButton btnStart, btnBrowser, btnOcGui, btnOcCli, btnPatch, btnSysProxy;
        private Panel card1, card2;
        private NotifyIcon notifyIcon;

        public MainForm()
        {
            Text = "Antigravity Proxy";
            ClientSize = new Size(520, 512);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = true;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AntigravityProxy.ico");
            if (File.Exists(icoPath))
            {
                try { Icon = new Icon(icoPath); } catch { }
            }

            _settings = AppCore.LoadSettings();
            _p = Palette.Get(_settings.theme);

            BuildUI();
            ApplyTheme();
            RefreshProxyComboBox();
            InitTray();

            Shown += (s, e) =>
            {
                Activate();
                TopMost = true;
                TopMost = false;
                EnsureActiveProxySynced();
                UpdateStatusAsync();
                UpdatePatch();
            };

            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    notifyIcon.Visible = true;
                }
            };

            FormClosing += (s, e) =>
            {
                if (!_isExiting)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
            };
        }

        private void BuildUI()
        {
            lblTitle = new Label { Text = "Antigravity Proxy", Location = new Point(12, 10), Size = new Size(200, 26), Font = new Font("Segoe UI Semibold", 12f) };
            lblSub = new Label { Text = "прокси, патч и запуск", Location = new Point(14, 36), Size = new Size(200, 18), Font = new Font("Segoe UI", 8.5f) };
            Controls.Add(lblTitle);
            Controls.Add(lblSub);

            btnSettings = new UiButton { Text = "Настройки", Location = new Point(284, 12), Size = new Size(108, 32), Radius = 7 };
            btnTheme = new UiButton { Text = "", Location = new Point(398, 12), Size = new Size(106, 32), Radius = 7 };
            Controls.Add(btnSettings);
            Controls.Add(btnTheme);
            btnSettings.BringToFront();
            btnTheme.BringToFront();

            // Card 1
            card1 = new Panel { Location = new Point(16, 64), Size = new Size(488, 122) };
            Controls.Add(card1);

            lblStatus = new Label { Text = "Запускаю прокси…", Location = new Point(16, 14), Size = new Size(450, 24), Font = new Font("Segoe UI Semibold", 10.5f) };
            lblDetail = new Label { Text = "", Location = new Point(16, 40), Size = new Size(450, 34), Font = new Font("Segoe UI", 8.5f) };
            card1.Controls.Add(lblStatus);
            card1.Controls.Add(lblDetail);

            btnStatus = new UiButton { Text = "↻ Статус", Location = new Point(16, 80), Size = new Size(96, 28), Radius = 6 };
            btnSpeed = new UiButton { Text = "⚡ Скорость", Location = new Point(118, 80), Size = new Size(104, 28), Radius = 6 };
            cbActiveProxy = new UiComboBox { Location = new Point(228, 80), Size = new Size(244, 28), DropDownWidth = 280, Font = new Font("Segoe UI", 9f) };
            card1.Controls.Add(btnStatus);
            card1.Controls.Add(btnSpeed);
            card1.Controls.Add(cbActiveProxy);

            // Action buttons
            btnStart = new UiButton { Text = "▶ Запустить Antigravity", Location = new Point(16, 198), Size = new Size(488, 44), Radius = 8, Font = new Font("Segoe UI Semibold", 10f) };
            btnBrowser = new UiButton { Text = "🌐 AI Studio (Web)", Location = new Point(16, 250), Size = new Size(180, 36), Radius = 7 };
            btnOcGui = new UiButton { Text = "⚡ OpenCode GUI", Location = new Point(204, 250), Size = new Size(142, 36), Radius = 7 };
            btnOcCli = new UiButton { Text = ">_ OpenCode CLI", Location = new Point(354, 250), Size = new Size(150, 36), Radius = 7 };
            Controls.Add(btnStart);
            Controls.Add(btnBrowser);
            Controls.Add(btnOcGui);
            Controls.Add(btnOcCli);

            // Card 2
            card2 = new Panel { Location = new Point(16, 298), Size = new Size(488, 156) };
            Controls.Add(card2);

            lblPatch = new Label { Text = "Патч: проверка…", Location = new Point(16, 14), Size = new Size(300, 20), Font = new Font("Segoe UI", 9f) };
            btnPatch = new UiButton { Text = "Патч", Location = new Point(380, 10), Size = new Size(92, 28), Radius = 6 };
            lblApp = new Label { Text = "", Location = new Point(16, 44), Size = new Size(450, 44), Font = new Font("Segoe UI", 8f) };
            btnSysProxy = new UiButton { Text = "Системный прокси: выкл", Location = new Point(16, 106), Size = new Size(250, 32), Radius = 7 };
            card2.Controls.Add(lblPatch);
            card2.Controls.Add(btnPatch);
            card2.Controls.Add(lblApp);
            card2.Controls.Add(btnSysProxy);

            lblFoot = new Label { Text = "Прокси-настройки, тема и пути — в «Настройках».", Location = new Point(18, 474), Size = new Size(480, 18), Font = new Font("Segoe UI", 8.5f) };
            Controls.Add(lblFoot);

            // Handlers
            btnTheme.Click += (s, e) =>
            {
                _settings.theme = (_settings.theme == "dark") ? "light" : "dark";
                AppCore.SaveSettings(_settings);
                _p = Palette.Get(_settings.theme);
                ApplyTheme();
            };

            btnSettings.Click += (s, e) =>
            {
                using (var dlg = new SettingsDialog(_settings, _p, Icon))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _settings = AppCore.LoadSettings();
                        _p = Palette.Get(_settings.theme);
                        ApplyTheme();
                        RefreshProxyComboBox();
                        EnsureActiveProxySynced();
                        UpdateStatusAsync();
                        UpdatePatch();
                    }
                }
            };

            cbActiveProxy.SelectedIndexChanged += (s, e) =>
            {
                if (!_updatingProxyCb && cbActiveProxy.SelectedIndex >= 0)
                {
                    if (cbActiveProxy.SelectedIndex != _settings.activeProxyIndex)
                    {
                        SwitchActiveProxy(cbActiveProxy.SelectedIndex);
                    }
                }
            };

            btnStatus.Click += (s, e) =>
            {
                UpdateStatusAsync();
            };

            btnSpeed.Click += async (s, e) =>
            {
                if (!AppCore.TestPort(10809))
                {
                    lblStatus.Text = "Прокси не запущен";
                    lblStatus.ForeColor = _p.err;
                    return;
                }
                btnSpeed.Enabled = false;
                btnSpeed.Text = "⚡ Замер…";
                lblStatus.Text = "Замер скорости…";
                lblStatus.ForeColor = _p.warn;

                try
                {
                    var res = await Task.Run(() => AppCore.MeasureSpeed());
                    if (res.Ok)
                    {
                        lblStatus.Text = "Прокси работает";
                        lblStatus.ForeColor = _p.ok;
                        var act = GetActiveProxy();
                        string pName = (act != null && !string.IsNullOrEmpty(act.name)) ? act.name : "Прокси";
                        lblDetail.Text = string.Format("{0}: {1} Мбит/с · задержка {2} мс\r\nТест Cloudflare CDN (поток данных)", pName, res.Mbps, res.PingMs);
                    }
                    else
                    {
                        lblStatus.Text = "Ошибка замера скорости";
                        lblStatus.ForeColor = _p.warn;
                        lblDetail.Text = "Не удалось загрузить данные: " + res.Error;
                    }
                }
                catch (Exception ex)
                {
                    AppCore.Log("BtnSpeed: " + ex.ToString());
                    lblStatus.Text = "Ошибка замера";
                    lblStatus.ForeColor = _p.err;
                }
                finally
                {
                    btnSpeed.Enabled = true;
                    btnSpeed.Text = "⚡ Скорость";
                }
            };

            btnStart.Click += (s, e) =>
            {
                try
                {
                    string app = AppCore.FindAntigravityApp();
                    if (string.IsNullOrEmpty(app))
                    {
                        MessageBox.Show("Antigravity не найден.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!AppCore.TestPort(10809)) AppCore.RestartXray();

                    Environment.SetEnvironmentVariable("AG_LS_PROXY", "http://127.0.0.1:10809");
                    Environment.SetEnvironmentVariable("HTTP_PROXY", "http://127.0.0.1:10809");
                    Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://127.0.0.1:10809");
                    Environment.SetEnvironmentVariable("NO_PROXY", "localhost,127.0.0.1,::1," + Environment.MachineName);

                    foreach (var p in Process.GetProcessesByName("Antigravity"))
                    {
                        try { p.Kill(); } catch { }
                    }
                    System.Threading.Thread.Sleep(700);
                    Process.Start(app);
                    UpdateStatusAsync();
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Не удалось запустить: " + ex.Message;
                    lblStatus.ForeColor = _p.err;
                }
            };

            btnBrowser.Click += (s, e) =>
            {
                try
                {
                    string browser = AppCore.FindBrowser();
                    if (string.IsNullOrEmpty(browser))
                    {
                        MessageBox.Show("Chrome или Edge не найден.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!AppCore.TestPort(10809)) AppCore.RestartXray();
                    if (!Directory.Exists(AppCore.BrowserProfile)) Directory.CreateDirectory(AppCore.BrowserProfile);

                    string args = string.Format("--proxy-server=http://127.0.0.1:10809 --proxy-bypass-list=\"<local>;localhost;127.0.0.1;::1;{0}\" --user-data-dir=\"{1}\" --no-first-run --no-default-browser-check https://aistudio.google.com/", Environment.MachineName, AppCore.BrowserProfile);
                    Process.Start(browser, args);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось открыть браузер: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnOcGui.Click += (s, e) =>
            {
                try
                {
                    string oc = AppCore.FindOpenCodeGui();
                    if (string.IsNullOrEmpty(oc))
                    {
                        MessageBox.Show("OpenCode GUI не найден.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!AppCore.TestPort(10809)) AppCore.RestartXray();
                    SetProxyEnv();
                    Process.Start(oc);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось запустить OpenCode GUI: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnOcCli.Click += (s, e) =>
            {
                try
                {
                    string oc = AppCore.FindOpenCodeCli();
                    if (string.IsNullOrEmpty(oc))
                    {
                        MessageBox.Show("OpenCode CLI не найден.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!AppCore.TestPort(10809)) AppCore.RestartXray();
                    SetProxyEnv();
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoExit -NoProfile -Command \"& '" + oc + "'\""
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось запустить OpenCode CLI: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnPatch.Click += (s, e) =>
            {
                string patcher = AppCore.FindPatcher();
                if (string.IsNullOrEmpty(patcher))
                {
                    MessageBox.Show("Патчер не найден в «Загрузках».", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    string app = AppCore.FindAntigravityApp();
                    string dir = !string.IsNullOrEmpty(app) ? Path.GetDirectoryName(app) : "";
                    var psi = new ProcessStartInfo
                    {
                        FileName = patcher,
                        Verb = "runas"
                    };
                    if (!string.IsNullOrEmpty(dir)) psi.Arguments = "\"" + dir + "\"";
                    Process.Start(psi);
                    lblPatch.Text = "Патчер запущен — выберите PATCH";
                    lblPatch.ForeColor = _p.warn;
                }
                catch
                {
                    lblPatch.Text = "Не удалось запустить патчер";
                    lblPatch.ForeColor = _p.err;
                }
            };

            btnSysProxy.Click += (s, e) =>
            {
                bool current = AppCore.TestSystemProxy();
                AppCore.SetSystemProxy(!current);
                UpdateSysProxyButton();
            };
        }

        private void SetProxyEnv()
        {
            string bypass = "localhost,127.0.0.1,::1," + Environment.MachineName;
            Environment.SetEnvironmentVariable("HTTP_PROXY", "http://127.0.0.1:10809");
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://127.0.0.1:10809");
            Environment.SetEnvironmentVariable("ALL_PROXY", "http://127.0.0.1:10809");
            Environment.SetEnvironmentVariable("NO_PROXY", bypass);
        }

        private void ApplyTheme()
        {
            BackColor = _p.bg;

            lblTitle.ForeColor = _p.ink;
            lblSub.ForeColor = _p.muted;
            lblStatus.ForeColor = _p.ink;
            lblDetail.ForeColor = _p.muted;
            lblPatch.ForeColor = _p.muted;
            lblApp.ForeColor = _p.muted;
            lblFoot.ForeColor = _p.muted;

            lblTitle.BackColor = Color.Transparent;
            lblSub.BackColor = Color.Transparent;
            lblStatus.BackColor = Color.Transparent;
            lblDetail.BackColor = Color.Transparent;
            lblPatch.BackColor = Color.Transparent;
            lblApp.BackColor = Color.Transparent;
            lblFoot.BackColor = Color.Transparent;

            card1.BackColor = _p.card;
            card2.BackColor = _p.card;

            UiButton[] secButtons = { btnSettings, btnTheme, btnStatus, btnSpeed, btnBrowser, btnOcGui, btnOcCli, btnPatch };
            foreach (var b in secButtons)
            {
                b.Fill = _p.secondaryFill;
                b.Hover = _p.secondaryHover;
                b.Pressed = _p.secondaryPressed;
                b.BorderColor = _p.secondaryBorder;
                b.HoverBorderColor = _p.secondaryHoverBorder;
                b.BorderWidth = 1;
                b.ForeColor = _p.secondaryText;
            }

            btnStart.Fill = _p.accent;
            btnStart.Hover = _p.accentHover;
            btnStart.Pressed = _p.accentPressed;
            btnStart.BorderWidth = 0;
            btnStart.ForeColor = Color.White;

            btnTheme.Text = (_settings.theme == "dark") ? "☀ Светлая" : "🌙 Тёмная";
            btnTheme.ForeColor = _p.ink;

            if (cbActiveProxy != null)
            {
                cbActiveProxy.BackColor = _p.secondaryFill;
                cbActiveProxy.ForeColor = _p.ink;
                cbActiveProxy.ItemBackColor = _p.secondaryFill;
                cbActiveProxy.ItemForeColor = _p.ink;
                cbActiveProxy.SelectedItemBackColor = _p.accent;
                cbActiveProxy.SelectedItemForeColor = Color.White;
                cbActiveProxy.Invalidate();
            }

            UpdateSysProxyButton();
            Invalidate(true);
        }

        private void UpdateSysProxyButton()
        {
            bool on = AppCore.TestSystemProxy();
            btnSysProxy.Text = on ? "✓ Системный прокси: вкл" : "✕ Системный прокси: выкл";
            if (on)
            {
                btnSysProxy.Fill = _p.accent;
                btnSysProxy.Hover = _p.accentHover;
                btnSysProxy.Pressed = _p.accentPressed;
                btnSysProxy.BorderWidth = 0;
                btnSysProxy.ForeColor = Color.White;
            }
            else
            {
                btnSysProxy.Fill = _p.secondaryFill;
                btnSysProxy.Hover = _p.secondaryHover;
                btnSysProxy.Pressed = _p.secondaryPressed;
                btnSysProxy.BorderColor = _p.secondaryBorder;
                btnSysProxy.HoverBorderColor = _p.secondaryHoverBorder;
                btnSysProxy.BorderWidth = 1;
                btnSysProxy.ForeColor = _p.secondaryText;
            }
        }

        private ProxyItem GetActiveProxy()
        {
            if (_settings.proxies == null || _settings.proxies.Count == 0) return null;
            int idx = _settings.activeProxyIndex;
            if (idx < 0 || idx >= _settings.proxies.Count) idx = 0;
            return _settings.proxies[idx];
        }

        private void RefreshProxyComboBox()
        {
            if (cbActiveProxy == null) return;
            _updatingProxyCb = true;
            try
            {
                cbActiveProxy.Items.Clear();
                for (int i = 0; i < _settings.proxies.Count; i++)
                {
                    var px = _settings.proxies[i];
                    string n = !string.IsNullOrEmpty(px.name) ? px.name : ("Прокси " + (i + 1));
                    string h = !string.IsNullOrEmpty(px.host) ? string.Format("{0}:{1}", px.host, px.port) : "не настроен";
                    cbActiveProxy.Items.Add(string.Format("{0} ({1})", n, h));
                }
                int idx = _settings.activeProxyIndex;
                if (idx >= 0 && idx < _settings.proxies.Count)
                    cbActiveProxy.SelectedIndex = idx;
                else if (_settings.proxies.Count > 0)
                    cbActiveProxy.SelectedIndex = 0;
            }
            finally
            {
                _updatingProxyCb = false;
            }
        }

        private void EnsureActiveProxySynced()
        {
            try
            {
                var active = GetActiveProxy();
                if (active == null || string.IsNullOrEmpty(active.host)) return;

                // Check if Xray is currently listening
                bool portOk = AppCore.TestPort(10809);
                var cur = AppCore.GetCurrentXrayServer();

                // ONLY restart Xray if port is NOT open OR config does not match active proxy
                bool match = (cur != null && cur.host == active.host && cur.port == active.port);
                if (!portOk || !match)
                {
                    AppCore.WriteXrayConfig(active.host, active.port, active.user, active.pass);
                    AppCore.RestartXray();
                }
            }
            catch (Exception ex)
            {
                AppCore.Log("EnsureActiveProxySynced: " + ex.Message);
            }
        }

        private async void SwitchActiveProxy(int newIdx)
        {
            if (newIdx < 0 || newIdx >= _settings.proxies.Count) return;
            _settings.activeProxyIndex = newIdx;
            AppCore.SaveSettings(_settings);

            var p = _settings.proxies[newIdx];
            if (p != null && !string.IsNullOrEmpty(p.host))
            {
                lblStatus.Text = "Переключение прокси…";
                lblStatus.ForeColor = _p.warn;
                Update();

                await Task.Run(() =>
                {
                    AppCore.WriteXrayConfig(p.host, p.port, p.user, p.pass);
                    AppCore.RestartXray();
                });

                UpdateStatusAsync();
            }
        }

        private async void UpdateStatusAsync()
        {
            var active = GetActiveProxy();
            string proxyName = (active != null && !string.IsNullOrEmpty(active.name)) ? (active.name + " · ") : "";
            string where = (active != null && !string.IsNullOrEmpty(active.host)) ? string.Format("{0}:{1}", active.host, active.port) : "—";

            if (!AppCore.TestPort(10809))
            {
                lblStatus.Text = "Прокси не запущен";
                lblStatus.ForeColor = _p.err;
                lblDetail.Text = "Откройте «Настройки», проверьте адрес прокси и нажмите «Сохранить».";
                return;
            }

            lblStatus.Text = "Проверка статуса…";
            lblStatus.ForeColor = _p.warn;

            var res = await Task.Run(() => AppCore.CheckDirectProxyInfo("127.0.0.1", "10809", "", ""));
            if (res.Ok)
            {
                lblStatus.Text = "Прокси работает";
                lblStatus.ForeColor = _p.ok;
                lblDetail.Text = string.Format("Выход: {0} · {1}, {2} · пинг {3} мс\r\nПрокси: {4}{5}", res.Ip, res.Country, res.City, res.PingMs, proxyName, where);
            }
            else
            {
                lblStatus.Text = "Прокси работает, проверка не удалась";
                lblStatus.ForeColor = _p.warn;
                lblDetail.Text = string.Format("Прокси: {0}{1}", proxyName, where);
            }
        }

        private void UpdatePatch()
        {
            string app = AppCore.FindAntigravityApp();
            if (!string.IsNullOrEmpty(app))
            {
                lblApp.Text = "Antigravity:\r\n" + app;
                lblApp.ForeColor = _p.muted;
                string ls = AppCore.FindLanguageServer(app);
                var patched = AppCore.TestPatched(ls);
                if (patched == true) { lblPatch.Text = "Патч применён"; lblPatch.ForeColor = _p.ok; }
                else if (patched == false) { lblPatch.Text = "Патч не применён"; lblPatch.ForeColor = _p.err; }
                else { lblPatch.Text = "Патч: не удалось проверить"; lblPatch.ForeColor = _p.muted; }
            }
            else
            {
                lblApp.Text = "Antigravity не найден";
                lblApp.ForeColor = _p.err;
                lblPatch.Text = "language_server не найден";
                lblPatch.ForeColor = _p.muted;
            }
        }

        private void InitTray()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = (Icon != null) ? Icon : SystemIcons.Application,
                Text = "Antigravity Proxy",
                Visible = true
            };

            var trayMenu = new ContextMenuStrip();
            var miOpen = trayMenu.Items.Add("Открыть");
            var miSettings = trayMenu.Items.Add("Настройки");
            trayMenu.Items.Add(new ToolStripSeparator());
            var miExit = trayMenu.Items.Add("Выход");
            notifyIcon.ContextMenuStrip = trayMenu;

            Action showForm = () =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            };

            notifyIcon.DoubleClick += (s, e) => showForm();
            miOpen.Click += (s, e) => showForm();
            miSettings.Click += (s, e) =>
            {
                showForm();
                btnSettings.PerformClick();
            };
            miExit.Click += (s, e) =>
            {
                _isExiting = true;
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                Application.Exit();
            };
        }

        [STAThread]
        public static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                AppCore.Log("UI ThreadException: " + e.Exception);
                MessageBox.Show("Ошибка: " + e.Exception.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                AppCore.Log("AppDomain UnhandledException: " + e.ExceptionObject);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
