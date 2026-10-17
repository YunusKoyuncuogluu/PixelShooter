using UnityEngine;

namespace LevelManagement
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "PixelShooter/LevelDatabase")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private LevelData[] levels;

        public int Count
        {
            get
            {
                if (levels == null)
                {
                    return 0;
                }

                return levels.Length;
            }
        }

        public LevelData GetLevelData(int levelNumber)
        {
            int count = Count;
            if (count == 0)
            {
                return null;
            }

            int index = (levelNumber - 1) % count;
            if (index < 0)
            {
                index += count;
            }

            return levels[index];
        }
    }
}
