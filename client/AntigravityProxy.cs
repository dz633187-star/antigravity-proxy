using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityProxyApp
{
    public class ProxyItem
    {
        public string name { get; set; }
        public string protocol { get; set; }
        public string host { get; set; }
        public string port { get; set; }
        public string user { get; set; }
        public string pass { get; set; }

        public ProxyItem()
        {
            name = "Прокси";
            protocol = "http";
            host = "";
            port = "8000";
            user = "";
            pass = "";
        }

        public string NormalizedProtocol()
        {
            if (string.IsNullOrEmpty(protocol)) return "http";
            string p = protocol.Trim().ToLowerInvariant();
            return (p == "socks" || p == "socks5") ? "socks" : "http";
        }

        public ProxyItem Clone()
        {
            return new ProxyItem
            {
                name = this.name,
                protocol = this.NormalizedProtocol(),
                host = this.host,
                port = this.port,
                user = this.user,
                pass = this.pass
            };
        }
    }

    public class CustomAppItem
    {
        public string name { get; set; }
        public string path { get; set; }
        public string args { get; set; }
        public bool chromiumFlag { get; set; }

        public CustomAppItem()
        {
            name = "";
            path = "";
            args = "";
            chromiumFlag = false;
        }
    }

    public class AppSettings
    {
        public string theme { get; set; }
        public int activeProxyIndex { get; set; }
        public List<ProxyItem> proxies { get; set; }
        public List<CustomAppItem> customApps { get; set; }
        public bool? autoCheckUpdates { get; set; }

        public AppSettings()
        {
            theme = "dark";
            activeProxyIndex = 0;
            proxies = new List<ProxyItem>();
            customApps = new List<CustomAppItem>();
            autoCheckUpdates = true;
        }
    }

    public class Palette
    {
        public Color bg, card, elevated, ink, muted, border;
        public Color accent, accentHover, accentPressed;
        public Color ok, warn, err;
        public Color secondaryFill, secondaryHover, secondaryPressed, secondaryBorder, secondaryHoverBorder, secondaryText;
        public Color inputBg, disabledFill, disabledText;

        public static Palette Get(string theme)
        {
            if (theme == "dark")
            {
                // Obsidian Signal
                return new Palette
                {
                    bg = Color.FromArgb(15, 17, 21),
                    card = Color.FromArgb(23, 26, 33),
                    elevated = Color.FromArgb(30, 35, 45),
                    ink = Color.FromArgb(243, 244, 246),
                    muted = Color.FromArgb(139, 149, 165),
                    border = Color.FromArgb(38, 44, 56),
                    accent = Color.FromArgb(13, 148, 136),
                    accentHover = Color.FromArgb(20, 184, 166),
                    accentPressed = Color.FromArgb(15, 118, 110),
                    ok = Color.FromArgb(34, 197, 94),
                    warn = Color.FromArgb(245, 158, 11),
                    err = Color.FromArgb(239, 68, 68),
                    secondaryFill = Color.FromArgb(31, 36, 46),
                    secondaryHover = Color.FromArgb(42, 49, 62),
                    secondaryPressed = Color.FromArgb(24, 28, 36),
                    secondaryBorder = Color.FromArgb(50, 58, 74),
                    secondaryHoverBorder = Color.FromArgb(88, 102, 126),
                    secondaryText = Color.FromArgb(229, 233, 240),
                    inputBg = Color.FromArgb(19, 22, 29),
                    disabledFill = Color.FromArgb(27, 31, 39),
                    disabledText = Color.FromArgb(95, 104, 120)
                };
            }
            // Drafting Paper
            return new Palette
            {
                bg = Color.FromArgb(243, 245, 248),
                card = Color.White,
                elevated = Color.FromArgb(233, 237, 243),
                ink = Color.FromArgb(17, 24, 39),
                muted = Color.FromArgb(92, 102, 118),
                border = Color.FromArgb(214, 221, 232),
                accent = Color.FromArgb(15, 118, 110),
                accentHover = Color.FromArgb(13, 148, 136),
                accentPressed = Color.FromArgb(17, 94, 89),
                ok = Color.FromArgb(22, 163, 74),
                warn = Color.FromArgb(217, 119, 6),
                err = Color.FromArgb(220, 38, 38),
                secondaryFill = Color.FromArgb(248, 250, 252),
                secondaryHover = Color.FromArgb(238, 242, 247),
                secondaryPressed = Color.FromArgb(226, 232, 240),
                secondaryBorder = Color.FromArgb(203, 213, 225),
                secondaryHoverBorder = Color.FromArgb(148, 163, 184),
                secondaryText = Color.FromArgb(30, 41, 59),
                inputBg = Color.White,
                disabledFill = Color.FromArgb(241, 245, 249),
                disabledText = Color.FromArgb(148, 163, 184)
            };
        }
    }

    public static class UiDraw
    {
        public static GraphicsPath RoundRect(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            if (r <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = Math.Min(r * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class UiCard : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        public UiCard()
        {
            Radius = 10;
            FillColor = Color.FromArgb(23, 26, 33);
            BorderColor = Color.FromArgb(38, 44, 56);
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = (Parent != null) ? Parent.BackColor : BackColor;
            using (var pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundRect(rect, Radius))
            {
                using (var fb = new SolidBrush(FillColor))
                {
                    g.FillPath(fb, path);
                }
                if (BorderColor.A > 0)
                {
                    using (var pen = new Pen(BorderColor, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }
    }

    public class UiStatusDot : Control
    {
        private Color _dotColor = Color.Gray;
        public Color DotColor
        {
            get { return _dotColor; }
            set { _dotColor = value; Invalidate(); }
        }

        public UiStatusDot()
        {
            Size = new Size(14, 14);
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = (Parent != null) ? Parent.BackColor : Color.Transparent;
            if (Parent is UiCard) bg = ((UiCard)Parent).FillColor;
            using (var b = new SolidBrush(bg))
            {
                g.FillRectangle(b, ClientRectangle);
            }
            int cx = Width / 2;
            int cy = Height / 2;
            using (var halo = new SolidBrush(Color.FromArgb(48, _dotColor)))
            {
                g.FillEllipse(halo, cx - 6, cy - 6, 12, 12);
            }
            using (var core = new SolidBrush(_dotColor))
            {
                g.FillEllipse(core, cx - 3.5f, cy - 3.5f, 7f, 7f);
            }
        }
    }

    public class UiButton : Button
    {
        public int Radius { get; set; }
        private Color _fill = Color.FromArgb(13, 148, 136);
        public Color Fill { get { return _fill; } set { _fill = value; Invalidate(); } }
        private Color _hover = Color.FromArgb(20, 184, 166);
        public Color Hover { get { return _hover; } set { _hover = value; Invalidate(); } }
        private Color _pressed = Color.FromArgb(15, 118, 110);
        public Color Pressed { get { return _pressed; } set { _pressed = value; Invalidate(); } }
        private Color _border = Color.FromArgb(50, 58, 74);
        public Color BorderColor { get { return _border; } set { _border = value; Invalidate(); } }
        private Color _hoverBorder = Color.FromArgb(88, 102, 126);
        public Color HoverBorderColor { get { return _hoverBorder; } set { _hoverBorder = value; Invalidate(); } }
        public Color DisabledFill { get; set; }
        public Color DisabledText { get; set; }
        public Color DotColor { get; set; }
        public int BorderWidth { get; set; }

        private bool _isHover, _isDown;

        public UiButton()
        {
            Radius = 7;
            BorderWidth = 1;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            DisabledFill = Color.FromArgb(27, 31, 39);
            DisabledText = Color.FromArgb(95, 104, 120);
            DotColor = Color.Transparent;
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

            Color parentBg = (Parent != null) ? Parent.BackColor : BackColor;
            if (Parent is UiCard) parentBg = ((UiCard)Parent).FillColor;
            using (var pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, ClientRectangle);
            }

            Color bg = !Enabled ? DisabledFill : (_isDown ? _pressed : (_isHover ? _hover : _fill));
            Color bc = !Enabled ? _border : (_isHover ? _hoverBorder : _border);
            Color fg = !Enabled ? DisabledText : ForeColor;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundRect(rect, Radius))
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

            if (DotColor.A > 0)
            {
                int dotX = 12;
                int dotY = (Height - 8) / 2;
                using (var db = new SolidBrush(DotColor))
                {
                    g.FillEllipse(db, dotX, dotY, 8, 8);
                }
                var textRect = new Rectangle(24, 0, Width - 30, Height);
                TextRenderer.DrawText(g, Text, Font, textRect, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
            }
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
            ItemBackColor = Color.FromArgb(31, 36, 46);
            ItemForeColor = Color.FromArgb(243, 244, 246);
            SelectedItemBackColor = Color.FromArgb(13, 148, 136);
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
            var rect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Font, rect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    public class UiListBox : ListBox
    {
        public Color ItemBackColor { get; set; }
        public Color ItemForeColor { get; set; }
        public Color SelectedItemBackColor { get; set; }
        public Color SelectedItemForeColor { get; set; }
        public Color SubTextColor { get; set; }

        public UiListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            ItemHeight = 40;
            ItemBackColor = Color.FromArgb(23, 26, 33);
            ItemForeColor = Color.FromArgb(243, 244, 246);
            SelectedItemBackColor = Color.FromArgb(30, 48, 52);
            SelectedItemForeColor = Color.White;
            SubTextColor = Color.FromArgb(139, 149, 165);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) != 0;
            Color bg = isSelected ? SelectedItemBackColor : ItemBackColor;
            Color fg = isSelected ? SelectedItemForeColor : ItemForeColor;
            Color subFg = isSelected ? Color.FromArgb(210, 235, 232) : SubTextColor;

            using (var b = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            string raw = Items[e.Index] != null ? Items[e.Index].ToString() : "";
            string[] parts = raw.Split(new[] { '\n' }, 2);
            string title = parts[0];
            string sub = parts.Length > 1 ? parts[1] : "";

            var rTitle = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 4, Math.Max(0, e.Bounds.Width - 16), 18);
            var rSub = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 21, Math.Max(0, e.Bounds.Width - 16), 15);

            using (var fTitle = new Font("Segoe UI Semibold", 9f))
            using (var fSub = new Font("Segoe UI", 8f))
            {
                TextRenderer.DrawText(e.Graphics, title, fTitle, rTitle, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                if (!string.IsNullOrEmpty(sub))
                {
                    TextRenderer.DrawText(e.Graphics, sub, fSub, rSub, subFg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
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
        public string Note { get; set; }
        public string Error { get; set; }
    }

    public class ServiceSnapshot
    {
        public bool XrayInstalled { get; set; }
        public bool XrayHttpUp { get; set; }
        public bool XraySocksUp { get; set; }
        public int XrayPid { get; set; }
        public bool SysProxyOn { get; set; }
        public string SysProxyServer { get; set; }
        public bool AgdnsUp { get; set; }
        public bool? PatchApplied { get; set; }
        public string AntigravityPath { get; set; }
        public bool AntigravityRunning { get; set; }
        public bool LanguageServerRunning { get; set; }
        public bool TelegramRunning { get; set; }
        public bool OpenCodeRunning { get; set; }
        public bool AgUnlockerConflict { get; set; }
    }

    public class UpdateInfo
    {
        public bool Available { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string Notes { get; set; }
        public string Error { get; set; }
    }

    public class AppCore
    {
        public const string AppVersion = "1.3.0";
        private const string RepoOwner = "dz633187-star";
        private const string RepoName = "antigravity-proxy";

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public static string AppRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityProxy");
        public static string XrayDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XrayNL");
        public static string XrayCfg = Path.Combine(XrayDir, "config.json");
        public static string XrayExe = Path.Combine(XrayDir, "xray.exe");
        public static string SettingsFile = Path.Combine(AppRoot, "settings.json");
        public static string BrowserProfile = Path.Combine(AppRoot, "BrowserProfile");
        public static string LogFile = Path.Combine(Path.GetTempPath(), "antigravity-proxy.log");
        public static string LastXrayError = "";

        public static string FindXrayExe()
        {
            string[] candidates = {
                XrayExe,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xray.exe"),
                Path.Combine(AppRoot, "xray.exe")
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            }
            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in pathEnv.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string p = Path.Combine(dir.Trim(), "xray.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return "";
        }

        public static bool DownloadXray(ProxyItem fallbackProxy, Action<string> onProgress)
        {
            const string url = "https://github.com/XTLS/Xray-core/releases/latest/download/Xray-windows-64.zip";
            string zipPath = Path.Combine(Path.GetTempPath(), "xray-windows-64-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                if (!Directory.Exists(XrayDir)) Directory.CreateDirectory(XrayDir);
                if (onProgress != null) onProgress("Скачиваю Xray-core с GitHub…");

                bool downloaded = false;
                string lastErr = "";

                // 1. Попытка напрямую
                try
                {
                    DownloadFile(url, zipPath, null);
                    downloaded = true;
                }
                catch (Exception ex1)
                {
                    lastErr = ex1.Message;
                    Log("DownloadXray direct failed: " + ex1.Message);
                }

                // 2. Попытка через настроенный HTTP-прокси, если GitHub недоступен напрямую
                if (!downloaded && fallbackProxy != null && !string.IsNullOrEmpty(fallbackProxy.host) && fallbackProxy.NormalizedProtocol() == "http")
                {
                    try
                    {
                        if (onProgress != null) onProgress("Прямая загрузка не удалась, пробую через прокси…");
                        int pt = 8000;
                        int.TryParse(fallbackProxy.port, out pt);
                        var wp = new WebProxy(fallbackProxy.host, pt);
                        if (!string.IsNullOrEmpty(fallbackProxy.user))
                            wp.Credentials = new NetworkCredential(fallbackProxy.user, fallbackProxy.pass ?? "");
                        DownloadFile(url, zipPath, wp);
                        downloaded = true;
                    }
                    catch (Exception ex2)
                    {
                        lastErr = ex2.Message;
                        Log("DownloadXray proxy failed: " + ex2.Message);
                    }
                }

                if (!downloaded)
                {
                    LastXrayError = "Не удалось скачать Xray-core: " + lastErr;
                    return false;
                }

                if (onProgress != null) onProgress("Распаковываю xray.exe…");
                bool extracted = false;
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.Equals("xray.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            string dst = XrayExe;
                            entry.ExtractToFile(dst, true);
                            extracted = true;
                            break;
                        }
                    }
                }

                if (!extracted || !File.Exists(XrayExe))
                {
                    LastXrayError = "В архиве не найден xray.exe";
                    return false;
                }

                LastXrayError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastXrayError = "Ошибка установки Xray-core: " + ex.Message;
                Log("DownloadXray: " + ex.ToString());
                return false;
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        private static void DownloadFile(string url, string destinationPath, IWebProxy proxy)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "AntigravityProxy/1.3";
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            if (proxy != null) req.Proxy = proxy;

            using (var resp = req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(destinationPath))
            {
                src.CopyTo(dst);
            }
        }

        private static string FetchText(string url, IWebProxy proxy, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "AntigravityProxy/" + AppVersion;
            req.Accept = "application/vnd.github+json, application/json";
            req.AllowAutoRedirect = true;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            if (proxy != null) req.Proxy = proxy;

            using (var resp = req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string FetchTextWithFallback(string url, int timeoutMs = 8000)
        {
            try
            {
                return FetchText(url, null, timeoutMs);
            }
            catch (Exception ex1)
            {
                Log("FetchText direct (" + url + "): " + ex1.Message);
                if (TestPort(10809, 250))
                {
                    return FetchText(url, new WebProxy("127.0.0.1", 10809), timeoutMs);
                }
                throw;
            }
        }

        private static Version ParseVersionSafe(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new Version(0, 0, 0);
            string clean = raw.Trim().TrimStart('v', 'V');
            int dash = clean.IndexOf('-');
            if (dash > 0) clean = clean.Substring(0, dash);
            Version v;
            if (Version.TryParse(clean, out v)) return v;
            var m = Regex.Match(clean, @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (m.Success)
            {
                int maj = int.Parse(m.Groups[1].Value);
                int min = int.Parse(m.Groups[2].Value);
                int bld = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
                return new Version(maj, min, bld);
            }
            return new Version(0, 0, 0);
        }

        public static UpdateInfo CheckForUpdates()
        {
            var current = ParseVersionSafe(AppVersion);
            var info = new UpdateInfo
            {
                Available = false,
                CurrentVersion = AppVersion,
                LatestVersion = AppVersion,
                DownloadUrl = "",
                Notes = ""
            };

            string bestVerStr = AppVersion;
            Version bestVer = current;
            string bestUrl = "";
            string bestNotes = "";
            string lastErr = "";
            bool anySuccess = false;

            // 1. Проверяем GitHub Releases API
            try
            {
                string apiUrl = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest", RepoOwner, RepoName);
                string json = FetchTextWithFallback(apiUrl, 8000);
                var ser = new JavaScriptSerializer();
                var d = ser.Deserialize<Dictionary<string, object>>(json);
                if (d != null && d.ContainsKey("tag_name") && d["tag_name"] != null)
                {
                    anySuccess = true;
                    string tag = d["tag_name"].ToString();
                    Version v = ParseVersionSafe(tag);
                    string dl = "";
                    if (d.ContainsKey("assets") && d["assets"] is object[])
                    {
                        foreach (Dictionary<string, object> asset in (object[])d["assets"])
                        {
                            string aname = (asset.ContainsKey("name") && asset["name"] != null) ? asset["name"].ToString() : "";
                            if (aname.Equals("AntigravityProxy.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                dl = (asset.ContainsKey("browser_download_url") && asset["browser_download_url"] != null) ? asset["browser_download_url"].ToString() : "";
                                break;
                            }
                        }
                    }
                    if (v > bestVer && !string.IsNullOrEmpty(dl))
                    {
                        bestVer = v;
                        bestVerStr = tag.TrimStart('v', 'V');
                        bestUrl = dl;
                        bestNotes = (d.ContainsKey("body") && d["body"] != null) ? d["body"].ToString() : "";
                    }
                }
            }
            catch (Exception ex)
            {
                lastErr = ex.Message;
                Log("CheckForUpdates releases: " + ex.Message);
            }

            // 2. Проверяем client/version.json в ветке main (подхватывает обновления даже без нового тега)
            try
            {
                string verUrl = string.Format("https://raw.githubusercontent.com/{0}/{1}/main/client/version.json?t={2}", RepoOwner, RepoName, DateTime.UtcNow.Ticks);
                string json = FetchTextWithFallback(verUrl, 8000);
                var ser = new JavaScriptSerializer();
                var d = ser.Deserialize<Dictionary<string, object>>(json);
                if (d != null && d.ContainsKey("version") && d["version"] != null)
                {
                    anySuccess = true;
                    string vStr = d["version"].ToString().Trim().TrimStart('v', 'V');
                    Version v = ParseVersionSafe(vStr);
                    string dl = (d.ContainsKey("downloadUrl") && d["downloadUrl"] != null)
                        ? d["downloadUrl"].ToString()
                        : string.Format("https://raw.githubusercontent.com/{0}/{1}/main/client/AntigravityProxy.exe", RepoOwner, RepoName);
                    if (v > bestVer)
                    {
                        bestVer = v;
                        bestVerStr = vStr;
                        bestUrl = dl;
                        bestNotes = (d.ContainsKey("notes") && d["notes"] != null) ? d["notes"].ToString() : "";
                    }
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(lastErr)) lastErr = ex.Message;
                Log("CheckForUpdates version.json: " + ex.Message);
            }

            if (!anySuccess)
            {
                info.Error = "Не удалось проверить обновления: " + lastErr;
                return info;
            }

            info.LatestVersion = bestVerStr;
            if (bestVer > current && !string.IsNullOrEmpty(bestUrl))
            {
                info.Available = true;
                info.DownloadUrl = bestUrl;
                info.Notes = bestNotes;
            }
            return info;
        }

        public static bool DownloadAndApplyUpdate(string downloadUrl, out string error)
        {
            error = "";
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(currentExe);
            string stagedExe = Path.Combine(dir, "AntigravityProxy.exe.new");

            try
            {
                bool downloaded = false;
                string dlErr = "";
                try
                {
                    DownloadFile(downloadUrl, stagedExe, null);
                    downloaded = true;
                }
                catch (Exception ex1)
                {
                    dlErr = ex1.Message;
                    if (TestPort(10809, 250))
                    {
                        DownloadFile(downloadUrl, stagedExe, new WebProxy("127.0.0.1", 10809));
                        downloaded = true;
                    }
                }

                if (!downloaded || !File.Exists(stagedExe))
                {
                    error = "Не удалось скачать обновление: " + dlErr;
                    return false;
                }

                // Проверяем валидность PE-файла (MZ + минимальный размер)
                var fi = new FileInfo(stagedExe);
                if (fi.Length < 30000)
                {
                    try { File.Delete(stagedExe); } catch { }
                    error = "Скачанный файл повреждён (слишком малый размер).";
                    return false;
                }
                byte[] header = new byte[2];
                using (var fs = File.OpenRead(stagedExe))
                {
                    if (fs.Read(header, 0, 2) < 2 || header[0] != 0x4D || header[1] != 0x5A)
                    {
                        error = "Скачанный файл не является исполняемым файлом Windows.";
                        return false;
                    }
                }

                // Запускаем скрытый скрипт замены exe. Xray при этом продолжает работать без обрыва интернета!
                string cmdArgs = string.Format(
                    "/c ping 127.0.0.1 -n 3 > nul & move /y \"{0}\" \"{1}\" & start \"\" \"{1}\"",
                    stagedExe, currentExe);

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(stagedExe)) File.Delete(stagedExe); } catch { }
                error = ex.Message;
                Log("DownloadAndApplyUpdate: " + ex.ToString());
                return false;
            }
        }

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
                    var loaded = ser.Deserialize<AppSettings>(json);
                    if (loaded != null) s = loaded;
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
                        new ProxyItem { name = "Мой прокси", protocol = "http", host = "", port = "8000", user = "", pass = "" }
                    };
                }
            }

            foreach (var px in s.proxies)
            {
                if (px != null) px.protocol = px.NormalizedProtocol();
            }

            if (s.customApps == null) s.customApps = new List<CustomAppItem>();

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
                File.WriteAllText(SettingsFile, json, new UTF8Encoding(false));
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
                if (dict == null || !dict.ContainsKey("outbounds")) return null;
                var outbounds = dict["outbounds"] as object[];
                if (outbounds == null) return null;
                foreach (Dictionary<string, object> ob in outbounds)
                {
                    if (!ob.ContainsKey("protocol") || !ob.ContainsKey("settings")) continue;
                    string proto = (ob["protocol"] ?? "").ToString().ToLowerInvariant();
                    if (proto != "http" && proto != "socks") continue;

                    var settings = ob["settings"] as Dictionary<string, object>;
                    if (settings != null && settings.ContainsKey("servers"))
                    {
                        var servers = settings["servers"] as object[];
                        if (servers != null && servers.Length > 0)
                        {
                            var srv = servers[0] as Dictionary<string, object>;
                            string u = "", p = "";
                            if (srv != null && srv.ContainsKey("users"))
                            {
                                var users = srv["users"] as object[];
                                if (users != null && users.Length > 0)
                                {
                                    var usr = users[0] as Dictionary<string, object>;
                                    if (usr != null)
                                    {
                                        if (usr.ContainsKey("user") && usr["user"] != null) u = usr["user"].ToString();
                                        if (usr.ContainsKey("pass") && usr["pass"] != null) p = usr["pass"].ToString();
                                    }
                                }
                            }
                            return new ProxyItem
                            {
                                name = "Основной",
                                protocol = proto,
                                host = (srv != null && srv.ContainsKey("address") && srv["address"] != null) ? srv["address"].ToString() : "",
                                port = (srv != null && srv.ContainsKey("port") && srv["port"] != null) ? srv["port"].ToString() : "8000",
                                user = u,
                                pass = p
                            };
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void WriteXrayConfig(string host, string port, string user, string pass, string protocol = "http")
        {
            int pt = 8000;
            int.TryParse(port, out pt);
            string proto = (!string.IsNullOrEmpty(protocol) && protocol.Trim().ToLowerInvariant().StartsWith("socks")) ? "socks" : "http";

            var server = new Dictionary<string, object>
            {
                { "address", host },
                { "port", pt }
            };

            if (!string.IsNullOrEmpty(user))
            {
                server["users"] = new object[]
                {
                    new Dictionary<string, object> { { "user", user }, { "pass", pass ?? "" } }
                };
            }

            var outbounds = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "tag", "proxy" },
                    { "protocol", proto },
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

        public static bool TestPort(int port = 10809, int timeoutMs = 400)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect("127.0.0.1", port, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                    bool conn = ok && client.Connected;
                    return conn;
                }
            }
            catch { return false; }
        }

        public static bool RestartXray()
        {
            LastXrayError = "";
            try
            {
                string exe = FindXrayExe();
                if (string.IsNullOrEmpty(exe))
                {
                    LastXrayError = "Не найден xray.exe (нажмите «Скачать Xray»)";
                    Log("RestartXray: xray.exe not found");
                    return false;
                }

                foreach (var p in Process.GetProcessesByName("xray"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
                System.Threading.Thread.Sleep(350);

                if (!File.Exists(XrayCfg))
                {
                    LastXrayError = "Не создан конфиг Xray (укажите прокси в Настройках)";
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "run -c \"" + XrayCfg + "\"",
                    WorkingDirectory = Path.GetDirectoryName(exe),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };
                var proc = Process.Start(psi);

                for (int i = 0; i < 20; i++)
                {
                    System.Threading.Thread.Sleep(250);
                    if (TestPort(10809)) return true;
                    if (proc != null && proc.HasExited)
                    {
                        string errText = "";
                        try { errText = proc.StandardError.ReadToEnd().Trim(); } catch { }
                        if (errText.Length > 180) errText = errText.Substring(0, 180);
                        LastXrayError = string.IsNullOrEmpty(errText)
                            ? ("Xray завершился с кодом " + proc.ExitCode)
                            : errText;
                        Log("RestartXray exited early: " + LastXrayError);
                        return false;
                    }
                }
                LastXrayError = "Xray запущен, но порт 10809 не открылся";
            }
            catch (Exception ex)
            {
                LastXrayError = ex.Message;
                Log("RestartXray: " + ex.Message);
            }
            return false;
        }

        public static void StartWithProxyEnv(string fileName, string arguments = "", string workingDir = null)
        {
            string bypass = "localhost,127.0.0.1,::1," + Environment.MachineName;
            string httpProxy = "http://127.0.0.1:10809";

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                UseShellExecute = false
            };

            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }
            else
            {
                try
                {
                    string dir = Path.GetDirectoryName(fileName);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        psi.WorkingDirectory = dir;
                }
                catch { }
            }

            psi.EnvironmentVariables["AG_LS_PROXY"] = httpProxy;
            psi.EnvironmentVariables["HTTP_PROXY"] = httpProxy;
            psi.EnvironmentVariables["HTTPS_PROXY"] = httpProxy;
            psi.EnvironmentVariables["ALL_PROXY"] = httpProxy;
            psi.EnvironmentVariables["NO_PROXY"] = bypass;
            psi.EnvironmentVariables["http_proxy"] = httpProxy;
            psi.EnvironmentVariables["https_proxy"] = httpProxy;
            psi.EnvironmentVariables["all_proxy"] = httpProxy;
            psi.EnvironmentVariables["no_proxy"] = bypass;

            Process.Start(psi);
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
                return new DirectInfoResult { Ok = false, Error = "Пустой ответ сервера" };
            }
        }

        public static DirectInfoResult CheckSocks5Direct(string host, string port, string user, string pass)
        {
            try
            {
                int pt = 1080;
                int.TryParse(port, out pt);
                var sw = Stopwatch.StartNew();
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect(host, pt, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(7000) || !client.Connected)
                    {
                        return new DirectInfoResult { Ok = false, Error = "Таймаут TCP-подключения к SOCKS5" };
                    }
                    client.EndConnect(ar);
                    client.ReceiveTimeout = 7000;
                    client.SendTimeout = 7000;

                    using (var ns = client.GetStream())
                    {
                        bool hasAuth = !string.IsNullOrEmpty(user);
                        byte[] greeting = hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
                        ns.Write(greeting, 0, greeting.Length);

                        byte[] resp = new byte[2];
                        int r = ns.Read(resp, 0, 2);
                        if (r < 2 || resp[0] != 0x05)
                            return new DirectInfoResult { Ok = false, Error = "Сервер не отвечает по SOCKS5" };

                        if (resp[1] == 0xFF)
                            return new DirectInfoResult { Ok = false, Error = "SOCKS5 отклонил метод авторизации" };

                        if (resp[1] == 0x02)
                        {
                            if (!hasAuth)
                                return new DirectInfoResult { Ok = false, Error = "SOCKS5 требует логин и пароль" };

                            byte[] uBytes = Encoding.UTF8.GetBytes(user);
                            byte[] pBytes = Encoding.UTF8.GetBytes(pass ?? "");
                            if (uBytes.Length > 255 || pBytes.Length > 255)
                                return new DirectInfoResult { Ok = false, Error = "Слишком длинный логин или пароль" };

                            byte[] auth = new byte[3 + uBytes.Length + pBytes.Length];
                            auth[0] = 0x01;
                            auth[1] = (byte)uBytes.Length;
                            Buffer.BlockCopy(uBytes, 0, auth, 2, uBytes.Length);
                            auth[2 + uBytes.Length] = (byte)pBytes.Length;
                            Buffer.BlockCopy(pBytes, 0, auth, 3 + uBytes.Length, pBytes.Length);
                            ns.Write(auth, 0, auth.Length);

                            byte[] authResp = new byte[2];
                            int arRead = ns.Read(authResp, 0, 2);
                            if (arRead < 2 || authResp[1] != 0x00)
                                return new DirectInfoResult { Ok = false, Error = "Неверный логин или пароль SOCKS5" };
                        }

                        sw.Stop();
                        return new DirectInfoResult
                        {
                            Ok = true,
                            Ip = host,
                            Note = "SOCKS5 рукопожатие успешно",
                            PingMs = (int)sw.ElapsedMilliseconds
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new DirectInfoResult { Ok = false, Error = msg };
            }
        }

        public static DirectInfoResult CheckDirectProxyInfo(string host, string port, string user, string pass, string protocol = "http")
        {
            if (!string.IsNullOrEmpty(protocol) && protocol.Trim().ToLowerInvariant().StartsWith("socks"))
            {
                return CheckSocks5Direct(host, port, user, pass);
            }

            try
            {
                int pt = 8000;
                int.TryParse(port, out pt);
                var proxy = new WebProxy(host, pt);
                if (!string.IsNullOrEmpty(user))
                {
                    proxy.Credentials = new NetworkCredential(user, pass ?? "");
                }

                try
                {
                    return QueryDirectProxyEndpoint("https://ipinfo.io/json", proxy, 9000);
                }
                catch (Exception ex1)
                {
                    Log("DirectInfo ipinfo.io: " + ex1.Message);
                    try
                    {
                        return QueryDirectProxyEndpoint("http://ip-api.com/json", proxy, 9000);
                    }
                    catch (Exception ex2)
                    {
                        Log("DirectInfo ip-api.com: " + ex2.Message);
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

        public static bool TestSystemProxy(out string serverOut)
        {
            serverOut = "";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key == null) return false;
                    var enable = key.GetValue("ProxyEnable");
                    var server = key.GetValue("ProxyServer") as string;
                    serverOut = server ?? "";
                    return (enable is int && (int)enable == 1) && (server != null && server.Contains("10809"));
                }
            }
            catch { return false; }
        }

        public static bool TestSystemProxy()
        {
            string dummy;
            return TestSystemProxy(out dummy);
        }

        public static bool IsAnySystemProxyEnabled(out string serverOut)
        {
            serverOut = "";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key == null) return false;
                    var enable = key.GetValue("ProxyEnable");
                    var server = key.GetValue("ProxyServer") as string;
                    serverOut = server ?? "";
                    return (enable is int && (int)enable == 1);
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
                // Мгновенно уведомляем WinINet и приложения об изменении настроек прокси
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch (Exception ex) { Log("SetSystemProxy: " + ex.Message); }
        }

        public static ServiceSnapshot CollectServices()
        {
            var snap = new ServiceSnapshot();
            snap.XrayInstalled = !string.IsNullOrEmpty(FindXrayExe());
            snap.XrayHttpUp = TestPort(10809, 250);
            snap.XraySocksUp = TestPort(10808, 250);
            snap.AgdnsUp = TestPort(53, 250);

            try
            {
                var xrays = Process.GetProcessesByName("xray");
                if (xrays.Length > 0) snap.XrayPid = xrays[0].Id;
            }
            catch { }

            string srv;
            snap.SysProxyOn = IsAnySystemProxyEnabled(out srv);
            snap.SysProxyServer = srv;

            snap.AntigravityPath = FindAntigravityApp();
            if (!string.IsNullOrEmpty(snap.AntigravityPath))
            {
                snap.PatchApplied = TestAntigravityPatched(snap.AntigravityPath);
            }

            try { snap.AntigravityRunning = Process.GetProcessesByName("Antigravity").Length > 0; } catch { }
            try
            {
                snap.LanguageServerRunning = Process.GetProcessesByName("language_server").Length > 0
                    || Process.GetProcessesByName("language_server_windows_x64").Length > 0;
            }
            catch { }
            try { snap.TelegramRunning = Process.GetProcessesByName("Telegram").Length > 0; } catch { }
            try { snap.OpenCodeRunning = Process.GetProcessesByName("OpenCode").Length > 0 || Process.GetProcessesByName("opencode").Length > 0; } catch { }
            try { snap.AgUnlockerConflict = Process.GetProcessesByName("ag_dns").Length > 0; } catch { }

            return snap;
        }

        public static string FindAntigravityApp()
        {
            // 1. Сначала проверим запущенный процесс Antigravity (если установлен в нестандартную папку)
            try
            {
                foreach (var p in Process.GetProcessesByName("Antigravity"))
                {
                    try
                    {
                        string path = p.MainModule.FileName;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                    }
                    catch { }
                }
            }
            catch { }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string prog = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates = {
                Path.Combine(local, @"Programs\antigravity\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity 2.0\Antigravity.exe"),
                Path.Combine(local, @"Programs\Antigravity IDE\Antigravity IDE.exe"),
                Path.Combine(local, @"Programs\Antigravity IDE\Antigravity.exe"),
                Path.Combine(prog, @"Antigravity\Antigravity.exe"),
                Path.Combine(prog, @"Antigravity IDE\Antigravity IDE.exe"),
                Path.Combine(prog86, @"Antigravity\Antigravity.exe")
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            }
            return "";
        }

        public static List<string> FindLanguageServers(string appPath)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(appPath)) return result;
            string dir = Path.GetDirectoryName(appPath);
            string resDir = Path.Combine(dir, "resources");
            if (Directory.Exists(resDir))
            {
                try
                {
                    var files = Directory.GetFiles(resDir, "language_server*.exe", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        if (!f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                            result.Add(f);
                    }
                }
                catch { }
            }
            return result;
        }

        public static string FindLanguageServer(string appPath)
        {
            var list = FindLanguageServers(appPath);
            return list.Count > 0 ? list[0] : "";
        }

        private static bool ContainsBytes(byte[] buffer, int count, byte[] pattern)
        {
            int limit = count - pattern.Length;
            byte first = pattern[0];
            for (int i = 0; i <= limit; i++)
            {
                if (buffer[i] != first) continue;
                bool match = true;
                for (int j = 1; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }

        public static bool? TestPatched(string lsPath)
        {
            if (string.IsNullOrEmpty(lsPath) || !File.Exists(lsPath)) return null;
            try
            {
                byte[] needle = Encoding.ASCII.GetBytes("inexigible");
                int overlap = needle.Length;
                const int chunkSize = 4 * 1024 * 1024;
                byte[] buf = new byte[chunkSize + overlap];

                using (var fs = new FileStream(lsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    int carry = 0;
                    int read;
                    while ((read = fs.Read(buf, carry, chunkSize)) > 0)
                    {
                        int total = carry + read;
                        if (ContainsBytes(buf, total, needle)) return true;

                        if (total >= overlap)
                        {
                            Buffer.BlockCopy(buf, total - overlap, buf, 0, overlap);
                            carry = overlap;
                        }
                        else
                        {
                            carry = total;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log("TestPatched (" + lsPath + "): " + ex.Message);
                return null;
            }
        }

        public static bool? TestAntigravityPatched(string appPath)
        {
            var servers = FindLanguageServers(appPath);
            if (servers.Count == 0) return null;

            bool anyChecked = false;
            foreach (var ls in servers)
            {
                var res = TestPatched(ls);
                if (res == true) return true;
                if (res == false) anyChecked = true;
            }
            return anyChecked ? (bool?)false : null;
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

        public static string FindTelegramApp()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\tg\shell\open\command"))
                {
                    if (key != null)
                    {
                        string cmd = key.GetValue("") as string;
                        if (!string.IsNullOrEmpty(cmd))
                        {
                            var m = Regex.Match(cmd, @"\""([^\""]+Telegram\.exe)\""");
                            if (m.Success && File.Exists(m.Groups[1].Value))
                                return m.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = {
                Path.Combine(appData, @"Telegram Desktop\Telegram.exe"),
                Path.Combine(localData, @"Telegram Desktop\Telegram.exe"),
                @"C:\Program Files\Telegram Desktop\Telegram.exe",
                @"C:\Program Files (x86)\Telegram Desktop\Telegram.exe"
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p)) return p;
            }

            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Telegram.lnk");
                if (File.Exists(desktopLnk))
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic shortcut = shell.CreateShortcut(desktopLnk);
                        string target = shortcut.TargetPath;
                        if (File.Exists(target)) return target;
                    }
                }
            }
            catch { }

            return null;
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

        private UiComboBox cbList, cbProto;
        private UiButton btnAdd, btnDel, btnActive, btnTest, btnSave, btnCancel;
        private TextBox tbName, tbH, tbPt, tbU, tbPw;
        private RadioButton rbDark, rbLight;
        private CheckBox chkAutoUpdate;
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
            ClientSize = new Size(476, 546);
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

        private void StyleSec(UiButton b, Color? hoverBorder = null)
        {
            b.Fill = _p.secondaryFill;
            b.Hover = _p.secondaryHover;
            b.Pressed = _p.secondaryPressed;
            b.BorderColor = _p.secondaryBorder;
            b.HoverBorderColor = hoverBorder ?? _p.secondaryHoverBorder;
            b.BorderWidth = 1;
            b.ForeColor = _p.secondaryText;
            b.DisabledFill = _p.disabledFill;
            b.DisabledText = _p.disabledText;
        }

        private void BuildControls()
        {
            var lblTitle = new Label
            {
                Text = "Профили прокси",
                Location = new Point(20, 16),
                Size = new Size(220, 24),
                ForeColor = _p.ink,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 11f)
            };
            Controls.Add(lblTitle);

            cbList = new UiComboBox
            {
                Location = new Point(20, 48),
                Size = new Size(248, 28),
                Font = new Font("Segoe UI", 9f),
                BackColor = _p.elevated,
                ForeColor = _p.ink,
                ItemBackColor = _p.elevated,
                ItemForeColor = _p.ink,
                SelectedItemBackColor = _p.accent,
                SelectedItemForeColor = Color.White
            };
            Controls.Add(cbList);

            btnAdd = new UiButton { Text = "+ Добавить", Location = new Point(276, 47), Size = new Size(96, 28), Radius = 6 };
            StyleSec(btnAdd, _p.accent);
            Controls.Add(btnAdd);

            btnDel = new UiButton { Text = "Удалить", Location = new Point(378, 47), Size = new Size(78, 28), Radius = 6 };
            StyleSec(btnDel, _p.err);
            Controls.Add(btnDel);

            var card = new UiCard
            {
                Location = new Point(20, 88),
                Size = new Size(436, 252),
                FillColor = _p.card,
                BorderColor = _p.border,
                Radius = 10
            };
            Controls.Add(card);

            card.Controls.Add(new Label { Text = "Имя профиля", Location = new Point(16, 14), Size = new Size(200, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbName = new TextBox { Location = new Point(16, 33), Size = new Size(286, 24), BackColor = _p.inputBg, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            card.Controls.Add(tbName);

            card.Controls.Add(new Label { Text = "Протокол", Location = new Point(312, 14), Size = new Size(100, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            cbProto = new UiComboBox
            {
                Location = new Point(312, 32),
                Size = new Size(108, 26),
                Font = new Font("Segoe UI", 9f),
                BackColor = _p.elevated,
                ForeColor = _p.ink,
                ItemBackColor = _p.elevated,
                ItemForeColor = _p.ink,
                SelectedItemBackColor = _p.accent,
                SelectedItemForeColor = Color.White
            };
            cbProto.Items.Add("HTTP");
            cbProto.Items.Add("SOCKS5");
            card.Controls.Add(cbProto);

            card.Controls.Add(new Label { Text = "Сервер / Хост", Location = new Point(16, 68), Size = new Size(140, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbH = new TextBox { Location = new Point(16, 87), Size = new Size(286, 24), BackColor = _p.inputBg, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f) };
            card.Controls.Add(tbH);

            card.Controls.Add(new Label { Text = "Порт", Location = new Point(312, 68), Size = new Size(80, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbPt = new TextBox { Location = new Point(312, 87), Size = new Size(108, 24), BackColor = _p.inputBg, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f) };
            card.Controls.Add(tbPt);

            card.Controls.Add(new Label { Text = "Логин", Location = new Point(16, 122), Size = new Size(140, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbU = new TextBox { Location = new Point(16, 141), Size = new Size(196, 24), BackColor = _p.inputBg, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            card.Controls.Add(tbU);

            card.Controls.Add(new Label { Text = "Пароль", Location = new Point(224, 122), Size = new Size(100, 16), ForeColor = _p.muted, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            tbPw = new TextBox { Location = new Point(224, 141), Size = new Size(196, 24), UseSystemPasswordChar = true, BackColor = _p.inputBg, ForeColor = _p.ink, BorderStyle = BorderStyle.FixedSingle };
            card.Controls.Add(tbPw);

            btnActive = new UiButton { Text = "Сделать активным", Location = new Point(16, 180), Size = new Size(196, 32), Radius = 6 };
            card.Controls.Add(btnActive);

            btnTest = new UiButton { Text = "Проверить соединение", Location = new Point(224, 180), Size = new Size(196, 32), Radius = 6 };
            StyleSec(btnTest);
            card.Controls.Add(btnTest);

            lblNote = new Label
            {
                Text = "Проверка выполняется напрямую и не затрагивает активный Xray.",
                Location = new Point(16, 218),
                Size = new Size(404, 28),
                ForeColor = _p.muted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.25f)
            };
            card.Controls.Add(lblNote);

            var themeCard = new UiCard
            {
                Location = new Point(20, 352),
                Size = new Size(436, 106),
                FillColor = _p.card,
                BorderColor = _p.border,
                Radius = 10
            };
            Controls.Add(themeCard);

            themeCard.Controls.Add(new Label
            {
                Text = "Оформление и обновления",
                Location = new Point(16, 12),
                Size = new Size(240, 20),
                ForeColor = _p.ink,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5f)
            });
            rbDark = new RadioButton { Text = "Тёмная", Location = new Point(18, 38), Size = new Size(110, 22), ForeColor = _p.ink, BackColor = Color.Transparent };
            rbLight = new RadioButton { Text = "Светлая", Location = new Point(140, 38), Size = new Size(110, 22), ForeColor = _p.ink, BackColor = Color.Transparent };
            if (_settings.theme == "dark") rbDark.Checked = true; else rbLight.Checked = true;
            themeCard.Controls.Add(rbDark);
            themeCard.Controls.Add(rbLight);

            chkAutoUpdate = new CheckBox
            {
                Text = "Проверять обновления автоматически при запуске",
                Location = new Point(18, 70),
                Size = new Size(400, 22),
                ForeColor = _p.muted,
                BackColor = Color.Transparent,
                Checked = (_settings.autoCheckUpdates != false)
            };
            themeCard.Controls.Add(chkAutoUpdate);

            btnSave = new UiButton
            {
                Text = "Сохранить изменения",
                Location = new Point(20, 480),
                Size = new Size(184, 40),
                Radius = 7,
                Fill = _p.accent,
                Hover = _p.accentHover,
                Pressed = _p.accentPressed,
                BorderWidth = 0,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            Controls.Add(btnSave);

            btnCancel = new UiButton { Text = "Отмена", Location = new Point(214, 480), Size = new Size(110, 40), Radius = 7 };
            StyleSec(btnCancel);
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
                var newItem = new ProxyItem { name = "Прокси " + (_localProxies.Count + 1), protocol = "http", host = "", port = "8000" };
                _localProxies.Add(newItem);
                _currentIdx = _localProxies.Count - 1;
                RefreshList(_currentIdx);
                LoadFields(_currentIdx);
                tbName.Focus();
                tbName.SelectAll();
                lblNote.Text = "Новый профиль создан. Заполните поля и сохраните изменения.";
                lblNote.ForeColor = _p.muted;
            };

            btnDel.Click += (s, e) =>
            {
                if (_localProxies.Count <= 1)
                {
                    MessageBox.Show("Нельзя удалить единственный профиль.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                lblNote.Text = string.Format("«{0}» выбран активным. Нажмите «Сохранить изменения».", _localProxies[_currentIdx].name);
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
                lblNote.Text = string.Format("Проверка {0} ({1}://{2}:{3})…", cur.name, cur.NormalizedProtocol(), cur.host, cur.port);
                lblNote.ForeColor = _p.warn;

                try
                {
                    string proto = cur.NormalizedProtocol();
                    var info = await Task.Run(() => AppCore.CheckDirectProxyInfo(cur.host, cur.port, cur.user, cur.pass, proto));
                    if (info.Ok)
                    {
                        if (proto == "socks")
                        {
                            lblNote.Text = string.Format("Готово: {0} ({1} мс)", info.Note, info.PingMs);
                            lblNote.ForeColor = _p.ok;
                        }
                        else
                        {
                            string loc = string.IsNullOrEmpty(info.Country) ? "" : string.Format(" ({0}, {1})", info.Country, info.City);
                            lblNote.Text = string.Format("OK: {0}{1} · {2} мс · замер скорости…", info.Ip, loc, info.PingMs);
                            lblNote.ForeColor = _p.ok;

                            var sp = await Task.Run(() => AppCore.MeasureSpeed(cur.host, cur.port, cur.user, cur.pass));
                            string spStr = sp.Ok ? string.Format(" · {0} Мбит/с", sp.Mbps) : "";
                            lblNote.Text = string.Format("OK: {0}{1} · {2} мс{3}", info.Ip, loc, info.PingMs, spStr);
                            lblNote.ForeColor = _p.ok;
                        }
                    }
                    else
                    {
                        lblNote.Text = "Ошибка: " + info.Error;
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
                    btnTest.Text = "Проверить соединение";
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
                    lblNote.Text = "Укажите адрес сервера хотя бы для одного профиля.";
                    lblNote.ForeColor = _p.err;
                    return;
                }

                _settings.proxies = _localProxies;
                _settings.activeProxyIndex = _localActiveIdx;
                _settings.theme = rbDark.Checked ? "dark" : "light";
                _settings.autoCheckUpdates = chkAutoUpdate.Checked;
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
                cur.protocol = (cbProto.SelectedIndex == 1) ? "socks" : "http";
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
                cbProto.SelectedIndex = (cur.NormalizedProtocol() == "socks") ? 1 : 0;
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
                btnActive.Text = "Текущий активный";
                btnActive.Fill = _p.accent;
                btnActive.Hover = _p.accentHover;
                btnActive.Pressed = _p.accentPressed;
                btnActive.BorderWidth = 0;
                btnActive.ForeColor = Color.White;
                btnActive.DotColor = Color.White;
            }
            else
            {
                btnActive.Text = "Сделать активным";
                btnActive.DotColor = Color.Transparent;
                StyleSec(btnActive);
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
                    string mark = (i == _localActiveIdx) ? "[Активный] " : "";
                    string n = !string.IsNullOrEmpty(item.name) ? item.name : ("Прокси " + (i + 1));
                    string proto = item.NormalizedProtocol().ToUpperInvariant();
                    string h = !string.IsNullOrEmpty(item.host) ? string.Format("{0}://{1}:{2}", proto, item.host, item.port) : "не задан";
                    cbList.Items.Add(string.Format("{0}{1} — {2}", mark, n, h));
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
        private int _activeTab = 0;

        // Header
        private Label lblTitle, lblHeaderStatus;
        private UiStatusDot dotHeader;
        private UiButton btnUpdate, btnSettings, btnTheme;
        private UpdateInfo _pendingUpdate;

        // Tab bar
        private UiCard tabBar;
        private UiButton btnTabHome, btnTabApps, btnTabServices;
        private Panel pnlHome, pnlApps, pnlServices;

        // Tab 0: Home
        private UiCard cardNode, cardLaunch, cardSys;
        private UiStatusDot dotNode;
        private Label lblStatus, lblDetail, lblNodeMeta;
        private UiButton btnStatus, btnSpeed;
        private UiComboBox cbActiveProxy;
        private UiButton btnStart, btnBrowser, btnTelegram, btnOcGui, btnOcCli;
        private UiStatusDot dotPatch;
        private Label lblPatch, lblApp;
        private UiButton btnPatch, btnSysProxy;

        // Tab 1: Any App Launcher
        private UiCard cardRunAny, cardSavedApps;
        private Label lblRunTitle, lblRunPath, lblRunArgs, lblRunFeedback, lblSavedTitle;
        private TextBox tbAppPath, tbAppArgs;
        private CheckBox chkChromiumFlag;
        private UiButton btnBrowseApp, btnRunAny, btnSaveApp, btnRunSaved, btnDelSaved;
        private UiListBox lbSavedApps;

        // Tab 2: Services Monitor
        private UiCard cardSrvList, cardSrvProcs;
        private UiStatusDot dotSrvHttp, dotSrvSocks, dotSrvSys, dotSrvDns, dotSrvPatch, dotSrvProcs;
        private Label lblSrvHttp, lblSrvHttpVal, lblSrvSocks, lblSrvSocksVal;
        private Label lblSrvSys, lblSrvSysVal, lblSrvDns, lblSrvDnsVal;
        private Label lblSrvPatch, lblSrvPatchVal;
        private Label lblSrvProcsTitle, lblSrvProcsVal, lblSrvConflict, lblVersion;
        private UiButton btnSrvRestartXray, btnSrvToggleSys, btnSrvPatch, btnSrvRefresh, btnCheckUpdate;

        private NotifyIcon notifyIcon;

        public MainForm()
        {
            Text = "Antigravity Proxy";
            ClientSize = new Size(540, 564);
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
            RefreshSavedAppsList();
            SwitchTab(0);
            InitTray();

            Shown += async (s, e) =>
            {
                Activate();
                UpdatePatch();
                await Task.Run(() => EnsureActiveProxySynced());
                UpdateStatusAsync();
                if (_settings.autoCheckUpdates != false)
                {
                    await Task.Delay(2500);
                    CheckUpdatesAsync(true);
                }
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
            // === HEADER ===
            lblTitle = new Label
            {
                Text = "Antigravity Proxy",
                Location = new Point(18, 12),
                Size = new Size(220, 24),
                Font = new Font("Segoe UI Semibold", 12.5f)
            };
            dotHeader = new UiStatusDot { Location = new Point(17, 40), Size = new Size(14, 14) };
            lblHeaderStatus = new Label
            {
                Text = "Проверка состояния…",
                Location = new Point(34, 38),
                Size = new Size(250, 18),
                Font = new Font("Segoe UI", 8.5f)
            };
            Controls.Add(lblTitle);
            Controls.Add(dotHeader);
            Controls.Add(lblHeaderStatus);

            btnUpdate = new UiButton { Text = "Обновить", Location = new Point(226, 16), Size = new Size(90, 32), Radius = 7, Visible = false };
            btnSettings = new UiButton { Text = "Настройки", Location = new Point(324, 16), Size = new Size(100, 32), Radius = 7 };
            btnTheme = new UiButton { Text = "Светлая", Location = new Point(432, 16), Size = new Size(90, 32), Radius = 7 };
            Controls.Add(btnUpdate);
            Controls.Add(btnSettings);
            Controls.Add(btnTheme);

            // === SEGMENTED TAB BAR ===
            tabBar = new UiCard { Location = new Point(18, 66), Size = new Size(504, 38), Radius = 9 };
            Controls.Add(tabBar);

            btnTabHome = new UiButton { Text = "Главная", Location = new Point(4, 4), Size = new Size(164, 30), Radius = 7, BorderWidth = 0 };
            btnTabApps = new UiButton { Text = "Приложения", Location = new Point(170, 4), Size = new Size(164, 30), Radius = 7, BorderWidth = 0 };
            btnTabServices = new UiButton { Text = "Сервисы", Location = new Point(336, 4), Size = new Size(164, 30), Radius = 7, BorderWidth = 0 };
            tabBar.Controls.Add(btnTabHome);
            tabBar.Controls.Add(btnTabApps);
            tabBar.Controls.Add(btnTabServices);

            // === CONTENT CONTAINERS ===
            pnlHome = new Panel { Location = new Point(0, 114), Size = new Size(540, 446) };
            pnlApps = new Panel { Location = new Point(0, 114), Size = new Size(540, 446), Visible = false };
            pnlServices = new Panel { Location = new Point(0, 114), Size = new Size(540, 446), Visible = false };
            Controls.Add(pnlHome);
            Controls.Add(pnlApps);
            Controls.Add(pnlServices);

            BuildHomeTab();
            BuildAppsTab();
            BuildServicesTab();
            WireEvents();
        }

        private void BuildHomeTab()
        {
            // Card 1: Active Node
            cardNode = new UiCard { Location = new Point(18, 4), Size = new Size(504, 134), Radius = 10 };
            pnlHome.Controls.Add(cardNode);

            dotNode = new UiStatusDot { Location = new Point(16, 17), Size = new Size(14, 14) };
            lblStatus = new Label { Text = "Проверка узла…", Location = new Point(36, 14), Size = new Size(448, 22), Font = new Font("Segoe UI Semibold", 10.5f) };
            lblDetail = new Label { Text = "", Location = new Point(18, 42), Size = new Size(468, 20), Font = new Font("Consolas", 8.75f) };
            lblNodeMeta = new Label { Text = "", Location = new Point(18, 63), Size = new Size(468, 18), Font = new Font("Segoe UI", 8.25f) };
            cardNode.Controls.Add(dotNode);
            cardNode.Controls.Add(lblStatus);
            cardNode.Controls.Add(lblDetail);
            cardNode.Controls.Add(lblNodeMeta);

            btnStatus = new UiButton { Text = "Обновить", Location = new Point(16, 92), Size = new Size(92, 28), Radius = 6 };
            btnSpeed = new UiButton { Text = "Скорость", Location = new Point(114, 92), Size = new Size(92, 28), Radius = 6 };
            cbActiveProxy = new UiComboBox { Location = new Point(214, 92), Size = new Size(274, 28), DropDownWidth = 300, Font = new Font("Segoe UI", 9f) };
            cardNode.Controls.Add(btnStatus);
            cardNode.Controls.Add(btnSpeed);
            cardNode.Controls.Add(cbActiveProxy);

            // Card 2: Quick Launchers
            cardLaunch = new UiCard { Location = new Point(18, 148), Size = new Size(504, 152), Radius = 10 };
            pnlHome.Controls.Add(cardLaunch);

            btnStart = new UiButton { Text = "Запустить Antigravity", Location = new Point(14, 14), Size = new Size(476, 40), Radius = 7, Font = new Font("Segoe UI Semibold", 10f) };
            btnBrowser = new UiButton { Text = "AI Studio (Web)", Location = new Point(14, 62), Size = new Size(234, 34), Radius = 7 };
            btnTelegram = new UiButton { Text = "Telegram (SOCKS5)", Location = new Point(256, 62), Size = new Size(234, 34), Radius = 7 };
            btnOcGui = new UiButton { Text = "OpenCode GUI", Location = new Point(14, 104), Size = new Size(234, 34), Radius = 7 };
            btnOcCli = new UiButton { Text = "OpenCode CLI", Location = new Point(256, 104), Size = new Size(234, 34), Radius = 7 };
            cardLaunch.Controls.Add(btnStart);
            cardLaunch.Controls.Add(btnBrowser);
            cardLaunch.Controls.Add(btnTelegram);
            cardLaunch.Controls.Add(btnOcGui);
            cardLaunch.Controls.Add(btnOcCli);

            // Card 3: System & Patch
            cardSys = new UiCard { Location = new Point(18, 310), Size = new Size(504, 122), Radius = 10 };
            pnlHome.Controls.Add(cardSys);

            dotPatch = new UiStatusDot { Location = new Point(16, 17), Size = new Size(14, 14) };
            lblPatch = new Label { Text = "Патч: проверка…", Location = new Point(36, 14), Size = new Size(340, 20), Font = new Font("Segoe UI Semibold", 9.25f) };
            btnPatch = new UiButton { Text = "Патч", Location = new Point(398, 12), Size = new Size(90, 28), Radius = 6 };
            lblApp = new Label { Text = "", Location = new Point(18, 40), Size = new Size(468, 20), Font = new Font("Consolas", 8f) };
            btnSysProxy = new UiButton { Text = "Системный прокси: выкл", Location = new Point(16, 72), Size = new Size(260, 34), Radius = 7 };
            cardSys.Controls.Add(dotPatch);
            cardSys.Controls.Add(lblPatch);
            cardSys.Controls.Add(btnPatch);
            cardSys.Controls.Add(lblApp);
            cardSys.Controls.Add(btnSysProxy);
        }

        private void BuildAppsTab()
        {
            cardRunAny = new UiCard { Location = new Point(18, 4), Size = new Size(504, 214), Radius = 10 };
            pnlApps.Controls.Add(cardRunAny);

            lblRunTitle = new Label
            {
                Text = "Запуск любого приложения через прокси",
                Location = new Point(16, 14),
                Size = new Size(460, 22),
                Font = new Font("Segoe UI Semibold", 10f)
            };
            cardRunAny.Controls.Add(lblRunTitle);

            lblRunPath = new Label { Text = "Исполняемый файл (.exe, .cmd, .bat)", Location = new Point(16, 42), Size = new Size(300, 16), Font = new Font("Segoe UI", 8.25f) };
            tbAppPath = new TextBox { Location = new Point(16, 60), Size = new Size(376, 24), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 8.75f) };
            btnBrowseApp = new UiButton { Text = "Обзор…", Location = new Point(400, 58), Size = new Size(88, 26), Radius = 6 };
            cardRunAny.Controls.Add(lblRunPath);
            cardRunAny.Controls.Add(tbAppPath);
            cardRunAny.Controls.Add(btnBrowseApp);

            lblRunArgs = new Label { Text = "Аргументы запуска (необязательно)", Location = new Point(16, 92), Size = new Size(300, 16), Font = new Font("Segoe UI", 8.25f) };
            tbAppArgs = new TextBox { Location = new Point(16, 110), Size = new Size(472, 24), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 8.75f) };
            cardRunAny.Controls.Add(lblRunArgs);
            cardRunAny.Controls.Add(tbAppArgs);

            chkChromiumFlag = new CheckBox
            {
                Text = "Добавить --proxy-server=http://127.0.0.1:10809 (для браузеров и Electron)",
                Location = new Point(16, 140),
                Size = new Size(472, 20),
                Font = new Font("Segoe UI", 8.25f)
            };
            cardRunAny.Controls.Add(chkChromiumFlag);

            btnRunAny = new UiButton { Text = "Запустить через прокси", Location = new Point(16, 168), Size = new Size(240, 34), Radius = 7, Font = new Font("Segoe UI Semibold", 9.25f) };
            btnSaveApp = new UiButton { Text = "Сохранить в список", Location = new Point(266, 168), Size = new Size(154, 34), Radius = 7 };
            cardRunAny.Controls.Add(btnRunAny);
            cardRunAny.Controls.Add(btnSaveApp);

            // Saved Apps Card
            cardSavedApps = new UiCard { Location = new Point(18, 228), Size = new Size(504, 204), Radius = 10 };
            pnlApps.Controls.Add(cardSavedApps);

            lblSavedTitle = new Label
            {
                Text = "Сохранённые приложения (двойной клик для запуска)",
                Location = new Point(16, 12),
                Size = new Size(350, 20),
                Font = new Font("Segoe UI Semibold", 9.25f)
            };
            cardSavedApps.Controls.Add(lblSavedTitle);

            lbSavedApps = new UiListBox { Location = new Point(16, 38), Size = new Size(472, 118) };
            cardSavedApps.Controls.Add(lbSavedApps);

            btnRunSaved = new UiButton { Text = "Запустить выбранное", Location = new Point(16, 164), Size = new Size(170, 30), Radius = 6 };
            btnDelSaved = new UiButton { Text = "Удалить", Location = new Point(194, 164), Size = new Size(92, 30), Radius = 6 };
            lblRunFeedback = new Label { Text = "", Location = new Point(296, 170), Size = new Size(192, 18), Font = new Font("Segoe UI", 8.25f), TextAlign = ContentAlignment.MiddleRight };
            cardSavedApps.Controls.Add(btnRunSaved);
            cardSavedApps.Controls.Add(btnDelSaved);
            cardSavedApps.Controls.Add(lblRunFeedback);
        }

        private void AddServiceRow(UiCard parent, int y, string title, out UiStatusDot dot, out Label lblName, out Label lblVal, UiButton actionBtn = null)
        {
            dot = new UiStatusDot { Location = new Point(16, y + 6), Size = new Size(14, 14) };
            lblName = new Label
            {
                Text = title,
                Location = new Point(36, y),
                Size = new Size(210, 18),
                Font = new Font("Segoe UI Semibold", 9f)
            };
            lblVal = new Label
            {
                Text = "Проверка…",
                Location = new Point(36, y + 18),
                Size = new Size(actionBtn != null ? 330 : 450, 17),
                Font = new Font("Consolas", 8.25f)
            };
            parent.Controls.Add(dot);
            parent.Controls.Add(lblName);
            parent.Controls.Add(lblVal);
            if (actionBtn != null)
            {
                actionBtn.Location = new Point(378, y + 3);
                actionBtn.Size = new Size(110, 28);
                actionBtn.Radius = 6;
                parent.Controls.Add(actionBtn);
            }
        }

        private void BuildServicesTab()
        {
            cardSrvList = new UiCard { Location = new Point(18, 4), Size = new Size(504, 272), Radius = 10 };
            pnlServices.Controls.Add(cardSrvList);

            btnSrvRestartXray = new UiButton { Text = "Перезапуск" };
            btnSrvToggleSys = new UiButton { Text = "Включить" };
            btnSrvPatch = new UiButton { Text = "Патч" };

            AddServiceRow(cardSrvList, 14, "Локальный Xray (HTTP :10809)", out dotSrvHttp, out lblSrvHttp, out lblSrvHttpVal, btnSrvRestartXray);
            AddServiceRow(cardSrvList, 64, "Локальный Xray (SOCKS5 :10808)", out dotSrvSocks, out lblSrvSocks, out lblSrvSocksVal);
            AddServiceRow(cardSrvList, 114, "Системный прокси Windows", out dotSrvSys, out lblSrvSys, out lblSrvSysVal, btnSrvToggleSys);
            AddServiceRow(cardSrvList, 164, "AGDNS (Локальный DoH :53)", out dotSrvDns, out lblSrvDns, out lblSrvDnsVal);
            AddServiceRow(cardSrvList, 214, "Патч Antigravity", out dotSrvPatch, out lblSrvPatch, out lblSrvPatchVal, btnSrvPatch);

            cardSrvProcs = new UiCard { Location = new Point(18, 286), Size = new Size(504, 100), Radius = 10 };
            pnlServices.Controls.Add(cardSrvProcs);

            dotSrvProcs = new UiStatusDot { Location = new Point(16, 16), Size = new Size(14, 14) };
            lblSrvProcsTitle = new Label
            {
                Text = "Активные процессы",
                Location = new Point(36, 13),
                Size = new Size(250, 20),
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            lblSrvProcsVal = new Label
            {
                Text = "Проверка…",
                Location = new Point(18, 40),
                Size = new Size(468, 22),
                Font = new Font("Consolas", 8.5f)
            };
            lblSrvConflict = new Label
            {
                Text = "",
                Location = new Point(18, 66),
                Size = new Size(468, 22),
                Font = new Font("Segoe UI", 8.25f)
            };
            cardSrvProcs.Controls.Add(dotSrvProcs);
            cardSrvProcs.Controls.Add(lblSrvProcsTitle);
            cardSrvProcs.Controls.Add(lblSrvProcsVal);
            cardSrvProcs.Controls.Add(lblSrvConflict);

            btnSrvRefresh = new UiButton { Text = "Обновить статусы", Location = new Point(18, 398), Size = new Size(164, 34), Radius = 7 };
            btnCheckUpdate = new UiButton { Text = "Проверить обновления", Location = new Point(190, 398), Size = new Size(184, 34), Radius = 7 };
            lblVersion = new Label
            {
                Text = "v" + AppCore.AppVersion,
                Location = new Point(386, 405),
                Size = new Size(136, 20),
                Font = new Font("Consolas", 8.5f),
                TextAlign = ContentAlignment.MiddleRight
            };
            pnlServices.Controls.Add(btnSrvRefresh);
            pnlServices.Controls.Add(btnCheckUpdate);
            pnlServices.Controls.Add(lblVersion);
        }

        private void WireEvents()
        {
            btnTabHome.Click += (s, e) => SwitchTab(0);
            btnTabApps.Click += (s, e) => SwitchTab(1);
            btnTabServices.Click += (s, e) => SwitchTab(2);

            btnTheme.Click += (s, e) =>
            {
                _settings.theme = (_settings.theme == "dark") ? "light" : "dark";
                AppCore.SaveSettings(_settings);
                _p = Palette.Get(_settings.theme);
                ApplyTheme();
            };

            btnSettings.Click += async (s, e) =>
            {
                using (var dlg = new SettingsDialog(_settings, _p, Icon))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _settings = AppCore.LoadSettings();
                        _p = Palette.Get(_settings.theme);
                        ApplyTheme();
                        RefreshProxyComboBox();
                        if (string.IsNullOrEmpty(AppCore.FindXrayExe()))
                        {
                            await EnsureXrayInstalledAndStartAsync();
                        }
                        else
                        {
                            await Task.Run(() => EnsureActiveProxySynced());
                            UpdateStatusAsync();
                        }
                        UpdatePatch();
                        if (_activeTab == 2) RefreshServicesAsync();
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

            btnStatus.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(AppCore.FindXrayExe()))
                {
                    await EnsureXrayInstalledAndStartAsync();
                }
                else
                {
                    UpdateStatusAsync();
                }
            };

            btnSpeed.Click += async (s, e) =>
            {
                if (!AppCore.TestPort(10809))
                {
                    lblStatus.Text = "Прокси не запущен";
                    lblStatus.ForeColor = _p.err;
                    dotNode.DotColor = _p.err;
                    return;
                }
                btnSpeed.Enabled = false;
                btnSpeed.Text = "Замер…";
                lblStatus.Text = "Замер скорости…";
                lblStatus.ForeColor = _p.warn;
                dotNode.DotColor = _p.warn;

                try
                {
                    var res = await Task.Run(() => AppCore.MeasureSpeed());
                    if (res.Ok)
                    {
                        lblStatus.Text = "Прокси работает";
                        lblStatus.ForeColor = _p.ok;
                        dotNode.DotColor = _p.ok;
                        var act = GetActiveProxy();
                        string pName = (act != null && !string.IsNullOrEmpty(act.name)) ? act.name : "Прокси";
                        lblDetail.Text = string.Format("{0} Мбит/с  |  пинг {1} мс  (Cloudflare CDN)", res.Mbps, res.PingMs);
                        lblNodeMeta.Text = string.Format("Активный профиль: {0}", pName);
                    }
                    else
                    {
                        lblStatus.Text = "Ошибка замера скорости";
                        lblStatus.ForeColor = _p.warn;
                        dotNode.DotColor = _p.warn;
                        lblDetail.Text = res.Error;
                    }
                }
                catch (Exception ex)
                {
                    AppCore.Log("BtnSpeed: " + ex.ToString());
                    lblStatus.Text = "Ошибка замера";
                    lblStatus.ForeColor = _p.err;
                    dotNode.DotColor = _p.err;
                }
                finally
                {
                    btnSpeed.Enabled = true;
                    btnSpeed.Text = "Скорость";
                }
            };

            btnStart.Click += (s, e) =>
            {
                try
                {
                    string app = AppCore.FindAntigravityApp();
                    if (string.IsNullOrEmpty(app))
                    {
                        MessageBox.Show("Antigravity не найден на этом компьютере.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!AppCore.TestPort(10809)) AppCore.RestartXray();

                    foreach (var p in Process.GetProcessesByName("Antigravity"))
                    {
                        try { p.Kill(); } catch { }
                    }
                    System.Threading.Thread.Sleep(700);
                    AppCore.StartWithProxyEnv(app);
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

            btnTelegram.Click += (s, e) =>
            {
                try
                {
                    if (!AppCore.TestPort(10809) || !AppCore.TestPort(10808))
                    {
                        AppCore.RestartXray();
                    }

                    string tg = AppCore.FindTelegramApp();
                    string proxyUrl = "tg://socks?server=127.0.0.1&port=10808";

                    if (!string.IsNullOrEmpty(tg))
                    {
                        AppCore.StartWithProxyEnv(tg, "-- \"" + proxyUrl + "\"", Path.GetDirectoryName(tg));
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo { FileName = proxyUrl, UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось запустить Telegram: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    AppCore.StartWithProxyEnv(oc);
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
                    AppCore.StartWithProxyEnv("powershell.exe", "-NoExit -NoProfile -Command \"& '" + oc + "'\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось запустить OpenCode CLI: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Action runPatcherAction = () =>
            {
                string patcher = AppCore.FindPatcher();
                if (string.IsNullOrEmpty(patcher))
                {
                    MessageBox.Show("Файл Open_AG_Patcher*.exe не найден в «Загрузках».", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    string app = AppCore.FindAntigravityApp();
                    string dir = !string.IsNullOrEmpty(app) ? Path.GetDirectoryName(app) : "";
                    var psi = new ProcessStartInfo { FileName = patcher, Verb = "runas" };
                    if (!string.IsNullOrEmpty(dir)) psi.Arguments = "\"" + dir + "\"";
                    Process.Start(psi);
                    lblPatch.Text = "Патчер запущен — выберите PATCH";
                    lblPatch.ForeColor = _p.warn;
                    dotPatch.DotColor = _p.warn;
                }
                catch
                {
                    lblPatch.Text = "Не удалось запустить патчер";
                    lblPatch.ForeColor = _p.err;
                    dotPatch.DotColor = _p.err;
                }
            };

            btnPatch.Click += (s, e) => runPatcherAction();
            btnSrvPatch.Click += (s, e) => runPatcherAction();

            Action toggleSysProxyAction = () =>
            {
                bool current = AppCore.TestSystemProxy();
                AppCore.SetSystemProxy(!current);
                UpdateSysProxyButton();
                if (_activeTab == 2) RefreshServicesAsync();
            };

            btnSysProxy.Click += (s, e) => toggleSysProxyAction();
            btnSrvToggleSys.Click += (s, e) => toggleSysProxyAction();

            // === TAB 1: ANY APP ===
            btnBrowseApp.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Выберите приложение для запуска через прокси";
                    ofd.Filter = "Приложения (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|Все файлы (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        tbAppPath.Text = ofd.FileName;
                        lblRunFeedback.Text = "";
                    }
                }
            };

            btnRunAny.Click += (s, e) =>
            {
                LaunchCustomApp(tbAppPath.Text.Trim(), tbAppArgs.Text.Trim(), chkChromiumFlag.Checked);
            };

            btnSaveApp.Click += (s, e) =>
            {
                string path = tbAppPath.Text.Trim();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    lblRunFeedback.Text = "Сначала выберите существующий файл";
                    lblRunFeedback.ForeColor = _p.err;
                    return;
                }
                string name = Path.GetFileNameWithoutExtension(path);
                _settings.customApps.Add(new CustomAppItem
                {
                    name = name,
                    path = path,
                    args = tbAppArgs.Text.Trim(),
                    chromiumFlag = chkChromiumFlag.Checked
                });
                AppCore.SaveSettings(_settings);
                RefreshSavedAppsList();
                lblRunFeedback.Text = "Сохранено: " + name;
                lblRunFeedback.ForeColor = _p.ok;
            };

            btnRunSaved.Click += (s, e) => RunSelectedSavedApp();
            lbSavedApps.DoubleClick += (s, e) => RunSelectedSavedApp();

            btnDelSaved.Click += (s, e) =>
            {
                int idx = lbSavedApps.SelectedIndex;
                if (idx < 0 || idx >= _settings.customApps.Count) return;
                _settings.customApps.RemoveAt(idx);
                AppCore.SaveSettings(_settings);
                RefreshSavedAppsList();
                lblRunFeedback.Text = "Удалено из списка";
                lblRunFeedback.ForeColor = _p.muted;
            };

            // === TAB 2: SERVICES ===
            btnSrvRefresh.Click += (s, e) => RefreshServicesAsync();
            btnCheckUpdate.Click += (s, e) => CheckUpdatesAsync(false);
            btnUpdate.Click += (s, e) =>
            {
                if (_pendingUpdate != null && _pendingUpdate.Available)
                {
                    PromptAndInstallUpdateAsync(_pendingUpdate);
                }
                else
                {
                    CheckUpdatesAsync(false);
                }
            };

            btnSrvRestartXray.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(AppCore.FindXrayExe()))
                {
                    await EnsureXrayInstalledAndStartAsync();
                    RefreshServicesAsync();
                    return;
                }
                btnSrvRestartXray.Enabled = false;
                btnSrvRestartXray.Text = "Запуск…";
                await Task.Run(() =>
                {
                    var act = GetActiveProxy();
                    if (act != null && !string.IsNullOrEmpty(act.host))
                    {
                        AppCore.WriteXrayConfig(act.host, act.port, act.user, act.pass, act.NormalizedProtocol());
                    }
                    AppCore.RestartXray();
                });
                btnSrvRestartXray.Enabled = true;
                btnSrvRestartXray.Text = "Перезапуск";
                RefreshServicesAsync();
                UpdateStatusAsync();
            };
        }

        private void LaunchCustomApp(string path, string args, bool chromiumFlag)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                lblRunFeedback.Text = "Файл не найден";
                lblRunFeedback.ForeColor = _p.err;
                MessageBox.Show("Укажите существующий файл программы.", "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                if (!AppCore.TestPort(10809)) AppCore.RestartXray();
                string finalArgs = args ?? "";
                if (chromiumFlag)
                {
                    string flag = string.Format("--proxy-server=http://127.0.0.1:10809 --proxy-bypass-list=\"<local>;localhost;127.0.0.1;::1;{0}\"", Environment.MachineName);
                    finalArgs = string.IsNullOrEmpty(finalArgs) ? flag : (flag + " " + finalArgs);
                }
                AppCore.StartWithProxyEnv(path, finalArgs, Path.GetDirectoryName(path));
                lblRunFeedback.Text = "Запущено: " + Path.GetFileName(path);
                lblRunFeedback.ForeColor = _p.ok;
            }
            catch (Exception ex)
            {
                lblRunFeedback.Text = "Ошибка запуска";
                lblRunFeedback.ForeColor = _p.err;
                MessageBox.Show("Не удалось запустить приложение: " + ex.Message, "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSelectedSavedApp()
        {
            int idx = lbSavedApps.SelectedIndex;
            if (idx < 0 || idx >= _settings.customApps.Count) return;
            var item = _settings.customApps[idx];
            LaunchCustomApp(item.path, item.args, item.chromiumFlag);
        }

        private void RefreshSavedAppsList()
        {
            lbSavedApps.Items.Clear();
            if (_settings.customApps == null) return;
            foreach (var app in _settings.customApps)
            {
                string extra = app.chromiumFlag ? " [Chromium]" : "";
                lbSavedApps.Items.Add(string.Format("{0}{1}\n{2}", app.name, extra, app.path));
            }
        }

        private void SwitchTab(int tabIndex)
        {
            _activeTab = tabIndex;
            pnlHome.Visible = (tabIndex == 0);
            pnlApps.Visible = (tabIndex == 1);
            pnlServices.Visible = (tabIndex == 2);
            StyleTabs();
            if (tabIndex == 2) RefreshServicesAsync();
        }

        private void StyleTabs()
        {
            UiButton[] tabs = { btnTabHome, btnTabApps, btnTabServices };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = (i == _activeTab);
                if (active)
                {
                    tabs[i].Fill = _p.card;
                    tabs[i].Hover = _p.card;
                    tabs[i].Pressed = _p.card;
                    tabs[i].ForeColor = _p.ink;
                    tabs[i].BorderColor = _p.border;
                    tabs[i].HoverBorderColor = _p.border;
                    tabs[i].BorderWidth = 1;
                    tabs[i].Font = new Font("Segoe UI Semibold", 9f);
                }
                else
                {
                    tabs[i].Fill = _p.elevated;
                    tabs[i].Hover = _p.secondaryHover;
                    tabs[i].Pressed = _p.secondaryPressed;
                    tabs[i].ForeColor = _p.muted;
                    tabs[i].BorderWidth = 0;
                    tabs[i].Font = new Font("Segoe UI", 9f);
                }
                tabs[i].Invalidate();
            }
        }

        private void StyleSecondaryButton(UiButton b)
        {
            b.Fill = _p.secondaryFill;
            b.Hover = _p.secondaryHover;
            b.Pressed = _p.secondaryPressed;
            b.BorderColor = _p.secondaryBorder;
            b.HoverBorderColor = _p.secondaryHoverBorder;
            b.BorderWidth = 1;
            b.ForeColor = _p.secondaryText;
            b.DisabledFill = _p.disabledFill;
            b.DisabledText = _p.disabledText;
        }

        private void StylePrimaryButton(UiButton b)
        {
            b.Fill = _p.accent;
            b.Hover = _p.accentHover;
            b.Pressed = _p.accentPressed;
            b.BorderWidth = 0;
            b.ForeColor = Color.White;
            b.DisabledFill = _p.disabledFill;
            b.DisabledText = _p.disabledText;
        }

        private void ApplyTheme()
        {
            BackColor = _p.bg;
            pnlHome.BackColor = _p.bg;
            pnlApps.BackColor = _p.bg;
            pnlServices.BackColor = _p.bg;

            tabBar.FillColor = _p.elevated;
            tabBar.BorderColor = _p.border;

            UiCard[] cards = { cardNode, cardLaunch, cardSys, cardRunAny, cardSavedApps, cardSrvList, cardSrvProcs };
            foreach (var c in cards)
            {
                c.FillColor = _p.card;
                c.BorderColor = _p.border;
                c.BackColor = _p.card;
            }

            Label[] inkLabels = { lblTitle, lblStatus, lblRunTitle, lblSavedTitle, lblSrvHttp, lblSrvSocks, lblSrvSys, lblSrvDns, lblSrvPatch, lblSrvProcsTitle };
            foreach (var l in inkLabels) { l.ForeColor = _p.ink; l.BackColor = Color.Transparent; }

            Label[] mutedLabels = { lblHeaderStatus, lblDetail, lblNodeMeta, lblPatch, lblApp, lblRunPath, lblRunArgs, lblRunFeedback, lblSrvHttpVal, lblSrvSocksVal, lblSrvSysVal, lblSrvDnsVal, lblSrvPatchVal, lblSrvProcsVal, lblSrvConflict, lblVersion };
            foreach (var l in mutedLabels) { l.ForeColor = _p.muted; l.BackColor = Color.Transparent; }

            chkChromiumFlag.ForeColor = _p.muted;
            chkChromiumFlag.BackColor = Color.Transparent;

            tbAppPath.BackColor = _p.inputBg;
            tbAppPath.ForeColor = _p.ink;
            tbAppArgs.BackColor = _p.inputBg;
            tbAppArgs.ForeColor = _p.ink;

            lbSavedApps.BackColor = _p.inputBg;
            lbSavedApps.ItemBackColor = _p.inputBg;
            lbSavedApps.ItemForeColor = _p.ink;
            lbSavedApps.SubTextColor = _p.muted;
            lbSavedApps.SelectedItemBackColor = _p.accent;
            lbSavedApps.SelectedItemForeColor = Color.White;

            UiButton[] secButtons = { btnSettings, btnTheme, btnStatus, btnSpeed, btnBrowser, btnTelegram, btnOcGui, btnOcCli, btnPatch, btnBrowseApp, btnSaveApp, btnRunSaved, btnDelSaved, btnSrvRestartXray, btnSrvToggleSys, btnSrvPatch, btnSrvRefresh, btnCheckUpdate };
            foreach (var b in secButtons) StyleSecondaryButton(b);

            StylePrimaryButton(btnStart);
            StylePrimaryButton(btnRunAny);
            StylePrimaryButton(btnUpdate);

            btnTheme.Text = (_settings.theme == "dark") ? "Светлая" : "Тёмная";

            if (cbActiveProxy != null)
            {
                cbActiveProxy.BackColor = _p.elevated;
                cbActiveProxy.ForeColor = _p.ink;
                cbActiveProxy.ItemBackColor = _p.elevated;
                cbActiveProxy.ItemForeColor = _p.ink;
                cbActiveProxy.SelectedItemBackColor = _p.accent;
                cbActiveProxy.SelectedItemForeColor = Color.White;
                cbActiveProxy.Invalidate();
            }

            StyleTabs();
            UpdateSysProxyButton();
            Invalidate(true);
        }

        private void UpdateSysProxyButton()
        {
            bool on = AppCore.TestSystemProxy();
            btnSysProxy.Text = on ? "Системный прокси: включён" : "Системный прокси: выключен";
            btnSysProxy.DotColor = on ? _p.ok : _p.muted;
            if (on)
            {
                StylePrimaryButton(btnSysProxy);
                btnSysProxy.DotColor = Color.White;
            }
            else
            {
                StyleSecondaryButton(btnSysProxy);
                btnSysProxy.DotColor = _p.muted;
            }
            btnSysProxy.Invalidate();
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

                bool portOk = AppCore.TestPort(10809);
                var cur = AppCore.GetCurrentXrayServer();

                bool match = (cur != null
                    && cur.NormalizedProtocol() == active.NormalizedProtocol()
                    && (cur.host ?? "") == (active.host ?? "")
                    && (cur.port ?? "") == (active.port ?? "")
                    && (cur.user ?? "") == (active.user ?? "")
                    && (cur.pass ?? "") == (active.pass ?? ""));

                if (!portOk || !match)
                {
                    AppCore.WriteXrayConfig(active.host, active.port, active.user, active.pass, active.NormalizedProtocol());
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
                if (string.IsNullOrEmpty(AppCore.FindXrayExe()))
                {
                    await EnsureXrayInstalledAndStartAsync();
                    return;
                }
                lblStatus.Text = "Переключение узла…";
                lblStatus.ForeColor = _p.warn;
                dotNode.DotColor = _p.warn;
                dotHeader.DotColor = _p.warn;
                lblHeaderStatus.Text = "Переключение прокси…";
                Update();

                await Task.Run(() =>
                {
                    AppCore.WriteXrayConfig(p.host, p.port, p.user, p.pass, p.NormalizedProtocol());
                    AppCore.RestartXray();
                });

                UpdateStatusAsync();
                if (_activeTab == 2) RefreshServicesAsync();
            }
        }

        private async Task EnsureXrayInstalledAndStartAsync()
        {
            btnStatus.Enabled = false;
            btnSrvRestartXray.Enabled = false;
            lblStatus.Text = "Установка Xray-core…";
            lblStatus.ForeColor = _p.warn;
            dotNode.DotColor = _p.warn;
            dotHeader.DotColor = _p.warn;
            lblHeaderStatus.Text = "Загрузка Xray-core…";
            lblDetail.Text = "Подключение к GitHub Releases…";
            lblNodeMeta.Text = "";

            var act = GetActiveProxy();
            bool ok = await Task.Run(() => AppCore.DownloadXray(act, msg =>
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        lblDetail.Text = msg;
                        lblSrvHttpVal.Text = msg;
                    }));
                }
                catch { }
            }));

            if (ok)
            {
                lblStatus.Text = "Запуск Xray-core…";
                lblDetail.Text = "Применяю настройки прокси…";
                await Task.Run(() =>
                {
                    if (act != null && !string.IsNullOrEmpty(act.host))
                    {
                        AppCore.WriteXrayConfig(act.host, act.port, act.user, act.pass, act.NormalizedProtocol());
                        AppCore.RestartXray();
                    }
                });
            }
            else
            {
                lblStatus.Text = "Не удалось скачать Xray-core";
                lblStatus.ForeColor = _p.err;
                dotNode.DotColor = _p.err;
                dotHeader.DotColor = _p.err;
                lblDetail.Text = AppCore.LastXrayError;
                MessageBox.Show(AppCore.LastXrayError + "\r\n\r\nВы также можете скачать Xray-windows-64.zip вручную и положить xray.exe в:\r\n" + AppCore.XrayDir,
                    "Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            btnStatus.Enabled = true;
            btnSrvRestartXray.Enabled = true;
            UpdateStatusAsync();
            if (_activeTab == 2) RefreshServicesAsync();
        }

        private async void UpdateStatusAsync()
        {
            var active = GetActiveProxy();
            string proxyName = (active != null && !string.IsNullOrEmpty(active.name)) ? active.name : "Прокси";
            string proto = (active != null) ? active.NormalizedProtocol().ToUpperInvariant() : "HTTP";
            string where = (active != null && !string.IsNullOrEmpty(active.host)) ? string.Format("{0}:{1}", active.host, active.port) : "не задан";

            if (string.IsNullOrEmpty(AppCore.FindXrayExe()))
            {
                lblStatus.Text = "Не установлен Xray-core";
                lblStatus.ForeColor = _p.warn;
                dotNode.DotColor = _p.warn;
                dotHeader.DotColor = _p.warn;
                lblHeaderStatus.Text = "Требуется xray.exe";
                lblDetail.Text = "Нажмите «Скачать Xray» для автоматической установки";
                lblNodeMeta.Text = "Папка: " + AppCore.XrayDir;
                btnStatus.Text = "Скачать Xray";
                StylePrimaryButton(btnStatus);
                btnStatus.Invalidate();
                return;
            }
            else
            {
                btnStatus.Text = "Обновить";
                StyleSecondaryButton(btnStatus);
                btnStatus.Invalidate();
            }

            if (!AppCore.TestPort(10809))
            {
                lblStatus.Text = "Прокси не запущен";
                lblStatus.ForeColor = _p.err;
                dotNode.DotColor = _p.err;
                dotHeader.DotColor = _p.err;
                lblHeaderStatus.Text = "Xray остановлен";
                lblDetail.Text = !string.IsNullOrEmpty(AppCore.LastXrayError)
                    ? AppCore.LastXrayError
                    : "Откройте «Настройки» и сохраните параметры прокси";
                lblNodeMeta.Text = string.Format("{0} ({1} · {2})", proxyName, proto, where);
                return;
            }

            lblStatus.Text = "Проверка соединения…";
            lblStatus.ForeColor = _p.warn;
            dotNode.DotColor = _p.warn;
            dotHeader.DotColor = _p.warn;
            lblHeaderStatus.Text = "Xray :10809 · проверка…";

            var res = await Task.Run(() => AppCore.CheckDirectProxyInfo("127.0.0.1", "10809", "", "", "http"));
            if (res.Ok)
            {
                lblStatus.Text = "Прокси работает";
                lblStatus.ForeColor = _p.ok;
                dotNode.DotColor = _p.ok;
                dotHeader.DotColor = _p.ok;
                string loc = string.IsNullOrEmpty(res.Country) ? "" : string.Format(" · {0}, {1}", res.Country, res.City);
                lblHeaderStatus.Text = string.Format("Xray активен · {0} ({1} мс)", res.Ip, res.PingMs);
                lblDetail.Text = string.Format("{0}{1}  |  {2} мс", res.Ip, loc, res.PingMs);
                lblNodeMeta.Text = string.Format("{0} ({1} · {2})", proxyName, proto, where);
            }
            else
            {
                lblStatus.Text = "Xray запущен, выход не отвечает";
                lblStatus.ForeColor = _p.warn;
                dotNode.DotColor = _p.warn;
                dotHeader.DotColor = _p.warn;
                lblHeaderStatus.Text = "Xray :10809 · нет ответа выхода";
                lblDetail.Text = string.Format("{0} ({1} · {2})", proxyName, proto, where);
                lblNodeMeta.Text = res.Error ?? "";
            }
        }

        private async void UpdatePatch()
        {
            lblPatch.Text = "Патч: проверка…";
            lblPatch.ForeColor = _p.muted;
            dotPatch.DotColor = _p.muted;

            var result = await Task.Run(() =>
            {
                string app = AppCore.FindAntigravityApp();
                bool? patched = null;
                if (!string.IsNullOrEmpty(app))
                {
                    patched = AppCore.TestAntigravityPatched(app);
                }
                return Tuple.Create(app, patched);
            });

            string foundApp = result.Item1;
            bool? isPatched = result.Item2;

            if (!string.IsNullOrEmpty(foundApp))
            {
                lblApp.Text = foundApp;
                lblApp.ForeColor = _p.muted;
                if (isPatched == true) { lblPatch.Text = "Патч применён"; lblPatch.ForeColor = _p.ok; dotPatch.DotColor = _p.ok; }
                else if (isPatched == false) { lblPatch.Text = "Патч не применён"; lblPatch.ForeColor = _p.err; dotPatch.DotColor = _p.err; }
                else { lblPatch.Text = "Патч: не удалось проверить"; lblPatch.ForeColor = _p.muted; dotPatch.DotColor = _p.muted; }
            }
            else
            {
                lblApp.Text = "Antigravity не найден в системе";
                lblApp.ForeColor = _p.err;
                lblPatch.Text = "language_server.exe не найден";
                lblPatch.ForeColor = _p.muted;
                dotPatch.DotColor = _p.muted;
            }
        }

        private async void RefreshServicesAsync()
        {
            btnSrvRefresh.Enabled = false;
            var snap = await Task.Run(() => AppCore.CollectServices());
            btnSrvRefresh.Enabled = true;

            // 1. Xray HTTP
            if (!snap.XrayInstalled)
            {
                dotSrvHttp.DotColor = _p.warn;
                lblSrvHttpVal.Text = "Не установлен xray.exe (нажмите «Скачать Xray»)";
                lblSrvHttpVal.ForeColor = _p.warn;
                btnSrvRestartXray.Text = "Скачать Xray";
            }
            else if (snap.XrayHttpUp)
            {
                dotSrvHttp.DotColor = _p.ok;
                lblSrvHttpVal.Text = snap.XrayPid > 0
                    ? string.Format("Работает · 127.0.0.1:10809 · PID {0}", snap.XrayPid)
                    : "Работает · 127.0.0.1:10809";
                lblSrvHttpVal.ForeColor = _p.ok;
                btnSrvRestartXray.Text = "Перезапуск";
            }
            else
            {
                dotSrvHttp.DotColor = _p.err;
                lblSrvHttpVal.Text = !string.IsNullOrEmpty(AppCore.LastXrayError)
                    ? ("Остановлен · " + AppCore.LastXrayError)
                    : "Остановлен (порт 10809 не слушается)";
                lblSrvHttpVal.ForeColor = _p.err;
                btnSrvRestartXray.Text = "Перезапуск";
            }

            // 2. Xray SOCKS5
            if (snap.XraySocksUp)
            {
                dotSrvSocks.DotColor = _p.ok;
                lblSrvSocksVal.Text = "Работает · 127.0.0.1:10808 (TCP + UDP)";
                lblSrvSocksVal.ForeColor = _p.ok;
            }
            else
            {
                dotSrvSocks.DotColor = _p.err;
                lblSrvSocksVal.Text = "Остановлен (порт 10808 не слушается)";
                lblSrvSocksVal.ForeColor = _p.err;
            }

            // 3. System Proxy
            bool ourSys = snap.SysProxyOn && snap.SysProxyServer.Contains("10809");
            if (ourSys)
            {
                dotSrvSys.DotColor = _p.ok;
                lblSrvSysVal.Text = "Включён · " + snap.SysProxyServer;
                lblSrvSysVal.ForeColor = _p.ok;
                btnSrvToggleSys.Text = "Выключить";
            }
            else if (snap.SysProxyOn)
            {
                dotSrvSys.DotColor = _p.warn;
                lblSrvSysVal.Text = "Сторонний прокси · " + snap.SysProxyServer;
                lblSrvSysVal.ForeColor = _p.warn;
                btnSrvToggleSys.Text = "Переключить";
            }
            else
            {
                dotSrvSys.DotColor = _p.muted;
                lblSrvSysVal.Text = "Выключен (прямое соединение Windows)";
                lblSrvSysVal.ForeColor = _p.muted;
                btnSrvToggleSys.Text = "Включить";
            }

            // 4. AGDNS
            if (snap.AgdnsUp)
            {
                dotSrvDns.DotColor = _p.ok;
                lblSrvDnsVal.Text = "Работает · 127.0.0.1:53 (DoH)";
                lblSrvDnsVal.ForeColor = _p.ok;
            }
            else
            {
                dotSrvDns.DotColor = _p.muted;
                lblSrvDnsVal.Text = "Не запущен (используется системный DNS)";
                lblSrvDnsVal.ForeColor = _p.muted;
            }

            // 5. Patch
            if (snap.PatchApplied == true)
            {
                dotSrvPatch.DotColor = _p.ok;
                lblSrvPatchVal.Text = "Применён (language_server.exe)";
                lblSrvPatchVal.ForeColor = _p.ok;
            }
            else if (snap.PatchApplied == false)
            {
                dotSrvPatch.DotColor = _p.err;
                lblSrvPatchVal.Text = "Не применён";
                lblSrvPatchVal.ForeColor = _p.err;
            }
            else
            {
                dotSrvPatch.DotColor = _p.muted;
                lblSrvPatchVal.Text = string.IsNullOrEmpty(snap.AntigravityPath) ? "Antigravity не найден" : "Не удалось проверить";
                lblSrvPatchVal.ForeColor = _p.muted;
            }

            // 6. Active Processes
            var activeList = new List<string>();
            if (snap.AntigravityRunning) activeList.Add("Antigravity");
            if (snap.LanguageServerRunning) activeList.Add("language_server");
            if (snap.TelegramRunning) activeList.Add("Telegram");
            if (snap.OpenCodeRunning) activeList.Add("OpenCode");

            if (activeList.Count > 0)
            {
                dotSrvProcs.DotColor = _p.ok;
                lblSrvProcsVal.Text = "Запущены: " + string.Join(", ", activeList.ToArray());
                lblSrvProcsVal.ForeColor = _p.ink;
            }
            else
            {
                dotSrvProcs.DotColor = _p.muted;
                lblSrvProcsVal.Text = "Клиентские приложения сейчас не запущены";
                lblSrvProcsVal.ForeColor = _p.muted;
            }

            if (snap.AgUnlockerConflict)
            {
                lblSrvConflict.Text = "Внимание: обнаружен процесс ag_dns (AGUnlocker) — может конфликтовать с прокси";
                lblSrvConflict.ForeColor = _p.warn;
            }
            else
            {
                lblSrvConflict.Text = "Конфликтующих служб не обнаружено";
                lblSrvConflict.ForeColor = _p.muted;
            }
        }

        private async void CheckUpdatesAsync(bool silent)
        {
            if (btnCheckUpdate != null)
            {
                btnCheckUpdate.Enabled = false;
                if (!silent) btnCheckUpdate.Text = "Проверка…";
            }

            var info = await Task.Run(() => AppCore.CheckForUpdates());

            if (btnCheckUpdate != null)
            {
                btnCheckUpdate.Enabled = true;
                btnCheckUpdate.Text = "Проверить обновления";
            }

            if (info == null) return;

            if (!string.IsNullOrEmpty(info.Error))
            {
                if (!silent)
                {
                    MessageBox.Show(info.Error, "Обновления — Antigravity Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            if (info.Available)
            {
                _pendingUpdate = info;
                btnUpdate.Text = "v" + info.LatestVersion;
                btnUpdate.Visible = true;
                btnUpdate.BringToFront();

                if (silent)
                {
                    if (!Visible && notifyIcon != null)
                    {
                        notifyIcon.ShowBalloonTip(5000, "Доступно обновление Antigravity Proxy",
                            "Новая версия v" + info.LatestVersion + " готова к установке.", ToolTipIcon.Info);
                    }
                }
                else
                {
                    PromptAndInstallUpdateAsync(info);
                }
            }
            else
            {
                _pendingUpdate = null;
                btnUpdate.Visible = false;
                if (!silent)
                {
                    MessageBox.Show("У вас установлена последняя версия Antigravity Proxy (v" + AppCore.AppVersion + ").",
                        "Обновления", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async void PromptAndInstallUpdateAsync(UpdateInfo info)
        {
            if (info == null || !info.Available || string.IsNullOrEmpty(info.DownloadUrl)) return;

            string notes = string.IsNullOrWhiteSpace(info.Notes) ? "" : ("\r\n\r\nЧто нового:\r\n" + info.Notes.Trim());
            if (notes.Length > 450) notes = notes.Substring(0, 450) + "…";

            string msg = string.Format(
                "Доступна новая версия Antigravity Proxy: v{0} (текущая v{1}).{2}\r\n\r\nУстановить обновление сейчас?\r\nЛокальный Xray продолжит работать — интернет не прервётся.",
                info.LatestVersion, info.CurrentVersion, notes);

            var dr = MessageBox.Show(msg, "Обновление Antigravity Proxy", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            btnUpdate.Enabled = false;
            btnUpdate.Text = "Загрузка…";
            btnCheckUpdate.Enabled = false;

            string err = "";
            bool ok = await Task.Run(() => AppCore.DownloadAndApplyUpdate(info.DownloadUrl, out err));

            if (ok)
            {
                _isExiting = true;
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
                Application.Exit();
            }
            else
            {
                btnUpdate.Enabled = true;
                btnUpdate.Text = "v" + info.LatestVersion;
                btnCheckUpdate.Enabled = true;
                MessageBox.Show("Не удалось установить обновление:\r\n" + err,
                    "Ошибка обновления", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var miServices = trayMenu.Items.Add("Сервисы");
            var miSettings = trayMenu.Items.Add("Настройки");
            var miUpdate = trayMenu.Items.Add("Проверить обновления");
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
            miServices.Click += (s, e) =>
            {
                showForm();
                SwitchTab(2);
            };
            miSettings.Click += (s, e) =>
            {
                showForm();
                btnSettings.PerformClick();
            };
            miUpdate.Click += (s, e) =>
            {
                showForm();
                CheckUpdatesAsync(false);
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
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

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
