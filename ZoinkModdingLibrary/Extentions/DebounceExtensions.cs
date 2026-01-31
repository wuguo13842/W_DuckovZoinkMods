using System;
using System.Collections;
using UnityEngine;

namespace ZoinkModdingLibrary.Extentions
{
    public static class DebounceExtensions
    {
        private static float s_lastTaskTime;
        private static Coroutine? s_activeRefreshCoroutine;
        private static readonly float s_debounceDelay = 1f;

        /// <summary>
        /// 扩展方法：执行任务并防抖刷新
        /// </summary>
        public static void ExecuteWithDebounce(this MonoBehaviour mb, Action task, Action? onRefresh = null)
        {
            // 执行任务
            task?.Invoke();

            // 重置计时器
            s_lastTaskTime = Time.time;

            // 管理刷新协程
            ScheduleRefreshCheck(mb, onRefresh);
        }

        private static void ScheduleRefreshCheck(MonoBehaviour mb, Action? onRefresh)
        {
            // 如果已有协程在运行，先停止它
            if (s_activeRefreshCoroutine != null)
            {
                mb.StopCoroutine(s_activeRefreshCoroutine);
                s_activeRefreshCoroutine = null;
            }

            // 启动新的单次检查协程
            s_activeRefreshCoroutine = mb.StartCoroutine(SingleRefreshCheck(onRefresh));
        }

        /// <summary>
        /// 单次检查协程
        /// </summary>
        private static IEnumerator SingleRefreshCheck(Action? onRefresh)
        {
            // 等待防抖时间
            yield return new WaitForSeconds(s_debounceDelay);

            // 检查是否超时
            if (Time.time - s_lastTaskTime >= s_debounceDelay)
            {
                // 超时了，执行刷新
                onRefresh?.Invoke();
            }
            // 如果没超时，什么也不做（让这次检查自然结束）
            // 下次任务执行时会重新启动新的检查
            s_activeRefreshCoroutine = null; // 清理引用
        }
    }
}
