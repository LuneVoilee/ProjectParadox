#region

using System;
using System.Collections;
using Core.CoreInput;
using Tool;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

#endregion

namespace Core
{
    public class InputManager : PersistentSingletonMono<InputManager>,
        InputActions.IPlayerActions
    {
        private InputActions m_InputActions;

        public Vector2 MoveInput { get; private set; }
        public float ScrollInput { get; private set; }

        public float RotateInput { get; private set; }

        public Vector2 MousePosition { get; private set; }

        public Action OnClickAction;
        public Action OnRightClickAction;
        public Action OnESCClickAction;
        private bool m_GameplayClickThisFrame;
        private bool m_GameplayClickConsumed;
        private int m_GameplayClickFrame = -1;
        private Vector2 m_GameplayClickScreenPosition;

        private bool m_GameplayRightClickThisFrame;
        private bool m_GameplayRightClickConsumed;
        private int m_GameplayRightClickFrame = -1;
        private Vector2 m_GameplayRightClickScreenPosition;

        protected override void Awake()
        {
            base.Awake();
            m_InputActions = new InputActions();
            m_InputActions.Player.AddCallbacks(this);
        }

        private void OnEnable()
        {
            m_InputActions?.Enable();
        }

        private void OnDisable()
        {
            m_InputActions?.Disable();
        }

        private void LateUpdate()
        {
            if (m_GameplayClickThisFrame && Time.frameCount > m_GameplayClickFrame)
            {
                m_GameplayClickThisFrame = false;
                m_GameplayClickConsumed = false;
            }

            if (m_GameplayRightClickThisFrame && Time.frameCount > m_GameplayRightClickFrame)
            {
                m_GameplayRightClickThisFrame = false;
                m_GameplayRightClickConsumed = false;
            }
        }

        protected override void OnDestroy()
        {
            m_InputActions?.Dispose();
            base.OnDestroy();
        }

        public void DisableInput()
        {
            m_InputActions.Disable();
        }

        public void EnableInput()
        {
            m_InputActions.Enable();
        }

        public bool IsMouseLeftClick => Mouse.current.leftButton.wasReleasedThisFrame;

        public bool HasGameplayClickThisFrame => m_GameplayClickThisFrame && !m_GameplayClickConsumed;

        public bool TryConsumeGameplayClick(out Vector2 screenPosition)
        {
            screenPosition = m_GameplayClickScreenPosition;
            if (!HasGameplayClickThisFrame)
            {
                return false;
            }

            m_GameplayClickConsumed = true;
            return true;
        }

        public bool HasGameplayRightClickThisFrame => m_GameplayRightClickThisFrame && !m_GameplayRightClickConsumed;

        public bool TryConsumeGameplayRightClick(out Vector2 screenPosition)
        {
            screenPosition = m_GameplayRightClickScreenPosition;
            if (!HasGameplayRightClickThisFrame)
            {
                return false;
            }

            m_GameplayRightClickConsumed = true;
            return true;
        }

        #region Player Actions

        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnClick(InputAction.CallbackContext context)
        {
            if (!context.canceled)
            {
                return;
            }

            StartCoroutine(OnClickDelay());
        }

        private IEnumerator OnClickDelay()
        {
            yield return null;

            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                yield break;
            }

            RegisterGameplayClick(MousePosition);
            OnClickAction?.Invoke();
        }

        private void RegisterGameplayClick(Vector2 screenPosition)
        {
            // Gameplay 点击由 InputManager 统一过滤 UI 和输入细节，Capability 只消费这一层语义。
            m_GameplayClickThisFrame = true;
            m_GameplayClickConsumed = false;
            m_GameplayClickFrame = Time.frameCount;
            m_GameplayClickScreenPosition = screenPosition;
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (!context.canceled)
            {
                return;
            }

            RegisterGameplayRightClick(MousePosition);
            OnRightClickAction?.Invoke();
        }

        private void RegisterGameplayRightClick(Vector2 screenPosition)
        {
            m_GameplayRightClickThisFrame = true;
            m_GameplayRightClickConsumed = false;
            m_GameplayRightClickFrame = Time.frameCount;
            m_GameplayRightClickScreenPosition = screenPosition;
        }


        public void OnPoint(InputAction.CallbackContext context)
        {
            MousePosition = context.ReadValue<Vector2>();
        }

        public void OnScroll(InputAction.CallbackContext context)
        {
            ScrollInput = context.ReadValue<Vector2>().y;
            //Debug.Log("Input"+ScrollInput);
        }

        public void OnRotate(InputAction.CallbackContext context)
        {
            RotateInput = context.ReadValue<float>();
        }

        public void OnESC(InputAction.CallbackContext context)
        {
            OnESCClickAction?.Invoke();
        }

        #endregion
    }
}
