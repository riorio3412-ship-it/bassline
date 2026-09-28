using NUnit.Framework;
using BASSLINE.UI;
public sealed class PlayerUiTextTests
{
    [Test] public void TechnicalReasonNeverBecomesPlayerFacingText()
    {
        foreach(var value in new[]{"HiddenActorUnavailable","CASE_A_104","NullReferenceException","UnregisteredResult"})
            Assert.That(PlayerUiText.Status(value),Is.EqualTo("처리하지 못했어요. 현재 화면에서 다시 확인해 주세요."));
    }
    [Test] public void AuthoredFeedbackRetainsKnownReasonAndTime()
    {
        const string feedback="15:10에 받은 기록이에요. 그 전 시각은 확인하지 못했어요.";
        Assert.That(PlayerUiText.Status(feedback),Is.EqualTo(feedback));
        Assert.That(PlayerUiText.Status(null),Is.Empty);
        Assert.That(PlayerUiText.Status("M_HYP_3"),Is.EqualTo("가설을 적어 뒀어요."));
    }
}
