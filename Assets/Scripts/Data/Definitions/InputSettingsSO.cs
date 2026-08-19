using BattleRunner.Core.Gestures;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Input Settings", fileName = "InputSettings")]
    public sealed class InputSettingsSO : ScriptableObject
    {
        public GestureSettings Gestures = GestureSettings.Default;
    }
}
