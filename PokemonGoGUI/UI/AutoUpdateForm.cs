using System.Web;
using System;
using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Markdig;

namespace PokemonGoGUI.UI
{
    public partial class AutoUpdateForm : Form
    {
        public string LatestVersion { get; set; }
        public string CurrentVersion { get; set; }
        public bool AutoUpdate { get; set; }
        public string DownloadLink { get; set; }
        public string ChangelogLink { get; set; }
        public string Destination { get; set; }
        private WebClient _webclient = new WebClient();

        public AutoUpdateForm()
        {
            InitializeComponent();
        }

        public static string StripHTML(string HTMLText, bool decode = true)
        {
            Regex reg = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            var stripped = reg.Replace(HTMLText, "");
            return decode ? HttpUtility.HtmlDecode(stripped) : stripped;
        }

        private void AutoUpdateForm_Load(object sender, EventArgs e)
        {
            //richTextBox1.SetInnerMargins(25, 25, 25, 25);
            lblCurrent.Text = $"v{CurrentVersion}";
            lblLatest.Text = $"v{LatestVersion}";
            var client = new WebClient();
            var ChangelogRaw = client.DownloadString(ChangelogLink);
            var ChangelogFormatted = StripHTML(Markdown.ToHtml(ChangelogRaw)).Replace("Full Changelog", "").Replace("Change Log", "");
            if (ChangelogFormatted.Length > 0)
            {
                richTextBox1.Text = ChangelogFormatted;
            }
            else
            {
                richTextBox1.Text = "No Changelog Detected...";
            }
            if (AutoUpdate)
            {
                btnUpdate.Enabled = false;
                lblMessage.Enabled = true;
                btnUpdate.Text = "Downloading...";
                StartDownload();
            }
        }

        public bool DownloadFile(string url, string dest)
        {
            try
            {
                _webclient.DownloadFileCompleted += Client_DownloadFileCompleted;
                _webclient.DownloadProgressChanged += Client_DownloadProgressChanged;
                _webclient.DownloadFileAsync(new Uri(url), dest);
            }
            catch
            {
                _webclient.DownloadFileCompleted -= Client_DownloadFileCompleted;
                _webclient.DownloadProgressChanged -= Client_DownloadProgressChanged;
                Close();
            }
            return true;
        }

        private void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Invoke(new Action(() =>
            {
                DialogResult = DialogResult.OK;
                _webclient.DownloadFileCompleted -= Client_DownloadFileCompleted;
                _webclient.DownloadProgressChanged -= Client_DownloadProgressChanged;
                Close();
            }));
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Invoke(new Action(() =>
            {
                lblMessage.Text = $"Updating {Application.ProductName} from v{CurrentVersion} to v{LatestVersion} ({e.ProgressPercentage}% Completed)";
            }));
        }

        public void StartDownload()
        {
            DownloadFile(DownloadLink, Destination);
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            btnUpdate.Text = "Downloading...";
            btnUpdate.Enabled = false;
            StartDownload();
        }

        private void Btncancel_Click(object sender, EventArgs e)
        {
            _webclient.DownloadFileCompleted -= Client_DownloadFileCompleted;
            _webclient.DownloadProgressChanged -= Client_DownloadProgressChanged;
            Close();
        }
    }
}
