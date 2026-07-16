using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// Accumulates level-goal progress from the cascade recording: feed it every
    /// <see cref="CascadeStep"/> the resolver produced and ask <see cref="AllComplete"/>.
    /// Pure C# — the win condition of moves mode is unit-testable without Unity.
    /// Colour counts come from <see cref="CascadeStep.Cleared"/> (tiles carry their
    /// ColorIndex); score comes from <see cref="CascadeStep.Points"/>.
    /// </summary>
    public sealed class ObjectiveTracker
    {
        private readonly Objective[] _objectives;
        private readonly int[] _progress;

        /// <summary>Points accumulated across every consumed step.</summary>
        public int Score { get; private set; }

        public ObjectiveTracker(IEnumerable<Objective> objectives)
        {
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));

            _objectives = objectives.ToArray();
            if (_objectives.Length == 0)
                throw new ArgumentException("A level needs at least one objective.", nameof(objectives));

            _progress = new int[_objectives.Length];
        }

        public int Count => _objectives.Length;

        public Objective At(int index) => _objectives[index];

        /// <summary>Progress towards an objective, clamped to its target (for display).</summary>
        public int Progress(int index) => Math.Min(_progress[index], _objectives[index].TargetAmount);

        public bool IsComplete(int index) => _progress[index] >= _objectives[index].TargetAmount;

        public bool AllComplete
        {
            get
            {
                for (int i = 0; i < _objectives.Length; i++)
                    if (!IsComplete(i))
                        return false;
                return true;
            }
        }

        /// <summary>Folds one cascade wave into every objective's progress.</summary>
        public void Consume(CascadeStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));

            Score += step.Points;

            foreach (ClearedTile cleared in step.Cleared)
            {
                for (int i = 0; i < _objectives.Length; i++)
                {
                    if (_objectives[i].Type == ObjectiveType.CollectColor &&
                        cleared.Tile.ColorIndex == _objectives[i].ColorIndex)
                        _progress[i]++;
                }
            }

            for (int i = 0; i < _objectives.Length; i++)
            {
                if (_objectives[i].Type == ObjectiveType.Score)
                    _progress[i] = Score;
                else if (_objectives[i].Type == ObjectiveType.ClearJelly)
                    _progress[i] += step.JellyHits.Count;
            }
        }
    }
}
