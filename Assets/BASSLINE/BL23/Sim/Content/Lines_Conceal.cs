namespace BL23.Sim
{
    public static partial class LineBank
    {
        // 숨기기·숨겨 두기(Systems/Concealment.cs) 공용 대사. 누가 무언가를 몸에 숨기는 걸 가까이서 본 사람의 한마디와,
        // 수사 중 서랍·매트리스 밑·책 뒤를 뒤지다 숨겨진 물건을 찾아낸 사람의 말. 개인 파일에 같은 키가 있으면 그쪽이 우선한다.
        static void Init_Conceal()
        {
            const string A = "ANY";
            Add(A, "conceal_notice", P("…방금 뭘 넣으신 거예요?", "지금 뭘 감추신 거죠?", "그거, 방금 품에 넣은 거 뭐예요?"),
                                     C("…방금 뭘 넣은 거야?", "지금 뭘 숨긴 거야?", "그거 방금 품에 넣은 거 뭐야?"));
            Add(A, "conceal_notice_weapon", P("잠깐만요. 방금 그거 {item} 아니었어요?", "{item:을} 왜 몸에 숨기세요…?", "…그걸 왜 품에 넣으세요?"),
                                            C("잠깐. 방금 그거 {item} 아니었어?", "{item:을} 왜 몸에 숨겨…?", "…그걸 왜 품에 넣어?"));
            Add(A, "conceal_notice_blood", P("…그거, 피 묻은 거 아니에요?", "방금 숨기신 거… 붉은 얼룩이 있었어요.", "잠깐만요, 거기 묻은 거 뭐예요?"),
                                           C("…그거 피 묻은 거 아니야?", "방금 숨긴 거… 붉은 얼룩 있었어.", "잠깐, 거기 묻은 거 뭐야?"));
            Add(A, "stash_found", P("…어? 이게 왜 {place}에 있지?", "누가 {place}에 {item:을} 넣어 뒀네요.", "{item}… 여기 둔 사람이 누구예요?"),
                                  C("…어? 이게 왜 {place}에 있지?", "누가 {place}에 {item:을} 넣어 뒀네.", "{item}… 여기 둔 사람 누구야?"));
            Add(A, "stash_found_bad", P("여기 좀 보세요! {place}에 {item:이} 숨겨져 있었어요!", "…{item}. 누가 일부러 {place}에 숨긴 거예요.", "다들 와 보세요. {place}에서 {item:이} 나왔어요."),
                                      C("여기 좀 봐! {place}에 {item:이} 숨겨져 있었어!", "…{item}. 누가 일부러 {place}에 숨긴 거야.", "다들 와 봐. {place}에서 {item:이} 나왔어."));
        }
    }
}
