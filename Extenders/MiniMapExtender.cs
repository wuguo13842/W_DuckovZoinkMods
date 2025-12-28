using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using HarmonyLib;
using MiniMap.Managers;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniMapCommon
{
    public const float originMapZRotation = -30f;

    private static Vector3 GetCameraForward()
    {
        int index = ModSettingManager.GetValue("facingBase", 0);
        string facingBase = ModSettingManager.GetActualDropdownValue("facingBase", index, false);
        return facingBase == "Mouse"
            ? LevelManager.Instance.InputManager.InputAimPoint - CharacterMainControl.Main.transform.position
            : CharacterMainControl.Main.movementControl.targetAimDirection;
    }

    public static Vector3 GetPlayerMinimapGlobalPosition(MiniMapDisplay minimapDisplay)
    {
        Vector3 vector;
        var sceneID = SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex);
        minimapDisplay.TryConvertWorldToMinimap(LevelManager.Instance.MainCharacter.transform.position, sceneID, out vector);
        return minimapDisplay.transform.localToWorldMatrix.MultiplyPoint(vector);
    }

    public static Quaternion GetPlayerMinimapRotation(Vector3 to)
    {
        float currentMapZRotation = Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
        return Quaternion.Euler(0f, 0f, -currentMapZRotation);
    }

    public static Quaternion GetPlayerMinimapRotation()
    {
        Vector3 to = GetCameraForward();
        return GetPlayerMinimapRotation(to);
    }

    public static Quaternion GetPlayerMinimapRotationInverse(Vector3 to)
    {
        float currentMapZRotation = Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
        return Quaternion.Euler(0f, 0f, currentMapZRotation);
    }

    public static Quaternion GetPlayerMinimapRotationInverse()
    {
        Vector3 to = GetCameraForward();
        return GetPlayerMinimapRotationInverse(to);
    }

}

[HarmonyPatch(typeof(MiniMapCompass))]
[HarmonyPatch("SetupRotation")]
public static class MiniMapCompassSetupRotationExtender
{
    static FieldInfo? arrowField;

    public static bool Prefix(MiniMapCompass __instance)
    {
        try
        {
            if (arrowField == null)
            {
                arrowField = typeof(MiniMapCompass).GetField("arrow", BindingFlags.NonPublic | BindingFlags.Instance);
                if (arrowField == null)
                {
                    Debug.Log("[MiniMap] 无法获取指南针对象");
                }
            }

            Transform? trans = arrowField?.GetValue(__instance) as Transform;
            if (trans == null)
            {
                return false;
            }
            bool mapRotation = ModSettingManager.GetValue<bool>("mapRotation");
            if (mapRotation)
            {
                trans.localRotation = MiniMapCommon.GetPlayerMinimapRotation();
            }
            else
            {
                trans.localRotation = Quaternion.Euler(0f, 0f, MiniMapCommon.originMapZRotation);
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError("[MiniMap] 设置指南针旋转时出错：" + e.ToString());
            return true;
        }
    }

}

[HarmonyPatch(typeof(MiniMapDisplay))]
[HarmonyPatch("SetupRotation")]
public static class MiniMapDisplaySetupRotationExtender
{
    public static bool Prefix(MiniMapDisplay __instance)
    {
        try
        {
            bool mapRotation = ModSettingManager.GetValue<bool>("mapRotation");
            if (mapRotation)
            {
                __instance.transform.rotation = MiniMapCommon.GetPlayerMinimapRotationInverse();
            }
            else
            {
                __instance.transform.localRotation = Quaternion.Euler(0f, 0f, MiniMapCommon.originMapZRotation);
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError("[MiniMap] 设置小地图旋转时出错：" + e.ToString());
            return true;
        }
    }
}