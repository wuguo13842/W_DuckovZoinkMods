using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ZoinkModdingLibrary.Logging;

namespace ZoinkModdingLibrary.Utils
{
    public class SystemFileOperations
    {
        public static bool IsFolderEmpty(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return true;
            }
            return !Directory.EnumerateFiles(folderPath).Any() && !Directory.EnumerateDirectories(folderPath).Any();
        }

        public static bool ClearFolder(string folderPath, bool keepFolder = true)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Log.Warning($"文件夹不存在: {folderPath}");
                    if (keepFolder)
                    {
                        Directory.CreateDirectory(folderPath);
                        Log.Warning($"已创建文件夹: {folderPath}");
                    }
                    return true;
                }
                var isEmpty = IsFolderEmpty(folderPath);
                if (isEmpty)
                {
                    Log.Warning($"文件夹已为空: {folderPath}");
                    return true;
                }

                Log.Warning($"正在清空文件夹: {folderPath}");
                Directory.Delete(folderPath, true);
                if (keepFolder)
                {
                    Directory.CreateDirectory(folderPath);
                    Log.Warning($"文件夹已清空: {folderPath}");
                }
                else
                {
                    Log.Warning($"文件夹已删除: {folderPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"清空文件夹失败 {folderPath}: {ex.Message}");
                return false;
            }
        }

        public static void CopyFolder(string sourcePath, string targetPath, bool overwrite = true)
        {
            if (!Directory.Exists(sourcePath))
            {
                Log.Error($"源文件夹不存在: {sourcePath}");
                return;
            }
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
            var files = Directory.GetFiles(sourcePath);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetPath, fileName);
                File.Copy(file, destFile, overwrite);
            }
            var directories = Directory.GetDirectories(sourcePath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(targetPath, dirName);
                CopyFolder(dir, destDir, overwrite);
            }
        }

        public static void CopyFilesWithStructure(IEnumerable<string> files, string sourceRoot, string targetRoot, bool overwrite = true)
        {
            if (files == null || !files.Any())
            {
                Log.Warning("文件列表为空");
                return;
            }
            if (!Directory.Exists(sourceRoot))
            {
                Log.Error($"源根目录不存在: {sourceRoot}");
                return;
            }
            Log.Warning($"开始复制文件...");

            foreach (string sourceFile in files)
            {
                try
                {
                    string path = sourceFile;
                    if (Path.IsPathRooted(path))
                    {
                        path = Path.GetRelativePath(sourceRoot, path);
                    }
                    if (string.IsNullOrEmpty(path) || path == ".")
                    {
                        Log.Warning($"跳过无效路径: {sourceFile}");
                        continue;
                    }

                    var targetFile = Path.GetFullPath(path, targetRoot);
                    var targetDir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDir) && !string.IsNullOrEmpty(targetDir))
                    {
                        Log.Warning($"创建文件夹：{targetDir}");
                        Directory.CreateDirectory(targetDir);
                    }
                    string sourcePath = Path.GetFullPath(path, sourceRoot);
                    File.Copy(sourcePath, targetFile, overwrite);
                }
                catch (Exception ex)
                {
                    Log.Error($"复制失败 {sourceFile}: {ex.Message}"); 
                }
            }
            Log.Warning($"文件复制完毕");
        }
    }
}
