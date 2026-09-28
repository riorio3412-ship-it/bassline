using System;
using System.Text.RegularExpressions;

namespace BASSLINE.Core
{
    public readonly struct StableId : IEquatable<StableId>, IComparable<StableId>
    {
        public string Value { get; }
        public StableId(string value)
        {
            if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, @"^[A-Za-z][A-Za-z0-9_\-]*$"))
                throw new ArgumentException("Invalid stable ID", nameof(value));
            Value = value;
        }
        public bool Equals(StableId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is StableId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(StableId other) => StringComparer.Ordinal.Compare(Value, other.Value);
        public override string ToString() => Value ?? throw new InvalidOperationException("Uninitialized ID");
    }
}
