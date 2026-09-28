using System.Linq;
using BASSLINE.UI;
using NUnit.Framework;
namespace BASSLINE.Tests
{
    static class MansionUiTestEntry
    {
        public static void FinishSpeech(BASSLINE.Bootstrap.MansionRuntime runtime,FixtureHud hud)
        {
            for(int i=0;i<3600&&runtime.ReadConversationPlayback().Speaking;i++)runtime.AdvanceOne();
            Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Reading"));
            typeof(FixtureHud).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hud,null);
        }
        public static void Start(FixtureHud hud)
        {
            if(hud.CurrentScreen!=38)return;
            var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_38");
            Click(view,"새로 시작");Click(view,"문을 열고 들어가기");
            Assert.That(hud.CurrentScreen,Is.EqualTo(1));
        }
        static void Click(ProductionScreenView view,string label)
        {int index=System.Array.FindIndex(view.ActionLabels,t=>t.text==label);Assert.That(index,Is.GreaterThanOrEqualTo(0),label);view.Actions[index].onClick.Invoke();}
    }
}
