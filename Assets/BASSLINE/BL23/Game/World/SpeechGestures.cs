using System.Text.RegularExpressions;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Bodies follow what is said: a line's words, punctuation, emotion and intent pick a gesture and a face
    /// (a denial shakes the head, agreement nods, an accusation points, "I…" goes to the chest, uncertainty shrugs,
    /// anger slams, a trailing "…" looks away). Used for world speech, conversations and the trial.
    /// </summary>
    public static class SpeechGestures
    {
        static readonly Regex Deny = new Regex(@"^(아니|아냐|아뇨|그럴 리|말도 안)|(아니야|아니에요|아닙니다|아니라니까|그런 적 없|안 했)", RegexOptions.Compiled);
        static readonly Regex Agree = new Regex(@"^(그래|맞아|맞습니다|네[,.! ]|응[,.! ]|그렇죠|알겠)|(맞아요|그렇습니다|그럼요)", RegexOptions.Compiled);
        static readonly Regex Unsure = new Regex(@"(글쎄|모르겠|잘 모르|기억이 안|확실하지)", RegexOptions.Compiled);
        static readonly Regex Self = new Regex(@"(^|\s)(저는|나는|제가|내가|난 |전 )", RegexOptions.Compiled);
        static readonly Regex Accuse = new Regex(@"(네가|너야|너잖아|당신이|당신이죠|범인은|거짓말|숨기고 있)", RegexOptions.Compiled);
        static readonly Regex Show = new Regex(@"(이걸 봐|이것 좀|보세요|보시죠|여기 있|이 증거|이 기록)", RegexOptions.Compiled);
        static readonly Regex Greet = new Regex(@"^(안녕|좋은 아침|반가워|반갑습니다|또 봐|나중에 봐)", RegexOptions.Compiled);
        static readonly Regex Thanks = new Regex(@"(고마워|고맙습니다|감사합니다|미안해|미안합니다|죄송)", RegexOptions.Compiled);

        /// <summary>Play a gesture + face on this actor for a spoken line. emotion may be Neutral when unknown.</summary>
        public static void Perform(ActorView v, string text, Emotion emotion = Emotion.Neutral, bool accusation = false)
        {
            if (v == null || v.Rig == null || v.Rig.Anim == null || string.IsNullOrEmpty(text)) return;
            string t = text.Trim();
            var g = Pick(t, emotion, accusation);
            float dur = Mathf.Clamp(t.Length * 0.055f, 1.1f, 4.2f);
            if (g == Gesture.Nod || g == Gesture.ShakeHead) dur = Mathf.Min(dur, 1.4f);
            if (g == Gesture.Slam || g == Gesture.Point) dur = 1.3f;
            v.Rig.Anim.PlayGesture(g, dur);
            var e = Face(t, emotion, g);
            if (e != Expr.Neutral || emotion != Emotion.Neutral) v.Rig.SetExpression(e, 0.85f);
        }

        /// <summary>What a listener does when something strong is said to them.</summary>
        public static void React(ActorView listener, Emotion speakerEmotion, bool accused)
        {
            if (listener?.Rig?.Anim == null) return;
            if (accused) { listener.Rig.Anim.PlayGesture(Gesture.Flinch, 1.0f); listener.Rig.SetExpression(Expr.Surprised, 0.8f); return; }
            if (speakerEmotion == Emotion.Angry) listener.Rig.Anim.PlayGesture(Gesture.Flinch, 0.9f);
            else if (speakerEmotion == Emotion.Sad || speakerEmotion == Emotion.Crying) listener.Rig.Anim.PlayGesture(Gesture.Listen, 2f);
            else if (speakerEmotion == Emotion.Surprised || speakerEmotion == Emotion.Fear) listener.Rig.Anim.PlayGesture(Gesture.Surprised, 1f);
        }

        static Gesture Pick(string t, Emotion emo, bool acc)
        {
            if (t.Contains("브라보") || t.Contains("박수")) return Gesture.Clap;
            if (t.Contains("선 넘") || t.Contains("불만이") || t.Contains("한계가 있")) return Gesture.TalkEmphatic;
            if (acc || (Accuse.IsMatch(t) && (t.Contains("!") || emo == Emotion.Angry))) return Gesture.Point;
            if (emo == Emotion.Angry) return t.EndsWith("!") && t.Length < 40 ? Gesture.Slam : Gesture.TalkEmphatic;
            if (emo == Emotion.Crying || emo == Emotion.Sad) return t.Contains("…") ? Gesture.Think : Gesture.Cry;
            if (emo == Emotion.Fear) return Gesture.Cower;
            if (emo == Emotion.Surprised) return Gesture.Surprised;
            if (emo == Emotion.Laugh) return Gesture.Laugh;
            if (Deny.IsMatch(t)) return Gesture.ShakeHead;
            if (Show.IsMatch(t)) return Gesture.Present;
            if (Greet.IsMatch(t)) return Gesture.Wave;
            if (Thanks.IsMatch(t)) return Gesture.Bow;
            if (Agree.IsMatch(t)) return Gesture.Nod;
            if (Unsure.IsMatch(t)) return Gesture.Shrug;
            if (t.EndsWith("?")) return t.Length > 25 ? Gesture.TalkEmphatic : Gesture.Think;
            if (Self.IsMatch(t) && t.Length < 60) return Gesture.HandOnChest;
            if (t.EndsWith("…") || t.StartsWith("…")) return Gesture.Think;
            if (emo == Emotion.Smirk || emo == Emotion.Disgust) return Gesture.CrossArms;
            return t.Length > 32 || t.Contains("!") ? Gesture.TalkEmphatic : Gesture.Talk;
        }

        static Expr Face(string t, Emotion emo, Gesture g)
        {
            switch (emo)
            {
                case Emotion.Smile: return Expr.Smile; case Emotion.Grin: return Expr.Grin; case Emotion.Angry: return Expr.Angry;
                case Emotion.Sad: return Expr.Sad; case Emotion.Surprised: return Expr.Surprised; case Emotion.Fear: return Expr.Fear;
                case Emotion.Smirk: return Expr.Smirk; case Emotion.Disgust: return Expr.Disgust; case Emotion.Crying: return Expr.Crying;
                case Emotion.Laugh: return Expr.Laugh; case Emotion.Pain: return Expr.Pain; case Emotion.Blank: return Expr.Blank; case Emotion.Break: return Expr.Break;
            }
            if (g == Gesture.ShakeHead && t.Contains("!")) return Expr.Angry;
            if (g == Gesture.Bow || g == Gesture.Wave) return Expr.Smile;
            if (g == Gesture.Shrug || g == Gesture.Think) return Expr.Neutral;
            if (t.Contains("ㅋ") || t.Contains("하하") || t.Contains("후후")) return Expr.Laugh;
            return Expr.Neutral;
        }
    }
}
