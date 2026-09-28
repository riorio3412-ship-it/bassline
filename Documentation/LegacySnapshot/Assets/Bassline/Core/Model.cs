using System;
using System.Collections.Generic;
using System.Linq;

namespace Bassline
{
    public enum Phase { Announcement, Daily, Investigation, Trial, Verdict, FinalHearing, Loop, PlayerOut }
    public enum EvidenceKind { Official, Physical, Testimony }
    public enum LinkKind { Supports, Contradicts, Source, Excludes }
    public enum SpeechAct { Question, Claim, Rebuttal, Defend, Reveal, Withhold, Mediate, Agree, ChangeTopic, Request }

    [Serializable] public class Content
    {
        public List<CharacterData> characters = new List<CharacterData>();
        public List<RuleData> rules = new List<RuleData>();
        public CharacterData Person(int id) { return characters.Single(x => x.id == id); }
        public RuleData Rule(string id) { return rules.Single(x => x.id == id); }
    }
    [Serializable] public class CharacterData
    {
        public int id, age, observation, reasoning, argument, empathy, deception, composure, cooperation, suspicion, desire;
        public string name, gender, job, tagline, appearance, personality, voice, contract, behavior, relationships, quote, color;
    }
    [Serializable] public class RuleData
    {
        public string id, name, type, description, conditions;
        public int minAlive, minChapter;
        public bool major, implemented, proposal;
    }
    [Serializable] public class Relationship
    {
        public int target, trust, affection, grievance;
    }
    [Serializable] public class Knowledge
    {
        public string evidenceId;
        public int learnedAt, source;
        public bool verified;
    }
    [Serializable] public class CharacterState
    {
        public int id, room, bedroom, pressure, fear, fatigue, hypothesis = -1, promisedVote = -1;
        public bool alive = true, escaped, rewardExpired;
        public string action = "주변을 살피는 중", voteReason = "";
        public List<Relationship> relationships = new List<Relationship>();
        public List<Knowledge> knowledge = new List<Knowledge>();
        public List<int> candidates = new List<int>();
        public Relationship Relation(int target)
        {
            var r = relationships.Find(x => x.target == target);
            if (r == null) { r = new Relationship { target = target }; relationships.Add(r); }
            return r;
        }
        public bool Knows(string id) { return knowledge.Any(k => k.evidenceId == id); }
    }
    [Serializable] public class RoomState
    {
        public int id;
        public string name;
        public List<int> links = new List<int>();
        public bool accessible = true;
    }
    [Serializable] public class ItemState
    {
        public int id, owner, holder, room;
        public string name, state = "정상";
        public List<int> history = new List<int>();
    }
    [Serializable] public class WorldEvent
    {
        public int id, tick, actor = -1, target = -1, from = -1, to = -1, item = -1;
        public string kind, text;
        public bool publicEvent;
    }
    [Serializable] public class Evidence
    {
        public string id, title, text, route, sourceFamily;
        public EvidenceKind kind;
        public int room, speaker = -1, tick;
        public List<int> eventIds = new List<int>();
        public List<int> excludes = new List<int>();
        public List<int> supports = new List<int>();
        public List<string> requires = new List<string>();
    }
    [Serializable] public class EvidenceLink
    {
        public string from, to, explanation;
        public LinkKind kind;
    }
    [Serializable] public class Incident
    {
        // Sealed truth is read only by generator, validator and adjudication, never NPC reasoning.
        public int culprit, tick, room, item, motiveEvent;
        public string template, title;
        public List<int> victims = new List<int>();
        public List<int> suspects = new List<int>();
        public List<string> motives = new List<string>();
        public List<Evidence> evidence = new List<Evidence>();
        public List<EvidenceLink> links = new List<EvidenceLink>();
        public List<int> positions = new List<int>();
    }
    [Serializable] public class Speech
    {
        public int speaker, target = -1, tick;
        public SpeechAct act;
        public string text, evidenceId = "", secondId = "", reason = "";
    }
    [Serializable] public class Ballot
    {
        public int voter, target;
        public string reason;
    }
    [Serializable] public class TrialState
    {
        public int turn, speakerCursor, topic, nominated = -1, tieRound;
        public bool settled, correct;
        public List<Speech> speeches = new List<Speech>();
        public List<Ballot> ballots = new List<Ballot>();
        public List<string> disclosed = new List<string>();
        public List<string> usedClaims = new List<string>();
        public List<string> playerLinks = new List<string>();
        public List<int> tieCandidates = new List<int>();
        public string verdict = "";
    }
    [Serializable] public class LoopRecord
    {
        public int loop, chapter, culprit, nominee;
        public string title, verdict;
        public List<WorldEvent> events = new List<WorldEvent>();
    }
    [Serializable] public class WorldState
    {
        public int schema = 1, seed, loop = 1, chapter = 1, tick, chapterStart, chapterEventStart, deadlineActor = -1;
        public uint randomState;
        public Phase phase;
        public List<CharacterState> characters = new List<CharacterState>();
        public List<RoomState> rooms = new List<RoomState>();
        public List<ItemState> items = new List<ItemState>();
        public List<WorldEvent> events = new List<WorldEvent>();
        public List<string> rules = new List<string>();
        public List<string> previousRules = new List<string>();
        public List<int> disenfranchised = new List<int>();
        public List<Ballot> preliminary = new List<Ballot>();
        public List<LoopRecord> archive = new List<LoopRecord>();
        public Incident incident;
        public TrialState trial = new TrialState();
        public int hearingVolunteer = -1;
        public bool preliminaryConfirmed;
        public string notice = "";
        public CharacterState Person(int id) { return characters.Single(x => x.id == id); }
        public List<CharacterState> Living { get { return characters.Where(x => x.alive).ToList(); } }
        public bool HasRule(string id) { return rules.Contains(id); }
        public int Next(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            uint x = randomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            randomState = x == 0 ? 2463534242u : x;
            return (int)(randomState % (uint)max);
        }
        public WorldEvent Record(string kind, string text, int actor = -1, int target = -1, bool visible = false, int from = -1, int to = -1, int item = -1)
        {
            var e = new WorldEvent { id = events.Count, tick = tick, kind = kind, text = text, actor = actor, target = target, publicEvent = visible, from = from, to = to, item = item };
            events.Add(e); return e;
        }
        public static int Clamp(int x, int low = 0, int high = 100) { return Math.Max(low, Math.Min(high, x)); }
        public string Time { get { return string.Format("{0:00}:{1:00}", 8 + tick / 60, tick % 60); } }
    }
}
