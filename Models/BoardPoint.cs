namespace Backgammon.Backend.Models;

// Represents one of the 24 points (triangles) on the board
public class BoardPoint
{
    public int PointIndex { get; init; } // 1-24
    public List<Checker> Checkers { get; set; } = new List<Checker>();
}