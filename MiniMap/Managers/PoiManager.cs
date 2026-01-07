using Duckov.MiniMaps;
using Duckov.Scenes;
using MiniMap.Poi;
using MiniMap.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

namespace MiniMap.Managers
{
    public static class PoiManager
    {
        public static void OnLenvelIntialized()
        {
            PoiShows poiShows = new PoiShows()
            {
                ShowOnlyActivated = ModSettingManager.GetValue("showOnlyActivated", false),
                ShowPetPoi = ModSettingManager.GetValue("showPetPoi", true),
                ShowInMap = ModSettingManager.GetValue("showPoiInMap", true),
                ShowInMiniMap = ModSettingManager.GetValue("showPoiInMiniMap", true),
            };
            PoiCommon.CreatePoiIfNeeded(LevelManager.Instance?.MainCharacter, out _, out DirectionPointOfInterest? mainDirectionPoi, poiShows);
            PoiCommon.CreatePoiIfNeeded(LevelManager.Instance?.PetCharacter, out CharacterPointOfInterest? petPoi, out DirectionPointOfInterest? petDirectionPoi, poiShows);
        }

        public static void SetPoiShow(CharacterPointOfInterestBase poi)
        {
            PoiShows poiShows = new PoiShows()
            {
                ShowOnlyActivated = ModSettingManager.GetValue("showOnlyActivated", false),
                ShowPetPoi = ModSettingManager.GetValue("showPetPoi", true),
                ShowInMap = ModSettingManager.GetValue("showPoiInMap", true),
                ShowInMiniMap = ModSettingManager.GetValue("showPoiInMiniMap", true),
            };
            poi.SetShows(poiShows);
        }
    }
}
