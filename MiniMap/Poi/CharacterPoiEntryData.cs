using LeTai.TrueShadow;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace MiniMap.Poi
{
    public struct CharacterPoiEntryData
    {
        public Transform? indicatorContainer;
        public Transform? iconContainer;
        public Image? icon;
        public Transform? direction;
        public Image? arrow;
        public TrueShadow? shadow;
        public TextMeshProUGUI? displayName;
        public ProceduralImage? areaDisplay;
        public Image? areaFill;
    }
}
