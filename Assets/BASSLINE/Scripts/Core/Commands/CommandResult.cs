using System;

namespace BASSLINE.Core
{
    public enum CommandStatus { Rejected, Pending, Committed }
    [Serializable] public sealed class CommandResult
    {
        public CommandStatus Status;
        public string ReasonCode;
        public string PublicReason;
        public long NewRevision;
        public string[] EventIds = Array.Empty<string>();
        public static CommandResult Reject(string code,long revision) => new CommandResult {
            Status=CommandStatus.Rejected,ReasonCode=code,PublicReason="Action unavailable",NewRevision=revision
        };
        public CommandResult Copy() => new CommandResult { Status=Status,ReasonCode=ReasonCode,PublicReason=PublicReason,NewRevision=NewRevision,EventIds=(string[])EventIds.Clone() };
    }
    [Serializable] public sealed class CommandEnvelope
    {
        public string CommandId, ActorId, TargetId, Action;
        public long ExpectedRevision, RequestTick;
    }
    public interface ICommandPort { CommandResult Submit(CommandEnvelope command); }
    public interface IClockView { long WorldTick { get; } bool WorldPaused { get; } }
    public interface IReadPausePort:IClockView {void AcquireReadPause(string owner);void ReleaseReadPause(string owner);}
    // Only receiver-scoped B/C DTOs belong here. Never expose an A event or world snapshot.
    public interface IKnowledgeView { string OwnerId { get; } LastConfirmedLocation[] Locations(); }
    [Serializable] public sealed class LastConfirmedLocation
    {
        public string SubjectId, RoomId, ReceiptId;
        public long ConfirmedTick;
    }
}
