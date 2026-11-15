using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityProxyUninstall
{
    public class UiButton : Button
    {
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color BorderColor { get; set; }

        private bool _isHovered;

        public UiButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            ForeColor = Color.White;
            NormalColor = Color.FromArgb(30, 41, 59);
            HoverColor = Color.FromArgb(51, 65, 85);
            BorderColor = Color.FromArgb(71, 85, 105);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var brush = new SolidBrush(Enabled ? (_isHovered ? HoverColor : NormalColor) : Color.FromArgb(40, 45, 55)))
            {
                g.FillRectangle(brush, rect);
            }
            using (var pen = new Pen(BorderColor))
            {
                g.DrawRectangle(pen, rect);
            }
            TextRenderer.DrawText(g, Text, Font, rect, Enabled ? ForeColor : Color.FromArgb(120, 130, 145),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class UninstallForm : Form
    {
        private CheckBox chkSettings;
        private CheckBox chkStopXray;
        private UiButton btnUninstall;
        private UiButton btnCancel;
        private Label lblStatus;

        public UninstallForm()
        {
            Text = "Удаление Antigravity Proxy";
            Size = new Size(460, 270);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9.5f);

            var lblHeader = new Label
            {
                Text = "Удаление Antigravity Proxy",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 20),
                AutoSize = true
            };
            Controls.Add(lblHeader);

            var lblDesc = new Label
            {
                Text = "Вы действительно хотите удалить приложение Antigravity Proxy с этого компьютера?",
                Location = new Point(24, 52),
                Size = new Size(400, 38),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            Controls.Add(lblDesc);

            chkSettings = new CheckBox
            {
                Text = "Удалить сохранённые профили прокси и данные браузера",
                Location = new Point(24, 96),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Checked = false
            };
            Controls.Add(chkSettings);

            chkStopXray = new CheckBox
            {
                Text = "Остановить службу Xray (локальный прокси)",
                Location = new Point(24, 124),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Checked = true
            };
            Controls.Add(chkStopXray);

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(24, 154),
                Size = new Size(400, 22),
                ForeColor = Color.FromArgb(56, 189, 248)
            };
            Controls.Add(lblStatus);

            btnUninstall = new UiButton
            {
                Text = "Удалить",
                Location = new Point(220, 184),
                Size = new Size(100, 32),
                NormalColor = Color.FromArgb(185, 28, 28),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderColor = Color.FromArgb(239, 68, 68)
            };
            btnUninstall.Click += (s, e) => PerformUninstall();
            Controls.Add(btnUninstall);

            btnCancel = new UiButton
            {
                Text = "Отмена",
                Location = new Point(330, 184),
                Size = new Size(94, 32)
            };
            btnCancel.Click += (s, e) => Close();
            Controls.Add(btnCancel);

            try
            {
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AntigravityProxy.ico");
                if (File.Exists(ico)) Icon = new Icon(ico);
            }
            catch {}
        }

        private void PerformUninstall()
        {
            btnUninstall.Enabled = false;
            btnCancel.Enabled = false;
            lblStatus.Text = "Удаление приложения…";
            Update();

            try
            {
                // 1. Закрываем запущенный клиент
                foreach (var p in Process.GetProcessesByName("AntigravityProxy"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch {}
                }

                // 2. Если выбрано - останавливаем xray
                if (chkStopXray.Checked)
                {
                    foreach (var p in Process.GetProcessesByName("xray"))
                    {
                        try { p.Kill(); p.WaitForExit(1000); } catch {}
                    }
                }

                // 3. Удаляем ярлыки
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string desktopLnk = Path.Combine(desktop, "Antigravity Proxy.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startLnk = Path.Combine(programs, "Antigravity Proxy.lnk");
                if (File.Exists(startLnk)) File.Delete(startLnk);

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string taskbarLnk = Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Antigravity Proxy.lnk");
                if (File.Exists(taskbarLnk)) File.Delete(taskbarLnk);

                // 4. Удаляем запись из реестра
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AntigravityProxy", false);
                }
                catch {}

                // 5. Очищаем настройки при необходимости
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                if (chkSettings.Checked)
                {
                    try
                    {
                        string settingsJson = Path.Combine(appDir, "settings.json");
                        if (File.Exists(settingsJson)) File.Delete(settingsJson);
                        string bp = Path.Combine(appDir, "BrowserProfile");
                        if (Directory.Exists(bp)) Directory.Delete(bp, true);
                    }
                    catch {}
                }

                // 6. Удаляем основные бинарники
                string[] filesToDelete = new string[]
                {
                    "AntigravityProxy.exe",
                    "AntigravityProxy.ico",
                    "AntigravityProxy.cs",
                    "AntigravityProxy.ps1",
                    "install-app.ps1",
                    "Antigravity Proxy.vbs",
                    "Antigravity Proxy.cmd",
                    "Antigravity Auto.cmd",
                    "build.cmd"
                };

                foreach (var f in filesToDelete)
                {
                    string p = Path.Combine(appDir, f);
                    try { if (File.Exists(p)) File.Delete(p); } catch {}
                }

                // 7. Скрипт самоудаления деинсталлятора и пустой папки
                string ownExe = Process.GetCurrentProcess().MainModule.FileName;
                string cmd = string.Format("/c ping 127.0.0.1 -n 2 > nul & del /f /q \"{0}\" & rmdir \"{1}\"", ownExe, appDir);
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                MessageBox.Show("Antigravity Proxy успешно удален с вашего компьютера.", "Удаление завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при удалении: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnUninstall.Enabled = true;
                btnCancel.Enabled = true;
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallForm());
        }
    }
}
