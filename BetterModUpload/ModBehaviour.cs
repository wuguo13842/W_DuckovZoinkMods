using BetterModUpload.Pathcers;
using Duckov.Modding.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZoinkModdingLibrary;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.Patcher;

namespace BetterModUpload
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static readonly string MOD_ID = "com.zoink.bettermodupload";
        public static readonly string MOD_NAME = "BetterModUpload";
        public static readonly string TEMP_FOLDER = Path.Combine(Path.GetTempPath(), "DuckovModTemp");
        private static ModBehaviour? instance;
        private GameObject? filesInfoRootObj;
        private bool inited = false;

        public TextMeshProUGUI? ignoreDetected { get; private set; }
        private TextMeshProUGUI? uploadFilesInfoTitle { get; set; }
        public TextMeshProUGUI? uploadFilesInfo { get; private set; }
        private TextMeshProUGUI? ignoredFilesInfoTitle { get; set; }
        public TextMeshProUGUI? ignoredFilesInfo { get; private set; }
        public static ModBehaviour? Instance => instance;
        public static Harmony Harmony { get; } = new Harmony(MOD_ID);

        private List<PatcherBase> patchers = new List<PatcherBase>
        {
            ModEntryPatcher.Instance
        };

        void Awake()
        {
            if (Instance != null)
            {
                Log.Error("ModBehaviour 已实例化");
                return;
            }
            instance = this;

        }

        private void OnEnable()
        {
            Log.Info("Patching...");
            foreach (var patcher in patchers)
            {
                patcher.Setup(Harmony).Patch();
            }
            //Initialize();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                inited = false;
                Initialize();
            }
        }

        private void OnDisable()
        {
            Log.Error("Unpatching...");
            foreach (var patcher in patchers)
            {
                patcher.Unpatch();
            }
        }

        private void Update()
        {
            if (!inited)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            if (inited) return;
            GameObject? menu = GameObject.Find("Canvas/MainMenuContainer/Menu");
            Transform? extraInfo = menu?.transform.Find("ModManagerUI/Main/UploadPanel/PanelContainer/Panel/ExtraInfo");
            if (extraInfo == null)
            {
                inited = false;
                return;
            }

            //根节点
            Log.Warning("正在创建上传文件列表");
            filesInfoRootObj = new GameObject("FilesToUpload");
            filesInfoRootObj.transform.SetParent(extraInfo);
            filesInfoRootObj.transform.SetSiblingIndex(2);
            VerticalLayoutGroup layout = filesInfoRootObj.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childScaleHeight = true;
            layout.childScaleWidth = true;

            //steamignore 提示
            GameObject ignoreDetectedObj = new GameObject("IgnoreDetected");
            ignoreDetectedObj.transform.SetParent(filesInfoRootObj.transform);
            ignoreDetected = ignoreDetectedObj.AddComponent<TextMeshProUGUI>();
            ignoreDetected.color = Color.yellow;
            ignoreDetected.fontSize = 16f;
            ignoreDetected.text = "检测到 .steamignore 文件，匹配其规则的文件将不上传";
            ignoreDetectedObj.SetActive(false);

            //待上传文件标题
            GameObject uploadFilesInfoTitleObj = new GameObject("UploadTitle");
            uploadFilesInfoTitleObj.transform.SetParent(filesInfoRootObj.transform);
            uploadFilesInfoTitle = uploadFilesInfoTitleObj.AddComponent<TextMeshProUGUI>();
            uploadFilesInfoTitle.color = Color.green;
            uploadFilesInfoTitle.fontSize = 20f;
            uploadFilesInfoTitle.fontStyle = FontStyles.Bold;
            uploadFilesInfoTitle.text = "Files To Upload:";

            //待上传文件
            GameObject uploadFilesInfoFilesObj = new GameObject("UploadFiles");
            uploadFilesInfoFilesObj.transform.SetParent(filesInfoRootObj.transform);
            uploadFilesInfo = uploadFilesInfoFilesObj.AddComponent<TextMeshProUGUI>();
            uploadFilesInfo.color = Color.green;
            uploadFilesInfo.fontSize = 16f;

            //忽略文件标题
            GameObject ignoredFilesInfoTitleObj = new GameObject("IgnoredTitle");
            ignoredFilesInfoTitleObj.transform.SetParent(filesInfoRootObj.transform);
            ignoredFilesInfoTitle = ignoredFilesInfoTitleObj.AddComponent<TextMeshProUGUI>();
            ignoredFilesInfoTitle.color = Color.gray;
            ignoredFilesInfoTitle.fontSize = 20f;
            ignoredFilesInfoTitle.fontStyle = FontStyles.Bold;
            ignoredFilesInfoTitle.text = "Ignored Files:";

            //忽略文件
            GameObject ignoredFilesInfoFilesObj = new GameObject("IgnoredFiles");
            ignoredFilesInfoFilesObj.transform.SetParent(filesInfoRootObj.transform);
            ignoredFilesInfo = ignoredFilesInfoFilesObj.AddComponent<TextMeshProUGUI>();
            ignoredFilesInfo.color = Color.gray;
            ignoredFilesInfo.fontSize = 16f;
            ignoredFilesInfo.fontStyle = FontStyles.Strikethrough;

            Log.Warning("上传文件列表创建成功");
            inited = true;
        }
    }
}
