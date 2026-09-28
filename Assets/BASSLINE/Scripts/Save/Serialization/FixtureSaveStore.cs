using System;
using System.IO;
using System.Text;
using BASSLINE.World.Fixture;
namespace BASSLINE.Save
{
 public sealed class FixtureSaveStore
 {
  readonly Func<LifeSnapshot,string> encode;readonly Func<string,LifeSnapshot> decode;
  public FixtureSaveStore(Func<LifeSnapshot,string> encode,Func<string,LifeSnapshot> decode){this.encode=encode;this.decode=decode;}
  public void Save(string path,LifeSnapshot snapshot,Action<SaveCheckpoint> fault=null)
  {
   LifeWorld.Validate(snapshot);string payload=encode(snapshot),full=Path.GetFullPath(path),temp=full+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(full));
   var bytes=Encoding.UTF8.GetBytes(AtomicSaveStore.Hash(payload)+"\n"+payload);using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}fault?.Invoke(SaveCheckpoint.AfterTempFlush);Load(temp);fault?.Invoke(SaveCheckpoint.BeforeReplace);
   if(File.Exists(full))File.Replace(temp,full,full+".previous");else File.Move(temp,full);fault?.Invoke(SaveCheckpoint.AfterReplace);
  }
  public LifeSnapshot Load(string path){string value=File.ReadAllText(path,Encoding.UTF8);if(value.Length<65||value[64]!='\n'||AtomicSaveStore.Hash(value.Substring(65))!=value.Substring(0,64))throw new InvalidDataException("Fixture checksum mismatch");var snapshot=decode(value.Substring(65));LifeWorld.Validate(snapshot);return snapshot;}
 }
}
