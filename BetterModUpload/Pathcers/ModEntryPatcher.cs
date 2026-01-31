using BetterModUpload.IgnoreParser;
using Cysharp.Threading.Tasks;
using Duckov.Modding;
using Duckov.Modding.UI;
using Sirenix.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.Patcher;
using ZoinkModdingLibrary.Utils;

namespace BetterModUpload.Pathcers
{
    [TypePatcher(typeof(ModEntry))]
    public class ModEntryPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new ModEntryPatcher();
        private ModEntryPatcher() { }


        [MethodPatcher("OnUploadButtonClicked", PatchType.Prefix, BindingFlags.Instance | BindingFlags.NonPublic)]
        public static bool OnUploadButtonClickedPrefix(ModManagerUI? ___master, ModInfo ___info)
        {

            if (___master != null)
            {
                ModBehaviour.Instance?.uploadFilesInfo?.SetText("");
                ModBehaviour.Instance?.ignoredFilesInfo?.SetText("No Ignored File!");

                string tempFolder = Path.Combine(ModBehaviour.TEMP_FOLDER, ___info.name);
                var files = Directory.GetFiles(___info.path, "*.*", SearchOption.AllDirectories)
                    .Where(s => Path.GetFileName(s) != ".steamignore")
                    .Select(s => Path.GetRelativePath(___info.path, s));

                string ignoreFilePath = Path.Combine(___info.path, ".steamignore");
                if (!File.Exists(ignoreFilePath) || string.IsNullOrWhiteSpace(File.ReadAllText(ignoreFilePath)))
                {
                    Log.Warning("未找到 .steamignore 文件或该文件为空！Mod将正常上传！");
                    ModBehaviour.Instance?.uploadFilesInfo?.SetText(string.Join("\n", files));
                    ModBehaviour.Instance?.ignoreDetected?.gameObject.SetActive(false);
                    return true;
                }
                Log.Warning("已找到 .steamignore 文件，上传时将按其规则忽略文件。");
                ModBehaviour.Instance?.ignoreDetected?.gameObject.SetActive(true);
                SteamIgnoreParser parser = new SteamIgnoreParser(ignoreFilePath);
                var filesToCopy = parser.FilterNonIgnoredFiles(files);
                var ignoredFiles = parser.FilterIgnoredFiles(files);

                ModBehaviour.Instance?.uploadFilesInfo?.SetText(string.Join("\n", filesToCopy));
                ModBehaviour.Instance?.ignoredFilesInfo?.SetText(string.Join("\n", ignoredFiles));
                SystemFileOperations.CopyFilesWithStructure(filesToCopy, ___info.path, tempFolder);

                ModInfo newInfo = new ModInfo()
                {
                    path = tempFolder,
                    description = ___info.description,
                    displayName = ___info.displayName,
                    dllFound = ___info.dllFound,
                    isSteamItem = ___info.isSteamItem,
                    name = ___info.name,
                    preview = ___info.preview,
                    publishedFileId = ___info.publishedFileId,
                    tags = ___info.tags,
                    version = ___info.version,
                };


                MethodInfo? beginUpload = ___master.GetType().GetMethod("BeginUpload", BindingFlags.Instance | BindingFlags.NonPublic);
                if (beginUpload != null)
                {
                    UniTask task = (UniTask)beginUpload.Invoke(___master, new object[] { newInfo });
                    task.ContinueWith(() =>
                    {
                        string newInfoFile = Path.Combine(newInfo.path, "info.ini");
                        string originInfoFile = Path.Combine(___info.path, "info.ini");
                        File.Copy(newInfoFile, originInfoFile, true);
                        SystemFileOperations.ClearFolder(newInfo.path, false);
                    });
                }
            }
            return false;
        }
    }
}
