using System;
using System.Collections.Generic;
using System.Linq;

namespace BASSLINE.Core
{
    [Flags] public enum ClockScope { World = 1, Court = 2, Both = 3 }
    [Serializable] public sealed class PauseRecord
    {
        public string Owner;
        public ClockScope Scope;
    }
    public sealed class PauseCoordinator
    {
        private readonly Dictionary<string, ClockScope> owners = new Dictionary<string, ClockScope>(StringComparer.Ordinal);
        public bool IsPaused(ClockScope scope) => owners.Values.Any(x => (x & scope) != 0);
        public void Acquire(StableId owner, ClockScope scope)
        {
            if (scope == 0 || (scope & ~ClockScope.Both) != 0) throw new ArgumentOutOfRangeException(nameof(scope));
            if (owners.ContainsKey(owner.ToString())) throw new InvalidOperationException("Duplicate pause owner");
            owners.Add(owner.ToString(), scope);
        }
        public bool Release(StableId owner) => owners.Remove(owner.ToString());
        public PauseRecord[] Capture() => owners.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new PauseRecord { Owner = x.Key, Scope = x.Value }).ToArray();
        public static PauseCoordinator Restore(IEnumerable<PauseRecord> records)
        {
            var result = new PauseCoordinator();
            foreach (var record in records) result.Acquire(new StableId(record.Owner), record.Scope);
            return result;
        }
    }
    public sealed class WorldClock
    {
        public long Tick { get; private set; }
        public int TickRate { get; }
        private readonly PauseCoordinator pause;
        public WorldClock(PauseCoordinator pause, int tickRate, long initialTick = 0)
        {
            if (tickRate < 1 || initialTick < 0) throw new ArgumentOutOfRangeException();
            this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
            TickRate = tickRate; Tick = initialTick;
        }
        // One authority tick per call. The driver discards paused wall time.
        public bool Step()
        {
            if (pause.IsPaused(ClockScope.World)) return false;
            Tick = checked(Tick + 1); return true;
        }
    }
}
