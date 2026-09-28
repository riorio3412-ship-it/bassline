using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class MansionLegacySaveTests
    {
        [UnityTest] public IEnumerator ActualV6CompletedSaveUpgradesWithoutLosingArchiveOrKnowledge()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            string legacy=File.ReadAllText(Path.Combine(Application.dataPath,"../Verification/BL22_legacy_v6_fullflow.json"));
            var source=JsonUtility.FromJson<MansionSessionSnapshot>(legacy);
            Assert.That(source.OptionalObjects&16,Is.Zero,"The fixture must predate the chapter book presence marker.");
            Assert.That(source.World.Chapter,Is.EqualTo(2));Assert.That(source.Proceedings.Archive.Length,Is.EqualTo(1));
            File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(legacy)+"\n"+legacy);runtime.LoadFrom(runtime.SavePath);
            var loaded=runtime.CaptureSession();
            Assert.That(loaded.OptionalObjects&16,Is.EqualTo(16));
            Assert.That(loaded.OptionalObjects&1024,Is.EqualTo(1024));
            Assert.That(loaded.ResidentConversations,Is.Empty,"Old saves must not invent past resident speech.");
            Assert.That(loaded.World.Tick,Is.EqualTo(source.World.Tick));Assert.That(loaded.World.Cases.Chapter,Is.EqualTo(2));
            Assert.That(loaded.World.Cases.StartingResidents,Is.EqualTo(source.World.Residents.Count(r=>r.Alive&&r.Present)));
            Assert.That(loaded.World.Cases.Entries,Is.Empty);Assert.That(loaded.Proceedings.Archive.Length,Is.EqualTo(1));
            string[] playerRecords=runtime.Knowledge.For("CH_01").Records().Select(r=>r.Id).ToArray();
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.World.Tick,Is.EqualTo(source.World.Tick));Assert.That(runtime.ReadArchive().Cases.Count,Is.EqualTo(1));
            Assert.That(runtime.Knowledge.For("CH_01").Records().Select(r=>r.Id).ToArray(),Is.EqualTo(playerRecords));
        }
    }
}
