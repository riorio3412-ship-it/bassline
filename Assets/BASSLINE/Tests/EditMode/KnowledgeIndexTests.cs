using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
namespace BASSLINE.Tests
{
    public sealed class KnowledgeIndexTests
    {
        [Test] public void LastDirectIsOwnerScopedCopiedAndRestoredWithoutPromotingHearsay()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"});
            string id=ledger.Observe("CH_01",new KnownRecord{SubjectId="CH_03",Predicate="AtPlace",Value="R_HALL",FromTick=10,ToTick=11},10);
            ledger.Deliver("CH_01","CH_02",id,11);
            Assert.That(ledger.LastDirect("CH_02","CH_03","AtPlace"),Is.Null);
            var copy=ledger.LastDirect("CH_01","CH_03","AtPlace");copy.Value="tampered";
            var restored=KnowledgeLedger.Restore(ledger.Capture(),new[]{"CH_01","CH_02"},11);
            Assert.That(restored.LastDirect("CH_01","CH_03","AtPlace").Value,Is.EqualTo("R_HALL"));
            Assert.That(restored.LastDirect("CH_02","CH_03","AtPlace"),Is.Null);
            Assert.That(restored.For("CH_02").Records()[0].Direct,Is.False);
        }
    }
}
