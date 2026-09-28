using System;

namespace BL23.Sim
{
    /// <summary>Player conveniences that touch the kernel (validated commands, like every other player action).</summary>
    public sealed partial class Simulation
    {
        /// <summary>Minutes of investigation that must pass before the player may end it early.</summary>
        public const double MinInvestigation = 20;

        /// <summary>Can the player end the investigation now? `why` says why not; `left` is the investigation time that remains.</summary>
        public bool CanEndInvestigation(out string why, out double left)
        {
            left = S.Ch.InvestigationEnd >= 0 ? Math.Max(0, S.Ch.InvestigationEnd - S.Clock) : 0;
            why = null;
            if (S.Phase != Phase.Investigation) { why = "수사 중이 아니다"; return false; }
            if (P == null || !P.Alive) { why = "움직일 수 없다"; return false; }
            double since = S.Ch.FirstAnnounce >= 0 ? S.Clock - S.Ch.FirstAnnounce : 0;
            if (since < MinInvestigation) { why = $"{Math.Max(1, (int)Math.Ceiling(MinInvestigation - since))}분 뒤부터 가능"; return false; }
            return true;
        }

        /// <summary>민혁 is done investigating: the remaining investigation time is given up and the trial is summoned (the usual
        /// assembly rules, including the safety check for the critically injured, still apply).</summary>
        public bool PlayerEndInvestigation()
        {
            if (!CanEndInvestigation(out _, out var left)) return false;
            S.Ch.InvestigationEnd = S.Clock;
            S.Log("PlayerEndInvestigation", Cast.Player, data: ((int)Math.Round(left)).ToString());
            return true;
        }
    }
}
