using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    internal class MboxMessageIndex
    {
        public int Number;
        public long Offset;
        public long Length;
        public string Date;
        public string From;
        public string To;
        public string Subject;
    }

    internal class MboxIndexResult
    {
        public string FilePath;
        public List<MboxMessageIndex> Messages;
        public bool FromCache;
    }

    internal class MboxFullTextIndex
    {
        public string FilePath;
        public List<byte[]> Filters = new List<byte[]>();
        public bool FromCache;
    }

    internal class MboxSearchResult
    {
        public string FilePath;
        public List<int> MessageNumbers = new List<int>();
        public bool IndexFromCache;
        public int CandidateCount;
    }

    internal class MboxAttachment
    {
        public string FileName;
        public string ContentType;
        public byte[] Data;
        public override string ToString()
        {
            string size = Data == null ? "?" : FormatBytes(Data.LongLength);
            return FileName + "   (" + ContentType + ", " + size + ")";
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;
            string[] units = new string[] { "B", "KB", "MB", "GB" };
            int i = 0;
            while (value >= 1024 && i < units.Length - 1) { value /= 1024.0; i++; }
            return value.ToString(i == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[i];
        }
    }

    internal class MboxPreviewResult
    {
        public string Text;
        public List<MboxAttachment> Attachments = new List<MboxAttachment>();
        public bool Truncated;
    }

    public class MboxViewerForm : Form
    {
        private readonly string initialRoot;
        private string currentFile;
        private List<MboxMessageIndex> allMessages = new List<MboxMessageIndex>();
        private readonly BackgroundWorker indexWorker;
        private readonly BackgroundWorker searchWorker;

        private TreeView tree;
        private DataGridView grid;
        private RichTextBox preview;
        private ListBox lstAttachments;
        private TextBox txtSearch;
        private CheckBox chkFullText;
        private Button btnSearch;
        private Label lblFile;
        private Label lblStatus;
        private ProgressBar progress;
        private Button btnOpenFile;
        private Button btnRefreshIndex;
        private Button btnExportEml;
        private Button btnPrint;
        private Button btnPdf;
        private Button btnSaveAttachment;

        private const long PreviewMaxBytes = 24L * 1024L * 1024L;
        private const long SearchMaxBytes = 512L * 1024L;
        private const int BloomBytes = 32;

        public MboxViewerForm(string rootPath)
        {
            initialRoot = rootPath;
            Text = "CM IMAP Sicherung – MBOX Viewer";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1050, 650);
            Size = new Size(1320, 820);
            Font = new Font("Segoe UI", 9F);
            BackColor = UiTheme.Window;

            BuildUi();

            indexWorker = new BackgroundWorker();
            indexWorker.WorkerReportsProgress = true;
            indexWorker.WorkerSupportsCancellation = true;
            indexWorker.DoWork += IndexWorker_DoWork;
            indexWorker.ProgressChanged += IndexWorker_ProgressChanged;
            indexWorker.RunWorkerCompleted += IndexWorker_Completed;

            searchWorker = new BackgroundWorker();
            searchWorker.WorkerReportsProgress = true;
            searchWorker.WorkerSupportsCancellation = true;
            searchWorker.DoWork += SearchWorker_DoWork;
            searchWorker.ProgressChanged += SearchWorker_ProgressChanged;
            searchWorker.RunWorkerCompleted += SearchWorker_Completed;

            LoadFolderTree(initialRoot);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var header = new Panel();
            header.Height = 76;
            header.Dock = DockStyle.Top;
            header.BackColor = UiTheme.Header;
            header.Padding = new Padding(18, 12, 18, 10);
            header.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(header, 0, 0);

            var title = new Label();
            title.Text = "MBOX Viewer";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(16, 10);
            header.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "Backups sicher durchsuchen, E-Mails lesen, Anhänge speichern und Nachrichten als EML exportieren";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(218, 226, 238);
            subtitle.Location = new Point(18, 43);
            header.Controls.Add(subtitle);

            var toolbar = new TableLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.AutoSize = true;
            toolbar.ColumnCount = 11;
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Margin = new Padding(0, 0, 0, 10);

            var searchLabel = new Label();
            searchLabel.Text = "Suche:";
            searchLabel.AutoSize = true;
            searchLabel.Anchor = AnchorStyles.Left;
            searchLabel.Margin = new Padding(0, 7, 8, 0);
            toolbar.Controls.Add(searchLabel, 0, 0);

            txtSearch = new TextBox();
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Margin = new Padding(0, 3, 8, 3);
            txtSearch.TextChanged += delegate { if (chkFullText == null || !chkFullText.Checked) ApplyFilter(); };
            txtSearch.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunSearch(); } };
            toolbar.Controls.Add(txtSearch, 1, 0);

            chkFullText = new CheckBox();
            chkFullText.Text = "Volltext";
            chkFullText.AutoSize = true;
            chkFullText.Anchor = AnchorStyles.Left;
            chkFullText.Margin = new Padding(0, 7, 8, 0);
            chkFullText.CheckedChanged += delegate { if (!chkFullText.Checked) ApplyFilter(); };
            toolbar.Controls.Add(chkFullText, 2, 0);

            btnSearch = MakeButton("Suchen", 75, true);
            btnSearch.Click += delegate { RunSearch(); };
            toolbar.Controls.Add(btnSearch, 3, 0);

            btnOpenFile = MakeButton("MBOX öffnen...", 110, false);
            btnOpenFile.Click += BtnOpenFile_Click;
            toolbar.Controls.Add(btnOpenFile, 4, 0);

            btnRefreshIndex = MakeButton("Index neu", 90, false);
            btnRefreshIndex.Click += delegate { if (!String.IsNullOrEmpty(currentFile)) StartIndex(currentFile, true); };
            toolbar.Controls.Add(btnRefreshIndex, 5, 0);

            btnExportEml = MakeButton("Als EML exportieren", 135, false);
            btnExportEml.Click += BtnExportEml_Click;
            toolbar.Controls.Add(btnExportEml, 6, 0);

            btnPrint = MakeButton("Drucken", 80, false);
            btnPrint.Click += delegate { PrintSelectedMessage(false); };
            toolbar.Controls.Add(btnPrint, 7, 0);

            btnPdf = MakeButton("PDF", 65, false);
            btnPdf.Click += delegate { PrintSelectedMessage(true); };
            toolbar.Controls.Add(btnPdf, 8, 0);

            var close = MakeButton("Schließen", 90, false);
            close.Click += delegate { Close(); };
            toolbar.Controls.Add(close, 9, 0);
            root.Controls.Add(toolbar, 0, 1);

            var outer = new SplitContainer();
            outer.Dock = DockStyle.Fill;
            outer.BackColor = UiTheme.Border;
            root.Controls.Add(outer, 0, 2);

            var left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.BackColor = UiTheme.Surface;
            left.Padding = new Padding(10);
            left.RowCount = 2;
            left.ColumnCount = 1;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var folderTitle = new Label();
            folderTitle.Text = "MBOX-Dateien";
            folderTitle.AutoSize = true;
            folderTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            folderTitle.ForeColor = UiTheme.Text;
            folderTitle.Margin = new Padding(0, 0, 0, 8);
            left.Controls.Add(folderTitle, 0, 0);
            tree = new TreeView();
            tree.Dock = DockStyle.Fill;
            tree.BorderStyle = BorderStyle.None;
            tree.BackColor = UiTheme.Surface;
            tree.AfterSelect += Tree_AfterSelect;
            left.Controls.Add(tree, 0, 1);
            outer.Panel1.Controls.Add(left);

            var right = new SplitContainer();
            right.Dock = DockStyle.Fill;
            right.Orientation = Orientation.Horizontal;
            outer.Panel2.Controls.Add(right);

            // Splitter erst nach dem ersten Layout setzen. Ein neuer SplitContainer
            // ist beim Konstruktor noch sehr klein; feste MinSize/SplitterDistance-Werte
            // können deshalb bereits beim Öffnen des Viewers eine Ausnahme auslösen.
            Shown += delegate
            {
                try
                {
                    if (outer.Width > 800)
                    {
                        outer.Panel1MinSize = 180;
                        outer.Panel2MinSize = 450;
                        outer.SplitterDistance = Math.Max(200, Math.Min(280, outer.Width - 500));
                    }
                    if (right.Height > 520)
                    {
                        right.Panel1MinSize = 180;
                        right.Panel2MinSize = 180;
                        right.SplitterDistance = Math.Max(240, Math.Min(360, right.Height - 240));
                    }
                }
                catch { }
            };

            var listPanel = new TableLayoutPanel();
            listPanel.Dock = DockStyle.Fill;
            listPanel.BackColor = UiTheme.Surface;
            listPanel.Padding = new Padding(10);
            listPanel.RowCount = 2;
            listPanel.ColumnCount = 1;
            listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            lblFile = new Label();
            lblFile.Text = "Keine MBOX-Datei geöffnet";
            lblFile.AutoSize = true;
            lblFile.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblFile.ForeColor = UiTheme.Text;
            lblFile.Margin = new Padding(0, 0, 0, 8);
            listPanel.Controls.Add(lblFile, 0, 0);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = UiTheme.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 32;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.SelectionChanged += Grid_SelectionChanged;

            AddColumn("Nr", "#", 38);
            AddColumn("Date", "Datum", 110);
            AddColumn("From", "Absender", 150);
            AddColumn("To", "Empfänger", 130);
            AddColumn("Subject", "Betreff", 240);
            AddColumn("Size", "Größe", 70);
            listPanel.Controls.Add(grid, 0, 1);
            right.Panel1.Controls.Add(listPanel);

            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 9F);
            var tabPreview = new TabPage("Nachricht");
            preview = new RichTextBox();
            preview.Dock = DockStyle.Fill;
            preview.ReadOnly = true;
            preview.BackColor = Color.White;
            preview.ForeColor = UiTheme.Text;
            preview.BorderStyle = BorderStyle.None;
            preview.Font = new Font("Segoe UI", 10F);
            preview.DetectUrls = true;
            tabPreview.Controls.Add(preview);

            var tabAttachments = new TabPage("Anhänge");
            var attLayout = new TableLayoutPanel();
            attLayout.Dock = DockStyle.Fill;
            attLayout.Padding = new Padding(10);
            attLayout.RowCount = 2;
            attLayout.ColumnCount = 1;
            attLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            attLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lstAttachments = new ListBox();
            lstAttachments.Dock = DockStyle.Fill;
            lstAttachments.DoubleClick += delegate { SaveSelectedAttachment(); };
            attLayout.Controls.Add(lstAttachments, 0, 0);
            btnSaveAttachment = MakeButton("Anhang speichern...", 140, true);
            btnSaveAttachment.Click += delegate { SaveSelectedAttachment(); };
            attLayout.Controls.Add(btnSaveAttachment, 0, 1);
            tabAttachments.Controls.Add(attLayout);

            tabs.TabPages.Add(tabPreview);
            tabs.TabPages.Add(tabAttachments);
            right.Panel2.Controls.Add(tabs);

            var statusPanel = new TableLayoutPanel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.AutoSize = true;
            statusPanel.ColumnCount = 2;
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            lblStatus = new Label();
            lblStatus.Text = "Bereit";
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = UiTheme.Muted;
            lblStatus.Anchor = AnchorStyles.Left;
            statusPanel.Controls.Add(lblStatus, 0, 0);
            progress = new ProgressBar();
            progress.Dock = DockStyle.Fill;
            progress.Height = 16;
            progress.Visible = false;
            statusPanel.Controls.Add(progress, 1, 0);
            root.Controls.Add(statusPanel, 0, 3);
        }

        private Button MakeButton(string text, int width, bool primary)
        {
            var b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 32;
            b.Margin = new Padding(0, 0, 7, 0);
            if (primary) UiTheme.ApplyPrimary(b); else UiTheme.ApplySecondary(b);
            return b;
        }

        private void AddColumn(string name, string header, int weight)
        {
            var c = new DataGridViewTextBoxColumn();
            c.Name = name;
            c.HeaderText = header;
            c.ReadOnly = true;
            c.FillWeight = weight;
            grid.Columns.Add(c);
        }

        private void LoadFolderTree(string rootPath)
        {
            tree.Nodes.Clear();
            if (String.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                lblStatus.Text = "Kein gültiger Backupordner gewählt. MBOX-Datei kann manuell geöffnet werden.";
                return;
            }

            try
            {
                List<string> files = FindMboxFiles(rootPath);
                foreach (string file in files.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                {
                    string rel = MakeRelativePath(rootPath, file);
                    AddTreePath(rel, file);
                }
                tree.ExpandAll();
                lblStatus.Text = files.Count == 0 ? "Keine MBOX-Dateien gefunden." : files.Count + " MBOX-Datei(en) gefunden.";
                TreeNode first = FindFirstFileNode(tree.Nodes);
                if (first != null) tree.SelectedNode = first;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ordner konnte nicht gelesen werden: " + ex.Message;
            }
        }

        private void AddTreePath(string relativePath, string filePath)
        {
            string[] parts = relativePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            TreeNodeCollection nodes = tree.Nodes;
            TreeNode current = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                TreeNode next = null;
                foreach (TreeNode n in nodes)
                {
                    if (String.Equals(n.Text, part, StringComparison.CurrentCultureIgnoreCase)) { next = n; break; }
                }
                if (next == null)
                {
                    next = new TreeNode(part);
                    nodes.Add(next);
                }
                current = next;
                nodes = next.Nodes;
            }
            if (current != null)
            {
                current.Tag = filePath;
                current.ForeColor = UiTheme.Text;
            }
        }

        private static TreeNode FindFirstFileNode(TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag is string) return n;
                TreeNode child = FindFirstFileNode(n.Nodes);
                if (child != null) return child;
            }
            return null;
        }

        private static List<string> FindMboxFiles(string rootPath)
        {
            var result = new List<string>();
            try { result.AddRange(Directory.EnumerateFiles(rootPath, "*.mbox", SearchOption.AllDirectories).Where(f => { try { return new FileInfo(f).Length > 0; } catch { return false; } })); } catch { }
            try { result.AddRange(Directory.EnumerateFiles(rootPath, "*.mbx", SearchOption.AllDirectories).Where(f => { try { return new FileInfo(f).Length > 0; } catch { return false; } })); } catch { }
            result = result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (result.Count > 0) return result;

            try
            {
                foreach (string file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".zip" || ext == ".log" || ext == ".sha256" || ext == ".json" || ext == ".xml" || ext == ".idx") continue;
                    try
                    {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (fs.Length < 5) continue;
                            byte[] b = new byte[5];
                            if (fs.Read(b, 0, 5) == 5 && Encoding.ASCII.GetString(b) == "From ") result.Add(file);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string MakeRelativePath(string root, string file)
        {
            try
            {
                string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string f = Path.GetFullPath(file);
                if (f.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return f.Substring(r.Length);
            }
            catch { }
            return Path.GetFileName(file);
        }

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string file = e.Node == null ? null : e.Node.Tag as string;
            if (!String.IsNullOrEmpty(file)) StartIndex(file, false);
        }

        private void BtnOpenFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "MBOX-Dateien (*.mbox;*.mbx)|*.mbox;*.mbx|Alle Dateien (*.*)|*.*";
                dlg.Title = "MBOX-Datei öffnen";
                if (dlg.ShowDialog(this) == DialogResult.OK) StartIndex(dlg.FileName, false);
            }
        }

        private void StartIndex(string file, bool force)
        {
            if (indexWorker.IsBusy || searchWorker.IsBusy)
            {
                MessageBox.Show("Bitte warten, bis der aktuelle Index- oder Suchvorgang abgeschlossen ist.", "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(file)) return;

            string invalidReason;
            if (!IsLikelyReadableMbox(file, out invalidReason))
            {
                lblFile.Text = Path.GetFileName(file);
                lblStatus.Text = invalidReason;
                preview.Text = "Diese Datei kann derzeit nicht als MBOX geöffnet werden.\r\n\r\n" + invalidReason +
                    "\r\n\r\nFalls die Sicherung zuvor mit einem Fehler abgebrochen wurde, bitte das Konto nach dem Backup-Fix erneut sichern.";
                grid.Rows.Clear();
                lstAttachments.Items.Clear();
                return;
            }

            currentFile = file;
            if (force)
            {
                try { string fti = GetFullTextIndexPath(file); if (File.Exists(fti)) File.Delete(fti); } catch { }
            }
            allMessages.Clear();
            grid.Rows.Clear();
            preview.Clear();
            lstAttachments.Items.Clear();
            lblFile.Text = Path.GetFileName(file) + "  •  " + FormatBytes(new FileInfo(file).Length);
            lblStatus.Text = force ? "Index wird neu erstellt ..." : "MBOX wird geöffnet ...";
            progress.Visible = true;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            SetViewerBusy(true);
            indexWorker.RunWorkerAsync(new object[] { file, force });
        }

        private static bool IsLikelyReadableMbox(string file, out string reason)
        {
            reason = "";
            try
            {
                var fi = new FileInfo(file);
                if (!fi.Exists) { reason = "Datei wurde nicht gefunden."; return false; }
                if (fi.Length == 0) { reason = "Die MBOX-Datei ist leer (0 Byte)."; return false; }

                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int max = (int)Math.Min(fi.Length, 1024L * 1024L);
                    byte[] data = new byte[max];
                    int got = fs.Read(data, 0, max);
                    if (got >= 5 && data[0] == (byte)'F' && data[1] == (byte)'r' && data[2] == (byte)'o' && data[3] == (byte)'m' && data[4] == (byte)' ') return true;
                    for (int i = 1; i + 5 <= got; i++)
                    {
                        if (data[i - 1] == 10 && data[i] == (byte)'F' && data[i + 1] == (byte)'r' && data[i + 2] == (byte)'o' && data[i + 3] == (byte)'m' && data[i + 4] == (byte)' ') return true;
                    }
                }
                reason = "Keine MBOX-Trennzeile ('From ') gefunden. Die Datei ist möglicherweise unvollständig oder kein MBOX-Format.";
                return false;
            }
            catch (Exception ex)
            {
                reason = "Datei konnte nicht gelesen werden: " + ex.Message;
                return false;
            }
        }

        private void SetViewerBusy(bool busy)
        {
            btnOpenFile.Enabled = !busy;
            btnRefreshIndex.Enabled = !busy;
            txtSearch.Enabled = !busy;
            if (chkFullText != null) chkFullText.Enabled = !busy;
            if (btnSearch != null) btnSearch.Enabled = !busy;
            tree.Enabled = !busy;
        }

        private void IndexWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] args = (object[])e.Argument;
            string file = (string)args[0];
            bool force = (bool)args[1];
            e.Result = LoadOrBuildIndex(file, force, indexWorker);
        }

        private void IndexWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Text = e.UserState == null ? "Index wird erstellt ..." : e.UserState.ToString();
        }

        private void IndexWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            progress.Visible = false;
            progress.MarqueeAnimationSpeed = 0;
            SetViewerBusy(false);
            if (e.Error != null)
            {
                lblStatus.Text = "Fehler: " + e.Error.Message;
                MessageBox.Show(e.Error.Message, "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = e.Result as MboxIndexResult;
            if (result == null) return;
            currentFile = result.FilePath;
            allMessages = result.Messages ?? new List<MboxMessageIndex>();
            ApplyFilter();
            lblStatus.Text = allMessages.Count + " Nachricht(en)" + (result.FromCache ? "  •  Index geladen" : "  •  Index erstellt");
        }

        private static MboxIndexResult LoadOrBuildIndex(string file, bool force, BackgroundWorker worker)
        {
            string indexPath = GetIndexPath(file);
            if (!force)
            {
                List<MboxMessageIndex> cached;
                if (TryLoadIndex(indexPath, file, out cached))
                    return new MboxIndexResult { FilePath = file, Messages = cached, FromCache = true };
            }

            List<MboxMessageIndex> messages = BuildIndex(file, worker);
            try { SaveIndex(indexPath, file, messages); } catch { }
            return new MboxIndexResult { FilePath = file, Messages = messages, FromCache = false };
        }

        private static string GetIndexPath(string file)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CM IMAP Sicherung", "MboxViewerIndex");
            Directory.CreateDirectory(dir);
            string normalized = Path.GetFullPath(file).ToLowerInvariant();
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var sb = new StringBuilder();
            for (int i = 0; i < 12; i++) sb.Append(hash[i].ToString("x2"));
            return Path.Combine(dir, sb.ToString() + ".idx");
        }

        private static bool TryLoadIndex(string indexPath, string file, out List<MboxMessageIndex> messages)
        {
            messages = null;
            try
            {
                if (!File.Exists(indexPath)) return false;
                var fi = new FileInfo(file);
                using (var br = new BinaryReader(File.OpenRead(indexPath), Encoding.UTF8))
                {
                    if (br.ReadString() != "CMIMAP_MBOX_INDEX_1") return false;
                    if (br.ReadInt64() != fi.Length) return false;
                    if (br.ReadInt64() != fi.LastWriteTimeUtc.Ticks) return false;
                    int count = br.ReadInt32();
                    if (count < 0 || count > 5000000) return false;
                    messages = new List<MboxMessageIndex>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var m = new MboxMessageIndex();
                        m.Number = br.ReadInt32();
                        m.Offset = br.ReadInt64();
                        m.Length = br.ReadInt64();
                        m.Date = br.ReadString();
                        m.From = br.ReadString();
                        m.To = br.ReadString();
                        m.Subject = br.ReadString();
                        messages.Add(m);
                    }
                }
                return true;
            }
            catch { messages = null; return false; }
        }

        private static void SaveIndex(string indexPath, string file, List<MboxMessageIndex> messages)
        {
            var fi = new FileInfo(file);
            string tmp = indexPath + ".tmp";
            using (var bw = new BinaryWriter(File.Create(tmp), Encoding.UTF8))
            {
                bw.Write("CMIMAP_MBOX_INDEX_1");
                bw.Write(fi.Length);
                bw.Write(fi.LastWriteTimeUtc.Ticks);
                bw.Write(messages.Count);
                foreach (var m in messages)
                {
                    bw.Write(m.Number);
                    bw.Write(m.Offset);
                    bw.Write(m.Length);
                    bw.Write(m.Date ?? "");
                    bw.Write(m.From ?? "");
                    bw.Write(m.To ?? "");
                    bw.Write(m.Subject ?? "");
                }
            }
            if (File.Exists(indexPath)) File.Delete(indexPath);
            File.Move(tmp, indexPath);
        }

        private class Boundary
        {
            public long Start;
            public long Length;
        }

        private static List<MboxMessageIndex> BuildIndex(string file, BackgroundWorker worker)
        {
            var boundaries = FindMessageBoundaries(file, worker);
            var result = new List<MboxMessageIndex>(boundaries.Count);
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.RandomAccess))
            {
                for (int i = 0; i < boundaries.Count; i++)
                {
                    if (worker.CancellationPending) break;
                    Dictionary<string, string> headers = ReadHeaders(fs, boundaries[i].Start, boundaries[i].Length);
                    var m = new MboxMessageIndex();
                    m.Number = i + 1;
                    m.Offset = boundaries[i].Start;
                    m.Length = boundaries[i].Length;
                    m.Date = NormalizeDate(GetHeader(headers, "Date"));
                    m.From = DecodeMimeWords(GetHeader(headers, "From"));
                    m.To = DecodeMimeWords(GetHeader(headers, "To"));
                    m.Subject = DecodeMimeWords(GetHeader(headers, "Subject"));
                    result.Add(m);
                    if ((i % 250) == 0)
                        worker.ReportProgress(0, "Metadaten: " + (i + 1) + " / " + boundaries.Count + " Nachrichten");
                }
            }
            return result;
        }

        private static List<Boundary> FindMessageBoundaries(string file, BackgroundWorker worker)
        {
            var result = new List<Boundary>();
            byte[] pattern = Encoding.ASCII.GetBytes("From ");
            byte[] buffer = new byte[1024 * 1024];
            long fileLength = new FileInfo(file).Length;
            long position = 0;
            long previousMessageStart = -1;
            long separatorStart = -1;
            bool lineStart = true;
            bool separatorLine = false;
            int match = 0;
            long candidateStart = 0;
            long lastReport = 0;

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.SequentialScan))
            {
                int n;
                while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < n; i++, position++)
                    {
                        byte b = buffer[i];
                        if (lineStart)
                        {
                            if (match == 0) candidateStart = position;
                            if (b == pattern[match])
                            {
                                match++;
                                if (match == pattern.Length)
                                {
                                    separatorLine = true;
                                    separatorStart = candidateStart;
                                    lineStart = false;
                                    match = 0;
                                }
                            }
                            else
                            {
                                lineStart = false;
                                match = 0;
                            }
                        }

                        if (b == 10)
                        {
                            if (separatorLine)
                            {
                                if (previousMessageStart >= 0)
                                {
                                    long len = separatorStart - previousMessageStart;
                                    if (len > 0) result.Add(new Boundary { Start = previousMessageStart, Length = len });
                                }
                                previousMessageStart = position + 1;
                                separatorLine = false;
                            }
                            lineStart = true;
                            match = 0;
                        }
                    }

                    if (position - lastReport >= 64L * 1024L * 1024L)
                    {
                        lastReport = position;
                        int pct = fileLength <= 0 ? 0 : (int)Math.Min(100, (position * 100L) / fileLength);
                        worker.ReportProgress(pct, "MBOX wird indexiert: " + pct + "%  •  " + FormatBytes(position) + " / " + FormatBytes(fileLength));
                    }
                }
            }

            if (previousMessageStart >= 0 && fileLength > previousMessageStart)
                result.Add(new Boundary { Start = previousMessageStart, Length = fileLength - previousMessageStart });

            if (result.Count == 0 && fileLength > 0)
                throw new InvalidDataException("Keine gültigen MBOX-Nachrichten gefunden. Eine MBOX-Datei sollte Nachrichten mit einer 'From '-Trennzeile enthalten.");
            return result;
        }

        private static Dictionary<string, string> ReadHeaders(FileStream fs, long start, long messageLength)
        {
            int max = (int)Math.Min(Math.Min(messageLength, 256L * 1024L), Int32.MaxValue);
            byte[] data = new byte[max];
            fs.Seek(start, SeekOrigin.Begin);
            int got = 0;
            while (got < max)
            {
                int n = fs.Read(data, got, max - got);
                if (n <= 0) break;
                got += n;
                int end = FindHeaderEnd(data, got);
                if (end >= 0) { got = end; break; }
            }
            string raw = Encoding.GetEncoding(28591).GetString(data, 0, got);
            return ParseHeaderBlock(raw);
        }

        private static int FindHeaderEnd(byte[] data, int count)
        {
            for (int i = 1; i < count; i++)
            {
                if (data[i - 1] == 10 && data[i] == 10) return i + 1;
                if (i >= 3 && data[i - 3] == 13 && data[i - 2] == 10 && data[i - 1] == 13 && data[i] == 10) return i + 1;
            }
            return -1;
        }

        private static Dictionary<string, string> ParseHeaderBlock(string raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string unfolded = Regex.Replace(raw.Replace("\r\n", "\n"), "\n[\\t ]+", " ");
            string[] lines = unfolded.Split('\n');
            foreach (string line in lines)
            {
                int idx = line.IndexOf(':');
                if (idx <= 0) continue;
                string key = line.Substring(0, idx).Trim();
                string value = line.Substring(idx + 1).Trim();
                if (!dict.ContainsKey(key)) dict[key] = value;
                else dict[key] = dict[key] + ", " + value;
            }
            return dict;
        }

        private static string GetHeader(Dictionary<string, string> headers, string name)
        {
            string value;
            return headers.TryGetValue(name, out value) ? value : "";
        }

        private static string NormalizeDate(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dto))
                return dto.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            return value;
        }

        private static string DecodeMimeWords(string input)
        {
            if (String.IsNullOrEmpty(input)) return "";
            try
            {
                return Regex.Replace(input, @"=\?([^?]+)\?([bBqQ])\?([^?]+)\?=", delegate(Match m)
                {
                    try
                    {
                        string charset = m.Groups[1].Value;
                        string enc = m.Groups[2].Value.ToUpperInvariant();
                        string data = m.Groups[3].Value;
                        byte[] bytes;
                        if (enc == "B") bytes = Convert.FromBase64String(data);
                        else bytes = DecodeQuotedPrintableToBytes(data.Replace('_', ' '));
                        Encoding e = Encoding.GetEncoding(charset);
                        return e.GetString(bytes);
                    }
                    catch { return m.Value; }
                });
            }
            catch { return input; }
        }


        private void RunSearch()
        {
            if (chkFullText == null || !chkFullText.Checked)
            {
                ApplyFilter();
                return;
            }
            string query = (txtSearch.Text ?? "").Trim();
            if (query.Length == 0)
            {
                ApplyFilter();
                return;
            }
            if (String.IsNullOrEmpty(currentFile) || allMessages.Count == 0) return;
            if (searchWorker.IsBusy)
            {
                MessageBox.Show("Eine Volltextsuche läuft bereits.", "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            lblStatus.Text = "Volltextindex wird geprüft ...";
            progress.Visible = true;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 20;
            SetViewerBusy(true);
            searchWorker.RunWorkerAsync(new object[] { currentFile, query, allMessages });
        }

        private void SearchWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] args = (object[])e.Argument;
            string file = (string)args[0];
            string query = (string)args[1];
            List<MboxMessageIndex> messages = (List<MboxMessageIndex>)args[2];
            MboxFullTextIndex fti = LoadOrBuildFullTextIndex(file, messages, searchWorker);
            List<string> tokens = Tokenize(query).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var candidates = new List<int>();
            for (int i = 0; i < messages.Count && i < fti.Filters.Count; i++)
            {
                if (searchWorker.CancellationPending) { e.Cancel = true; return; }
                if (BloomMayContainAll(fti.Filters[i], tokens)) candidates.Add(i);
            }

            var result = new MboxSearchResult();
            result.FilePath = file;
            result.IndexFromCache = fti.FromCache;
            result.CandidateCount = candidates.Count;
            int done = 0;
            foreach (int i in candidates)
            {
                if (searchWorker.CancellationPending) { e.Cancel = true; return; }
                MboxMessageIndex m = messages[i];
                string searchable = BuildSearchableText(file, m, SearchMaxBytes);
                if (Contains(searchable, query) || tokens.All(t => Contains(searchable, t))) result.MessageNumbers.Add(m.Number);
                done++;
                if ((done % 50) == 0) searchWorker.ReportProgress(0, "Volltextsuche: " + done + " / " + candidates.Count + " Kandidaten");
            }
            e.Result = result;
        }

        private void SearchWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null) lblStatus.Text = e.UserState.ToString();
        }

        private void SearchWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            progress.Visible = false;
            progress.MarqueeAnimationSpeed = 0;
            SetViewerBusy(false);
            if (e.Cancelled) { lblStatus.Text = "Volltextsuche abgebrochen."; return; }
            if (e.Error != null)
            {
                lblStatus.Text = "Volltextsuche fehlgeschlagen: " + e.Error.Message;
                MessageBox.Show(e.Error.Message, "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var result = e.Result as MboxSearchResult;
            if (result == null) return;
            var wanted = new HashSet<int>(result.MessageNumbers);
            grid.Rows.Clear();
            foreach (var m in allMessages.Where(x => wanted.Contains(x.Number)))
            {
                int row = grid.Rows.Add(m.Number.ToString(), m.Date, m.From, m.To, m.Subject, FormatBytes(m.Length));
                grid.Rows[row].Tag = m;
            }
            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
            lblStatus.Text = grid.Rows.Count + " Volltexttreffer • " + result.CandidateCount + " Kandidaten • " + (result.IndexFromCache ? "Index geladen" : "Index erstellt");
        }

        private static string GetFullTextIndexPath(string file)
        {
            return Path.ChangeExtension(GetIndexPath(file), ".fti");
        }

        private static MboxFullTextIndex LoadOrBuildFullTextIndex(string file, List<MboxMessageIndex> messages, BackgroundWorker worker)
        {
            string path = GetFullTextIndexPath(file);
            var fi = new FileInfo(file);
            try
            {
                if (File.Exists(path))
                {
                    using (var br = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
                    {
                        if (br.ReadString() == "CMIMAP_FTI_1" && br.ReadInt64() == fi.Length && br.ReadInt64() == fi.LastWriteTimeUtc.Ticks && br.ReadInt32() == messages.Count)
                        {
                            var filters = new List<byte[]>(messages.Count);
                            for (int i = 0; i < messages.Count; i++)
                            {
                                byte[] bits = br.ReadBytes(BloomBytes);
                                if (bits.Length != BloomBytes) throw new InvalidDataException("Volltextindex unvollständig.");
                                filters.Add(bits);
                            }
                            return new MboxFullTextIndex { FilePath = file, Filters = filters, FromCache = true };
                        }
                    }
                }
            }
            catch { }

            var built = new List<byte[]>(messages.Count);
            for (int i = 0; i < messages.Count; i++)
            {
                if (worker.CancellationPending) break;
                string text = BuildSearchableText(file, messages[i], SearchMaxBytes);
                built.Add(BuildBloomFilter(text));
                if ((i % 100) == 0) worker.ReportProgress(0, "Volltextindex: " + (i + 1) + " / " + messages.Count + " Nachrichten");
            }
            if (built.Count == messages.Count)
            {
                try
                {
                    string tmp = path + ".tmp";
                    using (var bw = new BinaryWriter(File.Create(tmp), Encoding.UTF8))
                    {
                        bw.Write("CMIMAP_FTI_1");
                        bw.Write(fi.Length);
                        bw.Write(fi.LastWriteTimeUtc.Ticks);
                        bw.Write(messages.Count);
                        foreach (byte[] bits in built) bw.Write(bits);
                    }
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch { }
            }
            return new MboxFullTextIndex { FilePath = file, Filters = built, FromCache = false };
        }

        private static byte[] BuildBloomFilter(string text)
        {
            byte[] bits = new byte[BloomBytes];
            foreach (string token in Tokenize(text).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                uint h1 = Fnv1a(token, 2166136261u);
                uint h2 = Fnv1a(token, 2166136261u ^ 0x9e3779b9u);
                SetBloomBit(bits, h1);
                SetBloomBit(bits, h2);
                SetBloomBit(bits, h1 ^ (h2 << 1));
                SetBloomBit(bits, h2 ^ (h1 >> 1));
            }
            return bits;
        }

        private static bool BloomMayContainAll(byte[] bits, List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return true;
            foreach (string token in tokens)
            {
                uint h1 = Fnv1a(token, 2166136261u);
                uint h2 = Fnv1a(token, 2166136261u ^ 0x9e3779b9u);
                if (!HasBloomBit(bits, h1) || !HasBloomBit(bits, h2) || !HasBloomBit(bits, h1 ^ (h2 << 1)) || !HasBloomBit(bits, h2 ^ (h1 >> 1))) return false;
            }
            return true;
        }

        private static void SetBloomBit(byte[] bits, uint hash)
        {
            int bit = (int)(hash % (BloomBytes * 8));
            bits[bit / 8] |= (byte)(1 << (bit % 8));
        }

        private static bool HasBloomBit(byte[] bits, uint hash)
        {
            if (bits == null || bits.Length != BloomBytes) return false;
            int bit = (int)(hash % (BloomBytes * 8));
            return (bits[bit / 8] & (1 << (bit % 8))) != 0;
        }

        private static uint Fnv1a(string value, uint seed)
        {
            uint hash = seed;
            string v = (value ?? "").ToLowerInvariant();
            for (int i = 0; i < v.Length; i++) { hash ^= v[i]; hash *= 16777619u; }
            return hash;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) yield break;
            foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{Nd}]{2,}")) yield return m.Value;
        }

        private static string BuildSearchableText(string file, MboxMessageIndex message, long maxBytes)
        {
            long take = Math.Min(message.Length, maxBytes);
            byte[] data = new byte[(int)take];
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(message.Offset, SeekOrigin.Begin);
                int got = 0;
                while (got < data.Length)
                {
                    int n = fs.Read(data, got, data.Length - got);
                    if (n <= 0) break;
                    got += n;
                }
                if (got != data.Length) Array.Resize(ref data, got);
            }
            string raw = Encoding.GetEncoding(28591).GetString(data);
            MboxPreviewResult parsed = ParseMimeEntity(raw, 0);
            return String.Join("\n", new string[] { message.Date, message.From, message.To, message.Subject, parsed.Text ?? "" });
        }

        private void ApplyFilter()
        {
            string q = (txtSearch.Text ?? "").Trim();
            IEnumerable<MboxMessageIndex> items = allMessages;
            if (q.Length > 0)
            {
                items = items.Where(delegate(MboxMessageIndex m)
                {
                    return Contains(m.Subject, q) || Contains(m.From, q) || Contains(m.To, q) || Contains(m.Date, q);
                });
            }

            grid.Rows.Clear();
            foreach (var m in items)
            {
                int row = grid.Rows.Add(m.Number.ToString(), m.Date, m.From, m.To, m.Subject, FormatBytes(m.Length));
                grid.Rows[row].Tag = m;
            }
            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
            lblStatus.Text = grid.Rows.Count + " von " + allMessages.Count + " Nachricht(en) angezeigt.";
        }

        private static bool Contains(string value, string query)
        {
            return (value ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0 || String.IsNullOrEmpty(currentFile)) return;
            var message = grid.SelectedRows[0].Tag as MboxMessageIndex;
            if (message == null) return;
            ShowMessage(message);
        }

        private void ShowMessage(MboxMessageIndex message)
        {
            try
            {
                MboxPreviewResult result = LoadPreview(currentFile, message);
                var sb = new StringBuilder();
                sb.AppendLine("Von: " + message.From);
                sb.AppendLine("An: " + message.To);
                sb.AppendLine("Datum: " + message.Date);
                sb.AppendLine("Betreff: " + message.Subject);
                sb.AppendLine(new string('─', 78));
                if (result.Truncated)
                {
                    sb.AppendLine("[Hinweis: Die Nachricht ist sehr groß. Für die Vorschau wurden nur die ersten " + FormatBytes(PreviewMaxBytes) + " geladen.]\r\n");
                }
                sb.Append(result.Text ?? "");
                preview.Text = sb.ToString();
                preview.SelectionStart = 0;
                preview.ScrollToCaret();

                lstAttachments.Items.Clear();
                foreach (var a in result.Attachments) lstAttachments.Items.Add(a);
                btnSaveAttachment.Enabled = lstAttachments.Items.Count > 0;
            }
            catch (Exception ex)
            {
                preview.Text = "Nachricht konnte nicht angezeigt werden.\r\n\r\n" + ex.Message;
                lstAttachments.Items.Clear();
            }
        }

        private static MboxPreviewResult LoadPreview(string file, MboxMessageIndex message)
        {
            long take = Math.Min(message.Length, PreviewMaxBytes);
            byte[] data = new byte[(int)take];
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(message.Offset, SeekOrigin.Begin);
                int got = 0;
                while (got < data.Length)
                {
                    int n = fs.Read(data, got, data.Length - got);
                    if (n <= 0) break;
                    got += n;
                }
                if (got != data.Length) Array.Resize(ref data, got);
            }

            string raw = Encoding.GetEncoding(28591).GetString(data);
            var result = ParseMimeEntity(raw, 0);
            result.Truncated = message.Length > take;
            if (String.IsNullOrWhiteSpace(result.Text)) result.Text = "[Kein darstellbarer Textinhalt gefunden.]";
            return result;
        }

        private static MboxPreviewResult ParseMimeEntity(string raw, int depth)
        {
            var result = new MboxPreviewResult();
            if (depth > 8 || String.IsNullOrEmpty(raw)) return result;

            int sepLen;
            int sep = FindTextHeaderEnd(raw, out sepLen);
            string headerText = sep >= 0 ? raw.Substring(0, sep) : "";
            string body = sep >= 0 ? raw.Substring(sep + sepLen) : raw;
            var headers = ParseHeaderBlock(headerText);
            string contentType = GetHeader(headers, "Content-Type");
            string transfer = GetHeader(headers, "Content-Transfer-Encoding");
            string disposition = GetHeader(headers, "Content-Disposition");
            string boundary = GetParameter(contentType, "boundary");
            string fileName = GetParameter(disposition, "filename");
            if (String.IsNullOrEmpty(fileName)) fileName = GetParameter(contentType, "name");
            fileName = DecodeMimeWords(TrimQuotes(fileName));

            bool multipart = contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0 && !String.IsNullOrEmpty(boundary);
            if (multipart)
            {
                string marker = "--" + boundary;
                string[] parts = body.Split(new string[] { marker }, StringSplitOptions.None);
                string plain = null;
                string html = null;
                foreach (string p0 in parts)
                {
                    string p = p0.TrimStart('\r', '\n');
                    if (p.Length == 0 || p.StartsWith("--")) continue;
                    MboxPreviewResult child = ParseMimeEntity(p, depth + 1);
                    if (child.Attachments.Count > 0) result.Attachments.AddRange(child.Attachments);
                    if (!String.IsNullOrWhiteSpace(child.Text))
                    {
                        if (plain == null) plain = child.Text;
                        if (html == null && p.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0) html = child.Text;
                    }
                }
                result.Text = plain ?? html ?? "";
                return result;
            }

            byte[] decoded = DecodeTransfer(body, transfer);
            string mediaType = contentType;
            int semicolon = mediaType.IndexOf(';');
            if (semicolon >= 0) mediaType = mediaType.Substring(0, semicolon);
            mediaType = mediaType.Trim().ToLowerInvariant();
            if (String.IsNullOrEmpty(mediaType)) mediaType = "text/plain";

            bool attachment = disposition.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0 || !String.IsNullOrEmpty(fileName);
            if (attachment)
            {
                if (String.IsNullOrEmpty(fileName)) fileName = "Anhang.bin";
                result.Attachments.Add(new MboxAttachment { FileName = SanitizeFileName(fileName), ContentType = mediaType, Data = decoded });
                return result;
            }

            if (mediaType.StartsWith("text/"))
            {
                string charset = TrimQuotes(GetParameter(contentType, "charset"));
                Encoding enc;
                try { enc = String.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
                catch { enc = Encoding.UTF8; }
                string text;
                try { text = enc.GetString(decoded); }
                catch { text = Encoding.UTF8.GetString(decoded); }
                if (mediaType == "text/html") text = HtmlToText(text);
                result.Text = text;
            }
            return result;
        }

        private static int FindTextHeaderEnd(string raw, out int sepLen)
        {
            int i = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (i >= 0) { sepLen = 4; return i; }
            i = raw.IndexOf("\n\n", StringComparison.Ordinal);
            if (i >= 0) { sepLen = 2; return i; }
            sepLen = 0;
            return -1;
        }

        private static string GetParameter(string header, string name)
        {
            if (String.IsNullOrEmpty(header)) return "";
            Match m = Regex.Match(header, "(?:^|;)\\s*" + Regex.Escape(name) + "\\s*=\\s*(?:\"([^\"]*)\"|([^;\\r\\n]*))", RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value.Trim();
        }

        private static string TrimQuotes(string s)
        {
            if (String.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') return s.Substring(1, s.Length - 2);
            return s;
        }

        private static byte[] DecodeTransfer(string body, string transfer)
        {
            string t = (transfer ?? "").Trim().ToLowerInvariant();
            if (t == "base64")
            {
                try { return Convert.FromBase64String(Regex.Replace(body, @"\s+", "")); }
                catch { return Encoding.GetEncoding(28591).GetBytes(body); }
            }
            if (t == "quoted-printable") return DecodeQuotedPrintableToBytes(body);
            return Encoding.GetEncoding(28591).GetBytes(body);
        }

        private static byte[] DecodeQuotedPrintableToBytes(string text)
        {
            using (var ms = new MemoryStream())
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '=')
                    {
                        if (i + 2 < text.Length && text[i + 1] == '\r' && text[i + 2] == '\n') { i += 2; continue; }
                        if (i + 1 < text.Length && text[i + 1] == '\n') { i += 1; continue; }
                        if (i + 2 < text.Length)
                        {
                            int hi = Hex(text[i + 1]);
                            int lo = Hex(text[i + 2]);
                            if (hi >= 0 && lo >= 0) { ms.WriteByte((byte)((hi << 4) | lo)); i += 2; continue; }
                        }
                    }
                    if (c <= 255) ms.WriteByte((byte)c);
                    else
                    {
                        byte[] b = Encoding.UTF8.GetBytes(new char[] { c });
                        ms.Write(b, 0, b.Length);
                    }
                }
                return ms.ToArray();
            }
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return -1;
        }

        private static string HtmlToText(string html)
        {
            if (String.IsNullOrEmpty(html)) return "";
            string s = Regex.Replace(html, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(br|p|div|tr|li|h[1-6])\b[^>]*>", "\r\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = WebUtility.HtmlDecode(s);
            s = Regex.Replace(s, @"\r?\n[ \t]+", "\r\n");
            s = Regex.Replace(s, @"\r?\n{3,}", "\r\n\r\n");
            return s.Trim();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return String.IsNullOrWhiteSpace(name) ? "Anhang.bin" : name;
        }


        private void PrintSelectedMessage(bool asPdf)
        {
            var message = GetSelectedMessage();
            if (message == null || String.IsNullOrEmpty(currentFile)) return;
            MboxPreviewResult result;
            try { result = LoadPreview(currentFile, message); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            string text = "Von: " + message.From + "\r\nAn: " + message.To + "\r\nDatum: " + message.Date + "\r\nBetreff: " + message.Subject + "\r\n" + new string('-', 72) + "\r\n" + (result.Text ?? "");
            using (var doc = new PrintDocument())
            {
                doc.DocumentName = String.IsNullOrWhiteSpace(message.Subject) ? "E-Mail" : message.Subject;
                int charIndex = 0;
                Font font = new Font("Segoe UI", 10F);
                doc.PrintPage += delegate(object sender, PrintPageEventArgs e)
                {
                    RectangleF area = e.MarginBounds;
                    int charsFitted, linesFilled;
                    e.Graphics.MeasureString(text.Substring(charIndex), font, area.Size, StringFormat.GenericTypographic, out charsFitted, out linesFilled);
                    if (charsFitted <= 0) charsFitted = Math.Min(1, text.Length - charIndex);
                    e.Graphics.DrawString(text.Substring(charIndex, charsFitted), font, Brushes.Black, area, StringFormat.GenericTypographic);
                    charIndex += charsFitted;
                    e.HasMorePages = charIndex < text.Length;
                };

                if (asPdf)
                {
                    string pdfPrinter = PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault(x => String.Equals(x, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase));
                    if (String.IsNullOrEmpty(pdfPrinter))
                    {
                        MessageBox.Show("Der Windows-Drucker 'Microsoft Print to PDF' wurde nicht gefunden.", "PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "PDF-Datei (*.pdf)|*.pdf";
                        dlg.FileName = SanitizeFileName((String.IsNullOrWhiteSpace(message.Subject) ? "E-Mail" : message.Subject) + ".pdf");
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        doc.PrinterSettings.PrinterName = pdfPrinter;
                        doc.PrinterSettings.PrintToFile = true;
                        doc.PrinterSettings.PrintFileName = dlg.FileName;
                        try { doc.Print(); MessageBox.Show("PDF wurde erstellt.", "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                        catch (Exception ex) { MessageBox.Show("PDF-Druck fehlgeschlagen.\r\n\r\n" + ex.Message, "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                }
                else
                {
                    using (var dlg = new PrintDialog())
                    {
                        dlg.Document = doc;
                        dlg.UseEXDialog = true;
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        try { doc.Print(); }
                        catch (Exception ex) { MessageBox.Show("Drucken fehlgeschlagen.\r\n\r\n" + ex.Message, "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                }
                font.Dispose();
            }
        }

        private void BtnExportEml_Click(object sender, EventArgs e)
        {
            var message = GetSelectedMessage();
            if (message == null || String.IsNullOrEmpty(currentFile)) return;
            string proposed = SanitizeFileName((String.IsNullOrWhiteSpace(message.Subject) ? "Nachricht" : message.Subject) + ".eml");
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "E-Mail-Datei (*.eml)|*.eml|Alle Dateien (*.*)|*.*";
                dlg.FileName = proposed;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                CopySegment(currentFile, message.Offset, message.Length, dlg.FileName);
                MessageBox.Show("Nachricht wurde als EML exportiert.", "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private MboxMessageIndex GetSelectedMessage()
        {
            if (grid.SelectedRows.Count == 0) return null;
            return grid.SelectedRows[0].Tag as MboxMessageIndex;
        }

        private static void CopySegment(string source, long offset, long length, string target)
        {
            byte[] buffer = new byte[1024 * 1024];
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.Seek(offset, SeekOrigin.Begin);
                long remaining = length;
                while (remaining > 0)
                {
                    int ask = (int)Math.Min(buffer.Length, remaining);
                    int n = input.Read(buffer, 0, ask);
                    if (n <= 0) break;
                    output.Write(buffer, 0, n);
                    remaining -= n;
                }
            }
        }

        private void SaveSelectedAttachment()
        {
            var a = lstAttachments.SelectedItem as MboxAttachment;
            if (a == null || a.Data == null) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = a.FileName;
                dlg.Filter = "Alle Dateien (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllBytes(dlg.FileName, a.Data);
                MessageBox.Show("Anhang wurde gespeichert.", "MBOX Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string FormatBytes(long bytes)
        {
            double value = Math.Max(0, bytes);
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (value >= 1024 && i < units.Length - 1) { value /= 1024.0; i++; }
            return value.ToString(i == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[i];
        }
    }
}
