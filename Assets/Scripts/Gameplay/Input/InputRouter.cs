using BattleRunner.Core.Gestures;
using BattleRunner.Data.Channels;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Vector2 = UnityEngine.Vector2;

namespace BattleRunner.Gameplay.Input
{
    /// <summary>
    /// The only class in the project that polls input devices (doc 02). Touch samples
    /// are density-normalized to centimeters and fed to the pure GestureClassifier;
    /// resulting intents go out through event channels — gameplay never reads touches.
    /// In the editor / on desktop: mouse drag or arrow keys / WASD steer lanes,
    /// up-arrow or W casts, down-arrow or S shields.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        private GestureClassifier _classifier;
        private FloatEventChannel _laneTarget;
        private VoidEventChannel _flickUp;
        private VoidEventChannel _flickDown;

        private float _dotsPerCm;
        private bool _touchWasDown;
        private bool _mouseWasDown;

        private const float KeyboardSteerSpeed = 1.4f; // screen-widths per second
        private float _keyboardLaneX = 0.5f;

        public void Initialize(GestureSettings settings,
            FloatEventChannel laneTarget, VoidEventChannel flickUp, VoidEventChannel flickDown)
        {
            _classifier = new GestureClassifier(settings);
            _laneTarget = laneTarget;
            _flickUp = flickUp;
            _flickDown = flickDown;

            float dpi = Screen.dpi;
            if (dpi <= 1f) dpi = 160f; // editor/desktop fallback
            _dotsPerCm = dpi / 2.54f;

            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (_classifier == null) return;

            bool handledTouch = PollTouch();
            if (!handledTouch) PollMouse();
            PollKeyboardShortcuts();
        }

        private bool PollTouch()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                if (_touchWasDown)
                {
                    // Touch list emptied without a formal end sample — treat as release at last point.
                    _touchWasDown = false;
                }
                return false;
            }

            Touch touch = touches[0];
            TouchSample sample = MakeSample(touch.screenPosition);

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchWasDown = true;
                    Emit(_classifier.OnTouchDown(sample));
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    if (!_touchWasDown)
                    {
                        _touchWasDown = true;
                        Emit(_classifier.OnTouchDown(sample));
                    }
                    else
                    {
                        Emit(_classifier.OnTouchMove(sample));
                    }
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _touchWasDown = false;
                    Emit(_classifier.OnTouchUp(sample));
                    break;
            }
            return true;
        }

        private void PollMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            TouchSample sample = MakeSample(position);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _mouseWasDown = true;
                Emit(_classifier.OnTouchDown(sample));
            }
            else if (mouse.leftButton.wasReleasedThisFrame && _mouseWasDown)
            {
                _mouseWasDown = false;
                Emit(_classifier.OnTouchUp(sample));
            }
            else if (_mouseWasDown && mouse.leftButton.isPressed)
            {
                Emit(_classifier.OnTouchMove(sample));
            }
        }

        /// <summary>
        /// Editor/desktop convenience so the game is playable without a touchscreen.
        /// Arrow keys and WASD both work: left/right steer smoothly, up casts, down shields.
        /// </summary>
        private void PollKeyboardShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                _flickUp?.Raise();
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                _flickDown?.Raise();

            float axis = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis += 1f;
            if (axis == 0f) return;

            _keyboardLaneX = Mathf.Clamp01(_keyboardLaneX + axis * KeyboardSteerSpeed * Time.deltaTime);
            _laneTarget?.Raise(_keyboardLaneX);
        }

        private TouchSample MakeSample(Vector2 screenPosition)
        {
            var cm = new System.Numerics.Vector2(
                screenPosition.x / _dotsPerCm,
                screenPosition.y / _dotsPerCm);
            float normalizedX = Mathf.Clamp01(screenPosition.x / Mathf.Max(1f, Screen.width));
            return new TouchSample(cm, normalizedX, Time.unscaledTime);
        }

        private void Emit(GestureEvent gestureEvent)
        {
            switch (gestureEvent.Type)
            {
                case GestureEventType.LaneTarget:
                    _laneTarget?.Raise(gestureEvent.LaneNormalizedX);
                    break;
                case GestureEventType.FlickUp:
                    _flickUp?.Raise();
                    break;
                case GestureEventType.FlickDown:
                    _flickDown?.Raise();
                    break;
            }
        }
    }
}
