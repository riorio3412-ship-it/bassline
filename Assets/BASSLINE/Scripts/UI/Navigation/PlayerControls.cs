using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BASSLINE.UI
{
    [Serializable] public sealed class ControlBinding { public string Action; public KeyCode Key; }
    [Serializable] public sealed class PlayerControls
    {
        public float Sensitivity=2, SensitivityY=2, FontScale=1, UiScale=1;
        public float Volume=.8f, MusicVolume=.7f, SfxVolume=.8f, TextSpeed=45, Fov=65;
        public bool InvertX, InvertY, ReduceMotion, ToggleRun, RunFovEffect;
        public bool ShowControlHints;
        public float RunFovAmount=4;
        public bool Fullscreen, HasDisplayOverride;
        public int ResolutionWidth, ResolutionHeight, VSync=1;
        public ControlBinding[] Bindings=Defaults();
        public static ControlBinding[] Defaults()=>new[]{B("Forward",KeyCode.W),B("Back",KeyCode.S),B("Left",KeyCode.A),B("Right",KeyCode.D),B("Run",KeyCode.LeftShift),B("Interact",KeyCode.E),B("Inspect",KeyCode.R),B("Drop",KeyCode.G),B("Note",KeyCode.N),B("Map",KeyCode.M),B("Focus",KeyCode.F),B("QuickSave",KeyCode.F5),B("QuickLoad",KeyCode.F9)};
        static ControlBinding B(string action,KeyCode key)=>new ControlBinding{Action=action,Key=key};
        public KeyCode Key(string action)=>Bindings.FirstOrDefault(x=>x.Action==action)?.Key??KeyCode.None;
        public bool Down(string action)=>Input.GetKeyDown(Key(action));
        public bool Held(string action)=>Input.GetKey(Key(action));
        public string Label(string action)=>Key(action)==KeyCode.LeftShift?"Shift":Key(action).ToString();
        public static string ActionLabel(string action)
        {
            switch(action){case "Forward":return "앞으로";case "Back":return "뒤로";case "Left":return "왼쪽";case "Right":return "오른쪽";case "Run":return "달리기";case "Interact":return "상호작용";case "Inspect":return "자세히 보기";case "Drop":return "내려놓기";case "Note":return "노트";case "Map":return "지도";case "Focus":return "FOCUS";case "QuickSave":return "빠른 저장";case "QuickLoad":return "빠른 불러오기";default:return action;}
        }
        public string Rebind(string action,KeyCode key)
        {
            if(!Bindings.Any(x=>x.Action==action)||!AllowedKey(key))return "예약된 키는 사용할 수 없습니다.";
            if(Bindings.Any(x=>x.Action!=action&&x.Key==key))return "이미 다른 동작에 사용 중인 키입니다.";
            Bindings.Single(x=>x.Action==action).Key=key;return "Applied";
        }
        public static PlayerControls Load()
        {
            string current=PlayerPrefs.GetString("BASSLINE.Controls.v2","");bool legacy=string.IsNullOrEmpty(current);
            if(legacy)current=PlayerPrefs.GetString("BASSLINE.Controls.v1","");return FromJson(current,legacy);
        }
        public static PlayerControls FromJson(string json,bool legacy=false)
        {
            try{if(string.IsNullOrWhiteSpace(json))return new PlayerControls();var result=new PlayerControls();JsonUtility.FromJsonOverwrite(json,result);if(legacy)result.SensitivityY=result.Sensitivity;result.Validate();return result;}
            catch{return new PlayerControls();}
        }
        public void Validate()
        {
            Sensitivity=Clamp(Sensitivity,.25f,6,2);SensitivityY=Clamp(SensitivityY,.25f,6,2);
            FontScale=Clamp(FontScale,1,2,1);UiScale=Clamp(UiScale,.85f,1.3f,1);
            Volume=Clamp(Volume,0,1,.8f);MusicVolume=Clamp(MusicVolume,0,1,.7f);SfxVolume=Clamp(SfxVolume,0,1,.8f);
            TextSpeed=Clamp(TextSpeed,0,120,45);Fov=Clamp(Fov,50,95,65);RunFovAmount=Clamp(RunFovAmount,0,8,4);VSync=Mathf.Clamp(VSync,0,1);
            if(ResolutionWidth<640||ResolutionHeight<360||ResolutionWidth>16384||ResolutionHeight>16384){ResolutionWidth=0;ResolutionHeight=0;}
            var defaults=Defaults();var valid=new List<ControlBinding>();var used=new HashSet<KeyCode>();
            foreach(var binding in Bindings??Array.Empty<ControlBinding>())
                if(binding!=null&&defaults.Any(x=>x.Action==binding.Action)&&!valid.Any(x=>x.Action==binding.Action)&&AllowedKey(binding.Key)&&used.Add(binding.Key))valid.Add(B(binding.Action,binding.Key));
            foreach(var binding in defaults){if(valid.Any(x=>x.Action==binding.Action))continue;var key=binding.Key;if(used.Contains(key))key=new[]{KeyCode.RightShift,KeyCode.LeftControl,KeyCode.RightControl,KeyCode.P,KeyCode.O,KeyCode.L,KeyCode.K,KeyCode.J,KeyCode.H,KeyCode.Y,KeyCode.U,KeyCode.I,KeyCode.T}.First(x=>!used.Contains(x));used.Add(key);valid.Add(B(binding.Action,key));}
            Bindings=defaults.Select(x=>valid.Single(v=>v.Action==x.Action)).ToArray();
        }
        static float Clamp(float value,float min,float max,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);
        static bool AllowedKey(KeyCode key)=>Enum.IsDefined(typeof(KeyCode),key)&&key!=KeyCode.None&&key!=KeyCode.Escape&&key!=KeyCode.Return&&key!=KeyCode.Tab&&key!=KeyCode.Mouse0;
        public void Save(){Validate();PlayerPrefs.SetString("BASSLINE.Controls.v2",JsonUtility.ToJson(this));PlayerPrefs.Save();}
    }
}
