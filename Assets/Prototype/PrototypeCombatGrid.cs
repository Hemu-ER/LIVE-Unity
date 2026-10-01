using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    // Vector2Int.x = row, y = column. Occupancy and movement reservations are separate.
    public sealed class PrototypeCombatGrid
    {
        public int Rows { get; }
        public int Columns { get; }
        private readonly PrototypeUnit[,] occupants;
        private readonly PrototypeUnit[,] reservations;

        public PrototypeCombatGrid(int rows, int columns)
        {
            if (rows < 1 || columns < 1) throw new ArgumentOutOfRangeException(nameof(rows));
            Rows = rows; Columns = columns;
            occupants = new PrototypeUnit[rows, columns];
            reservations = new PrototypeUnit[rows, columns];
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.x < Rows && cell.y >= 0 && cell.y < Columns;
        public static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        public PrototypeUnit Occupant(Vector2Int cell) => Contains(cell) ? occupants[cell.x, cell.y] : null;
        public bool IsFree(Vector2Int cell) => Contains(cell) && occupants[cell.x, cell.y] == null && reservations[cell.x, cell.y] == null;

        public void Place(PrototypeUnit unit, Vector2Int cell)
        {
            if (unit == null || !IsFree(cell)) throw new InvalidOperationException($"Invalid or occupied spawn cell: {cell}");
            occupants[cell.x, cell.y] = unit;
        }

        public bool TryReserveStep(PrototypeUnit unit, Vector2Int destination)
        {
            if (unit == null || Occupant(unit.Cell) != unit || Distance(unit.Cell, destination) != 1 || !IsFree(destination)) return false;
            reservations[destination.x, destination.y] = unit;
            return true;
        }

        public void CompleteStep(PrototypeUnit unit, Vector2Int destination)
        {
            if (!Contains(destination) || reservations[destination.x, destination.y] != unit)
                throw new InvalidOperationException("Movement destination was not reserved.");
            Release(unit);
            occupants[destination.x, destination.y] = unit;
        }

        public void Release(PrototypeUnit unit)
        {
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                if (occupants[row, column] == unit) occupants[row, column] = null;
                if (reservations[row, column] == unit) reservations[row, column] = null;
            }
        }

        public void Clear()
        {
            Array.Clear(occupants, 0, occupants.Length);
            Array.Clear(reservations, 0, reservations.Length);
        }

        public bool TryFindStep(PrototypeUnit unit, PrototypeUnit target, out Vector2Int next)
        {
            next = unit.Cell;
            var queue = new Queue<Vector2Int>();
            var firstSteps = new Dictionary<Vector2Int, Vector2Int> { [unit.Cell] = unit.Cell };
            queue.Enqueue(unit.Cell);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell != unit.Cell && Distance(cell, target.Cell) <= unit.Stats.AttackRange)
                {
                    next = firstSteps[cell];
                    return true;
                }
                var neighbours = new List<Vector2Int>
                {
                    cell + Vector2Int.up, cell + Vector2Int.down,
                    cell + Vector2Int.left, cell + Vector2Int.right
                };
                neighbours.Sort((a, b) =>
                {
                    int distance = Distance(a, target.Cell).CompareTo(Distance(b, target.Cell));
                    if (distance != 0) return distance;
                    int row = a.x.CompareTo(b.x);
                    return row != 0 ? row : a.y.CompareTo(b.y);
                });
                foreach (var neighbour in neighbours)
                {
                    if (!IsFree(neighbour) || firstSteps.ContainsKey(neighbour)) continue;
                    firstSteps[neighbour] = cell == unit.Cell ? neighbour : firstSteps[cell];
                    queue.Enqueue(neighbour);
                }
            }
            return false; // Wait and retry when allies temporarily block every route.
        }
    }
}
