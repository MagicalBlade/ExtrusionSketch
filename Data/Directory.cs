using System.Windows.Forms;

namespace ExtrusionSketch.Data
{
    internal class Directory
    {

        public static string? GetFolder()
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                return folderBrowserDialog.SelectedPath;
            }
            return null;
        }
    }
}
