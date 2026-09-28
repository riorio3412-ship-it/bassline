using System;
namespace BASSLINE.Core
{
    public sealed class TheoryView
    {
        public string Id,ParentId,Owner,Title,State,Lifecycle,TargetNode;public int Revision;
        public ArgumentNodeView[] Premises=Array.Empty<ArgumentNodeView>();
        public string[] Gaps=Array.Empty<string>(),Counterexamples=Array.Empty<string>(),ConditionalSupporters=Array.Empty<string>(),DeferredBy=Array.Empty<string>();
    }
    public sealed class TheoryComparisonView
    {
        public string Left="",Right="",Focused="",SelectedNode="",Mode="Comparison";
        public TheoryView[] Theories=Array.Empty<TheoryView>();
        public ArgumentNodeView[] ContextCandidates=Array.Empty<ArgumentNodeView>();
    }
    public interface IPlayerTheoryComparisonPort
    {
        TheoryComparisonView ReadTheoryComparison();
        string SwitchTheorySide();
        string NextComparedTheory();
        string NextTheoryPremise();
        string FocusTheoryPremise();
        string StateTheoryPosition(string position);
        string AttachTheoryContext(string claim,string span);
        string ReviseComparedTheory();
    }
}
