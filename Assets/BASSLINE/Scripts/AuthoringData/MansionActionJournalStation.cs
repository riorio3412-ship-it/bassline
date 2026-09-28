using UnityEngine;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureTarget))]
    public sealed class MansionActionJournalStation:MonoBehaviour
    {
        public string SourceId="",DeviceId="",RoomId="";
        public string ReaderNode="";
        public bool Powered=true;
        public const string Notice="이 단말은 연결된 장치의 경고 표시, 동작 유지, 원인 작동과 결과만 기록합니다. 주변 사람의 얼굴·대화·속마음이나 다른 장치의 행동은 기록하지 않습니다. 기록되지 않았다는 이유만으로 아무 일도 없었다고 판단할 수 없습니다.";
    }
}
