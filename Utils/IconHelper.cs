using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace BackupManager.Utils
{
    public static class IconHelper
    {
        private static Assembly Assembly => Assembly.GetExecutingAssembly();

        private static Image LoadImage(string resourceName, Image fallback)
        {
            var fullName = $"BackupManager.Resources.{resourceName}";
            using Stream? stream = Assembly.GetManifestResourceStream(fullName);
            return stream != null ? Image.FromStream(stream) : fallback;
        }

        private static Icon LoadIcon(string resourceName, Icon fallback)
        {
            var fullName = $"BackupManager.Resources.{resourceName}";
            using Stream? stream = Assembly.GetManifestResourceStream(fullName);
            return stream != null ? new Icon(stream) : fallback;
        }

        // --- JobPanel images ---
        private static Image? _folderOk;
        public static Image FolderOk => _folderOk ??= LoadImage("checked.png", SystemIcons.Information.ToBitmap());

        private static Image? _folderError;
        public static Image FolderError => _folderError ??= LoadImage("cancel.png", SystemIcons.Error.ToBitmap());

        private static Image? _openFolder;
        public static Image OpenFolder => _openFolder ??= LoadImage("dossier-ouvert.png", SystemIcons.Application.ToBitmap());

        private static Image? _jobDetail;
        public static Image JobDetail => _jobDetail ??= LoadImage("job_detail.png", SystemIcons.Question.ToBitmap());

        // --- SystemTray icons ---
        private static Icon? _trayGreen;
        public static Icon TrayGreen => _trayGreen ??= ToIcon(LoadImage("checked.png", SystemIcons.Information.ToBitmap()));

        private static Icon? _trayOrange;
        public static Icon TrayOrange => _trayOrange ??= ToIcon(LoadImage("warning.png", SystemIcons.Information.ToBitmap()));

        private static Icon? _trayRed;
        public static Icon TrayRed => _trayRed ??= ToIcon(LoadImage("cancel.png", SystemIcons.Information.ToBitmap()));

        private static Icon? _logo;
        public static Icon Logo => _logo ??= ToIcon(LoadImage("logo-medium.png", SystemIcons.Application.ToBitmap()));

        // --- Conversion PNG -> Icon pour NotifyIcon ---
        public static Icon ToIcon(Image img) => Icon.FromHandle(((Bitmap)img).GetHicon());

        public static Image ToGrayScale(Image source)
        {
            // Matrice pour convertir en gris
            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(
                [
                    [0.3f,  0.3f,   0.3f,   0,  0],
                    [0.59f, 0.59f,  0.59f,  0,  0],
                    [0.11f, 0.11f,  0.11f,  0,  0],
                    [0,     0,      0,      1,  0],
                    [0,     0,      0,      0,  1]
                ]);

            var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);

            var newImage = new Bitmap(source.Width, source.Height);

            using (var g = Graphics.FromImage(newImage))
            {
                g.DrawImage(source,
                    new Rectangle(0, 0, source.Width, source.Height),
                    0, 0, source.Width, source.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            return newImage;
        }

    }
}
