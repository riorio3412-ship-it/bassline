using UnityEngine;
namespace BASSLINE.AuthoringData
{
    public sealed class UIScreenDefinition:ScriptableObject
    {
        public string ScreenId,Title,TimePolicy,CanvasGroup,DataDependencies,Status="FunctionalProxy";public GameObject Prefab;public UIThemeDefinition Theme;
    }
}
