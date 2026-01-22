﻿using MiniMap.Utils;
using UnityEngine;

namespace MiniMap.Poi
{
    public class DirectionPointOfInterest : CharacterPointOfInterestBase
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
            base.Update();
            
            // 直接从Character获取旋转信息，避免复杂的计算
            if (Character == null) return;
            
            bool isMain = Character.IsMainCharacter;
            if (isMain)
            {
                // 对于玩家，直接使用当前旋转，避免额外的计算
                RotationEulerAngle = Character.modelRoot.rotation.eulerAngles.z;
            }
            else
            {
                // 对于其他角色，使用movementControl的目标方向
                // 注意：这里仍然需要计算，但可以通过缓存减少计算频率
                Vector3 aimDirection = Character.movementControl.targetAimDirection;
                if (aimDirection != Vector3.zero)
                {
                    // 使用更简单的计算
                    RotationEulerAngle = Quaternion.LookRotation(aimDirection).eulerAngles.z;
                }
            }
        }
    }
}