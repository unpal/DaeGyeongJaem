using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Script.UI
{
    [Serializable]
    public struct PhraseCue
    {
        public string key;
        public TitleType titleType;
        public float fadeIn;
        public float duration;
        public float fadeOut;
        public string text;

        public PhraseCue(string key, TitleType titleType, float fadeIn, float duration, float fadeOut, string text)
        {
            this.key = key;
            this.titleType = titleType;
            this.fadeIn = fadeIn;
            this.duration = duration;
            this.fadeOut = fadeOut;
            this.text = text;
        }
    }

    public static class PhraseTable
    {
        private static readonly Dictionary<string, PhraseCue> Table = new(StringComparer.OrdinalIgnoreCase);
        private static bool isLoaded;

        public static void EnsureLoaded()
        {
            if (isLoaded) return;
            Load();
        }

        public static void Load()
        {
            Table.Clear();
            TextAsset csvAsset = Resources.Load<TextAsset>("Phrases");
            if (csvAsset == null)
            {
                Debug.LogWarning("[PhraseTable] Resources/Phrases.csv 파일을 찾을 수 없습니다.");
                isLoaded = true;
                return;
            }

            string[] lines = csvAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1)
            {
                isLoaded = true;
                return;
            }

            // 첫 번째 줄(헤더)을 건너뛰고 파싱
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Key,Slot,In,Stay,Out,Text (최대 6개 파트로 분할하여 텍스트 내부의 쉼표 보존)
                string[] parts = line.Split(new[] { ',' }, 6);
                if (parts.Length < 6)
                    continue;

                string key = parts[0].Trim();
                if (!Enum.TryParse(parts[1].Trim(), true, out TitleType slot))
                    slot = TitleType.CenterText;

                float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fadeIn);
                float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float stay);
                float.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fadeOut);
                string text = parts[5].Trim();
                if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
                {
                    text = text.Substring(1, text.Length - 2);
                }
                text = text.Replace("\"\"", "\"")
                           .Replace("\\r\\n", "\n")
                           .Replace("\\n", "\n")
                           .Replace("\\t", "\t");

                Table[key] = new PhraseCue(key, slot, fadeIn, stay, fadeOut, text);
            }

            isLoaded = true;
        }

        public static bool TryGet(string key, out PhraseCue cue, params object[] args)
        {
            EnsureLoaded();

            if (Table.TryGetValue(key, out cue))
            {
                if (args != null && args.Length > 0 && !string.IsNullOrEmpty(cue.text))
                {
                    try
                    {
                        cue.text = string.Format(cue.text, args);
                    }
                    catch (FormatException ex)
                    {
                        Debug.LogWarning($"[PhraseTable] 포맷 치환 오류: key={key}, text={cue.text}, error={ex.Message}");
                    }
                }
                return true;
            }

            cue = default;
            return false;
        }

        public static PhraseCue Get(string key, params object[] args)
        {
            if (TryGet(key, out PhraseCue cue, args))
                return cue;

            return new PhraseCue(key, TitleType.CenterText, 0.2f, 2f, 0.5f, key);
        }
    }
}
