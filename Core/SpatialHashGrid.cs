using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

// A generic spatial hash grid supporting toroidal wrap-around.
public class SpatialHashGrid<T>
{
    private readonly Dictionary<(int, int), List<T>> _cells = new();
    private readonly float _cellSize;
    private readonly int _cellsX;
    private readonly int _cellsY;
    private readonly Func<T, Vector2> _getPosition;
    private readonly float _worldHeight;
    private readonly float _worldWidth;

    public SpatialHashGrid(float cellSize, Func<T, Vector2> getPosition, float worldWidth, float worldHeight)
    {
        _cellSize = cellSize;
        _getPosition = getPosition;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _cellsX = (int)Math.Ceiling(worldWidth / cellSize);
        _cellsY = (int)Math.Ceiling(worldHeight / cellSize);
    }

    public void Clear()
    {
        _cells.Clear();
    }

    public void Insert(T item)
    {
        var pos = _getPosition(item);
        var cell = GetCellCoordinate(pos);
        if (!_cells.ContainsKey(cell)) _cells[cell] = new List<T>();
        _cells[cell].Add(item);
    }

    public void Rebuild(IEnumerable<T> items)
    {
        Clear();
        foreach (var item in items) Insert(item);
    }

    private (int, int) GetCellCoordinate(Vector2 pos)
    {
        var cellX = (int)Math.Floor(pos.X / _cellSize);
        var cellY = (int)Math.Floor(pos.Y / _cellSize);
        cellX = (cellX % _cellsX + _cellsX) % _cellsX;
        cellY = (cellY % _cellsY + _cellsY) % _cellsY;
        return (cellX, cellY);
    }

    public List<T> Query(Vector2 position, float radius)
    {
        List<T> results = new();

        var minCellX = (int)Math.Floor((position.X - radius) / _cellSize);
        var minCellY = (int)Math.Floor((position.Y - radius) / _cellSize);
        var maxCellX = (int)Math.Floor((position.X + radius) / _cellSize);
        var maxCellY = (int)Math.Floor((position.Y + radius) / _cellSize);

        var xs = GetIndices(minCellX, maxCellX, _cellsX);
        var ys = GetIndices(minCellY, maxCellY, _cellsY);

        foreach (var x in xs)
        foreach (var y in ys)
            if (_cells.TryGetValue((x, y), out var cellItems))
                results.AddRange(cellItems);

        return results;
    }

    private IEnumerable<int> GetIndices(int minIndex, int maxIndex, int cellCount)
    {
        minIndex = (minIndex % cellCount + cellCount) % cellCount;
        maxIndex = (maxIndex % cellCount + cellCount) % cellCount;

        return minIndex <= maxIndex
            ? Enumerable.Range(minIndex, maxIndex - minIndex + 1)
            : Enumerable.Range(minIndex, cellCount - minIndex)
                .Concat(Enumerable.Range(0, maxIndex + 1));
    }
}