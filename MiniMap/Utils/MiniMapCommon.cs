﻿using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using HarmonyLib;
using MiniMap.Managers;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniMap.Utils
{
    public static class MiniMapCommon
    {
        public const float originMapZRotation = -30f;

        // 注意：这个方法中的计算可以被CharacterMainControl的属性替换
        private static Vector3 GetCharacterForward(CharacterMainControl? character)
        {
            if (character == null)
            {
                return Vector3.zero;
            }
            string facingBase = ModSettingManager.GetActualDropdownValue("facingBase", false);
            
            // 优化建议：这些计算可以从CharacterMainControl直接获取
            return facingBase == "Mouse"
                ? LevelManager.Instance.InputManager.InputAimPoint - CharacterMainControl.Main.transform.position
                : CharacterMainControl.Main.movementControl.targetAimDirection;
        }

        private static Vector3 GetCharacterForward()
        {
            return GetCharacterForward(CharacterMainControl.Main);
        }

        public static Vector3 GetPlayerMinimapGlobalPosition(MiniMapDisplay minimapDisplay)
        {
            Vector3 vector;
            var sceneID = SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex);
            minimapDisplay.TryConvertWorldToMinimap(LevelManager.Instance.MainCharacter.transform.position, sceneID, out vector);
            return minimapDisplay.transform.localToWorldMatrix.MultiplyPoint(vector);
        }

        // 注意：这个计算在优化后可以避免频繁调用
        public static Quaternion GetChracterRotation(Vector3 to)
        {
            float currentMapZRotation = Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
            return Quaternion.Euler(0f, 0f, -currentMapZRotation);
        }

        // 注意：在优化后的系统中，这个方法应该减少调用频率
        public static Quaternion GetChracterRotation(CharacterMainControl? character)
        {
            if (character == null)
            {
                return Quaternion.Euler(0, 0, 0);
            }
            Vector3 to = GetCharacterForward(character);
            return GetChracterRotation(to);
        }

        public static Quaternion GetChracterRotation()
        {
            return GetChracterRotation(CharacterMainControl.Main);
        }

        // 注意：这个计算也可以缓存结果
        public static Quaternion GetPlayerMinimapRotationInverse(Vector3 to)
        {
            float currentMapZRotation = Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
            return Quaternion.Euler(0f, 0f, currentMapZRotation);
        }

        public static Quaternion GetPlayerMinimapRotationInverse()
        {
            Vector3 to = GetCharacterForward();
            return GetPlayerMinimapRotationInverse(to);
        }
    }
}