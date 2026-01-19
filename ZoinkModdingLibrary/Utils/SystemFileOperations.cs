using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        public static bool ClearFolder(string folderPath, bool keepFolder = true, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    logger.LogWarning($"文件夹不存在: {folderPath}");
                    if (keepFolder)
                    {
                        Directory.CreateDirectory(folderPath);
                        logger.LogWarning($"已创建文件夹: {folderPath}");
                    }
                    return true;
                }
                var isEmpty = IsFolderEmpty(folderPath);
                if (isEmpty)
                {
                    logger.LogWarning($"文件夹已为空: {folderPath}");
                    return true;
                }

                logger.LogWarning($"正在清空文件夹: {folderPath}");
                Directory.Delete(folderPath, true);
                if (keepFolder)
                {
                    Directory.CreateDirectory(folderPath);
                    logger.LogWarning($"文件夹已清空: {folderPath}");
                }
                else
                {
                    logger.LogWarning($"文件夹已删除: {folderPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"清空文件夹失败 {folderPath}: {ex.Message}");
                return false;
            }
        }

        public static void CopyFolder(string sourcePath, string targetPath, bool overwrite = true, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            if (!Directory.Exists(sourcePath))
            {
                logger.LogError($"源文件夹不存在: {sourcePath}");
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

        public static void CopyFilesWithStructure(IEnumerable<string> files, string sourceRoot, string targetRoot, bool overwrite = true, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            if (files == null || !files.Any())
            {
                logger.LogWarning("文件列表为空");
                return;
            }
            if (!Directory.Exists(sourceRoot))
            {
                logger.LogError($"源根目录不存在: {sourceRoot}");
                return;
            }
            logger.LogWarning($"开始复制文件...");

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
                        logger.LogWarning($"跳过无效路径: {sourceFile}");
                        continue;
                    }

                    var targetFile = Path.GetFullPath(path, targetRoot);
                    var targetDir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDir) && !string.IsNullOrEmpty(targetDir))
                    {
                        logger.LogWarning($"创建文件夹：{targetDir}");
                        Directory.CreateDirectory(targetDir);
                    }
                    string sourcePath = Path.GetFullPath(path, sourceRoot);
                    File.Copy(sourcePath, targetFile, overwrite);
                }
                catch (Exception ex)
                {
                    logger.LogError($"复制失败 {sourceFile}: {ex.Message}"); 
                }
            }
            logger.LogWarning($"文件复制完毕");
        }
    }
}
