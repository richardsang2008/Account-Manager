using Newtonsoft.Json;
using PokemonGoGUI.AccountScheduler;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager;
using PokemonGoGUI.GoManager.Models;
using PokemonGoGUI.Models;
using PokemonGoGUI.ProxyManager;
using PokemonGoGUI.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using PokemonGoGUI.Models.CommandLineUtility;

namespace PokemonGoGUI
{
    public partial class MainForm : Form
    {
        private List<Manager> _managers = new List<Manager>();
        private ProxyHandler _proxyHandler = new ProxyHandler();
        private List<Scheduler> _schedulers = new List<Scheduler>();
        private List<HashKey> _hashKeys = new List<HashKey>();
        private bool _spf = false;
        private bool _showStartup = true;
        private bool _autoupdate = true;
        private readonly string _saveFile = "data";
        private string _versionNumber = $"v{Assembly.GetExecutingAssembly().GetName().Version} - Forked GoManager Version";
        private static string[] _args;

        public MainForm(string[] args)
        {
            _args = args;
            InitializeComponent();

            fastObjectListViewMain.BackColor = Color.FromArgb(0, 0, 0);
            fastObjectListViewMain.ForeColor = Color.LightGray;

            fastObjectListViewProxies.BackColor = Color.FromArgb(0, 0, 0);
            fastObjectListViewProxies.ForeColor = Color.LightGray;

            fastObjectListViewScheduler.BackColor = Color.FromArgb(0, 0, 0);
            fastObjectListViewScheduler.ForeColor = Color.LightGray;

            fastObjectListViewHashKeys.BackColor = Color.FromArgb(0, 0, 0);
            fastObjectListViewHashKeys.ForeColor = Color.LightGray;

            //BackColor = Color.FromArgb(43, 43, 43);

            //tabPage1.BorderStyle = BorderStyle.None;
            //tabPage1.BackColor = Color.FromArgb(43, 43, 43);
            //fastOjectListViewMain.AlwaysGroupByColumn = olvColumnGroup;

            Text = "Account Manager - " + _versionNumber;

            olvColumnProxyAuth.AspectGetter = delegate (object x)
            {
                var proxy = (GoProxy)x;

                if (String.IsNullOrEmpty(proxy.Username) || String.IsNullOrEmpty(proxy.Password))
                {
                    return String.Empty;
                }

                return String.Format("{0}:{1}", proxy.Username, proxy.Password);
            };

            olvColumnCurrentFails.AspectGetter = delegate (object x)
            {
                var proxy = (GoProxy)x;

                return String.Format("{0}/{1}", proxy.CurrentConcurrentFails, proxy.MaxConcurrentFails);
            };


            olvColumnUsageCount.AspectGetter = delegate (object x)
            {
                var proxy = (GoProxy)x;

                return String.Format("{0}/{1}", proxy.CurrentAccounts, proxy.MaxAccounts);
            };
        }

        private void ShowDetails(IEnumerable<Manager> managers)
        {
            int count = fastObjectListViewMain.SelectedObjects.Count;

            if (count > 1)
            {
                DialogResult result = MessageBox.Show(String.Format("Are you sure you want to open {0} detail forms?", count), "Confirmation", MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            foreach (Manager manager in managers)
            {
                if (manager == null)
                    continue;

                var checkWindow = new ConsoleHelper();
                var window = checkWindow.FindWindowByCaption(manager.AccountName);

                if (window == IntPtr.Zero)
                {
                    var dForm = new DetailsForm(manager);
                    dForm.Show();
                }
                else
                {
                    checkWindow.ShowWindow(window);
                }
            }
        }

        private void FastObjectListViewMain_DoubleClick(object sender, EventArgs e)
        {
            ShowDetails(fastObjectListViewMain.SelectedObjects.Cast<Manager>());
        }

        private void RefreshManager(Manager manager)
        {
            //Update Main
            if (!manager.IsRunning)
                return;

            if (fastObjectListViewMain.IsDisposed || fastObjectListViewMain.Disposing)
            {
                return;
            }

            fastObjectListViewMain.RefreshObject(manager);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            if (_args.Count() > 0)
            {
                // Command line parsing
                var commandLine = new Arguments(_args);
                // Look for specific arguments values
                if (commandLine["import"] != null && commandLine["import"].Length > 0)
                {
                    //Open file commandLine["import"]
                    //
                }
                if (commandLine["config"] != null && commandLine["config"].Length > 0)
                {
                    //Open file commandLine["config"]
                    //
                }
            }
            //else 
            // 

            //
            await LoadSettings();

            if (_autoupdate)
            {
                bool IsLatest = await VersionCheckState.IsLatest();
                if (!IsLatest)
                    await VersionCheckState.Execute();
            }

            await VersionCheckState.CleanupOldFiles();

            //TODO: need review
            //var plugins = new PluginsEx();

            //await plugins.LoadPlugins();

            if (_showStartup)
            {
                var startForm = new StartupForm
                {
                    ShowOnStartUp = _showStartup
                };

                if (startForm.ShowDialog() == DialogResult.OK)
                {
                    _showStartup = startForm.ShowOnStartUp;
                }
            }
            UpdateStatusBar();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_managers.Any(x => x.IsRunning))
            {
                MessageBox.Show("Please stop bots before closing", "Information");

                e.Cancel = true;
            }

            SaveSettings();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            Trayicon.Visible = false;
            Trayicon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (FormWindowState.Minimized == this.WindowState)
            {
                Trayicon.BalloonTipIcon = ToolTipIcon.Info; //Shows the info icon so the user doesn't thing there is an error.
                Trayicon.BalloonTipTitle = $"Account Manager is minimized";
                Trayicon.BalloonTipText = "Click on this icon to restore";
                Trayicon.Text = $"Account Manager, Click here to restore";
                Trayicon.Visible = true;
                Trayicon.ShowBalloonTip(5000);
                Hide();
            }
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Refresh();
        }

        private async Task<bool> LoadSettings()
        {
            var gzipFile = _saveFile + ".json.gz";

            try
            {
                if (!File.Exists(gzipFile))
                    return false;

                var tempManagers = new List<Manager>();
                var tempHashKeys = new List<HashKey>();

                byte[] byteData = await Task.Run(() => File.ReadAllBytes(gzipFile));
                string data = Compression.Unzip(byteData);

                ProgramExportModel model = Serializer.FromJson<ProgramExportModel>(data);

                if (model.ProxyHandler != null)
                    _proxyHandler = model.ProxyHandler;
                if (model.Managers != null)
                    tempManagers = model.Managers;
                if (model.Schedulers != null)
                    _schedulers = model.Schedulers;
                if (model.HashKeys != null)
                    tempHashKeys = model.HashKeys;
                _spf = model.SPF;
                _showStartup = model.ShowWelcomeMessage;
                _autoupdate = model.AutoUpdate;

                foreach (Manager manager in tempManagers)
                {
                    manager.AddSchedulerEvent();
                    manager.ProxyHandler = _proxyHandler;
                    manager.OnLog += Manager_OnLog;

                    //Patch for version upgrade
                    if (String.IsNullOrEmpty(manager.UserSettings.DeviceId))
                    {
                        //Load some
                        manager.UserSettings.RandomizeDevice();
                    }

                    if (manager.Tracker != null)
                    {
                        manager.Tracker.CalculatedTrackingHours();
                    }

                    if (manager.AccountState == AccountState.Conecting || manager.AccountState == AccountState.HashIssues)
                    {
                        manager.AccountState = AccountState.Good;
                    }

                    _managers.Add(manager);
                }

                foreach (HashKey key in tempHashKeys)
                {
                    HashKey tested = TestHashKey(key);
                    if (!tested.IsValide)
                        MessageBox.Show("HashKey " + tested.Key + " :" + tested.KeyInfo + ", Please remove this key of HashKeys tab.");

                    _hashKeys.Add(tested);
                }

                fastObjectListViewMain.SetObjects(_managers);
                fastObjectListViewHashKeys.SetObjects(_hashKeys);
            }
            catch (Exception ex1)
            {
                MessageBox.Show("Failed to load settings\nReason: " + ex1.Message);
                //Failed to load settings
            }

            return true;
        }

        private void SaveSettings()
        {
            try
            {
                var model = new ProgramExportModel
                {
                    Managers = _managers,
                    ProxyHandler = _proxyHandler,
                    Schedulers = _schedulers,
                    HashKeys = _hashKeys,
                    SPF = _spf,
                    ShowWelcomeMessage = _showStartup,
                    AutoUpdate = _autoupdate,
                };

                string data = Serializer.ToJson(model);

                byte[] dataBytes = Compression.Zip(data);

                File.WriteAllBytes(_saveFile + ".json.gz", dataBytes);
            }
            catch
            {
                //Failed to save
            }
        }

        /*
        private void Manager_OnInventoryUpdate(object sender, EventArgs e)
        {
            var manager = sender as Manager;

            if (manager == null)
            {
                return;
            }

            RefreshManager(manager);
        }
        */

        private void Manager_OnLog(object sender, LoggerEventArgs e)
        {
            var manager = sender as Manager;

            if (manager == null)
            {
                return;
            }

            RefreshManager(manager);
        }

        private void AddNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var manager = new Manager(_proxyHandler);

            var asForm = new AccountSettingsForm(manager)
            {
                AutoUpdate = _autoupdate
            };

            if (asForm.ShowDialog() == DialogResult.OK)
            {
                _autoupdate = asForm.AutoUpdate;
                AddManager(manager);
            }

            fastObjectListViewMain.SetObjects(_managers);
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int totalAccounts = fastObjectListViewMain.SelectedObjects.Count;

            if (totalAccounts == 0)
            {
                return;
            }

            DialogResult dResult = MessageBox.Show(String.Format("Delete {0} accounts?", totalAccounts), "Are you sure?", MessageBoxButtons.YesNoCancel);

            if (dResult != DialogResult.Yes)
            {
                return;
            }

            bool messageShown = false;

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                if (manager.IsRunning && !messageShown)
                {
                    messageShown = true;

                    MessageBox.Show("Only accounts that are not running will be deleted");
                }

                if (!manager.IsRunning)
                {
                    manager.Stop();
                    manager.RemoveScheduler();

                    manager.OnLog -= Manager_OnLog;

                    _managers.Remove(manager);
                }
            }

            fastObjectListViewMain.SetObjects(_managers);
        }

        private void EditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int count = fastObjectListViewMain.SelectedObjects.Count;

            if (count > 1)
            {
                DialogResult result = MessageBox.Show(String.Format("Are you sure you want to open {0} edit forms?", count), "Confirmation", MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                var asForm = new AccountSettingsForm(manager)
                {
                    AutoUpdate = _autoupdate
                };

                if (asForm.ShowDialog() == DialogResult.OK)
                {
                    _autoupdate = asForm.AutoUpdate;
                }
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void FastObjectListViewMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Alt && e.KeyCode == Keys.U)
            {
                DialogResult result = MessageBox.Show("Show developer tools?", "Confirmation", MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    devToolsToolStripMenuItem.Visible = true;
                }
            }

            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            ShowDetails(fastObjectListViewMain.SelectedObjects.Cast<Manager>());

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void ViewDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowDetails(fastObjectListViewMain.SelectedObjects.Cast<Manager>());
        }

        private async void StartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            startToolStripMenuItem.Enabled = false;

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.HashKeys = _hashKeys.Select(x => x.Key).ToList();
                manager.UserSettings.SPF = _spf;
                manager.Start();

                await Task.Delay(200);
            }

            startToolStripMenuItem.Enabled = true;

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void StopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.Stop();
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private List<string> ImportAccounts()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Open account file";
                ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    return File.ReadAllLines(ofd.FileName).ToList();
                }
            }

            return new List<string>();
        }

        private string ImportConfig()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Open config file";
                ofd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    return ofd.FileName;
                }
            }

            return String.Empty;
        }

        private void AddManager(Manager manager)
        {
            if (_managers.Contains(manager))
                return;

            manager.OnLog += Manager_OnLog;

            _managers.Add(manager);
        }

        private string GetSaveFileName()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Save File";
                sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                return sfd.ShowDialog() == DialogResult.OK ? sfd.FileName : String.Empty;

            }
        }

        private void UpdateStatusBar()
        {
            toolStripStatusLabelTotalAccounts.Text = _managers.Count.ToString();

            //Longer running
            int tempBanned = 0;
            int running = 0;
            int permBan = 0;
            int flags = 0;
            int captcha = 0;

            var tempManagers = new List<Manager>(_managers);

            foreach (Manager manager in tempManagers)
            {
                if (manager.IsRunning)
                {
                    ++running;
                }

                if (manager.AccountState == AccountState.PermanentBan)
                {
                    ++permBan;
                }

                if (manager.AccountState == AccountState.Flagged)
                {
                    ++flags;
                }

                if (manager.AccountState == AccountState.CaptchaReceived)
                {
                    ++captcha;
                }

                if (manager.AccountState == AccountState.SoftBan)
                {
                    ++tempBanned;
                }

                if (manager.AccountState == AccountState.TemporalBan)
                {
                    ++tempBanned;
                }
            }

            toolStripStatusLabelAccountBanned.Text = permBan.ToString();
            toolStripStatusLabelTempBanned.Text = tempBanned.ToString();
            toolStripStatusLabelTotalRunning.Text = running.ToString();
            toolStripStatusLabelFlagged.Text = flags.ToString();
            toolStripStatusLabelCaptcha.Text = captcha.ToString();

            if (_proxyHandler?.Proxies != null)
            {
                toolStripStatusLabelTotalProxies.Text = _proxyHandler.Proxies.Count.ToString();
                toolStripStatusLabelBannedProxies.Text = _proxyHandler.Proxies.Count(x => x.IsBanned).ToString();
            }
        }

        internal void AddAccount(string username, string password)
        {
            List<Manager> managers = new List<Manager>(_managers);


            Manager newAccount = new Manager();  // Copy the first manager's settings to get all settings instead of pulling in manually 
            newAccount.UserSettings = managers.ElementAt(0).UserSettings.DeepClone();
            newAccount.RandomDeviceId();
            newAccount.UserSettings.AccountName = username.Trim();
            newAccount.UserSettings.Username = username.Trim();
            newAccount.UserSettings.Password = password.Trim();

            newAccount.UserSettings.AuthType = AuthType.Ptc;
            newAccount.UserSettings.MaxLevel = 30;
            newAccount.Level = 0;
            newAccount.ExpGained = 0;
            newAccount.PokestopsFarmed = 0;
            newAccount.PokemonCaught = 0;


            AddManager(newAccount);
            

            fastObjectListViewMain.SetObjects(_managers);
        }

        private async void WConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tSMI = sender as ToolStripMenuItem;

            bool useConfig;
            if (tSMI == null || !Boolean.TryParse(tSMI.Tag.ToString(), out useConfig))
            {
                return;
            }

            try
            {
                List<string> accounts = ImportAccounts();
                string configFile = String.Empty;

                if (useConfig)
                {
                    configFile = ImportConfig();
                }

                var tempManagers = new HashSet<Manager>(_managers);

                if (useConfig && String.IsNullOrEmpty(configFile))
                {
                    return;
                }

                int totalSuccess = 0;
                int total = accounts.Count;

                foreach (string account in accounts)
                {
                    string[] parts = account.Split(':');

                    /*
                     * User:Pass = 2
                     * User:Pass:MaxLevel = 3
                     * User:Pass:IP:Port = 4
                     * User:Pass:IP:Port:MaxLevel = 5
                     * User:Pass:IP:Port:pUsername:pPassword = 6
                     * User:Pass:IP:Port:pUsername:pPassword:MaxLevel = 7
                     */
                    if (parts.Length < 2 || parts.Length > 7)
                    {
                        continue;
                    }

                    var importModel = new AccountImport();

                    if (!importModel.ParseAccount(account))
                    {
                        continue;
                    }

                    var manager = new Manager(_proxyHandler);

                    if (useConfig)
                    {
                        MethodResult result = await manager.ImportConfigFromFile(configFile);

                        if (!result.Success)
                        {
                            MessageBox.Show("Failed to import configuration file");

                            return;
                        }
                    }
                    //Randomize device id;
                    manager.RandomDeviceId();
                    manager.UserSettings.AccountName = importModel.Username.Trim();
                    manager.UserSettings.Username = importModel.Username.Trim();
                    manager.UserSettings.Password = importModel.Password.Trim();
                    manager.UserSettings.ProxyIP = importModel.Address;
                    manager.UserSettings.ProxyPort = importModel.Port;
                    manager.UserSettings.ProxyUsername = importModel.ProxyUsername;
                    manager.UserSettings.ProxyPassword = importModel.ProxyPassword;

                    manager.UserSettings.AuthType = importModel.Username.Contains("@") ? AuthType.Google : AuthType.Ptc;

                    if (parts.Length % 2 == 1)
                    {
                        manager.UserSettings.MaxLevel = importModel.MaxLevel;
                    }

                    if (tempManagers.Add(manager))
                    {
                        AddManager(manager);
                        ++totalSuccess;
                    }
                }

                fastObjectListViewMain.SetObjects(_managers);

                MessageBox.Show(String.Format("Successfully imported {0} out of {1} accounts", totalSuccess, total));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to import usernames. Ex: {0}", ex.Message));
            }
        }

        private void TimerUpdate_Tick(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            if (statusStripStats.Visible)
                UpdateStatusBar();

            if (tabControlMain.SelectedTab == tabPageAccounts)
            {
                if (_managers.Count == 0)
                {
                    return;
                }

                fastObjectListViewMain.RefreshObject(_managers[0]);
            }
            else if (tabControlMain.SelectedTab == tabPageProxies)
            {
                if (_proxyHandler.Proxies.Count == 0)
                {
                    return;
                }

                fastObjectListViewProxies.RefreshObject(_proxyHandler.Proxies.First());
            }
            else if (tabControlMain.SelectedTab == tabPageScheduler)
            {
                if (_schedulers.Count == 0)
                {
                    return;
                }

                fastObjectListViewScheduler.RefreshObject(_schedulers[0]);
            }
            else if (tabControlMain.SelectedTab == tabPageHashKeys)
            {
                if (_hashKeys.Count == 0)
                {
                    return;
                }

                fastObjectListViewHashKeys.RefreshObject(_hashKeys[0]);
            }
        }

        private void ClearProxiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int totalAccounts = fastObjectListViewMain.SelectedObjects.Count;

            DialogResult result = MessageBox.Show(String.Format("Clear proxies from {0} accounts?", totalAccounts), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.RemoveProxy();
                manager.UserSettings.ProxyIP = null;
                manager.UserSettings.ProxyPort = 0;
                manager.UserSettings.ProxyUsername = null;
                manager.UserSettings.ProxyPassword = null;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            enableColorsToolStripMenuItem.Checked = !enableColorsToolStripMenuItem.Checked;
            bool isChecked = enableColorsToolStripMenuItem.Checked;

            fastObjectListViewMain.UseCellFormatEvents = isChecked;
            fastObjectListViewScheduler.UseCellFormatEvents = isChecked;
            fastObjectListViewProxies.UseCellFormatEvents = isChecked;
            fastObjectListViewHashKeys.UseCellFormatEvents = isChecked;

            if (isChecked)
            {
                fastObjectListViewMain.BackColor = Color.FromArgb(0, 0, 0);
                fastObjectListViewMain.ForeColor = Color.LightGray;

                fastObjectListViewProxies.BackColor = Color.FromArgb(0, 0, 0);
                fastObjectListViewProxies.ForeColor = Color.LightGray;

                fastObjectListViewScheduler.BackColor = Color.FromArgb(0, 0, 0);
                fastObjectListViewScheduler.ForeColor = Color.LightGray;

                fastObjectListViewHashKeys.BackColor = Color.FromArgb(0, 0, 0);
                fastObjectListViewHashKeys.ForeColor = Color.LightGray;
            }
            else
            {
                fastObjectListViewMain.BackColor = SystemColors.Window;
                fastObjectListViewMain.ForeColor = SystemColors.WindowText;

                fastObjectListViewProxies.BackColor = SystemColors.Window;
                fastObjectListViewProxies.ForeColor = SystemColors.WindowText;

                fastObjectListViewScheduler.BackColor = SystemColors.Window;
                fastObjectListViewScheduler.ForeColor = SystemColors.WindowText;

                fastObjectListViewHashKeys.BackColor = SystemColors.Window;
                fastObjectListViewHashKeys.ForeColor = SystemColors.WindowText;
            }
        }

        private void ShowGroupsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showGroupsToolStripMenuItem.Checked = !showGroupsToolStripMenuItem.Checked;
        }

        private void FastObjectListViewMain_FormatCell(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            var manager = (Manager)e.Model;

            if (manager == null)
                return;

            if (e.Column == olvColumnScheduler)
            {
                if (manager.AccountScheduler != null)
                {
                    e.SubItem.ForeColor = manager.AccountScheduler.NameColor;
                }
            }
            else if (e.Column == olvColumnUsername)
            {
                if (manager.LuckyEggActive)
                {
                    e.SubItem.ForeColor = Color.Gold;
                }
            }
            else if (e.Column == olvColumnExpPerHour)
            {
                int ExpPerHour = manager.ExpPerHour;

                if (ExpPerHour >= 80000)
                {
                    if (ExpPerHour >= 2000000)
                    {
                        e.SubItem.ForeColor = Color.Red;
                    }
                    else if(ExpPerHour >= 1500000)
                    {
                        e.SubItem.ForeColor = Color.Yellow;
                    }
                    else if (ExpPerHour >= 1000000)
                    {
                        e.SubItem.ForeColor = Color.Orange;
                    }
                    else if (ExpPerHour >= 900000)
                    {
                        e.SubItem.ForeColor = Color.Goldenrod;
                    }
                    else if (ExpPerHour >= 750000)
                    {
                        e.SubItem.ForeColor = Color.LightSkyBlue;
                    }
                    else if (ExpPerHour >= 400000)
                    {
                        e.SubItem.ForeColor = Color.LightGreen;
                    }
                    else if (ExpPerHour >= 200000)
                    {
                        e.SubItem.ForeColor = Color.Green;
                    }
                }
            }
            else if (e.Column == olvColumnAccountState)
            {
                switch (manager.AccountState)
                {
                    case AccountState.PermanentBan:
                        e.SubItem.ForeColor = Color.Red;
                        break;
                    case AccountState.NotVerified:
                        e.SubItem.ForeColor = Color.Red;
                        break;
                    case AccountState.SoftBan:
                        e.SubItem.ForeColor = Color.Yellow;
                        break;
                    case AccountState.Good:
                        e.SubItem.ForeColor = Color.Green;
                        break;
                    case AccountState.Flagged:
                        e.SubItem.ForeColor = Color.Magenta;
                        break;
                    case AccountState.CaptchaReceived:
                        e.SubItem.ForeColor = Color.Tomato;
                        break;
                    case AccountState.Conecting:
                        e.SubItem.ForeColor = Color.Blue;
                        break;
                    case AccountState.HashIssues:
                        e.SubItem.ForeColor = Color.Coral;
                        break;
                    case AccountState.Unknown:
                        e.SubItem.ForeColor = Color.Cyan;
                        break;
                    case AccountState.TemporalBan:
                        e.SubItem.ForeColor = Color.Yellow;
                        break;
                }
            }
            else if (e.Column == olvColumnPokemonCaught)
            {
                if (manager.Tracker.PokemonCaught + 100 >= manager.UserSettings.CatchPokemonDayLimit)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                if (manager.Tracker.PokemonCaught >= manager.UserSettings.CatchPokemonDayLimit)
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
            else if (e.Column == olvColumnPokestopsFarmed)
            {
                if (manager.Tracker.PokestopsFarmed + 100 >= manager.UserSettings.SpinPokestopsDayLimit)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                if (manager.Tracker.PokestopsFarmed >= manager.UserSettings.SpinPokestopsDayLimit)
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
            else if (e.Column == olvColumnBotState)
            {
                switch (manager.State)
                {
                    case BotState.Running:
                        e.SubItem.ForeColor = Color.LightGreen;
                        break;
                    case BotState.Starting:
                        e.SubItem.ForeColor = Color.Aqua;
                        break;
                    case BotState.Stopping:
                        e.SubItem.ForeColor = Color.Orange;
                        break;
                    case BotState.Stopped:
                        e.SubItem.ForeColor = Color.Red;
                        break;
                    case BotState.Paused:
                        e.SubItem.ForeColor = Color.Yellow;
                        break;
                    case BotState.Pausing:
                        e.SubItem.ForeColor = Color.YellowGreen;
                        break;
                }
            }
            else if (e.Column == olvColumnLastLogMessage)
            {
                Log log = manager.Logs.LastOrDefault();

                if (log == null)
                {
                    return;
                }

                e.SubItem.ForeColor = log.GetLogColor();
            }
            else if (e.Column == olvColumnPokemonCaught)
            {
                if (manager.AccountScheduler == null || manager.AccountScheduler.PokemonLimiter.Option == SchedulerOption.Nothing)
                {
                    return;
                }

                if (manager.PokemonCaught >= manager.AccountScheduler.PokemonLimiter.Max)
                {
                    e.SubItem.ForeColor = Color.Red;
                }
                else if (manager.PokemonCaught <= manager.AccountScheduler.PokemonLimiter.Min)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }

            }
            else if (e.Column == olvColumnPokestopsFarmed)
            {
                if (manager.AccountScheduler == null || manager.AccountScheduler.PokeStoplimiter.Option == SchedulerOption.Nothing)
                {
                    return;
                }

                if (manager.PokestopsFarmed >= manager.AccountScheduler.PokeStoplimiter.Max)
                {
                    e.SubItem.ForeColor = Color.Red;
                }
                else if (manager.PokestopsFarmed <= manager.AccountScheduler.PokeStoplimiter.Min)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
            }
        }

        private void GarbageCollectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("This should not be called outside testing purposes. Continue?", "Confirmation", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                GC.Collect();
            }
        }

        private void ExportStatsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            };

            IEnumerable<Manager> managers = fastObjectListViewMain.SelectedObjects.Cast<Manager>();

            Task.Run(() =>
                 {
                     Parallel.ForEach(managers, options, (manager) =>
                     {
                         manager.ExportStats().Wait();
                     });
                 });
        }

        private void UpdateDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            updateDetailsToolStripMenuItem.Enabled = false;

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            };

            IEnumerable<Manager> selectedManager = fastObjectListViewMain.SelectedObjects.Cast<Manager>();

            Task.Run(() =>
                {
                    Parallel.ForEach(selectedManager, options, (manager) =>
                    {
                        manager.UpdateDetails().Wait();
                    });
                });

            updateDetailsToolStripMenuItem.Enabled = true;
        }

        private void ImportProxiesToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int count = fastObjectListViewMain.SelectedObjects.Count;
            string fileName = String.Empty;

            if (count == 0)
            {
                MessageBox.Show("Please select 1 or more accounts");
                return;
            }

            string pPerAccount = Prompt.ShowDialog("Accounts per proxy", "Accounts per proxy", "1");

            if (String.IsNullOrEmpty(pPerAccount))
            {
                return;
            }

            int accountsPerProxy;
            if (!Int32.TryParse(pPerAccount, out accountsPerProxy) || accountsPerProxy <= 0)
            {
                MessageBox.Show("Invalid input");

                return;
            }

            if (count == 0)
            {
                return;
            }

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Open proxy file";
                ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    fileName = ofd.FileName;
                }
            }

            if (String.IsNullOrEmpty(fileName))
            {
                return;
            }

            var proxies = new List<ProxyEx>();

            try
            {
                string[] tempProxies = File.ReadAllLines(fileName);
                ProxyEx tempProxyEx = null;

                foreach (string proxyEx in tempProxies)
                {
                    if (ProxyEx.TryParse(proxyEx, out tempProxyEx))
                    {
                        proxies.Add(tempProxyEx);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to import proxy file. Ex: {0}", ex.Message));
                return;
            }

            if (proxies.Count == 0)
            {
                MessageBox.Show("No proxies found");
                return;
            }

            int proxyIndex = 0;
            int proxyUsage = 0;

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                ++proxyUsage;

                if (proxyUsage > accountsPerProxy)
                {
                    ++proxyIndex;
                    proxyUsage = 1;

                    if (proxyIndex >= proxies.Count)
                    {
                        MessageBox.Show("Out of proxies");
                        return;
                    }
                }

                ProxyEx proxy = proxies[proxyIndex];

                manager.UserSettings.ProxyIP = proxy.Address;
                manager.UserSettings.ProxyPort = proxy.Port;
                manager.UserSettings.ProxyUsername = proxy.Username;
                manager.UserSettings.ProxyPassword = proxy.Password;
            }
        }

        private void ExportAccountsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (fastObjectListViewMain.SelectedObjects.Count == 0)
            {
                return;
            }

            string filename = GetSaveFileName();

            if (String.IsNullOrEmpty(filename))
            {
                return;
            }

            try
            {
                IEnumerable<string> accounts = fastObjectListViewMain.SelectedObjects.Cast<Manager>().Select(x => String.Format("{0}:{1}", x.UserSettings.Username, x.UserSettings.Password));

                File.WriteAllLines(filename, accounts);

                MessageBox.Show(String.Format("Exported {0} accounts", accounts.Count()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to export accounts. Ex: {0}", ex.Message));
            }
        }

        private void ExportProxiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fastObjectListViewMain.SelectedObjects.Count == 0)
            {
                return;
            }

            string filename = GetSaveFileName();

            if (String.IsNullOrEmpty(filename))
            {
                return;
            }

            try
            {
                IEnumerable<string> proxies = fastObjectListViewMain.SelectedObjects.Cast<Manager>().Select(x => x.Proxy.ToString());

                File.WriteAllLines(filename, proxies);

                MessageBox.Show(String.Format("Exported {0} proxies", proxies.Count()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to export proxies. Ex: {0}", ex.Message));
            }
        }

        private void ClearCountsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("This will reset your last 23 hour count and is updated to accurately reflect your pokestops + pokemon counts.\n\nAre you sure you want to clear this?", "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.ClearStats();
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void LogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.ClearLog();
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void PauseUnPauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.TogglePause();
            }
        }

        private async void RestartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.Restart();

                await Task.Delay(100);
            }
        }

        #region Fast Settings

        private void ClaimLevelUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.ClaimLevelUpRewards = !claimLevelUpToolStripMenuItem.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableIPBanStopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.StopOnIPBan = !enableIPBanStopToolStripMenuItem.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableRotateProxiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.AutoRotateProxies = !enableRotateProxiesToolStripMenuItem.Checked;

                if (!manager.UserSettings.AutoRotateProxies)
                {
                    manager.UserSettings.ProxyIP = String.Empty;
                    manager.UserSettings.ProxyPort = 0;
                }
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetMaxRuntimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Max Runtime (hours)", "Set Max Runtime").Replace(",", ".");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            double value;
            if (!Double.TryParse(data, NumberStyles.Any, CultureInfo.InvariantCulture, out value) || value < 0)
            {
                MessageBox.Show("Invalid runtime value");
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.RunForHours = value;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Group name", "Set group name", "Default");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.GroupName = data;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetMaxLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Max Level:", "Set Max Level");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int level;
            if (!Int32.TryParse(data, out level) || level < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.MaxLevel = level;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void ContextMenuStripAccounts_Opening(object sender, CancelEventArgs e)
        {
            enableSpoofToolStripMenuItem.Checked = _spf;

            Manager manager = fastObjectListViewMain.SelectedObjects.Cast<Manager>().FirstOrDefault();

            if (manager == null)
            {
                return;
            }

            enableTransferToolStripMenuItem.Checked = manager.UserSettings.TransferPokemon;
            enableEvolveToolStripMenuItem1.Checked = manager.UserSettings.EvolvePokemon;
            enableRecycleToolStripMenuItem4.Checked = manager.UserSettings.RecycleItems;
            enableIncubateEggsToolStripMenuItem5.Checked = manager.UserSettings.IncubateEggs;
            enableLuckyEggsToolStripMenuItem6.Checked = manager.UserSettings.UseLuckyEgg;
            enableCatchPokemonToolStripMenuItem2.Checked = manager.UserSettings.CatchPokemon;
            enableRotateProxiesToolStripMenuItem.Checked = manager.UserSettings.AutoRotateProxies;
            enableIPBanStopToolStripMenuItem.Checked = manager.UserSettings.StopOnIPBan;
            claimLevelUpToolStripMenuItem.Checked = manager.UserSettings.ClaimLevelUpRewards;

            //Remove all
            schedulerToolStripMenuItem.DropDownItems.Clear();

            //Add none
            var noneSMI = new ToolStripMenuItem("None");
            noneSMI.Click += (o, s) =>
                {
                    foreach (Manager m in fastObjectListViewMain.SelectedObjects)
                    {
                        m.RemoveScheduler();
                    }
                };

            schedulerToolStripMenuItem.DropDownItems.Add(noneSMI);

            //Add all current schedulers
            if (_schedulers != null)
            {
                foreach (Scheduler scheduler in _schedulers)
                {
                    var tSMI = new ToolStripMenuItem(scheduler.Name)
                    {
                        Tag = scheduler
                    };
                    tSMI.Click += Schedule_Click;

                    schedulerToolStripMenuItem.DropDownItems.Add(tSMI);
                }
            }
        }

        private void Schedule_Click(object sender, EventArgs e)
        {
            var tSMI = sender as ToolStripMenuItem;

            if (tSMI == null)
            {
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.AddScheduler((Scheduler)tSMI.Tag);
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableTransferToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.TransferPokemon = !enableTransferToolStripMenuItem.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableEvolveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.EvolvePokemon = !enableEvolveToolStripMenuItem1.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableRecycleToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.RecycleItems = !enableRecycleToolStripMenuItem4.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableIncubateEggsToolStripMenuItem5_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.IncubateEggs = !enableIncubateEggsToolStripMenuItem5.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableLuckyEggsToolStripMenuItem6_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.UseLuckyEgg = !enableLuckyEggsToolStripMenuItem6.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void EnableCatchPokemonToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.CatchPokemon = !enableCatchPokemonToolStripMenuItem2.Checked;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetRequiredPokemonToolStripMenuItem_Click(object sender, EventArgs e)
        {

            string data = Prompt.ShowDialog("Evolvable pokemon required to evolve:", "Set Min Pokemon Before Evolve");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value >= 500 || value < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
            {
                manager.UserSettings.MinPokemonBeforeEvolve = value;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetPokestopRateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Snipe after pokestops amount:", "Set Pokestop Rate");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value >= 1000 || value < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetMinBallsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Minimum balls required to snipe:", "Set Minimum Balls");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value >= 1000 || value < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void SetMaxPokemonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Total pokemon per snipe:", "Set Maximum Pokemon To Snipe");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value >= 500 || value < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        private void AfterLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Snipe after level:", "Set Snipe After Level");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value >= 40 || value < 0)
            {
                MessageBox.Show("Invalid value");
                return;
            }

            fastObjectListViewMain.RefreshSelectedObjects();
        }

        #endregion

        private void ExportJsonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileName = String.Empty;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    fileName = sfd.FileName;
                }
                else
                {
                    return;
                }
            }

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            };

            IEnumerable<Manager> selectedManagers = fastObjectListViewMain.SelectedObjects.Cast<Manager>();
            var exportModels = new List<Manager>();

            foreach (Manager manager in selectedManagers)
            {
                exportModels.Add(manager);
            }

            try
            {
                string data = JsonConvert.SerializeObject(exportModels, Formatting.None);

                File.WriteAllText(fileName, data);

                MessageBox.Show(String.Format("Successfully exported {0} of {1} accounts", exportModels.Count, fastObjectListViewMain.SelectedObjects.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to save to file. Ex: {0}", ex.Message));
            }
        }

        private void ShowStatusBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            bool showGroups = !statusStripStats.Visible;

            statusStripStats.Visible = showGroups;

            const int scrollBarHeight = 38;

            if (showGroups)
            {
                UpdateStatusBar();
                fastObjectListViewMain.Height = this.Height - statusStripStats.Height - scrollBarHeight;
            }
            else
            {
                fastObjectListViewMain.Height = this.Height - scrollBarHeight;
            }
        }

        private void ImportConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string configFile = ImportConfig();

            if (String.IsNullOrEmpty(configFile))
            {
                return;
            }

            try
            {
                string data = File.ReadAllText(configFile);

                foreach (Manager manager in fastObjectListViewMain.SelectedObjects)
                {
                    manager.ImportConfig(data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to import config file. Ex: {0}", ex.Message));
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var startForm = new StartupForm
            {
                ShowOnStartUp = _showStartup
            };

            if (startForm.ShowDialog() == DialogResult.OK)
            {
                _showStartup = startForm.ShowOnStartUp;
            }
        }
        private void TabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedTab == tabPageProxies)
            {
                fastObjectListViewProxies.SetObjects(_proxyHandler.Proxies);
            }
            else if (tabControlMain.SelectedTab == tabPageScheduler)
            {
                fastObjectListViewScheduler.SetObjects(_schedulers);
            }
            else if (tabControlMain.SelectedTab == tabPageHashKeys)
            {
                fastObjectListViewHashKeys.SetObjects(_hashKeys);
            }
            else if (tabControlMain.SelectedTab == tabPageAccounts)
            {
                fastObjectListViewHashKeys.SetObjects(_managers);
            }
        }

        private async void BtnStartAcc_Click(object sender, EventArgs e)
        {
            btnStartAcc.Enabled = false;
            btnStopAcc.Enabled = true;

            while (btnStopAcc.Enabled)
            {
                var runningCount = _managers.Where(x => x.IsRunning).Count();

                //If up number 5 to 6 this run one more

                int simultAcc = Convert.ToInt32(numericUpDownSimAcc.Value);

                if (runningCount < simultAcc)
                {
                    var hasAccStart = _managers.FirstOrDefault(acc => !acc.IsRunning &&
                                                                acc.Level < acc.MaxLevel &&
                                                                acc.AccountState == AccountState.Good);
                    if (hasAccStart != null)
                    {
                        if (!hasAccStart.IsRunning)
                        {
                            hasAccStart.UserSettings.HashKeys = _hashKeys.Select(x => x.Key).ToList();
                            hasAccStart.UserSettings.SPF = _spf;
                            hasAccStart.Start();
                        }
                    }
                }

                int runState = _managers.Where(x => x.State == BotState.Running).Count();

                if (runState > simultAcc)
                {
                    _managers.FirstOrDefault(acc => acc.IsRunning && acc.State == BotState.Running).Stop();
                }

                await Task.Delay(2000);
            }

            btnStartAcc.Enabled = true;
        }

        private void BtnStoptAcc_Click(object sender, EventArgs e)
        { 
            foreach (var manager in _managers.Where(x => x.IsRunning))
            {
                manager.Stop();
            }

            btnStopAcc.Enabled = false;
        }

        private void PictureBoxAbout_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=SNATC29B4ZJD4");
        }

        #region Proxies

        private void ResetBanStateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (GoProxy proxy in fastObjectListViewProxies.SelectedObjects)
            {
                _proxyHandler.MarkProxy(proxy, false);
            }

            fastObjectListViewProxies.RefreshSelectedObjects();
        }

        private void SingleProxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Add proxy", "Proxy");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }

            bool success = _proxyHandler.AddProxy(data);

            if (!success)
            {
                MessageBox.Show("Invalid proxy format");
                return;
            }

            fastObjectListViewProxies.SetObjects(_proxyHandler.Proxies);
        }

        private void FromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileName = String.Empty;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Open proxy file";
                ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    fileName = ofd.FileName;
                }
            }

            if (String.IsNullOrEmpty(fileName))
            {
                return;
            }

            try
            {
                string[] proxyData = File.ReadAllLines(fileName);

                int count = 0;

                foreach (string pData in proxyData)
                {
                    if (_proxyHandler.AddProxy(pData))
                    {
                        ++count;
                    }
                }

                fastObjectListViewProxies.SetObjects(_proxyHandler.Proxies);

                MessageBox.Show(String.Format("Imported {0} proxies", count), "Info");
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to import proxy file. Ex: {0}", ex.Message), "Exception occured");
            }
        }

        private void DeleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int count = fastObjectListViewProxies.SelectedObjects.Count;

            if (count == 0)
            {
                return;
            }

            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to delete {0} proxies?", count), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            /*
            Dictionary<GoProxy, List<Manager>> pManagers = new Dictionary<GoProxy, List<Manager>>();

            foreach(Manager manager in _managers)
            {
                if(manager.CurrentProxy == null)
                {
                    continue;
                }

                if(pManagers.ContainsKey(manager.CurrentProxy))
                {
                    pManagers[manager.CurrentProxy].Add(manager);
                }
                else
                {
                    List<Manager> m = new List<Manager>();
                    m.Add(manager);

                    pManagers.Add(manager.CurrentProxy, m);
                }
            }*/

            bool messageShown = false;

            foreach (GoProxy proxy in fastObjectListViewProxies.SelectedObjects)
            {
                if (proxy.CurrentAccounts > 0 && !messageShown)
                {
                    messageShown = true;

                    MessageBox.Show("Only proxies with 0 accounts tied to them will be removed", "Information");
                }

                _proxyHandler.RemoveProxy(proxy);

                /*
                if(pManagers.ContainsKey(proxy))
                {
                    foreach(Manager manager in _managers)
                    {
                        manager.RemoveProxy();
                    }
                }*/
            }

            fastObjectListViewProxies.SetObjects(_proxyHandler.Proxies);
        }

        private void MaxConcurrentFailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Max concurrent fails", "Set fails", "3");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value <= 0)
            {
                MessageBox.Show("Invalid value", "Warning");
                return;
            }

            foreach (GoProxy proxy in fastObjectListViewProxies.SelectedObjects)
            {
                proxy.MaxConcurrentFails = value;
            }

            fastObjectListViewProxies.RefreshSelectedObjects();
        }

        private void MaxAccountsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Max Accounts", "Set Accounts", "3");

            if (String.IsNullOrEmpty(data))
            {
                return;
            }
            int value;
            if (!Int32.TryParse(data, out value) || value <= 0)
            {
                MessageBox.Show("Invalid value", "Warning");
                return;
            }

            foreach (GoProxy proxy in fastObjectListViewProxies.SelectedObjects)
            {
                proxy.MaxAccounts = value;
            }

            fastObjectListViewProxies.RefreshSelectedObjects();
        }

        private void FastObjectListViewProxies_FormatCell(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            var proxy = (GoProxy)e.Model;

            if (e.Column == olvColumnCurrentFails)
            {
                if (proxy.CurrentConcurrentFails == 0)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (proxy.CurrentConcurrentFails > 0)
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
                else if (proxy.CurrentConcurrentFails >= proxy.MaxConcurrentFails)
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
            else if (e.Column == olvColumnProxyBanned)
            {
                e.SubItem.ForeColor = proxy.IsBanned ? Color.Red : Color.Green;
            }
            else if (e.Column == olvColumnUsageCount)
            {
                if (proxy.CurrentAccounts == 0)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (proxy.CurrentAccounts <= proxy.MaxAccounts)
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
        }

        #endregion

        #region Dev Tools

        private void EnableSpfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _spf = !_spf;

            foreach (Manager manager in _managers)
            {
                manager.UserSettings.SPF = _spf;
            }

            //enableSpoofToolStripMenuItem.Checked = _spf;
        }

        private void LargeAddressAwareToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(LargeAddressAware.IsLargeAware(Application.ExecutablePath).ToString());
        }

        private void LogViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    var lvForm = new LogViewerForm(ofd.FileName);

                    lvForm.ShowDialog();
                }
            }
        }

        #endregion

        #region Scheduler

        private void DeleteToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            int count = fastObjectListViewScheduler.SelectedObjects.Count;

            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to remove {0} schedules?", count), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            //Deleting many at once will take awhile without this
            var managerSchedulers = new Dictionary<Scheduler, List<Manager>>();

            foreach (Manager manager in _managers)
            {
                if (manager.AccountScheduler == null)
                {
                    continue;
                }

                if (managerSchedulers.ContainsKey(manager.AccountScheduler))
                {
                    managerSchedulers[manager.AccountScheduler].Add(manager);
                }
                else
                {
                    var m = new List<Manager>
                    {
                        manager
                    };

                    managerSchedulers.Add(manager.AccountScheduler, m);
                }
            }

            foreach (Scheduler scheduler in fastObjectListViewScheduler.SelectedObjects)
            {
                _schedulers.Remove(scheduler);

                //Should always be true. If not, bug.
                if (managerSchedulers.ContainsKey(scheduler))
                {
                    foreach (Manager manager in managerSchedulers[scheduler])
                    {
                        manager.RemoveScheduler();
                    }
                }
            }

            fastObjectListViewScheduler.SetObjects(_schedulers);
        }

        private void EnablelDisableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Scheduler scheduler in fastObjectListViewScheduler.SelectedObjects)
            {
                scheduler.Enabled = !scheduler.Enabled;
            }

            fastObjectListViewScheduler.RefreshSelectedObjects();
        }

        private void EditToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int count = fastObjectListViewScheduler.SelectedObjects.Count;

            if (count > 1)
            {
                DialogResult result = MessageBox.Show(String.Format("Are you sure you want to open up {0} edit forms?", count), "Confirmation", MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            foreach (Scheduler scheduler in fastObjectListViewScheduler.SelectedObjects)
            {
                var schedulerForm = new SchedulerSettingForm(scheduler);

                schedulerForm.ShowDialog();
            }

            fastObjectListViewScheduler.RefreshSelectedObjects();
        }

        private void AddNewToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var scheduler = new Scheduler();

            var schedulerForm = new SchedulerSettingForm(scheduler);

            if (schedulerForm.ShowDialog() == DialogResult.OK)
            {
                _schedulers.Add(scheduler);
            }

            fastObjectListViewScheduler.SetObjects(_schedulers);
        }

        private void ManualCheckToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Scheduler scheduler in fastObjectListViewScheduler.SelectedObjects)
            {
                scheduler.ForceCall();
            }
        }

        private void FastObjectListViewScheduler_FormatCell(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            var scheduler = (Scheduler)e.Model;
            bool withinTime = scheduler.WithinTime();

            if (e.Column == olvColumnSchedulerName)
            {
                e.SubItem.ForeColor = scheduler.NameColor;
            }
            else if (e.Column == olvColumnSchedulerEnabled)
            {
                e.SubItem.ForeColor = scheduler.Enabled ? Color.Green : Color.Red;
            }
            else if (e.Column == olvColumnSchedulerStart || e.Column == olvColumnSchedulerEnd)
            {
                e.SubItem.ForeColor = withinTime ? Color.Green : Color.Red;
            }
        }
        # endregion

        #region HashKeys

        private void DeleteToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            foreach (var hashkey in fastObjectListViewHashKeys.SelectedObjects.Cast<HashKey>())
            {
                _hashKeys.Remove(hashkey as HashKey);
            }

            fastObjectListViewHashKeys.SetObjects(_hashKeys);
        }

        private void AddToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string input = Prompt.ShowDialog("Add Hash Key", "Hash Key");

            if (String.IsNullOrEmpty(input))
            {
                return;
            }

            HashKey newkey = new HashKey { Key = input, IsValide = false, KeyInfo = null };

            HashKey tested = TestHashKey(newkey);
            if (!tested.IsValide)
            {
                MessageBox.Show("HashKey " + tested.Key + " :" + tested.KeyInfo);
                return;
            }

            foreach (HashKey key in _hashKeys)
            {
                if (key.Key == tested.Key)
                {
                    var msg = $"This key already existes {tested.Key}, Hash key infos {tested.KeyInfo}";
                    MessageBox.Show(msg, "Duplicated key", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            _hashKeys.Add(tested);
            fastObjectListViewHashKeys.SetObjects(_hashKeys);
        }

        private void TestKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (HashKey key in fastObjectListViewHashKeys.SelectedObjects.Cast<HashKey>())
            {
                HashKey tested = TestHashKey(key);
                if (!tested.IsValide)
                    MessageBox.Show("HashKey " + tested.Key + " :" + tested.KeyInfo + ", Please remove this key of HashKeys tab.");
                _hashKeys.Remove(key);
                _hashKeys.Add(tested);
            }

            fastObjectListViewHashKeys.SetObjects(_hashKeys);
        }

        private void FastObjectListViewHashKeys_FormatCell(object sender, BrightIdeasSoftware.FormatCellEventArgs e)
        {
            if (e.Column == olvColumnKeys)
            {
                try
                {
                    e.SubItem.ForeColor = e.SubItem.Text.Substring(0, 2) == "PH" ? Color.Blue : Color.Green;
                }
                catch
                {
                    //Not keys found
                }
            }
            else if (e.Column == olvColumnHashInfos)
            {
                if (e.SubItem.Text == "The HashKey is invalid or has expired" || e.SubItem.Text.Contains("RPM: 0"))
                    e.SubItem.ForeColor = Color.Red;
                else
                    e.SubItem.ForeColor = Color.White;
            }
        }

        private void ImportToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Open Keys file";
                ofd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (String.IsNullOrEmpty(ofd.FileName))
                    {
                        return;
                    }

                    List<HashKey> newKeys = JsonConvert.DeserializeObject<List<HashKey>>(File.ReadAllText(ofd.FileName));
                    int totalSuccess = 0;
                    int total = newKeys.Count;
                    var existKeys = new List<string>();

                    foreach (HashKey key in _hashKeys)
                        existKeys.Add(key.Key);

                    foreach (HashKey key in newKeys)
                    {
                        if (existKeys.Contains(key.Key))
                            continue;
                        HashKey tested = TestHashKey(key);
                        if (!tested.IsValide)
                            MessageBox.Show("HashKey " + tested.Key + " :" + tested.KeyInfo + ", Please remove this key of HashKeys tab.");
                        _hashKeys.Add(tested);
                        ++totalSuccess;
                    }

                    fastObjectListViewHashKeys.SetObjects(_hashKeys);

                    MessageBox.Show(String.Format("Successfully imported {0} out of {1} keys", totalSuccess, total));
                }
            }
        }

        private void ExportToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string fileName = String.Empty;
            var keysToExport = new List<HashKey>();

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    fileName = sfd.FileName;
                }
                else
                {
                    return;
                }
            }

            IEnumerable<HashKey> selectedKeys = fastObjectListViewHashKeys.SelectedObjects.Cast<HashKey>();

            foreach (var key in selectedKeys)
            {
                keysToExport.Add(key);
            }

            try
            {
                string data = JsonConvert.SerializeObject(keysToExport, Formatting.Indented);

                File.WriteAllText(fileName, data);

                MessageBox.Show(String.Format("Successfully exported {0} of {1} keys", keysToExport.Count, fastObjectListViewHashKeys.SelectedObjects.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to save to file. Ex: {0}", ex.Message));
            }
        }

        private HashKey TestHashKey(HashKey haskkey)
        {
            HashKey result = new HashKey
            {
                Key = haskkey.Key,
                IsValide = false
            };

            string mode = null;

            try
            {
                var client = new HttpClient();
                string urlcheck = null;
                client.DefaultRequestHeaders.Add("X-AuthToken", result.Key);
                if (result.Key.Substring(0, 2) == "PH")
                {
                    urlcheck = $"http://hash.goman.io/api/v159_1/hash";
                    mode = "Remaining requests";
                }
                else
                {
                    urlcheck = $"https://pokehash.buddyauth.com/api/v159_1/hash";
                    mode = "RPM";
                }
                //result = $"Hash End-Point Set to '{urlcheck}'";
                HttpResponseMessage response = client.PostAsync(urlcheck, null).Result;
                string AuthKey = response.Headers.GetValues("X-AuthToken").FirstOrDefault();
                string MaxRequestCount = response.Headers.GetValues("X-MaxRequestCount").FirstOrDefault();
                DateTime AuthTokenExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(Convert.ToDouble(response.Headers.GetValues("X-AuthTokenExpiration").FirstOrDefault())).ToLocalTime();
                TimeSpan Expiration = AuthTokenExpiration - DateTime.Now;
                result.KeyInfo = string.Format($"{mode}: {MaxRequestCount} Expires in: {(Convert.ToDecimal(Expiration.Days) + (Convert.ToDecimal(Expiration.Hours) / 24)):0.00} days ({AuthTokenExpiration})");
                if (AuthTokenExpiration > DateTime.Now)
                    result.IsValide = true;
            }
            catch
            {
                result.KeyInfo = "The HashKey is invalid or has expired";
                result.IsValide = false;
            }

            return result;
        }

        private void RMFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                List<string> accounts = ImportAccounts();

                var tempManagers = new HashSet<Manager>(_managers);

                int totalSuccess = 0;
                int total = accounts.Count;

                foreach (string account in accounts)
                {
                    string[] parts = account.Split(',');

                    /*
                     * AccType,User,Pass = 3
                     */
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    var importModel = new AccountImport
                    {
                        Username = parts[1],
                        Password = parts[2]
                    };

                    var manager = new Manager(_proxyHandler);

                    manager.UserSettings.AuthType = (parts[0].Trim().ToLower() == "ptc") ? AuthType.Ptc : AuthType.Google;
                    manager.UserSettings.AccountName = importModel.Username.Trim();
                    manager.UserSettings.Username = importModel.Username.Trim();
                    manager.UserSettings.Password = importModel.Password.Trim();
                    manager.UserSettings.ProxyIP = importModel.Address;
                    manager.UserSettings.ProxyPort = importModel.Port;
                    manager.UserSettings.ProxyUsername = importModel.ProxyUsername;
                    manager.UserSettings.ProxyPassword = importModel.ProxyPassword;

                    manager.UserSettings.MaxLevel = 30;
                    if (parts.Length > 3)
                        manager.UserSettings.MaxLevel = int.Parse(parts[4]);

                    if (tempManagers.Add(manager))
                    {
                        AddManager(manager);
                        ++totalSuccess;
                    }
                }

                fastObjectListViewMain.SetObjects(_managers);

                MessageBox.Show(String.Format("Successfully imported {0} out of {1} accounts", totalSuccess, total));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to import usernames. Ex: {0}", ex.Message));
            }
        }

        private void ImportJsonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileName = String.Empty;

            using (var sfd = new OpenFileDialog())
            {
                sfd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    fileName = sfd.FileName;
                }
                else
                {
                    return;
                }
            }

            try
            {
                IEnumerable<Manager> selectedManagers = Serializer.FromJson<List<Manager>>(File.ReadAllText(fileName));
                int count = selectedManagers.Count();
                foreach (Manager manager in selectedManagers)
                {
                    if (_managers.Contains(manager))
                        count--;
                    AddManager(manager);
                }

                fastObjectListViewMain.SetObjects(_managers);

                MessageBox.Show(String.Format("Successfully imported {0}/{1}.", count, selectedManagers.Count()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to open to file. Ex: {0}", ex.Message));
            }
        }

        private void ExportAccountsRMFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fastObjectListViewMain.SelectedObjects.Count == 0)
            {
                return;
            }

            string filename = GetSaveFileName();

            if (String.IsNullOrEmpty(filename))
            {
                return;
            }

            try
            {
                IEnumerable<string> accounts = fastObjectListViewMain.SelectedObjects.Cast<Manager>().Select(x => String.Format("{0},{1},{2}", x.UserSettings.AuthType.ToString().ToLower(), x.UserSettings.Username, x.UserSettings.Password));

                File.WriteAllLines(filename, accounts);

                MessageBox.Show(String.Format("Exported {0} accounts RocketMap format", accounts.Count()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to export accounts RocketMap format. Ex: {0}", ex.Message));
            }
        }

        private async void ExportGMModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileName = String.Empty;
            var exportModels = new List<AccountExportModel>();

            DialogResult dialogResult = MessageBox.Show("Update details before exporting?", "Update details", MessageBoxButtons.YesNoCancel);

            if (dialogResult == DialogResult.Cancel)
            {
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    fileName = sfd.FileName;
                }
                else
                {
                    return;
                }
            }

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            };

            IEnumerable<Manager> selectedManagers = fastObjectListViewMain.SelectedObjects.Cast<Manager>();

            await Task.Run(() =>
            {
                Parallel.ForEach(selectedManagers, options, (manager) =>
                {
                    if (dialogResult == DialogResult.Yes)
                    {
                        manager.UpdateDetails().Wait();
                    }

                    MethodResult<AccountExportModel> result = manager.GetAccountExport();

                    if (!result.Success)
                    {
                        return;
                    }

                    exportModels.Add(result.Data);
                });
            });

            try
            {
                string data = JsonConvert.SerializeObject(exportModels, Formatting.None);

                File.WriteAllText(fileName, data);

                MessageBox.Show(String.Format("Successfully exported {0} of {1} accounts", exportModels.Count, fastObjectListViewMain.SelectedObjects.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to save to file. Ex: {0}", ex.Message));
            }
        }
        #endregion
    }
}
