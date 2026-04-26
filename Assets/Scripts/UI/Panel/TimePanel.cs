#region

using System;
using Common.Contracts;
using Common.Event;
using Core.Reactive;
using DG.Tweening;
using TMPro;
using UnityEngine;

#endregion

namespace UI.Panel
{
    public class UITimeData : BasePanelData
    {
        public ReactiveValue<TimeType> TimeSpeed = new();
        public ReactiveValue<DateTime> CurrentDate = new();
        public Func<DateTime?> GetCurrentTime;
    }

    public partial class TimePanel : BasePanel<UITimeData>
    {
        private const float RollDuration = 0.22f;
        private const float RollDistance = 18f;

        private bool m_HasDisplayTime;
        private DateTime m_LastDisplayDate;
        private TimeTextRoller m_YearRoller;
        private TimeTextRoller m_MonthRoller;
        private TimeTextRoller m_DayRoller;
        private TimeTextRoller m_HourRoller;
        private TimeTextRoller m_FallbackRoller;

        protected override void OnBind()
        {
            CacheTimeTextRollers();
            Data.CurrentDate.Bind(WhenDateChanged);

            m_SpeedPauseButton.onClick.AddListener(ModifySpeedPause);
            m_Speed1Button.onClick.AddListener(ModifySpeed1);
            m_Speed2Button.onClick.AddListener(ModifySpeed2);
            m_Speed3Button.onClick.AddListener(ModifySpeed3);
            m_Speed4Button.onClick.AddListener(ModifySpeed4);
            m_Speed5Button.onClick.AddListener(ModifySpeed5);

            Data.TimeSpeed.Bind(WhenSpeedChanged);
        }

        protected override void OnUnbind()
        {
            KillTimeTextRollers();
            Data.CurrentDate.Unbind(WhenDateChanged);

            m_SpeedPauseButton.onClick.RemoveListener(ModifySpeedPause);
            m_Speed1Button.onClick.RemoveListener(ModifySpeed1);
            m_Speed2Button.onClick.RemoveListener(ModifySpeed2);
            m_Speed3Button.onClick.RemoveListener(ModifySpeed3);
            m_Speed4Button.onClick.RemoveListener(ModifySpeed4);
            m_Speed5Button.onClick.RemoveListener(ModifySpeed5);

            Data.TimeSpeed.Unbind(WhenSpeedChanged);
        }

        private void Update()
        {
            if (Data?.GetCurrentTime == null || !IsShowing)
            {
                return;
            }

            var currentTime = Data.GetCurrentTime();
            if (!currentTime.HasValue)
            {
                return;
            }

            ApplyDisplayTime(currentTime.Value, true);
        }

        private void WhenSpeedChanged(TimeType _, TimeType newSpeed)
        {
            EventBus.UI_OnSpeedChange?.Invoke(newSpeed);
        }

        private void WhenDateChanged(DateTime _, DateTime newDate)
        {
            ApplyDisplayTime(newDate, true);
        }

        private void ApplyDisplayTime(DateTime newDate, bool animate)
        {
            if (!ShouldUpdateDisplay(newDate))
            {
                return;
            }

            var shouldAnimate = animate && m_HasDisplayTime;
            var oldDate = m_LastDisplayDate;

            m_LastDisplayDate = newDate;
            m_HasDisplayTime = true;

            if (HasSplitTimeText())
            {
                ApplySplitDisplayTime(oldDate, newDate, shouldAnimate);
                return;
            }

            ApplyFallbackDisplayTime(newDate, shouldAnimate);
        }

        private bool ShouldUpdateDisplay(DateTime newDate)
        {
            if (!m_HasDisplayTime)
            {
                return true;
            }

            return newDate.Year != m_LastDisplayDate.Year
                   || newDate.Month != m_LastDisplayDate.Month
                   || newDate.Day != m_LastDisplayDate.Day
                   || newDate.Hour != m_LastDisplayDate.Hour;
        }

        private static string FormatDisplayTime(DateTime date)
        {
            return $"{date.Year}年{date.Month}月{date.Day}日 {date.Hour} : 00";
        }

        private void CacheTimeTextRollers()
        {
            m_YearRoller = new TimeTextRoller(m_YearText, RollDistance, RollDuration);
            m_MonthRoller = new TimeTextRoller(m_MonthText, RollDistance, RollDuration);
            m_DayRoller = new TimeTextRoller(m_DayText, RollDistance, RollDuration);
            m_HourRoller = new TimeTextRoller(m_HourText, RollDistance, RollDuration);
        }

        private TextMeshProUGUI GetTimeText(string key)
        {
            return GetComponentRef<TextMeshProUGUI>(key);
        }

        private void ApplySplitDisplayTime(DateTime oldDate, DateTime newDate, bool animate)
        {
            m_YearRoller.SetText(newDate.ToString("yyyy"),
                animate && oldDate.Year != newDate.Year);
            m_MonthRoller.SetText(newDate.ToString("MM"),
                animate && oldDate.Month != newDate.Month);
            m_DayRoller.SetText(newDate.ToString("dd"), animate && oldDate.Day != newDate.Day);
            m_HourRoller.SetText(newDate.ToString("HH"),
                animate && oldDate.Hour != newDate.Hour);
        }

        private void ApplyFallbackDisplayTime(DateTime newDate, bool animate)
        {
            m_FallbackRoller.SetText(FormatDisplayTime(newDate), animate);
        }

        private bool HasSplitTimeText()
        {
            return m_YearRoller.IsValid
                   && m_MonthRoller.IsValid
                   && m_DayRoller.IsValid
                   && m_HourRoller.IsValid;
        }

        private void KillTimeTextRollers()
        {
            m_YearRoller?.Kill();
            m_MonthRoller?.Kill();
            m_DayRoller?.Kill();
            m_HourRoller?.Kill();
            m_FallbackRoller?.Kill();
        }

        private class TimeTextRoller
        {
            private readonly TextMeshProUGUI m_Text;
            private readonly float m_RollDistance;
            private readonly float m_RollDuration;
            private readonly Vector2 m_BasePosition;
            private readonly Color m_BaseColor;

            public bool IsValid => m_Text != null;

            public TimeTextRoller(TextMeshProUGUI text, float rollDistance, float rollDuration)
            {
                m_Text = text;
                m_RollDistance = rollDistance;
                m_RollDuration = rollDuration;
                m_BasePosition = text != null ? text.rectTransform.anchoredPosition : default;
                m_BaseColor = text != null ? text.color : default;
            }

            public void SetText(string text, bool animate)
            {
                if (m_Text == null)
                {
                    return;
                }

                DOTween.Kill(m_Text);

                if (!animate)
                {
                    ResetText(text);
                    return;
                }

                var rectTransform = m_Text.rectTransform;
                var hiddenAbove = m_BasePosition + Vector2.up * m_RollDistance;
                var hiddenBelow = m_BasePosition + Vector2.down * m_RollDistance;
                var transparent = m_BaseColor;
                transparent.a = 0f;

                DOTween.Sequence()
                    .Append(DOTween.To(() => rectTransform.anchoredPosition,
                        value => rectTransform.anchoredPosition = value, hiddenAbove,
                        m_RollDuration * 0.5f).SetEase(Ease.InCubic))
                    .Join(DOTween.To(() => m_Text.color, value => m_Text.color = value, transparent,
                        m_RollDuration * 0.5f).SetEase(Ease.InCubic))
                    .AppendCallback(() =>
                    {
                        m_Text.text = text;
                        rectTransform.anchoredPosition = hiddenBelow;
                    })
                    .Append(DOTween.To(() => rectTransform.anchoredPosition,
                        value => rectTransform.anchoredPosition = value, m_BasePosition,
                        m_RollDuration * 0.5f).SetEase(Ease.OutCubic))
                    .Join(DOTween.To(() => m_Text.color, value => m_Text.color = value, m_BaseColor,
                        m_RollDuration * 0.5f).SetEase(Ease.OutCubic))
                    .SetTarget(m_Text);
            }

            public void Kill()
            {
                if (m_Text == null)
                {
                    return;
                }

                DOTween.Kill(m_Text);
                m_Text.rectTransform.anchoredPosition = m_BasePosition;
                m_Text.color = m_BaseColor;
            }

            private void ResetText(string text)
            {
                m_Text.rectTransform.anchoredPosition = m_BasePosition;
                m_Text.color = m_BaseColor;
                m_Text.text = text;
            }
        }

        #region SpeedButton

        private void ModifySpeedPause()
        {
            if (Data.TimeSpeed.Value == TimeType.Pause)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Pause;
        }

        private void ModifySpeed1()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed1)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed1;
        }

        private void ModifySpeed2()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed2)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed2;
        }

        private void ModifySpeed3()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed3)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed3;
        }

        private void ModifySpeed4()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed4)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed4;
        }

        private void ModifySpeed5()
        {
            if (Data.TimeSpeed.Value == TimeType.Speed5)
            {
                return;
            }

            Data.TimeSpeed.Value = TimeType.Speed5;
        }

        #endregion
    }
}