using System;
namespace BASSLINE.Core
{
    public sealed class JointProofChoice { public string Id,Speaker,Text; }
    public sealed class JointPartView { public string RecordId,Speaker,Text,State; }
    public sealed class JointArgumentView
    {
        public string Phase="None",Disclosure="FullRecord";
        public JointProofChoice[] Choices=Array.Empty<JointProofChoice>();
        public JointPartView[] Parts=Array.Empty<JointPartView>();
    }
    public interface IPlayerJointArgumentPort
    {
        JointArgumentView ReadJointArgument();
        string ToggleJointProof(string recordId);
        string MoveJointProofFirst(string recordId);
        string ToggleJointDisclosure();
        string RequestJointConsent();
        string StartJointArgument();
        string StopJointArgument();
    }
}
