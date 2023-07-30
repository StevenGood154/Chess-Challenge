using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.Application;

//using static ChessChallenge.Application.ConsoleHelper;

public class Edge : IComparable<Edge>
{
    public Move move;
    public Node node;

    public Edge(Move move, Node node)
    {
        this.move = move;
        this.node = node;
    }
    public int CompareTo(Edge? other)
    {
        return other.node.moveStrength.CompareTo(this.node.moveStrength);
    }
}

public class Node
{
    public static int nodeCount = 0;
    public Edge? bestMove { get; set; } = null;
    public int moveStrength { get; set; }
    //public ulong? position { get; set; } = null;
    public List<Edge>? edges { get; set; } = null;

    public Node(int moveStrength)
    {
        nodeCount++;
        this.moveStrength = moveStrength;
    }
}

public class TranspositionTable
{
    private Dictionary<ulong, (int, int)> table = new Dictionary<ulong, (int, int)>();
    public int? MoveStrength(ulong zobristKey, int depth)
    {
        if (table.ContainsKey(zobristKey))
        {
            var tuple = table[zobristKey];

            if (depth <= tuple.Item1)
            {
                return tuple.Item2;
            }
        }

        return null;
    }

    public void LogBoard(ulong zobristKey, int depth, int score) 
    {
        table[zobristKey] = (depth, score);
    }
}

public class MyBot : IChessBot
{
    private readonly int[] _pieceValues = { 0, 100, 320, 300, 500, 900, 50000 };

    private Node? _root = null;

    private bool _isWhite = false;

    private int _bigNumber = Int32.MaxValue / 10;

    private int cutOffCounter = 0;

    private TranspositionTable tTable = new TranspositionTable();

    public Move Think(Board board, Timer timer)
    {
        //_root = null;
        if (_root == null)
        {
            _isWhite = board.IsWhiteToMove;
            _root = new Node(_isWhite ? Int32.MinValue : Int32.MaxValue);
        }
        else
        {
            var lastMove = board.GameMoveHistory[^1];
            var chosenEdge = _root.edges?.Where(edge => edge.move.Equals(lastMove))?.First();
            if (chosenEdge != null)
            {
                _root = chosenEdge.node;
            }
            else
            {
                _root = new Node(_isWhite ? Int32.MinValue : Int32.MaxValue);
            }
        }

        var depthTimer = new List<int>();

        for (var depth = 3; ; depth++)
        {
            if (depthTimer.Count >= 2 && depthTimer[depthTimer.Count - 2] > 0)
            {
                var branchingFactor = depthTimer[depthTimer.Count - 1] / depthTimer[depthTimer.Count - 2];
                var nextLength = 2 * depthTimer[depthTimer.Count - 1] * branchingFactor;
                if (nextLength > 500)
                {
                    ConsoleHelper.Log(depth.ToString());
                    break;
                }
            }
            Search(_root, depth, Int32.MinValue, Int32.MaxValue, board);
            depthTimer.Add(timer.MillisecondsElapsedThisTurn);
        }
        var ourMove = _root.bestMove;
        _root = ourMove.node;
        //Console.WriteLine($"Percentage {(float)cutOffCounter/(float)Node.nodeCount}, Depth {depth - 1}");
        return ourMove.move;
    }

    void Search(Node node, int depth, int alpha, int beta, Board board)
    {
        var tableResult = this.tTable.MoveStrength(board.ZobristKey, depth);
        if (depth == 0)
        {
            node.moveStrength = EvaluatePosition(board);
            return;
        }

        if (node.edges == null)
        {
            var legalMoves = board.GetLegalMoves();
            Array.Sort(legalMoves, (Move a, Move b) =>
            {
                var aVal = 0.5 * (Math.Abs(4.5 - a.StartSquare.Rank) + Math.Abs(4.5 - a.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - a.TargetSquare.Rank) + Math.Abs(4.5 - a.TargetSquare.File)) - _pieceValues[(int)a.CapturePieceType];
                var bVal = 0.5 * (Math.Abs(4.5 - b.StartSquare.Rank) + Math.Abs(4.5 - b.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - b.TargetSquare.Rank) + Math.Abs(4.5 - b.TargetSquare.File)) - _pieceValues[(int)b.CapturePieceType];

                return (int)(aVal - bVal);
            });

            node.edges = legalMoves.Select(move => new Edge(move, new Node(!board.IsWhiteToMove ? Int32.MinValue : Int32.MaxValue))).ToList();
        }

        if (node.edges.Count == 0)
        {
            node.moveStrength = EvaluatePosition(board);
            return;
        }

        node.moveStrength = board.IsWhiteToMove ? Int32.MinValue : Int32.MaxValue;

        if (!board.IsWhiteToMove)
        {
            node.edges.Reverse();
        }

        foreach (var edge in node.edges)
        {
            board.MakeMove(edge.move);
            Search(edge.node, depth - 1, alpha, beta, board);
            board.UndoMove(edge.move);

            if (board.IsWhiteToMove)
            {
                //node.moveStrength = Math.Max(node.moveStrength, edge.node.moveStrength);
                if (edge.node.moveStrength > node.moveStrength)
                {
                    node.moveStrength = edge.node.moveStrength;
                    node.bestMove = edge;
                }
                alpha = Math.Max(alpha, node.moveStrength);
                if (node.moveStrength >= beta)
                {
                    cutOffCounter++;
                    break;
                }
            }
            else
            {
                //node.moveStrength = Math.Min(node.moveStrength, edge.node.moveStrength);
                if (edge.node.moveStrength < node.moveStrength)
                {
                    node.moveStrength = edge.node.moveStrength;
                    node.bestMove = edge;
                }
                beta = Math.Min(beta, node.moveStrength);
                if (node.moveStrength <= alpha)
                {
                    cutOffCounter++;
                    break;
                }
            }

            //if (beta <= alpha)
            //    break;
        }

        this.tTable.LogBoard(board.ZobristKey, depth, node.moveStrength);
        node.edges.Sort();
        //node.moveStrength = board.IsWhiteToMove ? node.edges.First().node.moveStrength : node.edges.Last().node.moveStrength;
    }

    int EvaluatePosition(Board board)
    {
        int evaluation = 0;
        if (board.IsInCheckmate())
        {
            evaluation = board.IsWhiteToMove ? -_bigNumber : _bigNumber;
        }
        else if (board.IsDraw())
        {
            evaluation = 0;
        }
        else
        {
            var allPieceLists = board.GetAllPieceLists();

            foreach (var pieceList in allPieceLists)
            {
                int pieceListValue = pieceList.Count * _pieceValues[(int)pieceList.TypeOfPieceInList];

                if (!pieceList.IsWhitePieceList)
                {
                    pieceListValue *= -1;
                }

                evaluation += pieceListValue;
            }
        }

        //Random rng = new();
        //evaluation += rng.Next(100) - 50;

        return evaluation;
    }
}