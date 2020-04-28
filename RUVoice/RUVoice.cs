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
                            AudioPlayer wAudioPlayer = __instance.gameObject.AddComponent<AudioPlayer>() as AudioPlayer;
                            wAudioPlayer.SetChar(audioChar);
                            wAudioPlayer.SetAudioFile(mytext);
                            wAudioPlayer.SetWhoop();
                            wAudioPlayer.PlaySound();
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

            public void Start()
            {
            }

            public void SetChar(CharacterComponent myChar)
            {
                this.audioChar = myChar;
                this.myAudioSource = this.audioChar.GetAudioSource(CharacterComponent.AudioSourceType.Character);
                this.SetAudioPitch();
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
                if (this.audioChar)
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
                this.myAudioSource.Play();
                //myAudioSource.PlayOneShot(URL.GetAudioClip(false, true));
                //audioChar.PlaySound(URL.GetAudioClip(false, true), 1.0f, CharacterComponent.AudioSourceType.Character);
                //audioContainer.PlayOneShot(URL.GetAudioClip(false, true), 1.0f);
                //audioContainer.clip = URL.GetAudioClip(false, true);
                //audioContainer.volume = 1.0f;
                //audioContainer.Play();
            }
        }
    }
}
