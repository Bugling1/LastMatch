using UnityEngine;

namespace LastMatch.View
{
    /// <summary>Campaign progress and best scores, stored in PlayerPrefs.</summary>
    public static class SaveData
    {
        public static int Beaten { get => PlayerPrefs.GetInt("lm.beaten", 0); set { PlayerPrefs.SetInt("lm.beaten", value); PlayerPrefs.Save(); } }
        public static int BestEndless { get => PlayerPrefs.GetInt("lm.bestEndless", 0); set { PlayerPrefs.SetInt("lm.bestEndless", value); PlayerPrefs.Save(); } }
        public static int Best(int level) => PlayerPrefs.GetInt("lm.best." + level, 0);
        public static void SetBest(int level, int score) { PlayerPrefs.SetInt("lm.best." + level, score); PlayerPrefs.Save(); }
        public static bool Muted { get => PlayerPrefs.GetInt("lm.muted", 0) == 1; set { PlayerPrefs.SetInt("lm.muted", value ? 1 : 0); PlayerPrefs.Save(); } }
    }
}
