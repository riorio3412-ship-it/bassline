using System.Linq;
using BASSLINE.Core;
using NUnit.Framework;
namespace BASSLINE.Tests
{
    public class PlayerRecordPresentationTests
    {
        KnownRecord Seen(string id,long from,long to,string place="Hall")=>new KnownRecord{Id=id,LoopId="LOOP_01",Source="CH_01",SubjectId="CH_04",Predicate="AtPlace",Value=place,PlaceId=place,Kind="Visual",Direct=true,IdentityConfirmed=true,FromTick=from,ToTick=to,ReceivedTick=to,Text=id};
        [Test] public void RepeatedSightingsStayOriginalAndDoNotInventContinuousPresence()
        {
            var early=Seen("first",1,2);var later=Seen("later",100,101);
            var input=new[]{early,later};var output=PlayerRecordPresentation.Compact(input);
            Assert.That(output.Length,Is.EqualTo(1));Assert.That(output[0],Is.SameAs(later));
            Assert.That(output[0].FromTick,Is.EqualTo(100));Assert.That(early.ToTick,Is.EqualTo(2));Assert.That(input.Length,Is.EqualTo(2));
        }
        [Test] public void ChangedLocationsAndDifferentWitnessesRemainSeparate()
        {
            var own=Seen("one",1,2);var elsewhere=Seen("two",3,4,"Library");var witness=Seen("three",5,6);witness.Source="CH_03";
            Assert.That(PlayerRecordPresentation.Compact(new[]{own,elsewhere,witness}).Length,Is.EqualTo(3));
        }
        [Test] public void MaterialCluesAppearBeforeRoutineSightings()
        {
            var routine=Seen("routine",100,101);var clue=new KnownRecord{Id="clue",Kind="Document",Predicate="SurfacePattern",Source="CH_01",Text="A mark",ReceivedTick=10};
            Assert.That(PlayerRecordPresentation.Compact(new[]{routine,clue}).First().Id,Is.EqualTo("clue"));
        }
        [Test] public void AutomaticComparisonDoesNotTreatPossessionAsUse()
        {
            var held=new KnownRecord{Predicate="HeldObject",Direct=true};
            Assert.That(PlayerRecordPresentation.ComparisonRule("UsedObject","Particular",10,new[]{held}),Is.EqualTo("LR04"));
            Assert.That(PlayerRecordPresentation.ComparisonRule("CausedOutcome","Particular",10,new[]{held}),Is.EqualTo("LR07"));
        }
    }
}
