using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AntigravityProxySetup
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
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

    public class SetupForm : Form
    {
        private TextBox txtInstallPath;
        private UiButton btnBrowse;
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private CheckBox chkTaskbar;
        private CheckBox chkRunAfter;
        private ProgressBar prgBar;
        private Label lblStatus;
        private UiButton btnInstall;
        private UiButton btnCancel;

        public SetupForm()
        {
            Text = "Установка Antigravity Proxy v1.3.0";
            Size = new Size(520, 430);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9.5f);

            var lblTitle = new Label
            {
                Text = "Установка Antigravity Proxy",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 20),
                AutoSize = true
            };
            Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Менеджер прокси для Antigravity, OpenCode и веб-сервисов",
                Location = new Point(24, 52),
                Size = new Size(460, 22),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            Controls.Add(lblSubtitle);

            var lblPath = new Label
            {
                Text = "Папка установки:",
                Location = new Point(24, 86),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            Controls.Add(lblPath);

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityProxy");
            txtInstallPath = new TextBox
            {
                Text = defaultPath,
                Location = new Point(24, 110),
                Size = new Size(360, 25),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(txtInstallPath);

            btnBrowse = new UiButton
            {
                Text = "Обзор…",
                Location = new Point(392, 108),
                Size = new Size(88, 27)
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtInstallPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallPath.Text = fbd.SelectedPath;
                    }
                }
            };
            Controls.Add(btnBrowse);

            var lblOptions = new Label
            {
                Text = "Ярлыки и запуск:",
                Location = new Point(24, 150),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            Controls.Add(lblOptions);

            chkDesktop = new CheckBox
            {
                Text = "Создать ярлык на Рабочем столе",
                Location = new Point(24, 175),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            Controls.Add(chkDesktop);

            chkStartMenu = new CheckBox
            {
                Text = "Создать ярлык в меню «Пуск» (в списке программ)",
                Location = new Point(24, 203),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            Controls.Add(chkStartMenu);

            chkTaskbar = new CheckBox
            {
                Text = "Закрепить на панели задач Windows",
                Location = new Point(24, 231),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            Controls.Add(chkTaskbar);

            chkRunAfter = new CheckBox
            {
                Text = "Запустить Antigravity Proxy после завершения",
                Location = new Point(24, 259),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            Controls.Add(chkRunAfter);

            prgBar = new ProgressBar
            {
                Location = new Point(24, 295),
                Size = new Size(456, 14),
                Style = ProgressBarStyle.Blocks,
                Value = 0,
                Visible = false
            };
            Controls.Add(prgBar);

            lblStatus = new Label
            {
                Text = "Готово к установке.",
                Location = new Point(24, 315),
                Size = new Size(456, 22),
                ForeColor = Color.FromArgb(56, 189, 248)
            };
            Controls.Add(lblStatus);

            btnInstall = new UiButton
            {
                Text = "Установить",
                Location = new Point(272, 345),
                Size = new Size(116, 32),
                NormalColor = Color.FromArgb(14, 116, 144),
                HoverColor = Color.FromArgb(14, 165, 233),
                BorderColor = Color.FromArgb(56, 189, 248)
            };
            btnInstall.Click += async (s, e) => await StartInstallation();
            Controls.Add(btnInstall);

            btnCancel = new UiButton
            {
                Text = "Отмена",
                Location = new Point(396, 345),
                Size = new Size(84, 32)
            };
            btnCancel.Click += (s, e) => Close();
            Controls.Add(btnCancel);

            try
            {
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AntigravityProxy.ico");
                if (File.Exists(ico)) Icon = new Icon(ico);
            }
            catch { }
        }

        private async Task StartInstallation()
        {
            string targetDir = txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("Пожалуйста, укажите папку для установки.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnCancel.Enabled = false;
            btnBrowse.Enabled = false;
            txtInstallPath.Enabled = false;
            chkDesktop.Enabled = false;
            chkStartMenu.Enabled = false;
            chkTaskbar.Enabled = false;
            chkRunAfter.Enabled = false;

            prgBar.Visible = true;
            prgBar.Value = 10;
            lblStatus.Text = "Подготовка файлов…";

            string sourceDir = AppDomain.CurrentDomain.BaseDirectory;

            bool success = await Task.Run(() =>
            {
                try
                {
                    // 1. Закрываем запущенный клиент если открыт
                    foreach (var p in Process.GetProcessesByName("AntigravityProxy"))
                    {
                        try { p.Kill(); p.WaitForExit(1000); } catch { }
                    }

                    // 2. Создаем целевую папку
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // 3. Копируем файлы приложения
                    string[] filesToCopy = new string[]
                    {
                        "AntigravityProxy.exe",
                        "AntigravityProxy.ico",
                        "Uninstall.exe",
                        "AntigravityProxy.cs",
                        "install-app.ps1"
                    };

                    foreach (var f in filesToCopy)
                    {
                        string src = Path.Combine(sourceDir, f);
                        string dst = Path.Combine(targetDir, f);
                        if (File.Exists(src))
                        {
                            File.Copy(src, dst, true);
                        }
                    }

                    // 4. Проверяем наличие папки XrayNL и копируем если нужно
                    string xrayDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XrayNL");
                    if (!Directory.Exists(xrayDir)) Directory.CreateDirectory(xrayDir);

                    // 5. Устанавливаем полные права (Full Control) для Всех (*S-1-1-0)
                    // Это исключает любые ошибки "Отказано в доступе" к config.json
                    try
                    {
                        var psiAcl1 = new ProcessStartInfo
                        {
                            FileName = "icacls.exe",
                            Arguments = string.Format("\"{0}\" /grant *S-1-1-0:(OI)(CI)F /T /C /Q", targetDir),
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (var p1 = Process.Start(psiAcl1)) p1.WaitForExit(3000);

                        var psiAcl2 = new ProcessStartInfo
                        {
                            FileName = "icacls.exe",
                            Arguments = string.Format("\"{0}\" /grant *S-1-1-0:(OI)(CI)F /T /C /Q", xrayDir),
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (var p2 = Process.Start(psiAcl2)) p2.WaitForExit(3000);
                    }
                    catch { }

                    string targetExe = Path.Combine(targetDir, "AntigravityProxy.exe");
                    string icoPath = Path.Combine(targetDir, "AntigravityProxy.ico");

                    // 6. Создаем ярлык на Рабочем столе
                    if (chkDesktop.Checked)
                    {
                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        CreateShortcut(Path.Combine(desktop, "Antigravity Proxy.lnk"), targetExe, targetDir, icoPath, "Antigravity Proxy");
                    }

                    // 7. Создаем ярлык в Меню «Пуск»
                    if (chkStartMenu.Checked)
                    {
                        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        CreateShortcut(Path.Combine(programs, "Antigravity Proxy.lnk"), targetExe, targetDir, icoPath, "Antigravity Proxy");
                    }

                    // 8. Закрепление на панели задач
                    if (chkTaskbar.Checked)
                    {
                        try
                        {
                            // A. Создаем ярлык в папке User Pinned\TaskBar
                            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                            string taskbarDir = Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
                            if (!Directory.Exists(taskbarDir)) Directory.CreateDirectory(taskbarDir);
                            string taskbarLnk = Path.Combine(taskbarDir, "Antigravity Proxy.lnk");
                            CreateShortcut(taskbarLnk, targetExe, targetDir, icoPath, "Antigravity Proxy");

                            // B. Вызываем Shell verb для надежного закрепления
                            PinToTaskbar(targetExe);
                        }
                        catch { }
                    }

                    // 9. Регистрация в реестре Windows (Установка и удаление программ)
                    RegisterUninstall(targetDir, targetExe, icoPath);

                    return true;
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show("Ошибка во время установки: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                    return false;
                }
            });

            prgBar.Value = 100;
            if (success)
            {
                lblStatus.Text = "Установка успешно завершена!";
                lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
                btnInstall.Text = "Готово";
                btnInstall.Enabled = true;
                btnInstall.Click += (s, e) => Close();

                if (chkRunAfter.Checked)
                {
                    try
                    {
                        string exe = Path.Combine(targetDir, "AntigravityProxy.exe");
                        if (File.Exists(exe)) Process.Start(exe);
                    }
                    catch { }
                }

                MessageBox.Show("Antigravity Proxy успешно установлен!\r\nЯрлыки добавлены в меню Пуск и на рабочий стол.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            else
            {
                lblStatus.Text = "Ошибка установки.";
                lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
                btnInstall.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        private static void CreateShortcut(string lnkPath, string targetExe, string workDir, string iconPath, string desc)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workDir;
                shortcut.Arguments = "";
                shortcut.Description = desc;
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath + ",0";
                }
                shortcut.Save();
            }
            catch { }
        }

        private static void PinToTaskbar(string targetExe)
        {
            try
            {
                // Запуск PowerShell скрипта с Shell.Application для вызова глагола taskbarpin
                string script = string.Format(
                    "$s = New-Object -ComObject Shell.Application; " +
                    "$f = $s.Namespace('{0}'); " +
                    "$item = $f.ParseName('{1}'); " +
                    "$verb = $item.Verbs() | Where-Object {{ $_.Name.replace('&','') -match 'TaskbarPin|Pin to taskbar|Закрепить на панели задач' }}; " +
                    "if ($verb) {{ $verb.DoIt() }}",
                    Path.GetDirectoryName(targetExe),
                    Path.GetFileName(targetExe)
                );

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi)) p.WaitForExit(3000);
            }
            catch { }
        }

        private static void RegisterUninstall(string installDir, string targetExe, string iconPath)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AntigravityProxy";
                using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string uninstallExe = Path.Combine(installDir, "Uninstall.exe");
                        key.SetValue("DisplayName", "Antigravity Proxy");
                        key.SetValue("DisplayVersion", "1.2.0");
                        key.SetValue("Publisher", "Antigravity");
                        key.SetValue("DisplayIcon", iconPath);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", string.Format("\"{0}\"", uninstallExe));
                        key.SetValue("QuietUninstallString", string.Format("\"{0}\" /quiet", uninstallExe));
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
