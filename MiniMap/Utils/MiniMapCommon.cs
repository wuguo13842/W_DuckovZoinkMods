using Duckov.MiniMaps;
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

        private static Vector3 GetCharacterForward(CharacterMainControl? character)
        {
            if (character == null)
            {
                return Vector3.zero;
            }
            string facingBase = ModSettingManager.GetActualDropdownValue("facingBase", false);
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

        public static Quaternion GetChracterRotation(Vector3 to)
        {
            float currentMapZRotation = Vector3.SignedAngle(Vector3.forward, to, Vector3.up);
            return Quaternion.Euler(0f, 0f, -currentMapZRotation);
        }

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