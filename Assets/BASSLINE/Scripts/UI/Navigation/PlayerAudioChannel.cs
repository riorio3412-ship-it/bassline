using UnityEngine;

namespace BASSLINE.UI
{
    public enum PlayerAudioBus { Music, Sfx }

    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerAudioChannel:MonoBehaviour
    {
        public PlayerAudioBus Channel=PlayerAudioBus.Sfx;
        [Range(0,1)]public float BaseVolume=1;
        public AudioSource Source;
        void Awake(){if(!Source)Source=GetComponent<AudioSource>();}
        void OnEnable(){Apply(PlayerControls.Load());}
        public void Apply(PlayerControls settings)
        {
            if(!Source)Source=GetComponent<AudioSource>();
            if(Source)Source.volume=BaseVolume*(Channel==PlayerAudioBus.Music?settings.MusicVolume:settings.SfxVolume);
        }
    }
}
