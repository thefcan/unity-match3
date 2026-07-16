using System;

namespace Match3.Core
{
    /// <summary>
    /// What a moves-limited level asks of the player. Enum + data rather than a class
    /// hierarchy: it serializes cleanly into ScriptableObjects, and future goal types
    /// (clear all jelly, bring down ingredients) are an enum member plus a matching
    /// data list on <see cref="CascadeStep"/> — the recording pattern already fits.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>Reach a score of <see cref="Objective.TargetAmount"/>.</summary>
        Score,
        /// <summary>Clear <see cref="Objective.TargetAmount"/> tiles of colour <see cref="Objective.ColorIndex"/>.</summary>
        CollectColor,
        /// <summary>Remove <see cref="Objective.TargetAmount"/> jelly layers (matches on jelly cells).</summary>
        ClearJelly,
    }

    /// <summary>One goal of a level: the type, the colour it applies to (if any), and how much.</summary>
    public readonly struct Objective
    {
        public ObjectiveType Type { get; }
        public int ColorIndex { get; }
        public int TargetAmount { get; }

        public Objective(ObjectiveType type, int colorIndex, int targetAmount)
        {
            if (targetAmount < 1)
                throw new ArgumentOutOfRangeException(nameof(targetAmount), "An objective needs a positive target.");

            Type = type;
            ColorIndex = colorIndex;
            TargetAmount = targetAmount;
        }

        public override string ToString() =>
            Type == ObjectiveType.Score ? $"Score {TargetAmount}" : $"Collect {TargetAmount} of colour {ColorIndex}";
    }
}
