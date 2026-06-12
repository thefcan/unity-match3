using System;

namespace Match3.Core
{
    /// <summary>
    /// An (x, y) cell coordinate on the board. X grows rightwards, Y grows UPWARDS
    /// (y = 0 is the bottom row), which keeps gravity math intuitive: falling means Y decreases.
    ///
    /// C# note for Java readers: a <c>readonly struct</c> is an immutable VALUE type.
    /// Unlike a Java object it is copied on assignment and never null, and it lives on
    /// the stack / inline in arrays — no heap allocation, no GC pressure. Ideal for
    /// small coordinate-like types that get created thousands of times per frame.
    /// </summary>
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        // C# auto-properties replace Java's private field + getter boilerplate.
        public int X { get; }
        public int Y { get; }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>True when the other cell is exactly one step away orthogonally (no diagonals).</summary>
        public bool IsAdjacentTo(GridPosition other) =>
            Math.Abs(X - other.X) + Math.Abs(Y - other.Y) == 1;

        public GridPosition Offset(int dx, int dy) => new GridPosition(X + dx, Y + dy);

        // Implementing IEquatable<T> avoids boxing in HashSet/Dictionary lookups
        // (the struct equivalent of overriding equals/hashCode in Java).
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Y})";
    }
}
