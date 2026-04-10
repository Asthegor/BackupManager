using System;
using System.Drawing;
using System.Windows.Forms;

using BackupManager.Models;

namespace BackupManager.UI
{
    public partial class JobDetailForm : Form
    {
        public JobDetailForm(BackupJob job)
        {
            Text = $"Détails – {job.Name}";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 180);

            var lblSrc = new Label { AutoSize = true, Location = new Point(14, 16), Text = "Répertoire source :" };
            var txtSrc = new TextBox { ReadOnly = true, Location = new Point(14, 36), Size = new Size(330, 23), Text = job.SourceDirectory };

            var lblDst = new Label { AutoSize = true, Location = new Point(14, 102), Text = "Répertoire destination :" };
            var txtDst = new TextBox { ReadOnly = true, Location = new Point(14, 122), Size = new Size(330, 23), Text = job.DestinationDirectory };

            var lblTime = new Label { AutoSize = true, Location = new Point(360, 16), Text = "Heure d'activation :" };
            var txtTime = new TextBox { ReadOnly = true, Location = new Point(360, 36), Size = new Size(140, 23), Text = job.BackupTime.ToString(@"hh\:mm") };

            var lblLast = new Label { AutoSize = true, Location = new Point(360, 102), Text = "Dernière sauvegarde :" };
            var txtLast = new TextBox { ReadOnly = true, Location = new Point(360, 122), Size = new Size(140, 23), Text = job.LastBackupDate?.ToString("yyyy-MM-dd HH:mm") ?? "—" };

            Controls.AddRange([lblSrc, txtSrc, lblDst, txtDst, lblTime, txtTime, lblLast, txtLast]);
        }
    }
}
