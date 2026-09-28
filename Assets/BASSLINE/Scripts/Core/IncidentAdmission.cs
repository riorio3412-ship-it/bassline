using System;
using System.Linq;
using System.Collections.Generic;
namespace BASSLINE.Core
{
    [Serializable] public sealed class IncidentEvidenceRoute
    {
        public string Id="";
        public string[] RootIds=Array.Empty<string>(),AccessDependencies=Array.Empty<string>(),Responsibilities=Array.Empty<string>();
        public string[] ObservationIds=Array.Empty<string>(),AccessNodes=Array.Empty<string>();
        public bool Available,Preserved,RequiresPerson,RequiresFavor,RequiresRareTool;
        public IncidentEvidenceRoute Copy(){var c=(IncidentEvidenceRoute)MemberwiseClone();c.RootIds=(string[])RootIds.Clone();c.AccessDependencies=(string[])AccessDependencies.Clone();c.Responsibilities=(string[])Responsibilities.Clone();c.ObservationIds=(ObservationIds??Array.Empty<string>()).ToArray();c.AccessNodes=(AccessNodes??Array.Empty<string>()).ToArray();return c;}
    }
    [Serializable] public sealed class IncidentAdmissionReceipt
    {
        public string CaseId="",TemplateId="",DefinitionRevision="",Status="Rejected",Reason="";
        public long Tick;public IncidentEvidenceRoute[] Routes=Array.Empty<IncidentEvidenceRoute>();
        public string[] AcceptedRouteIds=Array.Empty<string>();
        public IncidentAdmissionReceipt Copy(){var c=(IncidentAdmissionReceipt)MemberwiseClone();c.Routes=Routes.Select(r=>r.Copy()).ToArray();c.AcceptedRouteIds=(string[])AcceptedRouteIds.Clone();return c;}
    }
    public sealed class IncidentAdmissionInput
    {
        public string CaseId="",TemplateId="",DefinitionRevision="";
        public long Tick;public bool ContentReviewed,ActionRegistered,PhysicalOpportunity,WithinResultDelayBound;
        public IncidentEvidenceRoute[] Routes=Array.Empty<IncidentEvidenceRoute>();
    }
    // This is authority-only admission before a cause, not a clue generator or a player hint.
    // The physical adapter supplies routes from registered resources that already exist. It
    // cannot claim independence merely by giving two copies of one original different names.
    public static class IncidentAdmission
    {
        public static readonly string[] RequiredResponsibilities={"Identity","KnowingAction","Cause","ResultLink"};
        public static IncidentAdmissionReceipt Evaluate(IncidentAdmissionInput input)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            var receipt=new IncidentAdmissionReceipt{CaseId=input.CaseId,TemplateId=input.TemplateId,DefinitionRevision=input.DefinitionRevision,Tick=input.Tick};
            string Reject(string why){receipt.Reason=why;return why;}
            if(input.Tick<0||string.IsNullOrEmpty(input.CaseId)||string.IsNullOrEmpty(input.TemplateId)||string.IsNullOrEmpty(input.DefinitionRevision))Reject("MissingRegistration");
            else if(!input.ContentReviewed||!input.ActionRegistered)Reject("UnreviewedContent");
            else if(!input.WithinResultDelayBound)Reject("UnboundedDelayedRisk");
            else if(!input.PhysicalOpportunity)Reject("PhysicalOpportunityLost");
            else if(input.Routes==null||input.Routes.Any(r=>r==null||string.IsNullOrEmpty(r.Id)||r.RootIds==null||r.RootIds.Length==0||r.RootIds.Any(string.IsNullOrEmpty)||r.AccessDependencies==null||r.AccessDependencies.Length==0||r.AccessDependencies.Any(string.IsNullOrEmpty)||r.Responsibilities==null)||input.Routes.Select(r=>r.Id).Distinct().Count()!=input.Routes.Length)Reject("InvalidEvidenceRoute");
            else{
                receipt.Routes=input.Routes.Select(r=>r.Copy()).ToArray();
                var usable=receipt.Routes.Where(r=>r.Available&&RequiredResponsibilities.All(r.Responsibilities.Contains)).ToArray();
                for(int i=0;i<usable.Length;i++)for(int j=i+1;j<usable.Length;j++){
                    var a=usable[i];var b=usable[j];
                    if(a.RootIds.Intersect(b.RootIds).Any()||a.AccessDependencies.Intersect(b.AccessDependencies).Any())continue;
                    bool IndependentOfPerson(IncidentEvidenceRoute r)=>r.Preserved&&!r.RequiresPerson&&!r.RequiresFavor&&!r.RequiresRareTool;
                    if(!IndependentOfPerson(a)&&!IndependentOfPerson(b))continue;
                    receipt.Status="Admitted";receipt.Reason="IndependentExistingRoutes";receipt.AcceptedRouteIds=new[]{a.Id,b.Id};return receipt;
                }
                Reject("IndependentAccessibleRoutesMissing");
            }
            return receipt;
        }
        public static void ValidateReceipt(IncidentAdmissionReceipt receipt,string caseId,long worldTick)
        {
            if(receipt==null||receipt.CaseId!=caseId||receipt.Tick<0||receipt.Tick>worldTick||receipt.Routes==null||receipt.AcceptedRouteIds==null||!new[]{"Admitted","Rejected"}.Contains(receipt.Status))throw new ArgumentException("Invalid admission receipt");
            if(receipt.Status!="Admitted")return;
            var checkedAgain=Evaluate(new IncidentAdmissionInput{CaseId=caseId,TemplateId=receipt.TemplateId,DefinitionRevision=receipt.DefinitionRevision,Tick=receipt.Tick,ContentReviewed=true,ActionRegistered=true,PhysicalOpportunity=true,WithinResultDelayBound=true,Routes=receipt.Routes});
            if(checkedAgain.Status!="Admitted"||receipt.AcceptedRouteIds.Length!=2||receipt.AcceptedRouteIds.Distinct().Count()!=2)throw new ArgumentException("Missing independent paths in admitted plan");
            var accepted=receipt.Routes.Where(r=>receipt.AcceptedRouteIds.Contains(r.Id)).ToArray();
            var selected=Evaluate(new IncidentAdmissionInput{CaseId=caseId,TemplateId=receipt.TemplateId,DefinitionRevision=receipt.DefinitionRevision,Tick=receipt.Tick,ContentReviewed=true,ActionRegistered=true,PhysicalOpportunity=true,WithinResultDelayBound=true,Routes=accepted});
            if(selected.Status!="Admitted")throw new ArgumentException("Saved path pair is not independent");
        }
    }
}
