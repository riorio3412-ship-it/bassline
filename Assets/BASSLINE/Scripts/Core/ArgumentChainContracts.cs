using System;
namespace BASSLINE.Core
{
    public sealed class ArgumentNodeView
    {
        public string Key,ClaimId,SpanId,SpeakerId,Text,Review,State;
        public string[] Requires=Array.Empty<string>(),ProofIds=Array.Empty<string>();
    }
    public sealed class ArgumentChainView
    {
        public string Id="",Phase="Empty",Mode="Preparation",SelectedNode="",TargetNode="",ReturnTopic="";
        public int Revision,ChainCount;
        public ArgumentNodeView[] Nodes=Array.Empty<ArgumentNodeView>(),Candidates=Array.Empty<ArgumentNodeView>();
        public string[] CounterIds=Array.Empty<string>();
    }
    public interface IPlayerArgumentChainPort
    {
        ArgumentChainView ReadArgumentChain();
        string BeginArgumentChain(string claim,string span);
        string SelectArgumentNode(string key);
        string NextArgumentChain();
        string ToggleArgumentPremise(string claim,string span);
        string RemoveArgumentNode();
        string PublishArgumentChain();
        string ReviseArgumentChain();
        string WithdrawArgumentChain();
        string FocusArgumentNode();
    }
}
