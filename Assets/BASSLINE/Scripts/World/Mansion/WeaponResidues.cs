using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    // Physical state, not an observation. Cause, actor and victim never enter an inspection card.
    [Serializable] public sealed class WeaponResidue
    {
        public string Id="",WeaponId="",CaseId="";
        public long HitSequence,CauseSequence,DepositedTick;
        public long RinsedTick=-1,RinseSequence;
        public WeaponResidue Copy()=>(WeaponResidue)MemberwiseClone();
        public string Appearance=>RinsedTick<0?"RedStain":"FaintRedStain";
    }
    [Serializable] public sealed class WeaponResidueSnapshot
    {
        public int Version=1,Loop=1;
        public long InstalledTick;
        public WeaponResidue[] Marks=Array.Empty<WeaponResidue>();
        public WeaponResidueSnapshot Copy()=>new WeaponResidueSnapshot{Version=Version,Loop=Loop,InstalledTick=InstalledTick,Marks=Marks.Select(m=>m.Copy()).ToArray()};
    }
    public sealed class WeaponResidues
    {
        WeaponResidueSnapshot state;
        public WeaponResidues(int loop,long tick){state=new WeaponResidueSnapshot{Loop=loop,InstalledTick=tick};}
        public WeaponResidue[] For(string weapon)=>state.Marks.Where(m=>m.WeaponId==weapon).Select(m=>m.Copy()).ToArray();
        public bool Deposit(MansionWorld world,MansionIncidentSnapshot incident)
        {
            if(world.Paused||incident?.Settings==null||!incident.Settings.PlayerInitiated||incident.CauseTick!=world.Tick||world.Loop!=state.Loop||world.Tick<state.InstalledTick)return false;
            var s=incident.Settings;
            var hit=world.Events.LastOrDefault(e=>e.Type=="PlayerWeaponHit"&&e.Actor==s.ActorId&&e.Target==s.TargetId&&e.Detail==s.ObjectId&&e.Tick==world.Tick);
            var cause=world.Events.SingleOrDefault(e=>"M_EVENT_"+e.Sequence==incident.CauseEvent&&e.Type=="IncidentCauseCommitted"&&e.Detail==s.Id&&e.Tick==world.Tick);
            if(hit==null||cause==null||state.Marks.Any(m=>m.CauseSequence==cause.Sequence))return false;
            state.Marks=state.Marks.Concat(new[]{new WeaponResidue{Id="WEAPON_RESIDUE_L"+state.Loop+"_"+cause.Sequence,WeaponId=s.ObjectId,CaseId=s.Id,HitSequence=hit.Sequence,CauseSequence=cause.Sequence,DepositedTick=world.Tick}}).ToArray();
            return true;
        }
        public void Rinse(MansionWorld world,string weapon,long eventSequence)
        {
            var e=world.Events.SingleOrDefault(x=>x.Sequence==eventSequence&&x.Type=="WeaponRinsed"&&x.Target==weapon&&x.Tick==world.Tick);
            if(world.Paused||world.Loop!=state.Loop||e==null)throw new ArgumentException("Missing completed physical rinse");
            foreach(var mark in state.Marks.Where(m=>m.WeaponId==weapon&&m.RinsedTick<0)){mark.RinsedTick=world.Tick;mark.RinseSequence=e.Sequence;}
        }
        public WeaponResidueSnapshot Capture()=>state.Copy();
        public static WeaponResidues Restore(WeaponResidueSnapshot s,MansionState world,string[] weapons)
        {
            if(s==null||s.Version!=1||s.Loop!=world.Loop||s.InstalledTick<0||s.InstalledTick>world.Tick||s.Marks==null||s.Marks.Any(m=>m==null)||s.Marks.Select(m=>m.Id).Distinct().Count()!=s.Marks.Length)throw new ArgumentException("Invalid weapon residue snapshot");
            foreach(var m in s.Marks){
                var hit=world.Events.SingleOrDefault(e=>e.Sequence==m.HitSequence);
                var cause=world.Events.SingleOrDefault(e=>e.Sequence==m.CauseSequence);
                if(!weapons.Contains(m.WeaponId)||m.Id!="WEAPON_RESIDUE_L"+s.Loop+"_"+m.CauseSequence||m.DepositedTick<s.InstalledTick||m.DepositedTick>world.Tick||hit==null||hit.Type!="PlayerWeaponHit"||hit.Actor!="CH_01"||hit.Detail!=m.WeaponId||hit.Tick!=m.DepositedTick||cause==null||cause.Type!="IncidentCauseCommitted"||cause.Detail!=m.CaseId||cause.Tick!=m.DepositedTick||m.CauseSequence<=m.HitSequence||m.RinsedTick< -1||m.RinsedTick>world.Tick)throw new ArgumentException("Residue has no matching physical cause");
                if(m.RinsedTick<0){if(m.RinseSequence!=0)throw new ArgumentException("Unrinsed mark has a rinse event");}
                else if(m.RinsedTick<m.DepositedTick||!world.Events.Any(e=>e.Sequence==m.RinseSequence&&e.Type=="WeaponRinsed"&&e.Target==m.WeaponId&&e.Tick==m.RinsedTick))throw new ArgumentException("Residue was changed without a rinse");
            }
            return new WeaponResidues(s.Loop,s.InstalledTick){state=s.Copy()};
        }
    }
}
