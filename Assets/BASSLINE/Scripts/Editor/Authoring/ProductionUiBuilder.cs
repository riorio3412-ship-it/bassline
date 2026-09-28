using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using TMPro;
using BASSLINE.Core;
using BASSLINE.Save;
using BASSLINE.UI;
using BASSLINE.Bootstrap;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class ProductionUiBuilder
    {
        public const string Version="UIBuilder_001";
        const string ThemePath="Assets/BASSLINE/Data/UI/Themes/TH_BASSLINE_01.asset";
        static UIThemeDefinition theme;
        public static UIScreenDefinition[] BuildAssets()
        {
            string hash=AtomicSaveStore.Hash(Version);var index=ProductionImporter.LoadIndex();
            if(ProductionImporter.CanWrite(index,"TH_BASSLINE_01",ThemePath,hash)){
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));theme=AssetDatabase.LoadAssetAtPath<UIThemeDefinition>(ThemePath);
                if(!theme){theme=ScriptableObject.CreateInstance<UIThemeDefinition>();AssetDatabase.CreateAsset(theme,ThemePath);}AssetDatabase.SaveAssets();ProductionImporter.Track(index,"TH_BASSLINE_01",ThemePath,hash,"UI_ArtCandidate_FontReviewRequired");
            }else theme=AssetDatabase.LoadAssetAtPath<UIThemeDefinition>(ThemePath);
            var table=CsvTable.Read(File.ReadAllText(Path.Combine(ProductionImporter.SourceRoot,"04_UI/BASSLINE_UI_PREFAB_LIST.csv")));
            var definitions=new List<UIScreenDefinition>();
            foreach(var row in table){
                string id=row["ScreenID"],path=row["AssetPath"],definitionPath="Assets/BASSLINE/Data/UI/ScreenDefinitions/"+id+".asset",title=Title(id);
                index=ProductionImporter.LoadIndex();
                if(ProductionImporter.CanWrite(index,"VIEW_"+id,path,hash)){
                    Directory.CreateDirectory(Path.GetDirectoryName(path));var root=CreateScreen(id,title);PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);ProductionImporter.Track(index,"VIEW_"+id,path,hash,"UI_FunctionalProxy");
                }
                index=ProductionImporter.LoadIndex();
                if(ProductionImporter.CanWrite(index,"SCREENDEF_"+id,definitionPath,hash)){
                    Directory.CreateDirectory(Path.GetDirectoryName(definitionPath));var def=AssetDatabase.LoadAssetAtPath<UIScreenDefinition>(definitionPath);if(!def){def=ScriptableObject.CreateInstance<UIScreenDefinition>();AssetDatabase.CreateAsset(def,definitionPath);}
                    def.ScreenId=id;def.Title=title;def.TimePolicy=row["WorldTimePolicy"];def.CanvasGroup=row["ParentCanvas"];def.DataDependencies=row["ViewModel"];def.Theme=theme;def.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);EditorUtility.SetDirty(def);AssetDatabase.SaveAssets();ProductionImporter.Track(index,"SCREENDEF_"+id,definitionPath,hash,"UI_FunctionalProxy");
                }
                definitions.Add(AssetDatabase.LoadAssetAtPath<UIScreenDefinition>(definitionPath));
            }
            return definitions.ToArray();
        }
        public static void Attach(MonoBehaviour runtime,Camera camera)
        {
            var definitions=BuildAssets();var root=new GameObject("UIRoot",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var hud=root.AddComponent<FixtureHud>();hud.Source=runtime;hud.ViewCamera=camera;hud.PlayerFacing=camera.transform.parent;hud.Theme=theme;hud.ScreenDefinitions=definitions;
            hud.Clock=Text("Clock",root.transform,new Vector2(.87f,.95f),new Vector2(.975f,.99f),theme.Clock);hud.Clock.alignment=TextAlignmentOptions.TopRight;hud.Clock.color=theme.OffWhite;
            var interactionBacking=Rect("InteractionBacking",root.transform,new Vector2(.2f,.12f),new Vector2(.8f,.18f));
            hud.InteractionBackdrop=interactionBacking.AddComponent<UnityEngine.UI.Image>();hud.InteractionBackdrop.color=new Color(.025f,.03f,.045f,.92f);hud.InteractionBackdrop.raycastTarget=false;hud.InteractionBackdrop.enabled=false;
            hud.InteractionHint=Text("InteractionHint",root.transform,new Vector2(.2f,.12f),new Vector2(.8f,.18f),theme.Body);hud.InteractionHint.alignment=TextAlignmentOptions.Center;hud.InteractionHint.color=theme.OffWhite;
            var ambientPanel=Rect("Nearby voices",root.transform,new Vector2(.18f,.025f),new Vector2(.82f,.115f));ambientPanel.AddComponent<UnityEngine.UI.Image>().color=new Color(.025f,.035f,.045f,.9f);
            hud.AmbientCaption=Text("AmbientCaption",ambientPanel.transform,new Vector2(.035f,.06f),new Vector2(.965f,.94f),theme.Body);hud.AmbientCaption.alignment=TextAlignmentOptions.Center;hud.AmbientCaption.color=theme.OffWhite;hud.AmbientCaption.raycastTarget=false;ambientPanel.SetActive(false);
            foreach(var group in new[]{"ExplorationHUD","Dialogue","Note","Investigation","Trial","Truth","Evaluation","System"}){var layer=Rect(group,root.transform,Vector2.zero,Vector2.one);var canvas=layer.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=10+Array.IndexOf(new[]{"ExplorationHUD","Dialogue","Note","Investigation","Trial","Truth","Evaluation","System"},group)*10;layer.AddComponent<UnityEngine.UI.GraphicRaycaster>();}
            if(!UnityEngine.Object.FindAnyObjectByType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            ProductionImporter.Identity(root,"UIROOT_FIXTURE_K","UI_FunctionalProxy");
        }
        static GameObject CreateScreen(string id,string title)
        {
            var root=Rect("PF_"+id,null,Vector2.zero,Vector2.one);var view=root.AddComponent<ProductionScreenView>();view.ScreenId=id;
            var panel=Rect("Document",root.transform,new Vector2(.025f,.045f),new Vector2(.975f,.955f));panel.AddComponent<UnityEngine.UI.Image>().color=theme.OffWhite;
            view.Title=Text("Title",panel.transform,new Vector2(.025f,.885f),new Vector2(.97f,.975f),theme.Title);view.Title.text=title;
            view.BodyScroll=Scroll("Primary",panel.transform,new Vector2(.025f,.18f),new Vector2(.63f,.87f),out view.Body);
            view.ContextScroll=Scroll("Context",panel.transform,new Vector2(.66f,.18f),new Vector2(.975f,.87f),out view.Context);
            view.Status=Text("Status",panel.transform,new Vector2(.025f,.125f),new Vector2(.975f,.175f),theme.Caption);
            var actions=new List<UnityEngine.UI.Button>();var labels=new List<TMP_Text>();
            for(int i=0;i<8;i++){
                float x=.025f+i*.119f;var go=Rect("Action_"+i,panel.transform,new Vector2(x,.025f),new Vector2(x+.111f,.105f));var image=go.AddComponent<UnityEngine.UI.Image>();image.color=theme.DarkBlueGray;var button=go.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
                var colors=button.colors;colors.highlightedColor=theme.ColdGray;colors.selectedColor=theme.ColdGray;colors.pressedColor=theme.Teal;button.colors=colors;
                var label=Text("Label",go.transform,new Vector2(.03f,.05f),new Vector2(.97f,.95f),theme.Caption);label.alignment=TextAlignmentOptions.Center;label.color=theme.OffWhite;actions.Add(button);labels.Add(label);
            }
            view.Actions=actions.ToArray();view.ActionLabels=labels.ToArray();
            if(id=="UI_03"||id=="UI_23"||id=="UI_29"){
                panel.GetComponent<UnityEngine.UI.Image>().color=new Color(theme.DarkBlueGray.r,theme.DarkBlueGray.g,theme.DarkBlueGray.b,.92f);
                panel.GetComponent<RectTransform>().anchorMin=new Vector2(.03f,.035f);panel.GetComponent<RectTransform>().anchorMax=new Vector2(.97f,.39f);
                foreach(var t in panel.GetComponentsInChildren<TMP_Text>())t.color=theme.OffWhite;
                var standing=Rect("StandingSlot",root.transform,new Vector2(.03f,.39f),new Vector2(.38f,.96f));view.Standing=standing.AddComponent<UnityEngine.UI.Image>();view.Standing.preserveAspect=true;view.Standing.enabled=false;
            }
            if(id=="UI_16"){view.Body.text="What this supports";view.Context.text="What this does NOT establish";}
            if(id=="UI_13")view.Title.color=theme.Archive;
            return root;
        }
        static UnityEngine.UI.ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max,out TMP_Text text)
        {
            var root=Rect(name,parent,min,max);var scroll=root.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport=Rect("Viewport",root.transform,Vector2.zero,Vector2.one);viewport.AddComponent<UnityEngine.UI.Image>().color=new Color(1,1,1,.001f);viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var content=Rect("Content",viewport.transform,new Vector2(0,1),Vector2.one).GetComponent<RectTransform>();content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,600);
            var label=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(content,false);var rect=label.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            text=label.GetComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=theme.Body;text.color=theme.DarkBlueGray;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content;return scroll;
        }
        static GameObject Rect(string name,Transform parent,Vector2 min,Vector2 max){var go=new GameObject(name,typeof(RectTransform));if(parent)go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return go;}
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size){var go=Rect(name,parent,min,max);var text=go.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=size;text.color=theme.DarkBlueGray;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;}
        static readonly string[] titles={"탐색","상호작용","대화","응답","알림","약속 알림","NOTE","사건","인물","기록","지도 · 마지막 확인","일정","이전 회차 아카이브","현장 조사","관측 결과","증거 상세","조사 질문","확인 방법","증거 연결","사건 보드","가설","동선 재구성","재판","FOCUS","증거 제시","반론 검토","Crossfire","가설 비교","증언 요청","최종 재구성","투표","판결","전말","사건 평가","성장","정산","루프 결과","일시정지","설정","저장 · 불러오기","접근성"};
        public static string Title(string id)=>titles[int.Parse(id.Substring(3))-1];
    }
}

