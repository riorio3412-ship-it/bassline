using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        sealed class SettingRow
        {
            public string Name,Value,Help;public Action<int> Change;
            public SettingRow(string name,string value,string help,Action<int> change){Name=name;Value=value;Help=help;Change=change;}
        }
        int settingCategory,settingIndex;
        readonly string[] settingCategories={"시점 · 달리기","키 변경","화면 · 음향","읽기 · 접근성"};
        PlayerAudioFeedback audioFeedback;

        void RenderSettings(Action<string,Action> button,out string body,out string context)
        {
            var rows=SettingsRows();settingIndex=Mathf.Clamp(settingIndex,0,rows.Count-1);var current=rows[settingIndex];int from=settingIndex/6*6;
            body=settingCategories[settingCategory]+"\n\n"+string.Join("\n\n",rows.Skip(from).Take(6).Select((row,index)=>(from+index==settingIndex?"▶  ":"    ")+row.Name+"   "+row.Value));
            context=current.Help+"\n\n항목 "+(settingIndex+1)+" / "+rows.Count+"\n변경은 즉시 적용하고 이 기기에 저장합니다.\nTab / 방향키로 버튼 선택 · Enter 확인";
            if(rebindAction!="")context="새 키를 누르세요. Esc 취소\n\n"+PlayerControls.ActionLabel(rebindAction)+"\n이미 사용 중인 키는 덮어쓰지 않습니다.";
            button("범주 변경",()=>{settingCategory=(settingCategory+1)%settingCategories.Length;settingIndex=0;});
            button("이전 항목",()=>settingIndex=(settingIndex+rows.Count-1)%rows.Count);
            button("다음 항목",()=>settingIndex=(settingIndex+1)%rows.Count);
            button(settingCategory==1?"키 지정":"값 −",()=>{current.Change(-1);SaveControls();});
            button(settingCategory==1?"키 지정":"값 +",()=>{current.Change(1);SaveControls();});
            button("기본값 복원",()=>{Controls=new PlayerControls{HasDisplayOverride=true,ResolutionWidth=1280,ResolutionHeight=720};SaveControls();status="입력 · 화면 · 음향 설정을 기본값으로 복원했습니다.";});
        }
        List<SettingRow> SettingsRows()
        {
            var c=Controls;var rows=new List<SettingRow>();
            if(settingCategory==0)
            {
                rows.Add(new SettingRow("가로 감도",c.Sensitivity.ToString("0.00"),"마우스의 가로 움직임에 즉시 적용합니다.",d=>c.Sensitivity+=d*.25f));
                rows.Add(new SettingRow("세로 감도",c.SensitivityY.ToString("0.00"),"가로 감도와 독립적으로 설정합니다.",d=>c.SensitivityY+=d*.25f));
                rows.Add(new SettingRow("X축 반전",OnOff(c.InvertX),"마우스 좌우 방향을 반전합니다.",d=>c.InvertX=!c.InvertX));
                rows.Add(new SettingRow("Y축 반전",OnOff(c.InvertY),"마우스 상하 방향을 반전합니다.",d=>c.InvertY=!c.InvertY));
                rows.Add(new SettingRow("시야각",Mathf.RoundToInt(c.Fov)+"°","탐색 카메라의 수직 시야각입니다.",d=>c.Fov+=d*5));
                rows.Add(new SettingRow("달리기",c.ToggleRun?"토글":"누르는 동안","토글 모드는 달리기 키를 다시 누르면 걷기로 돌아갑니다. 메뉴를 열면 달리기가 해제됩니다.",d=>{c.ToggleRun=!c.ToggleRun;runLatched=false;}));
                rows.Add(new SettingRow("달리기 시야 효과",OnOff(c.RunFovEffect),"기본값은 꺼짐입니다. 켜면 달리는 동안 시야각만 조금 넓어집니다. 모션 감소 시 적용하지 않습니다.",d=>c.RunFovEffect=!c.RunFovEffect));
            }
            else if(settingCategory==1)
                foreach(var binding in c.Bindings){string action=binding.Action;rows.Add(new SettingRow(PlayerControls.ActionLabel(action),c.Label(action),"키 지정 후 새 키를 누르세요. Esc는 취소, Tab은 메뉴 탐색에 사용합니다.",d=>rebindAction=action));}
            else if(settingCategory==2)
            {
                rows.Add(new SettingRow("화면 모드",c.Fullscreen?"전체 화면":"창 모드","실행 빌드에 적용합니다. Editor Game View에서는 에디터의 화면 크기를 유지합니다.",d=>{c.Fullscreen=!c.Fullscreen;c.HasDisplayOverride=true;}));
                rows.Add(new SettingRow("해상도",c.ResolutionWidth>0?c.ResolutionWidth+" × "+c.ResolutionHeight:"현재 화면","모니터가 제공하는 해상도와 1280 × 720 / 1920 × 1080 중 선택합니다.",ChangeResolution));
                rows.Add(new SettingRow("수직 동기화",OnOff(c.VSync==1),"디스플레이의 갱신 주기에 맞춰 프레임 출력을 조절합니다.",d=>c.VSync=1-c.VSync));
                rows.Add(new SettingRow("전체 음량",Percent(c.Volume),"모든 소리를 함께 조절합니다.",d=>c.Volume+=d*.1f));
                rows.Add(new SettingRow("배경음악",Percent(c.MusicVolume),"음악 채널의 음량입니다. 조절하면 미리 듣기 소리가 재생됩니다.",d=>{c.MusicVolume+=d*.1f;SaveControls();audioFeedback.PreviewMusic();}));
                rows.Add(new SettingRow("효과음",Percent(c.SfxVolume),"상호작용과 메뉴 입력 효과음의 음량입니다.",d=>c.SfxVolume+=d*.1f));
            }
            else
            {
                rows.Add(new SettingRow("UI 배율",Percent(c.UiScale),"화면 안의 글자와 조작 영역을 함께 확대합니다.",d=>c.UiScale+=d*.05f));
                rows.Add(new SettingRow("글자 크기",Percent(c.FontScale),"긴 기록은 휠로 스크롤할 수 있습니다. 버튼 배치도 글자 크기에 맞춰 조정합니다.",d=>c.FontScale+=d*.1f));
                rows.Add(new SettingRow("대사 속도",c.TextSpeed<=0?"즉시 표시":Mathf.RoundToInt(c.TextSpeed)+" 글자/초","이미 수신한 대사의 표시 속도만 조절합니다. 세계 시간은 바뀌지 않습니다. 대화 중 상호작용 키로 바로 펼칠 수 있습니다.",d=>c.TextSpeed+=d*15));
                rows.Add(new SettingRow("조작키 안내",OnOff(c.ShowControlHints),"바라보는 대상에 쓸 수 있는 키를 보여줘요. 기본 조작은 노트에서도 확인할 수 있어요.",d=>c.ShowControlHints=!c.ShowControlHints));
                rows.Add(new SettingRow("모션 감소",OnOff(c.ReduceMotion),"대사는 즉시 표시하고 달리기 시야각 효과를 끕니다. 카메라 흔들림과 모션 블러는 사용하지 않습니다.",d=>c.ReduceMotion=!c.ReduceMotion));
            }
            return rows;
        }
        void ChangeResolution(int direction)
        {
            var resolutions=Screen.resolutions.Select(x=>new Vector2Int(x.width,x.height)).Concat(new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)}).Distinct().OrderBy(x=>x.x).ThenBy(x=>x.y).ToArray();
            int index=Array.FindIndex(resolutions,x=>x.x==Controls.ResolutionWidth&&x.y==Controls.ResolutionHeight);index=(Mathf.Max(0,index)+direction+resolutions.Length)%resolutions.Length;
            Controls.ResolutionWidth=resolutions[index].x;Controls.ResolutionHeight=resolutions[index].y;Controls.HasDisplayOverride=true;
        }
        static string OnOff(bool value)=>value?"켜짐":"꺼짐";
        static string Percent(float value)=>Mathf.RoundToInt(value*100)+"%";
    }
}
