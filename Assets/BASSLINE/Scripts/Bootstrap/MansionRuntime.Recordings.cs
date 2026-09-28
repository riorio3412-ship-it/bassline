using UnityEngine;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        // Retired prototype geometry is disabled before interaction targets are indexed.
        // Divine eyes are a separate world feature, never a replacement media source.
        void RetireSurveillanceStations()
        {
            foreach(var station in FindObjectsByType<MansionRecordingStation>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                station.gameObject.SetActive(false);
            foreach(var target in FindObjectsByType<FixtureTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(target.StableId==MansionRecordingStation.WorkshopCamera||target.StableId==MansionRecordingStation.WorkshopReader)
                    target.gameObject.SetActive(false);
        }
        static MansionRecordingSnapshot EmptyRetiredRecordings(int loop,long tick)=>new MansionRecordingSnapshot{Loop=loop,InstalledTick=tick,LastTick=tick};
        BASSLINE.Save.MansionSessionSnapshot UpgradeRecordingContent(BASSLINE.Save.MansionSessionSnapshot s)
        {
            // No old footage is converted into witness testimony or divine-eye evidence.
            if(s?.World!=null){s.Recordings=EmptyRetiredRecordings(s.World.Loop,s.World.Tick);s.OptionalObjects|=2048;
                if(s.Ui?.SelectedEvidence!=null&&s.Knowledge?.Memories!=null){
                    var retired=s.Knowledge.Memories.Where(m=>m!=null&&RetiredSurveillance.IsRecord(m.Record)).Select(m=>m.Record.Id).ToArray();
                    s.Ui.SelectedEvidence=s.Ui.SelectedEvidence.Where(id=>!retired.Contains(id)).ToArray();
                }
                if(s.Inspection!=null&&(s.Inspection.TargetId==MansionRecordingStation.WorkshopCamera||s.Inspection.TargetId==MansionRecordingStation.WorkshopReader))s.Inspection.State="Cancelled";
            }
            return s;
        }
    }
}
