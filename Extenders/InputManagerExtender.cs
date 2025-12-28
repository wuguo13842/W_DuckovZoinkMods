using HarmonyLib;
using UnityEngine;
using System.Reflection;
using MiniMap.Managers;

public static class InputManagerExtenderCommon
{
    public static FieldInfo getField(string fieldName)
    {
        return typeof(InputManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static MethodInfo getMethod(string methodName)
    {
        return typeof(InputManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
    }
}

[HarmonyPatch(typeof(InputManager))]
[HarmonyPatch("ActiveInput")]
public static class InputManagerActiveInputExtender
{
    private static FieldInfo? blockInputSourcesField;
    private static bool isFieldInfoInitialized = false;

    public static void Postfix()
    {
        if (!CustomMinimapManager.isEnabled || LevelManager.Instance == null)
            return;
        int count = GetBlockInputSourcesCount(LevelManager.Instance.InputManager);
        if (count <= 0)
            CustomMinimapManager.TryShow();
    }

    private static void InitializeFieldInfo()
    {
        if (isFieldInfoInitialized) return;

        try
        {
            blockInputSourcesField = typeof(InputManager).GetField("blockInputSources", BindingFlags.NonPublic | BindingFlags.Instance);
            isFieldInfoInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MiniMap] Error initializing field info: {e.Message}");
        }
    }

    public static int GetBlockInputSourcesCount(InputManager inputManager)
    {
        if (inputManager == null) return 0;

        if (!isFieldInfoInitialized)
        {
            InitializeFieldInfo();
        }

        if (blockInputSourcesField == null) return 0;

        try
        {
            object blockInputSourcesValue = blockInputSourcesField.GetValue(inputManager);

            if (blockInputSourcesValue != null)
            {
                PropertyInfo countProperty = blockInputSourcesValue.GetType().GetProperty("Count");
                if (countProperty != null)
                {
                    return (int)countProperty.GetValue(blockInputSourcesValue);
                }
            }

            return 0;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MiniMap] Error getting blockInputSources count: {e.Message}");
            return 0;
        }
    }
}
