namespace BASSLINE.Core
{
    // Input intent only: world movement, collision, and travel time remain authoritative.
    public interface IPlayerMotionPort { void SetRun(bool running); }
}
