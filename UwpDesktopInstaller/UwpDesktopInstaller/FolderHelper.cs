using System;
using System.IO;

namespace UwpDesktopInstaller
{
    public class FolderHelper
    {
        public static void CopyDir(string fromDir, string toDir, Action<string> invokeFile = null)
        {
            if (!Directory.Exists(fromDir))
                return;

            if (!Directory.Exists(toDir))
            {
                Directory.CreateDirectory(toDir);
            }

            string[] files = Directory.GetFiles(fromDir);
            foreach (string formFileName in files)
            {
                string fileName = Path.GetFileName(formFileName);
                string toFileName = Path.Combine(toDir, fileName);
                invokeFile?.Invoke(fileName);
                File.Copy(formFileName, toFileName,true);
            }
            string[] fromDirs = Directory.GetDirectories(fromDir);
            foreach (string fromDirName in fromDirs)
            {
                string dirName = Path.GetFileName(fromDirName);
                string toDirName = Path.Combine(toDir, dirName);
                CopyDir(fromDirName, toDirName, invokeFile);
            }
        }

        public static void MoveDir(string fromDir, string toDir)
        {
            if (!Directory.Exists(fromDir))
                return;

            CopyDir(fromDir, toDir, null);
            Directory.Delete(fromDir, true);
        }
    }
}
