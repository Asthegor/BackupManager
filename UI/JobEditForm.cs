using BackupManager.Models;


namespace BackupManager.UI
{
    public partial class JobEditForm : Form
    {
        private readonly bool _isEdit;
        private readonly BackupJob _job;

        private readonly TextBox txtName = null!;
        private readonly TextBox txtSrc = null!;
        private readonly TextBox txtDst = null!;
        private readonly DateTimePicker dtpTime = null!;
        private readonly CheckBox chkActive = null!;
        private readonly TreeView tvExclusions = null!;

        public BackupJob Result => _job;

        public JobEditForm(BackupJob? existing = null)
        {
            _isEdit = existing != null;
            _job = existing ?? new BackupJob();

            Text = _isEdit ? $"Modifier – {_job.Name}" : "Ajouter un job";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int xL = 14, xR = 180, wR = 360, y = 14, h = 23, line = 32;

            // Nom
            Controls.Add(new Label { Location = new Point(xL, y), AutoSize = true, Text = "Nom :" });
            txtName = new TextBox { Location = new Point(xR, y), Size = new Size(wR, h), Text = _job.Name };
            Controls.Add(txtName);
            y += line;

            // Source
            Controls.Add(new Label { Location = new Point(xL, y), AutoSize = true, Text = "Source :" });
            txtSrc = new TextBox { Location = new Point(xR, y), Size = new Size(wR - 90, h), Text = _job.SourceDirectory };
            var btnSrc = new Button { Location = new Point(xR + wR - 84, y - 1), Size = new Size(80, h + 2), Text = "Parcourir" };
            btnSrc.Click += (s, e) => BrowseFolder(txtSrc, PopulateTree);
            Controls.Add(txtSrc);
            Controls.Add(btnSrc);
            y += line;

            // Destination
            Controls.Add(new Label { Location = new Point(xL, y), AutoSize = true, Text = "Destination :" });
            txtDst = new TextBox { Location = new Point(xR, y), Size = new Size(wR - 90, h), Text = _job.DestinationDirectory };
            var btnDst = new Button { Location = new Point(xR + wR - 84, y - 1), Size = new Size(80, h + 2), Text = "Parcourir" };
            btnDst.Click += (s, e) => BrowseFolder(txtDst, null);
            Controls.Add(txtDst);
            Controls.Add(btnDst);
            y += line;

            // Heure de sauvegarde
            Controls.Add(new Label { Location = new Point(xL, y), AutoSize = true, Text = "Heure :" });

            dtpTime = new DateTimePicker
            {
                Location = new Point(xR, y),
                Size = new Size(120, 23),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",      // Affiche uniquement heure et minutes
                ShowUpDown = true,            // Affiche les flèches au lieu du calendrier
                Value = DateTime.Today.Add(_job.BackupTime)  // Initialise à l'heure du job
            };

            Controls.Add(dtpTime);
            y += line;

            // Activé
            chkActive = new CheckBox { Location = new Point(xR, y), AutoSize = true, Text = "Activé", Checked = _job.IsActive };
            Controls.Add(chkActive);
            y += line;

            // TreeView exclusions
            Controls.Add(new Label { Location = new Point(xL, y), AutoSize = true, Text = "Répertoires à exclure :" });
            y += line - 6;
            tvExclusions = new TreeView
            {
                Location = new Point(xR, y),
                Size = new Size(wR, 160),
                CheckBoxes = true,
                Scrollable = true
            };
            Controls.Add(tvExclusions);
            y += 160;

            // Buttons OK/Cancel
            var btnOk = new Button { Text = _isEdit ? "Enregistrer" : "Ajouter", Location = new Point(ClientSize.Width - 200, ClientSize.Height - 40), Size = new Size(84, 26), DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Annuler", Location = new Point(ClientSize.Width - 106, ClientSize.Height - 40), Size = new Size(84, 26), DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) =>
            {
                SaveIntoModel();
                this.DialogResult = DialogResult.OK; // ✅ ferme le formulaire correctement
                this.Close();
            };
            Controls.AddRange([btnOk, btnCancel]);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Initialisation TreeView si source définie
            PopulateTree();
        }

        private static void BrowseFolder(TextBox target, Action? after = null)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                target.Text = dlg.SelectedPath;
                after?.Invoke();
            }
        }
        private void PopulateTree()
        {
            tvExclusions.Nodes.Clear();
            string source = txtSrc.Text.Trim();
            if (!Directory.Exists(source))
                return;

            static TreeNode CreateNode(string path)
            {
                var dirInfo = new DirectoryInfo(path);
                var node = new TreeNode(dirInfo.Name) { Tag = path };

                // Ajouter uniquement les sous-répertoires
                foreach (var dir in dirInfo.GetDirectories())
                {
                    node.Nodes.Add(CreateNode(dir.FullName));
                }

                return node;
            }

            try
            {
                // Ajouter seulement les sous-dossiers du répertoire source
                foreach (var dir in new DirectoryInfo(source).GetDirectories())
                {
                    tvExclusions.Nodes.Add(CreateNode(dir.FullName));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignorer les dossiers auxquels l'accès est refusé
            }

            // Restaurer les exclusions existantes
            foreach (TreeNode node in tvExclusions.Nodes)
                RestoreChecks(node, _job.ExcludedDirectories, source);
        }


        private static void RestoreChecks(TreeNode node, List<string> exclusions, string source)
        {
            string rel = Path.GetRelativePath(source, node?.Tag?.ToString() ?? string.Empty);
            if (exclusions.Any(e => e.Equals(rel, StringComparison.OrdinalIgnoreCase)))
                node?.Checked = true;

            foreach (TreeNode child in node?.Nodes!)
                RestoreChecks(child, exclusions, source);
        }

        private void SaveIntoModel()
        {
            _job.Name = txtName.Text.Trim();
            _job.SourceDirectory = txtSrc.Text.Trim();
            _job.DestinationDirectory = txtDst.Text.Trim();
            _job.BackupTime = dtpTime.Value.TimeOfDay;
            _job.IsActive = chkActive.Checked;

            var exclusions = new List<string>();
            foreach (TreeNode node in tvExclusions.Nodes)
                CollectChecked(node, _job.SourceDirectory, exclusions);
            _job.ExcludedDirectories = exclusions;
        }

        private static void CollectChecked(TreeNode node, string rootPath, List<string> list)
        {
            if (node.Checked)
            {
                string rel = Path.GetRelativePath(rootPath, node?.Tag?.ToString() ?? string.Empty);
                list.Add(rel);
            }

            foreach (TreeNode child in node?.Nodes!)
                CollectChecked(child, rootPath, list);
        }
    }
}
