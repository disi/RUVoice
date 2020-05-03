using UnityEngine;
using UnityModManagerNet;
using Harmony12;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections;

namespace RUVoice
{
    static class Main
    {
        // static variables
        public static bool enabled;
        public static Dictionary<string, string> myTexts = new Dictionary<string, string>();
        public static Dictionary<string, float> myPitch = new Dictionary<string, float>();
        public static string path = Environment.CurrentDirectory;
        public static string pathAudio = "\\Mods\\RUVoice\\audio\\";
        public static string pathWWW;
        public static string PathAudioWWW;
        public static string audioDBFile = "_ru_text_audio_DB.csv";
        public static string pitchDBFile = "_ru_text_pitch_DB.csv";
        // 1.0f + myPitchChange, sane levels -+0.07f
        public static Single myPitchChange = 0.06f;
        // myVolumeChange-[0-6]/10 for Aggressive to Passive
        public static Single myVolumeChange = 1.3f;
        // maximum pause in dialogue speech
        public static float maxSilenceUpdates = 0.5f;
        // highpassfilter value
        public static float highPassFilterValue = 400.0f;
        // lowpassfilter value
        public static float lowPassFilterValue = 5000.0f;
        // distortionfilter value
        public static float distortionFilterValue = 0.1f;

        // Load mod
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // harmony instance
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // log of patched methods
            var originalMethods = harmony.GetPatchedMethods();
            foreach (var method in originalMethods)
            {
                Debug.Log("---------- harmony patched methods:" + method.ToString());
            }

            modEntry.OnToggle = OnToggle;

            return true;
        }

        // On off Mod
        public static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            Main.pathWWW = Main.path.Replace("\\", "//");
            Main.PathAudioWWW = Main.pathAudio.Replace("\\", "//");

            Main.LoadTextDB();
            Main.LoadPitchDB();

            return true; // If true, the mod will switch the state. If not, the state will not change.
        }

        // load texts DB
        public static bool LoadTextDB()
        {
            if (File.Exists(Main.path + pathAudio + audioDBFile))
            {
                StreamReader reader = new StreamReader(File.OpenRead(Main.path + pathAudio + audioDBFile));
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        string[] values = line.Split('`');
                        Main.myTexts.Add(values[0], values[1]);
                        //Debug.Log("added key: " + values[0] + " - " + values[1]);
                    }
                }
                reader.Close();
            }
            return true;
        }

        // load Pitch DB
        public static bool LoadPitchDB()
        {
            if (File.Exists(Main.path + pathAudio + pitchDBFile))
            {
                StreamReader reader = new StreamReader(File.OpenRead(Main.path + pathAudio + pitchDBFile));
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        string[] values = line.Split('`');
                        Main.myPitch.Add(values[0], float.Parse(values[1]));
                        //Debug.Log("------------------ Added pitch from file: " + values[0] + " - " + values[1]);
                    }
                }
                reader.Close();
            }
            return true;
        }

        // update Pitch DB
        public static bool UpdatePitchDB()
        {
            var writer = File.AppendText(Main.path + pathAudio + pitchDBFile);
            List<CharacterComponent> currentChars = Game.World.GetAllCharacters();
            foreach (CharacterComponent iChar in currentChars)
            {
                string searchKey = iChar.name.ToLower().Trim();
                if (!myPitch.ContainsKey(searchKey))
                {
                    float pitch = 1.0f + UnityEngine.Random.Range(-Main.myPitchChange, Main.myPitchChange);
                    Main.myPitch.Add(searchKey, pitch);
                    writer.WriteLine(searchKey + "`" + pitch.ToString());
                    Debug.Log("----------------- Added Pitch to file:" + searchKey + " " + pitch);
                }
            }
            writer.Close();
            return true;
        }

        // check if we have the voice file in DB
        public static bool CheckMyTexts(string text)
        {
            return Main.myTexts.ContainsKey(text);
        }

        // check if file exists
        public static bool CheckFile(string text)
        {
            return File.Exists(Main.path + Main.pathAudio + text + ".ogg");
        }

        // convert text to key
        public static string Text2Key(string text)
        {
            return text.Remove(0, 1).ToLower();
        }

        // patch the dialogue phrases
        [HarmonyPatch(typeof(DialogPhraseNode), "Start")]
        public class Start_Phrase_Patch
        {
            [HarmonyPostfix]
            static void Start(DialogPhraseNode __instance)
            {
                string mytext = Main.Text2Key(__instance.Text);
                if (Main.CheckMyTexts(mytext))
                {
                    //Debug.Log("---------- Searching key:" + __instance.Text.Remove(0,1).ToLower());
                    if (CheckFile(mytext))
                    {
                        //Debug.Log("------------- trying to play audio file: " + Main.audioFile);
                        DialogHUD myDialogHUD = GameObject.FindObjectOfType<DialogHUD>();
                        if (myDialogHUD != null)
                        {
                            CharacterComponent audioChar;
                            CharacterComponent[] myChars = GameObject.FindObjectsOfType<CharacterComponent>();
                            for (int i = 0; i < myChars.Length; i++)
                            {
                                if ((myChars[i].InDialog) && (!myChars[i].IsPlayer()))
                                {
                                    audioChar = myChars[i];
                                    AudioPlayer dAudioPlayer = myDialogHUD.gameObject.AddComponent<AudioPlayer>() as AudioPlayer;
                                    dAudioPlayer.SetChar(audioChar);
                                    dAudioPlayer.SetAudioFile(mytext);
                                    dAudioPlayer.SetWhoop();
                                    dAudioPlayer.PlaySound();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        // patch the dialogue stop audio when closed
        [HarmonyPatch(typeof(DialogEndNode), "Start")]
        public class Start_DialogEndNode_Patch
        {
            [HarmonyPrefix]
            static void Start(DialogEndNode __instance)
            {
                CharacterComponent audioChar;
                CharacterComponent[] myChars = GameObject.FindObjectsOfType<CharacterComponent>();
                for (int i = 0; i < myChars.Length; i++)
                {
                    if ((myChars[i].InDialog) && (!myChars[i].IsPlayer()))
                    {
                        audioChar = myChars[i];
                        AudioSource myAudioSource = audioChar.GetAudioSource(CharacterComponent.AudioSourceType.Character);
                        if (myAudioSource.isPlaying)
                        {
                            //myAudioSource.Stop();
                            FadeOutClass myFadeOut = __instance.gameObject.AddComponent<FadeOutClass>() as FadeOutClass;
                            myFadeOut.Fade(myAudioSource, 2.0f);
                        }
                        break;
                    }
                }
            }
        }

        // patch the whoop phrases
        [HarmonyPatch(typeof(WhoopNode), "Start")]
        public class Start_Whoop_Patch
        {
            [HarmonyPostfix]
            static void Start(WhoopNode __instance)
            {
                string mytext = Main.Text2Key(__instance.text);
                if (Main.CheckMyTexts(mytext))
                {
                    //Debug.Log("---------- Searching key:" + __instance.Text.Remove(0,1).ToLower());
                    if (Main.CheckFile(mytext))
                    {
                        //Debug.Log("------------- trying to play audio file: " + Main.audioFile);
                        GameObject objectOverride = __instance.GetObjectOverride();
                        CharacterComponent audioChar = objectOverride.GetComponent<CharacterComponent>();
                        if (audioChar)
                        {
                            if (!audioChar.InDialog)
                            {
                                AudioPlayer wAudioPlayer = __instance.gameObject.AddComponent<AudioPlayer>() as AudioPlayer;
                                wAudioPlayer.SetChar(audioChar);
                                wAudioPlayer.SetAudioFile(mytext);
                                wAudioPlayer.SetWhoop();
                                wAudioPlayer.PlaySound();
                            }
                        }
                        else
                        {
                            //Debug.Log("--------------- No CharacterComponent for WhoopsNode");
                        }
                    }
                }
            }
        }

        // audio player
        public class AudioPlayer : MonoBehaviour
        {
            
            CharacterComponent audioChar;
            AudioSource myAudioSource;
            string myAudioFile;
            string fullPath;
            // testing
            private AudioHighPassFilter highPassFilter;
            private AudioLowPassFilter lowPassFilter;
            private AudioDistortionFilter distortion;

            public void Start()
            {
            }

            public void SetChar(CharacterComponent myChar)
            {
                this.audioChar = myChar;
                this.myAudioSource = this.audioChar.GetAudioSource(CharacterComponent.AudioSourceType.Character);
                highPassFilter = this.myAudioSource.gameObject.GetComponent<AudioHighPassFilter>();
                lowPassFilter = this.myAudioSource.gameObject.GetComponent<AudioLowPassFilter>();
                distortion = this.myAudioSource.gameObject.GetComponent<AudioDistortionFilter>();
                if (!highPassFilter)
                {
                    highPassFilter = this.myAudioSource.gameObject.AddComponent<AudioHighPassFilter>();
                }
                if (!lowPassFilter)
                {
                    lowPassFilter = this.myAudioSource.gameObject.AddComponent<AudioLowPassFilter>();
                }
                if (!distortion)
                {
                    distortion = this.myAudioSource.gameObject.AddComponent<AudioDistortionFilter>();
                }
                highPassFilter.cutoffFrequency = Main.highPassFilterValue;
                lowPassFilter.cutoffFrequency = Main.lowPassFilterValue;
                lowPassFilter.lowpassResonanceQ = 1.0f;
                highPassFilter.cutoffFrequency = 1.0f;
                distortion.distortionLevel = Main.distortionFilterValue;
                this.SetAudioPitch();
                this.SetAudioVolume();
            }

            public void SetAudioPitch()
            {
                string searchKey = this.audioChar.name.ToLower().Trim();
                if (Main.myPitch.ContainsKey(searchKey))
                {
                    this.myAudioSource.pitch = Main.myPitch[searchKey];
                    //Debug.Log("------------ Set Audio Pitch to: " + this.myAudioSource.pitch);
                }
                else
                {
                    Main.UpdatePitchDB();
                    this.SetAudioPitch();
                }
            }

            // set audio volume depending on character AI
            public void SetAudioVolume()
            {
                CharacterProto charProto = this.audioChar.Character.CharProto;
                // loudest to quietest
                switch (charProto.aiBehavior)
                {
                    case CharacterProto.AIBehavior.Aggressive:
                        //Debug.Log("-------------- Aggressive: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (0 / 10);
                        break;
                    case CharacterProto.AIBehavior.Clawed:
                        //Debug.Log("-------------- Clawed: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (1 / 10);
                        break;
                    case CharacterProto.AIBehavior.Defence:
                        //Debug.Log("-------------- Defence: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (2 / 10);
                        break;
                    case CharacterProto.AIBehavior.Escape:
                        //Debug.Log("-------------- Escape: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (3 / 10);
                        break;
                    case CharacterProto.AIBehavior.Passive:
                        //Debug.Log("-------------- Passive: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (4 / 10);
                        break;
                    case CharacterProto.AIBehavior.Avoiding:
                        //Debug.Log("-------------- Avoiding: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (5 / 10);
                        break;
                    case CharacterProto.AIBehavior.Suicidal:
                        //Debug.Log("-------------- Suicidal: " + this.audioChar);
                        this.myAudioSource.volume = Main.myVolumeChange - (6 / 10);
                        break;
                }
            }

            public void SetAudioFile(string text)
            {
                this.myAudioFile = Main.pathWWW + Main.PathAudioWWW + text + ".ogg";
                if (audioChar.IsMale())
                {
                    //Debug.Log("----------------  is male");
                    this.fullPath = "file:///" + this.myAudioFile;
                }
                else
                {
                    //Debug.Log("----------------  is female");
                    this.fullPath = "file:///" + this.myAudioFile.Remove(this.myAudioFile.Length - 4, 4) + "F.ogg";
                }
            }

            // set the distance, the sound should be audible
            public void SetWhoop()
            {
                this.myAudioSource.spatialBlend = 1;
                //Debug.Log("---------- started AudioPlayer");
                if (this.audioChar.IsPlayer() || (this.audioChar.InDialog))
                {
                    //Debug.Log("---------------   Is Player: " + this.audioChar.name);
                    this.myAudioSource.maxDistance = 80.0f;
                }
                else
                {
                    if (this.audioChar.IsTeamMate())
                    {
                        //Debug.Log("---------------   Is teammate: " + this.audioChar.name);
                        this.myAudioSource.maxDistance = 40.0f;
                    }
                    else
                    {
                        //Debug.Log("---------------   Is something else: " + this.audioChar.name);
                        this.myAudioSource.maxDistance = 25.0f;
                    }
                }
            }

            public void PlaySound()
            {
                //Debug.Log("---------- myChar component: " + audioChar.ToString());
                //Debug.Log("----------------  " + audioChar.GetAudioSource(CharacterComponent.AudioSourceType.Character).outputAudioMixerGroup.name);
                if ((this.audioChar) && (this.audioChar.IsHuman()))
                {
                    if (this.myAudioSource)
                    {
                        //Debug.Log("---------- started AudioPlayer");
                        StartCoroutine(LoadAudio());
                    }
                    else
                    {
                        Debug.Log("---------- no audiosource found");
                    }
                }
            }

            IEnumerator LoadAudio()
            {
                //Debug.Log("---------- started download file");
                WWW URL = new WWW(fullPath);
                yield return URL;
                if (this.myAudioSource.isPlaying)
                {
                    this.myAudioSource.Stop();
                }
                this.myAudioSource.clip = URL.GetAudioClip(false, true);
                //Debug.Log("---------- Audio channels in clip: " + this.myAudioSource.clip.channels);
                this.myAudioSource.Play();
                //myAudioSource.PlayOneShot(URL.GetAudioClip(false, true));
                //audioChar.PlaySound(URL.GetAudioClip(false, true), 1.0f, CharacterComponent.AudioSourceType.Character);
                //audioContainer.PlayOneShot(URL.GetAudioClip(false, true), 1.0f);
                //audioContainer.clip = URL.GetAudioClip(false, true);
                //audioContainer.volume = 1.0f;
                //audioContainer.Play();
            }

            // set this high, so speech is not interrupted at the beginning
            public float updateStep = 5.0f;
            // 1024 audio samples are ~80ms in 44100Hz audio
            public int sampleDataLength = 2;
            private float currentUpdateTime = 0f;
            private float clipLoudness;
            private float[] clipSampleData;
            private bool isPaused = false;

            void Awake()
            {
                this.clipSampleData = new float[this.sampleDataLength];
            }

            // if playing, extend random silence in audio playback for dialogues
            void Update()
            {
                if ((this.myAudioSource) && (this.audioChar.InDialog))
                {
                    this.currentUpdateTime += Time.deltaTime;
                    if (currentUpdateTime >= this.updateStep)
                    {
                        if (!this.isPaused)
                        {
                            currentUpdateTime = 0f;
                            // audio data has only one channel 0
                            this.myAudioSource.GetOutputData(clipSampleData, 0);
                            this.clipLoudness = 0f;
                            foreach (float sample in clipSampleData)
                            {
                                this.clipLoudness += Mathf.Abs(sample);
                            }
                            if (this.clipLoudness == 0)
                            {
                                this.updateStep = UnityEngine.Random.Range(0.1f, Main.maxSilenceUpdates);
                                this.myAudioSource.Pause();
                                this.isPaused = true;
                                Debug.Log("!------------------- AudioSource paused " + audioChar.name + ", update in: " + this.updateStep);
                            }
                            else
                            {
                                this.updateStep = 5.0f;
                            }
                        }
                        else
                        {
                            this.myAudioSource.UnPause();
                            currentUpdateTime = 0f;
                            this.updateStep = 5.0f;
                            this.isPaused = false;
                            //Debug.Log("!------------------- AudioSource " + audioChar.name + " Unpaused!");
                        }
                    }
                }
            }
        }

        // class to fade out dialogue audio
        public class FadeOutClass : MonoBehaviour
        {
            AudioSource mySource;
            float fadeTime;
            public void Fade(AudioSource audioSource, float time)
            {
                this.mySource = audioSource;
                this.fadeTime = time;
                StartCoroutine(FadeOut());
            }
            IEnumerator FadeOut()
            {
                float startVolume = mySource.volume;
                while (mySource.volume > 0)
                {
                    mySource.volume -= startVolume * Time.deltaTime / fadeTime;
                    yield return null;
                }
                mySource.Stop();
                mySource.volume = startVolume;
            }
        }
    }
}
