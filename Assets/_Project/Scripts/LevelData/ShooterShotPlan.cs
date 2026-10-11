using System.Collections.Generic;
using Grids;

namespace LevelManagement
{
    public class ShooterShotPlan
    {
        public struct Row
        {
            public int ColorId;
            public int Pixels;
            public int Shots;
            public int Shooters;
        }

        private readonly Dictionary<CellData, int> cellShots;
        private readonly Dictionary<ShooterData, int> dataShots;

        public List<Row> Rows { get; }
        public bool IsBalanced { get; }

        public ShooterShotPlan(
            Dictionary<CellData, int> cellShots,
            Dictionary<ShooterData, int> dataShots,
            List<Row> rows,
            bool isBalanced)
        {
            this.cellShots = cellShots;
            this.dataShots = dataShots;
            Rows = rows;
            IsBalanced = isBalanced;
        }

        public int ForCell(CellData cell)
        {
            if (cell == null || cellShots == null)
                return 0;

            return cellShots.TryGetValue(cell, out int shots) ? shots : 0;
        }

        public int ForData(ShooterData data)
        {
            if (data == null || dataShots == null)
                return 0;

            return dataShots.TryGetValue(data, out int shots) ? shots : data.ShotCount;
        }
    }
}
