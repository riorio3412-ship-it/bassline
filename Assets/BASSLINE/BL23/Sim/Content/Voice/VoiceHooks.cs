using System.Collections.Generic;

namespace BL23.Sim
{
    /// <summary>
    /// Voice-pack hooks for texts that used to be one hard-coded sentence for everybody (Requests.cs). Each hook renders the
    /// asker's own key through the resolver and keeps the old sentence when that resident's pack has no line for it.
    /// Keys and slots (see VoicePackGuide.md):
    ///   req_invite_&lt;activity&gt; → req_invite   {when} {place} {act}     an invitation ("오후 3시 20분", "온실", "차 한잔")
    ///   req_find                                 {item}                  "I lost my …, bring it if you see it"
    ///   req_deliver                              {t}                     "take this note to …"
    ///   req_accept_invite / _find / _deliver     {when} {place} / {item} / {t}   the answer when 민혁 says yes
    ///   req_refused                              —                       the answer when 민혁 says no
    ///   req_thanks_find                          {item}                  the owner gets the thing back
    ///   req_thanks_deliver                       {t} (= the sender)      the addressee gets the note
    /// </summary>
    public sealed partial class Simulation
    {
        Dictionary<string, string> ReqSlots(Request r)
        {
            var d = new Dictionary<string, string>();
            if (r.Kind == "invite") { d["when"] = ClockFmt.Mark(r.At, true); d["place"] = S.RoomName(r.Room); d["act"] = InviteWord.TryGetValue(r.Activity ?? "", out var w) ? w : Activities.Get(r.Activity)?.Kor ?? "산책"; }
            var it = S.I(r.Item); if (it != null) d["item"] = r.Kind == "deliver" ? "쪽지" : it.Def?.Kor ?? it.Kor;
            if (r.To != null) d["t"] = "@" + r.To;
            return d;
        }

        /// <summary>Re-voice a new request's text in the asker's words (req_invite_&lt;act&gt; / req_invite / req_find / req_deliver).</summary>
        internal void VoiceRequest(Actor a, Request r)
        {
            if (a == null || r == null) return;
            var slots = ReqSlots(r); string text = null;
            if (r.Kind == "invite" && r.Activity != null) text = Render(a.Id, Cast.Player, "req_invite_" + r.Activity, slots);
            if (text == null) text = Render(a.Id, Cast.Player, "req_" + r.Kind, slots);
            if (!string.IsNullOrEmpty(text)) r.Text = LineBank.FixParticles(text);
        }

        /// <summary>Re-voice the answer lines of AnswerRequest (req_accept_&lt;kind&gt; / req_refused).</summary>
        internal void VoiceAnswer(Actor npc, Request r, List<Utterance> res)
        {
            if (npc == null || r == null || res == null || res.Count == 0) return;
            string key = r.State == "refused" ? "req_refused" : "req_accept_" + r.Kind;
            var text = Render(npc.Id, Cast.Player, key, ReqSlots(r));
            if (!string.IsNullOrEmpty(text)) res[0].Text = LineBank.FixParticles(text);
        }

        /// <summary>Re-voice the thanks of RequestHandover (req_thanks_find: the owner; req_thanks_deliver: the addressee, {t} = sender).</summary>
        internal Utterance VoiceThanks(Actor npc, Request r, Utterance u)
        {
            if (npc == null || r == null || u == null) return u;
            var slots = ReqSlots(r); if (r.Kind == "deliver") slots["t"] = "@" + r.From;
            var text = Render(npc.Id, Cast.Player, "req_thanks_" + r.Kind, slots);
            if (!string.IsNullOrEmpty(text)) u.Text = LineBank.FixParticles(text);
            return u;
        }
    }
}
