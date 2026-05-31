using System;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// Contract for any entity that supports level progression.
    /// </summary>
    public interface ILevelable
    {
        /// <summary>Current level of the entity.</summary>
        int Level { get; }

        /// <summary>Maximum level the entity can reach.</summary>
        int MaxLevel { get; }

        /// <summary>Whether the entity can still be leveled up.</summary>
        bool CanLevelUp { get; }

        /// <summary>Advances the entity by one level, if possible.</summary>
        void LevelUp();
    }

    /// <summary>
    /// Runtime representation of a single tile placed on the grid.
    /// Wraps a <see cref="TileDataSO"/> with mutable state (level, position)
    /// and exposes convenience queries for tile classification.
    /// </summary>
    public class TileEntity : ILevelable
    {
        // ─────────────────────────────────────────────
        //  Fields
        // ─────────────────────────────────────────────

        /// <summary>Static data asset that defines this tile's properties.</summary>
        public TileDataSO Data { get; }

        /// <summary>Position of this tile on the grid (column, row).</summary>
        public Vector2Int GridPosition { get; }

        private int _level;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>
        /// Fired whenever the tile's level changes.
        /// The payload is the new level value.
        /// </summary>
        public event Action<int> OnLevelChanged;

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="TileEntity"/> at the given grid position.
        /// </summary>
        /// <param name="data">The ScriptableObject data definition for this tile.</param>
        /// <param name="gridPosition">Grid coordinates where this tile resides.</param>
        /// <param name="initialLevel">Starting level (defaults to 1).</param>
        public TileEntity(TileDataSO data, Vector2Int gridPosition, int initialLevel = 1)
        {
            Data = data;
            GridPosition = gridPosition;
            _level = Mathf.Clamp(initialLevel, 1, data != null ? data.MaxLevel : 1);
        }

        // ─────────────────────────────────────────────
        //  ILevelable
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        public int Level => _level;

        /// <inheritdoc />
        public int MaxLevel => Data != null ? Data.MaxLevel : 1;

        /// <inheritdoc />
        public bool CanLevelUp => _level < MaxLevel;

        /// <inheritdoc />
        public void LevelUp()
        {
            if (!CanLevelUp) return;

            _level++;
            OnLevelChanged?.Invoke(_level);
        }

        // ─────────────────────────────────────────────
        //  Type Queries
        // ─────────────────────────────────────────────

        /// <summary>True when this tile is a resource-producing tile.</summary>
        public bool IsResource => Data != null && Data.Type == TileType.Resource;

        /// <summary>True when this tile is a facility tile.</summary>
        public bool IsFacility => Data != null && Data.Type == TileType.Facility;

        /// <summary>True when this tile is a road tile.</summary>
        public bool IsRoad => Data != null && Data.Type == TileType.Road;

        // ─────────────────────────────────────────────
        //  Visual Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the sprite associated with the current level from
        /// <see cref="TileDataSO.LevelSprites"/>.
        /// Falls back to the default <see cref="TileDataSO.Sprite"/> when no level sprite is available.
        /// </summary>
        /// <returns>The sprite for the current level.</returns>
        public Sprite GetCurrentSprite()
        {
            if (Data == null) return null;
            if (Data.LevelSprites == null || Data.LevelSprites.Length == 0)
            {
                return Data.Sprite;
            }

            // LevelSprites index 0 = Level 1
            int index = Mathf.Clamp(_level - 1, 0, Data.LevelSprites.Length - 1);
            return Data.LevelSprites[index] ?? Data.Sprite;
        }

        /// <summary>
        /// Returns a human-readable string for debugging purposes.
        /// </summary>
        public override string ToString()
        {
            string dataName = Data != null ? Data.DisplayName : "null";
            return $"[TileEntity] {dataName} Lv.{_level} @ {GridPosition}";
        }
    }
}
