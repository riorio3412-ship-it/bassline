using UnityEngine;

namespace BASSLINE.UI
{
    // Procedural input/volume-preview cues. These carry no truth or evidence meaning.
    public sealed class PlayerAudioFeedback:MonoBehaviour
    {
        AudioSource sfx,music;AudioClip selectClip,musicPreview;
        public void Initialize(PlayerControls settings)
        {
            if(sfx)return;
            sfx=Source("UI input feedback",PlayerAudioBus.Sfx,settings);
            music=Source("Music volume preview",PlayerAudioBus.Music,settings);
            selectClip=Tone("Procedural UI select proxy",.05f,720,.18f);
            musicPreview=Tone("Procedural music volume preview",.75f,220,.13f);
        }
        AudioSource Source(string label,PlayerAudioBus bus,PlayerControls controls)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);go.SetActive(false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;
            var channel=go.AddComponent<PlayerAudioChannel>();channel.Source=source;channel.Channel=bus;go.SetActive(true);channel.Apply(controls);return source;
        }
        public void Select(){if(sfx&&selectClip)sfx.PlayOneShot(selectClip);}
        public void PreviewMusic(){if(music&&musicPreview)music.PlayOneShot(musicPreview);}
        static AudioClip Tone(string name,float seconds,float hz,float level)
        {
            const int rate=22050;var data=new float[Mathf.CeilToInt(rate*seconds)];
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;float envelope=Mathf.Clamp01(t/.008f)*Mathf.Clamp01((seconds-t)/.06f);data[i]=Mathf.Sin(2*Mathf.PI*hz*t)*envelope*level;}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){if(selectClip)Destroy(selectClip);if(musicPreview)Destroy(musicPreview);}
    }
}
