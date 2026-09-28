using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BASSLINE.Core;
using BASSLINE.World;

namespace BASSLINE.Save
{
    public interface ISnapshotCodec { string Encode(SessionSnapshot snapshot); SessionSnapshot Decode(string text); }
    [Serializable] public sealed class SessionSnapshot
    {
        public string Magic="BASSLINE_BOOTSTRAP_SAVE";
        public int SchemaVersion=1;
        public string CatalogHash;
        public long WorldTick;
        public int TickRate;
        public double PendingWorldSeconds;
        public PauseRecord[] Pause;
        public WorldSnapshot World;
        // This format intentionally rejects legacy and full-game saves. More systems need explicit sections/migrations.
    }
    public enum SaveCheckpoint { AfterTempFlush,BeforeReplace,AfterReplace }
    public sealed class AtomicSaveStore
    {
        readonly ISnapshotCodec codec;
        public AtomicSaveStore(ISnapshotCodec codec){this.codec=codec;}
        public static string Hash(string value)
        { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
        public void Save(string path,SessionSnapshot snapshot,Action<SaveCheckpoint> fault=null)
        {
            var payload=codec.Encode(snapshot);var bytes=Encoding.UTF8.GetBytes(Hash(payload)+"\n"+payload);
            var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
            var temp=full+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            fault?.Invoke(SaveCheckpoint.AfterTempFlush);
            DecodeAndValidate(temp,snapshot.CatalogHash,snapshot.World.MapVersion,snapshot.World.RuleSetVersion);
            fault?.Invoke(SaveCheckpoint.BeforeReplace);
            if(File.Exists(full))File.Replace(temp,full,full+".previous");else File.Move(temp,full);
            fault?.Invoke(SaveCheckpoint.AfterReplace);
        }
        public SessionSnapshot Load(string path,string catalogHash,string map,string rules) => DecodeAndValidate(path,catalogHash,map,rules);
        // Recovery is explicit: caller chooses .previous after primary failure; never silently report a corrupt slot as loaded.
        SessionSnapshot DecodeAndValidate(string path,string catalogHash,string map,string rules)
        {
            var text=File.ReadAllText(path,Encoding.UTF8);int split=text.IndexOf('\n');
            if(split!=64||Hash(text.Substring(split+1))!=text.Substring(0,split))throw new InvalidDataException("Checksum mismatch");
            var s=codec.Decode(text.Substring(split+1));
            if(s==null||s.Magic!="BASSLINE_BOOTSTRAP_SAVE"||s.SchemaVersion!=1||s.CatalogHash!=catalogHash)throw new InvalidDataException("Unsupported schema/catalog");
            if(double.IsNaN(s.PendingWorldSeconds)||double.IsInfinity(s.PendingWorldSeconds)||s.PendingWorldSeconds<0)throw new InvalidDataException("Invalid pending world time");
            var pause=PauseCoordinator.Restore(s.Pause);var clock=new WorldClock(pause,s.TickRate,s.WorldTick);
            WorldState.Restore(s.World,clock,pause,map,rules);return s;
        }
    }
}
