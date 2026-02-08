using MiniMap.Utils;
using UnityEngine;
using Duckov; 

namespace MiniMap.Poi
{
    public class DirectionPointOfInterest : CharacterPoiBase
    {
        private float rotationEulerAngle;
        private float baseEulerAngle;

        public float RotationEulerAngle { get => rotationEulerAngle % 360; set => rotationEulerAngle = value % 360; }
        public float BaseEulerAngle { get => baseEulerAngle % 360; set => baseEulerAngle = value % 360; }
        public float RealEulerAngle => (baseEulerAngle + rotationEulerAngle) % 360;
        public override string DisplayName => string.Empty;
        public override bool IsArea => false;
        public override float AreaRadius => 0;
        public override Color Color => Color.white;

        protected override void Update()
        {
            // 移除死亡检查，只保留方向更新逻辑
            if (Character == null)
            {
                return;
            }
            base.Update();
            RotationEulerAngle = MiniMapCommon.GetChracterRotation(Character);
        }
    }
}