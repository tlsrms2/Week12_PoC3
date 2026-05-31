using UnityEngine;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Data
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Factory Delivery/Game Settings", order = 0)]
    public class GameSettingsSO : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("Width of the game grid in cells.")]
        [SerializeField] private int gridWidth = 20;

        [Tooltip("Height of the game grid in cells.")]
        [SerializeField] private int gridHeight = 20;

        [Tooltip("World-space size of a single grid cell.")]
        [SerializeField] private float cellSize = 2f;

        [Header("Default Settings")]
        [Tooltip("Default cooldown duration (in seconds) for resource production tiles.")]
        [SerializeField] private float defaultResourceCooldown = 5f;

        [Tooltip("Maximum upgrade level a tile can reach.")]
        [SerializeField] private int maxTileLevel = 5;

        [Header("Block / Day Structure")]
        [Tooltip("Number of placement blocks available per in-game day.")]
        [SerializeField] private int blocksPerDay = 4;

        [Tooltip("Maximum number of slices a player can use per day.")]
        [SerializeField] private int maxSlicesPerDay = 1;

        [Header("Economy")]
        [Tooltip("Base cost for a shop reroll.")]
        [SerializeField] private int rerollBaseCost = 10;

        [Tooltip("Multiplier applied to the reroll cost after each consecutive reroll.")]
        [SerializeField] private float rerollCostMultiplier = 2f;

        [Header("Logistics / Workers")]
        [Tooltip("Base movement speed of worker units (world units per second).")]
        [SerializeField] private float workerBaseSpeed = 3f;

        [Tooltip("Minimum distance workers maintain from each other to avoid overlap.")]
        [SerializeField] private float workerMinDistance = 0.5f;

        [Tooltip("Maximum carrying capacity of worker units.")]
        [SerializeField] private int workerCapacity = 3;

        /// <summary>
        /// Applies the editor configurations directly to the global static Constants.
        /// </summary>
        public void ApplyToConstants()
        {
            Constants.GridWidth = gridWidth;
            Constants.GridHeight = gridHeight;
            Constants.CellSize = cellSize;

            Constants.DefaultResourceCooldown = defaultResourceCooldown;
            Constants.MaxTileLevel = maxTileLevel;

            Constants.BlocksPerDay = blocksPerDay;
            Constants.MaxSlicesPerDay = maxSlicesPerDay;

            Constants.RerollBaseCost = rerollBaseCost;
            Constants.RerollCostMultiplier = rerollCostMultiplier;

            Constants.WorkerBaseSpeed = workerBaseSpeed;
            Constants.WorkerMinDistance = workerMinDistance;
            Constants.WorkerCapacity = workerCapacity;

            Debug.Log("[GameSettingsSO] Successfully applied editor configuration to Constants.");
        }

        /// <summary>
        /// 씬이 로드되기 극초기 단계(BeforeSceneLoad)에 Resources 폴더 아래의 "GameSettings" 에셋을 자동 로드하여
        /// 게임 내 중요 상수를 주입하는 안전장치 기능입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoadAndApply()
        {
            GameSettingsSO settings = Resources.Load<GameSettingsSO>("GameSettings");
            if (settings != null)
            {
                settings.ApplyToConstants();
                Debug.Log("[GameSettingsSO] [Auto-Load] Successfully auto-loaded and applied 'Resources/GameSettings.asset' before scene load.");
            }
            else
            {
                Debug.LogWarning("[GameSettingsSO] [Auto-Load] 'Resources/GameSettings.asset' not found in Resources folder. GameManager serialized reference will be used instead.");
            }
        }
    }
}
