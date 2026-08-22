using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding and Dijkstra cost-flooding over a rectangular grid, sharing
/// one binary min-heap and one set of gScore/cameFrom buffers. Both searches
/// are the same weighted-graph search; A* is Dijkstra with a goal and an
/// admissible heuristic (Manhattan distance, valid because the cheapest
/// terrain costs 1 per step), so one internal routine serves both public
/// methods instead of duplicating the search loop. Buffers are sized once at
/// construction and cleared, not reallocated, per query.
/// </summary>
public sealed class Pathfinder
{
    private readonly int _width;
    private readonly int _height;

    private readonly int[,] _gScore;
    private readonly Vector2Int[,] _cameFrom;
    private readonly bool[,] _hasCameFrom;
    private readonly bool[,] _closed;
    private MinHeap _openHeap;

    public Pathfinder(int width, int height) {
        _width = width;
        _height = height;
        _gScore = new int[width, height];
        _cameFrom = new Vector2Int[width, height];
        _hasCameFrom = new bool[width, height];
        _closed = new bool[width, height];
        // Worst case every cell is pushed more than once (lazy deletion
        // instead of decrease-key); *4 is generous headroom for a 10x10
        // board and still a trivial fixed allocation at construction.
        _openHeap = new MinHeap(width * height * 4);
    }

    /// <summary>
    /// Finds the cheapest path from `start` to `goal`. `isBlocked` should
    /// already exclude the moving unit's own start cell. Writes the path
    /// (start excluded, goal included) into `result` and returns true, or
    /// clears `result` and returns false if `goal` is unreachable.
    /// </summary>
    public bool TryFindPath(Vector2Int start, Vector2Int goal, ITerrainCostSource terrain, Func<Vector2Int, bool> isBlocked, List<Vector2Int> result) {
        result.Clear();
        RunSearch(start, goal, terrain, isBlocked, int.MaxValue);

        if (!_hasCameFrom[goal.x, goal.y] && goal != start) {
            return false;
        }

        Vector2Int cursor = goal;
        while (cursor != start) {
            result.Add(cursor);
            cursor = _cameFrom[cursor.x, cursor.y];
        }
        result.Reverse();
        return true;
    }

    /// <summary>
    /// Floods every cell reachable from `start` within `movementBudget`,
    /// writing cell -> accumulated cost into `result` (start included at
    /// cost 0). Pass `int.MaxValue` for an uncapped flood.
    /// </summary>
    public void FloodCosts(Vector2Int start, int movementBudget, ITerrainCostSource terrain, Func<Vector2Int, bool> isBlocked, Dictionary<Vector2Int, int> result) {
        result.Clear();
        RunSearch(start, null, terrain, isBlocked, movementBudget);

        for (int cellX = 0; cellX < _width; cellX++) {
            for (int cellY = 0; cellY < _height; cellY++) {
                if (_closed[cellX, cellY] && _gScore[cellX, cellY] <= movementBudget) {
                    result[new Vector2Int(cellX, cellY)] = _gScore[cellX, cellY];
                }
            }
        }
    }

    /// <summary>
    /// Shared Dijkstra/A* loop. `goal` null means flood everything within
    /// `budget`; a supplied `goal` adds the Manhattan heuristic and stops
    /// early once the goal is popped from the open set.
    /// </summary>
    private void RunSearch(Vector2Int start, Vector2Int? goal, ITerrainCostSource terrain, Func<Vector2Int, bool> isBlocked, int budget) {
        for (int cellX = 0; cellX < _width; cellX++) {
            for (int cellY = 0; cellY < _height; cellY++) {
                _gScore[cellX, cellY] = int.MaxValue;
                _hasCameFrom[cellX, cellY] = false;
                _closed[cellX, cellY] = false;
            }
        }
        _openHeap.Clear();

        _gScore[start.x, start.y] = 0;
        _openHeap.Push(start, goal.HasValue ? Heuristic(start, goal.Value) : 0);

        while (_openHeap.Count > 0) {
            Vector2Int current = _openHeap.Pop();
            if (_closed[current.x, current.y]) { continue; }
            _closed[current.x, current.y] = true;

            if (goal.HasValue && current == goal.Value) { break; }
            if (_gScore[current.x, current.y] > budget) { continue; }

            foreach (Vector2Int offset in GridDirections.Orthogonal) {
                Vector2Int neighbor = current + offset;
                if (!terrain.Contains(neighbor)) { continue; }
                if (_closed[neighbor.x, neighbor.y]) { continue; }
                if (isBlocked(neighbor)) { continue; }

                int tentativeG = _gScore[current.x, current.y] + terrain.MovementCost(neighbor);
                if (tentativeG >= _gScore[neighbor.x, neighbor.y] || tentativeG > budget) { continue; }

                _gScore[neighbor.x, neighbor.y] = tentativeG;
                _cameFrom[neighbor.x, neighbor.y] = current;
                _hasCameFrom[neighbor.x, neighbor.y] = true;

                int priority = tentativeG + (goal.HasValue ? Heuristic(neighbor, goal.Value) : 0);
                _openHeap.Push(neighbor, priority);
            }
        }
    }

    private static int Heuristic(Vector2Int cellA, Vector2Int cellB) {
        return Mathf.Abs(cellA.x - cellB.x) + Mathf.Abs(cellA.y - cellB.y);
    }

    /// <summary>
    /// Binary min-heap of (cell, priority), fixed capacity, lazy deletion —
    /// a stale duplicate entry is simply skipped when popped (see the
    /// `_closed` check in RunSearch) rather than removed on push. Nested and
    /// private because nothing outside Pathfinder needs a general-purpose
    /// priority queue.
    /// </summary>
    private struct MinHeap
    {
        private readonly Vector2Int[] _cells;
        private readonly int[] _priorities;
        private int _count;

        public int Count => _count;

        public MinHeap(int capacity) {
            _cells = new Vector2Int[capacity];
            _priorities = new int[capacity];
            _count = 0;
        }

        public void Clear() {
            _count = 0;
        }

        public void Push(Vector2Int cell, int priority) {
            int index = _count;
            _cells[index] = cell;
            _priorities[index] = priority;
            _count++;

            while (index > 0) {
                int parentIndex = (index - 1) / 2;
                if (_priorities[parentIndex] <= _priorities[index]) { break; }
                Swap(parentIndex, index);
                index = parentIndex;
            }
        }

        public Vector2Int Pop() {
            Vector2Int root = _cells[0];
            _count--;
            _cells[0] = _cells[_count];
            _priorities[0] = _priorities[_count];

            int index = 0;
            while (true) {
                int leftIndex = index * 2 + 1;
                int rightIndex = index * 2 + 2;
                int smallestIndex = index;

                if (leftIndex < _count && _priorities[leftIndex] < _priorities[smallestIndex]) { smallestIndex = leftIndex; }
                if (rightIndex < _count && _priorities[rightIndex] < _priorities[smallestIndex]) { smallestIndex = rightIndex; }
                if (smallestIndex == index) { break; }

                Swap(index, smallestIndex);
                index = smallestIndex;
            }

            return root;
        }

        private void Swap(int indexA, int indexB) {
            Vector2Int cellTemp = _cells[indexA];
            _cells[indexA] = _cells[indexB];
            _cells[indexB] = cellTemp;

            int priorityTemp = _priorities[indexA];
            _priorities[indexA] = _priorities[indexB];
            _priorities[indexB] = priorityTemp;
        }
    }
}
