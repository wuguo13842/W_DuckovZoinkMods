using System;
using Duckov.MiniMaps;
using MiniMap.Poi;
using UnityEngine;

namespace MiniMap.Utils
{
    /// <summary>
    /// 角色死亡事件处理器
    /// 监听 Health.OnDead 事件，在角色死亡时清理对应的POI
    /// 替代原有的每帧死亡检查，提升性能
    /// </summary>
	[Log(LogOutput.Output, LogOutput.Output, LogLevel.Debug, LogLevel.Debug)]
    public static class DeathEventHandler
    {
        private static bool _initialized = false;

        /// <summary>
        /// 初始化死亡事件处理器
        /// 订阅 Health.OnDead 事件
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                // 订阅全局死亡事件
                Health.OnDead += OnCharacterDied;
                _initialized = true;
                Log.Info("死亡事件处理器已初始化");
            }
            catch (Exception e)
            {
                Log.Error($"死亡事件处理器初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清理死亡事件处理器
        /// 取消订阅 Health.OnDead 事件
        /// </summary>
        public static void Cleanup()
        {
            if (!_initialized) return;
            
            try
            {
                // 取消订阅全局死亡事件
                Health.OnDead -= OnCharacterDied;
                _initialized = false;
                Log.Info("死亡事件处理器已清理");
            }
            catch (Exception e)
            {
                Log.Error($"死亡事件处理器清理失败: {e.Message}");
            }
        }

        /// <summary>
        /// 角色死亡事件处理
        /// 当任何角色死亡时触发，清理对应的POI
        /// </summary>
        /// <param name="health">死亡的Health组件</param>
        /// <param name="damageInfo">伤害信息（此处未使用）</param>
        private static void OnCharacterDied(Health health, DamageInfo damageInfo)
        {
            try
            {
                // 空值检查
                if (health == null)
                {
                    Log.Warning("死亡事件: Health为空");
                    return;
                }

                // 获取死亡的角色
                CharacterMainControl? character = health.TryGetCharacter();
                if (character == null)
                {
                    Log.Warning($"死亡事件: 无法获取角色，Health={health.GetInstanceID()}");
                    return;
                }

                // 跳过玩家角色（玩家死亡时不清理POI）
                if (character.IsMainCharacter)
                {
                    Log.Info("玩家死亡，保留POI");
                    return;
                }

                // 清理位置图标POI
                CharacterPointOfInterest? poi = character.GetComponent<CharacterPointOfInterest>();
                if (poi != null && poi.gameObject != null)
                {
                    // 销毁POI游戏对象（会自动触发OnDestroy和PointsOfInterests.Unregister）
                    GameObject.Destroy(poi.gameObject);
                    Log.Info($"已销毁位置图标POI: {character.name}");
                }

                // 清理方向箭头POI
                DirectionPointOfInterest? dirPoi = character.GetComponent<DirectionPointOfInterest>();
                if (dirPoi != null && dirPoi.gameObject != null)
                {
                    // 销毁方向箭头游戏对象
                    GameObject.Destroy(dirPoi.gameObject);
                    Log.Info($"已销毁方向箭头POI: {character.name}");
                }

                // 如果POI组件不存在，记录警告（可能是角色生成时未创建POI）
                if (poi == null && dirPoi == null)
                {
                    Log.Warning($"角色 {character.name} 死亡，但未找到POI组件");
                }
            }
            catch (Exception e)
            {
                Log.Error($"处理死亡事件时出错: {e.Message}");
            }
        }
    }
}