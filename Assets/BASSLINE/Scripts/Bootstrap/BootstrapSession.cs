using System;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.World;
using BASSLINE.Save;

namespace BASSLINE.Bootstrap
{
    public enum SessionMode { ArtReview, FixtureK, MansionM01 }
    public sealed class BootstrapSession : MonoBehaviour, IReadPausePort
    {
        public SessionMode Mode=SessionMode.ArtReview;
        public string RuleSetVersion="Bootstrap_001_REV10";
        public string CatalogHash;
        [Min(1)] public int TickRate=60;
        WorldClock clock; PauseCoordinator pause; WorldState world;
        double accumulator;
        public long WorldTick=>clock?.Tick??0;
        public bool WorldPaused=>pause!=null&&pause.IsPaused(ClockScope.World);
        void Awake()
        {
            if(Mode!=SessionMode.ArtReview)throw new NotSupportedException("FixtureK and MansionM01 simulation are not implemented yet");
            pause=new PauseCoordinator();clock=new WorldClock(pause,TickRate);
            world=new WorldState(clock,pause,"ARTREVIEW_01","M01",RuleSetVersion,new[]{"R_HALL"},Array.Empty<ActorRecord>(),Array.Empty<DoorRecord>());
        }
        void Update()
        {
            if(WorldPaused)return;
            accumulator+=Time.unscaledDeltaTime;
            // No time acceleration yet. Limit processing cost; retain unconsumed non-paused time.
            int count=0;while(accumulator>=1d/TickRate&&count++<8){clock.Step();accumulator-=1d/TickRate;}
        }
        public void AcquireReadPause(string owner){pause.Acquire(new StableId(owner),ClockScope.Both);}
        public void ReleaseReadPause(string owner){pause.Release(new StableId(owner));}
        public SessionSnapshot Capture()=>new SessionSnapshot {CatalogHash=CatalogHash,WorldTick=clock.Tick,TickRate=TickRate,PendingWorldSeconds=accumulator,Pause=pause.Capture(),World=world.Capture()};
        public void SaveBootstrap(string path)=>new AtomicSaveStore(new UnitySnapshotCodec()).Save(path,Capture());
        public void LoadBootstrap(string path)
        {
            var snapshot=new AtomicSaveStore(new UnitySnapshotCodec()).Load(path,CatalogHash,"M01",RuleSetVersion);
            var restoredPause=PauseCoordinator.Restore(snapshot.Pause);
            var restoredClock=new WorldClock(restoredPause,snapshot.TickRate,snapshot.WorldTick);
            var restoredWorld=WorldState.Restore(snapshot.World,restoredClock,restoredPause,"M01",RuleSetVersion);
            // Commit only after every section validates. Failed loads leave the current session intact.
            pause=restoredPause;clock=restoredClock;world=restoredWorld;TickRate=snapshot.TickRate;accumulator=snapshot.PendingWorldSeconds;
        }
    }
    public sealed class UnitySnapshotCodec : ISnapshotCodec
    {
        public string Encode(SessionSnapshot snapshot)=>JsonUtility.ToJson(snapshot);
        public SessionSnapshot Decode(string text)=>JsonUtility.FromJson<SessionSnapshot>(text);
    }
}
