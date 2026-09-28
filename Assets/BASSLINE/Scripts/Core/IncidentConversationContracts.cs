namespace BASSLINE.Core
{
    public interface IPlayerIncidentConversationPort
    {
        bool CanDiscussIncident(string actor);
        string AskAboutIncident(string actor,bool directOnly);
        string StateIncidentPosition(string actor,bool admit);
    }
}
