using System;

namespace BattleRunner.Core.Stats
{
    public enum ModifierKind
    {
        Flat = 0,
        Percent = 1
    }

    [Serializable]
    public struct StatModifier
    {
        public string StatId;
        public ModifierKind Kind;
        public float Value;

        public StatModifier(string statId, ModifierKind kind, float value)
        {
            StatId = statId;
            Kind = kind;
            Value = value;
        }

        public override string ToString() =>
            Kind == ModifierKind.Flat ? $"{StatId} +{Value}" : $"{StatId} +{Value:P0}";
    }
}
