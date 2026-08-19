using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Lightweight perf overlay (doc 04): fps, frame time, visible units, force.
    /// Toggle with a 3-finger tap on device or F1 in the editor. IMGUI keeps it
    /// dependency-free; it only runs while visible.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private CrowdController _crowd;
        private bool _visible;
        private float _smoothedDt = 1f / 60f;
        private GUIStyle _style;

        public void Initialize(CrowdController crowd) => _crowd = crowd;

        private void Update()
        {
            _smoothedDt = Mathf.Lerp(_smoothedDt, Time.unscaledDeltaTime, 0.06f);

            if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true)
                _visible = !_visible;
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (touchscreen != null)
            {
                int active = 0;
                foreach (var touch in touchscreen.touches)
                    if (touch.press.isPressed) active++;
                if (active >= 3 && Time.unscaledTime - _lastToggle > 1f)
                {
                    _visible = !_visible;
                    _lastToggle = Time.unscaledTime;
                }
            }
        }

        private float _lastToggle;

        private void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 28, normal = { textColor = Color.yellow } };
            float fps = 1f / Mathf.Max(1e-5f, _smoothedDt);
            string text = $"{fps:0} fps  ({_smoothedDt * 1000f:0.0} ms)\n" +
                          $"units {_crowd?.VisibleUnits ?? 0}  force {_crowd?.ForceCount ?? 0:N0}\n" +
                          $"mem {SystemInfo.systemMemorySize} MB device";
            GUI.Label(new Rect(20, 60, 600, 200), text, _style);
        }
    }
}
