#region

using DG.Tweening;
using UnityEngine;

#endregion

namespace UI.Panel
{
    public partial class SelectionIndicatorPanel : BasePanel
    {
        [Header("Animation Settings")] [Tooltip("角标向外扩张的距离（基于Canvas单位）")]
        public float expandOffset = 20f;

        [Tooltip("单次收缩/扩张的时长")] public float duration = 0.5f;
        public Ease easeType = Ease.OutQuad;

        // 用于缓存紧贴边界的原始归零位置
        private readonly Vector2[] targetPositions = new Vector2[4];

        protected override void Awake()
        {
            base.Awake();

            // 记录在Prefab中设置好的紧贴边界的位置作为目标位置
            targetPositions[0] = m_TopLeft.anchoredPosition;
            targetPositions[1] = m_TopRight.anchoredPosition;
            targetPositions[2] = m_BottomLeft.anchoredPosition;
            targetPositions[3] = m_BottomRight.anchoredPosition;
        }

        private void OnEnable()
        {
            PlayClampAnimation();
        }

        private void OnDisable()
        {
            // 严谨工程原则：组件禁用时必须清理 Tween，防止内存泄漏或空引用异常
            KillAllTweens();
            ResetPositions();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            KillAllTweens();
        }

        public void PlayClampAnimation()
        {
            // 确保没有残留动画
            KillAllTweens();

            // 1. 将四个角标瞬间推到“较大的包围距”外围
            m_TopLeft.anchoredPosition =
                targetPositions[0] + new Vector2(-expandOffset, expandOffset);
            m_TopRight.anchoredPosition =
                targetPositions[1] + new Vector2(expandOffset, expandOffset);
            m_BottomLeft.anchoredPosition =
                targetPositions[2] + new Vector2(-expandOffset, -expandOffset);
            m_BottomRight.anchoredPosition =
                targetPositions[3] + new Vector2(expandOffset, -expandOffset);

            // 2. 向内收缩到目标实际边界，并开启往复循环 (Yoyo)
            TweenAnchorPosition(m_TopLeft, targetPositions[0]).SetLoops(-1, LoopType.Yoyo)
                .SetEase(easeType);
            TweenAnchorPosition(m_TopRight, targetPositions[1]).SetLoops(-1, LoopType.Yoyo)
                .SetEase(easeType);
            TweenAnchorPosition(m_BottomLeft, targetPositions[2]).SetLoops(-1, LoopType.Yoyo)
                .SetEase(easeType);
            TweenAnchorPosition(m_BottomRight, targetPositions[3]).SetLoops(-1, LoopType.Yoyo)
                .SetEase(easeType);
        }

        private Tween TweenAnchorPosition(RectTransform target, Vector2 endValue)
        {
            return DOTween.To(() => target.anchoredPosition,
                    value => target.anchoredPosition = value, endValue, duration)
                .SetTarget(target);
        }

        private void KillAllTweens()
        {
            // 使用 target 精确清理，避免对象禁用/销毁后 tween 继续写位置。
            DOTween.Kill(m_TopLeft);
            DOTween.Kill(m_TopRight);
            DOTween.Kill(m_BottomLeft);
            DOTween.Kill(m_BottomRight);
        }

        private void ResetPositions()
        {
            // 恢复初始状态，以便下一次复用（例如放回对象池）
            m_TopLeft.anchoredPosition = targetPositions[0];
            m_TopRight.anchoredPosition = targetPositions[1];
            m_BottomLeft.anchoredPosition = targetPositions[2];
            m_BottomRight.anchoredPosition = targetPositions[3];
        }
    }
}