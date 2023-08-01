using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

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
        return other.node.moveStrength.CompareTo(node.moveStrength);
    }
}

public class Node
{
    public Node(int moveStrength)
    {
        this.moveStrength = moveStrength;
    }

    public Edge? bestMove { get; set; }

    public int moveStrength { get; set; }

    public List<Edge>? edges { get; set; }
}

public class TranspositionTable
{
    public Dictionary<ulong, (int, int)> table = new Dictionary<ulong, (int, int)>();
    public int? MoveStrength(ulong zobristKey, int depth)
    {
        if (table.ContainsKey(zobristKey))
        {
            var tuple = table[zobristKey];

            if (depth <= tuple.Item1)
                return tuple.Item2;
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

    private readonly int _bigNumber = 1000000000;

    private bool _isWhite;

    private Node? _root;

    private TranspositionTable tTable = new();

    private Dictionary<int, (int, int)> depthTracker = new Dictionary<int, (int, int)>(); // #DEBUG

    public Move Think(Board board, Timer timer)
    {
        if (_root == null)
        {
            _isWhite = board.IsWhiteToMove;
            _root = new Node(_isWhite ? -_bigNumber : _bigNumber);
        }
        else
        {
            var lastMove = board.GameMoveHistory[^1];
            var chosenEdge = _root.edges?.Where(edge => edge.move.Equals(lastMove))?.First();
            if (chosenEdge != null)
                _root = chosenEdge.node;
            else
                _root = new Node(_isWhite ? -_bigNumber : _bigNumber);
        }

        var depthTimer = new List<int>();

        for (var depth = 3; ; depth++)
        {
            if (depthTimer.Count >= 2 && depthTimer[depthTimer.Count - 2] > 0)
            {
                var branchingFactor = depthTimer[depthTimer.Count - 1] / depthTimer[depthTimer.Count - 2];
                var nextLength = 2 * depthTimer[depthTimer.Count - 1] * branchingFactor;
                if (nextLength > 500)
                    break;
                    
            }
            Search(_root, depth, -_bigNumber, _bigNumber, board);
            depthTimer.Add(timer.MillisecondsElapsedThisTurn);
        }

        var d = DebugGetDepth(_root);
        if (!depthTracker.ContainsKey(d))
        {
            depthTracker[d] = (1, timer.MillisecondsElapsedThisTurn);
        }
        else
        {
            depthTracker[d] = (depthTracker[d].Item1 + 1, depthTracker[d].Item2 + timer.MillisecondsElapsedThisTurn);
        }

        Console.Write(timer.MillisecondsElapsedThisTurn);
        Console.Write(" ms"); 
        Console.WriteLine();
        foreach (var key in depthTracker.Keys)
        {
            Console.Write(key);
            Console.Write(": ");
            Console.Write(depthTracker[key].Item2 / depthTracker[key].Item1);
            Console.Write(" ms / move");
            Console.WriteLine();
        }
        Console.WriteLine();

        var ourMove = _root.bestMove;
        _root = ourMove.node;
        return ourMove.move;
    }

    private void Search(Node node, int depth, int alpha, int beta, Board board)
    {
        //var tableResult = tTable.MoveStrength(board.ZobristKey, depth);
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
                //var aVal = 0.5 * (Math.Abs(4.5 - a.StartSquare.Rank) + Math.Abs(4.5 - a.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - a.TargetSquare.Rank) + Math.Abs(4.5 - a.TargetSquare.File)) - _pieceValues[(int)a.CapturePieceType];
                //var bVal = 0.5 * (Math.Abs(4.5 - b.StartSquare.Rank) + Math.Abs(4.5 - b.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - b.TargetSquare.Rank) + Math.Abs(4.5 - b.TargetSquare.File)) - _pieceValues[(int)b.CapturePieceType];

                var aVal = _pieceValues[(int)a.CapturePieceType] - _pieceValues[(int)a.MovePieceType];
                var bVal = _pieceValues[(int)b.CapturePieceType] - _pieceValues[(int)b.MovePieceType];


                return (int)(aVal - bVal);
            });

            node.edges = legalMoves.Select(move => new Edge(move, new Node(!board.IsWhiteToMove ? -_bigNumber : _bigNumber))).ToList();
        }

        if (node.edges.Count == 0)
        {
            node.moveStrength = EvaluatePosition(board);
            return;
        }

        node.moveStrength = board.IsWhiteToMove ? -_bigNumber : _bigNumber;

        if (!board.IsWhiteToMove) node.edges.Reverse(); // slow

        foreach (var edge in node.edges)
        {
            board.MakeMove(edge.move);
            Search(edge.node, depth - 1, alpha, beta, board);
            board.UndoMove(edge.move);

            var edgeStrength = edge.node.moveStrength;
            if (board.IsWhiteToMove)
            {
                //node.moveStrength = Math.Max(node.moveStrength, edge.node.moveStrength);
                if (edgeStrength > node.moveStrength)
                {
                    node.moveStrength = edgeStrength;
                    node.bestMove = edge;
                }

                alpha = Math.Max(alpha, node.moveStrength);
                if (node.moveStrength >= beta)
                    break;
            }
            else
            {
                //node.moveStrength = Math.Min(node.moveStrength, edge.node.moveStrength);
                if (edgeStrength < node.moveStrength)
                {
                    node.moveStrength = edgeStrength;
                    node.bestMove = edge;
                }

                beta = Math.Min(beta, node.moveStrength);
                if (node.moveStrength <= alpha)
                    break;
            }

            //if (beta <= alpha)
            //    break;
        }

        //tTable.LogBoard(board.ZobristKey, depth, node.moveStrength);
        node.edges.Sort();
        //node.moveStrength = board.IsWhiteToMove ? node.edges.First().node.moveStrength : node.edges.Last().node.moveStrength;
    }

    int EvaluatePosition(Board board)
    {
        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? -_bigNumber : _bigNumber;
        }

        if (board.IsDraw())
        {
            return 0;
        }

        var evaluation = 0;
        var bitboard = board.AllPiecesBitboard;
        while (bitboard > 0)
        {
            var index = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard);
            var square = new Square(index);
            var piece = board.GetPiece(square);

            var rank = square.Rank;
            var file = square.File;

            var isWhite = piece.IsWhite;

            var pieceValue = _pieceValues[(int)piece.PieceType];

            switch (piece.PieceType)
            {
                case PieceType.Pawn:
                    pieceValue += isWhite ? rank * rank : 49 - 2 * rank + rank * rank - 10;

                    if (board.GetPiece(new Square(Math.Max(0, rank - 1), rank - 1)).IsPawn ||
                        board.GetPiece(new Square(Math.Min(7, rank + 1), rank - 1)).IsPawn)
                    {
                        pieceValue += 15;
                    }
                    break;
                case PieceType.Knight or PieceType.Queen:
                    pieceValue += 50 - 10 * (int)Math.Sqrt((rank * rank) + (file * file));
                    break;
                case PieceType.Bishop:
                    pieceValue += 15 * (2 - Math.Min(Math.Abs(rank - file), Math.Abs(rank + file)));
                    break;
                case PieceType.Rook:
                    var friendlyPawnBitboard = board.GetPieceBitboard(PieceType.Pawn, isWhite);
                    var opponentPawnBitboard = board.GetPieceBitboard(PieceType.Pawn, !isWhite);

                    var rookAttacksBitboard =
                        BitboardHelper.GetSliderAttacks(PieceType.Rook, square, board);

                    for (int i = 0; i < 8; i++)
                    {
                        BitboardHelper.ClearSquare(ref rookAttacksBitboard, new Square(i, rank));
                    }

                    var attackedFriendlyPawnOnFileBitboard = friendlyPawnBitboard & rookAttacksBitboard;
                    var attackedOpponentsPawnOnFileBitboard = opponentPawnBitboard & rookAttacksBitboard;

                    if (attackedOpponentsPawnOnFileBitboard > 0)
                    {
                        pieceValue += 50;
                        if (attackedFriendlyPawnOnFileBitboard == 0)
                        {
                            pieceValue += 40;
                        }
                    }

                    break;
                default: // PieceType.King
                    break;
            }
            evaluation += isWhite ? pieceValue : -pieceValue;
        }
        return evaluation;
    }

    void DebugPrintBestLine(Node node) // #DEBUG
    { // #DEBUG
        Console.WriteLine($"Evaluated Strength: {node.moveStrength}"); // #DEBUG
  
        Console.Write("Best Line: "); // #DEBUG

        var bestMove = node.bestMove; // #DEBUG
        while (bestMove != null) // #DEBUG
        { // #DEBUG
            Console.Write(" "); // #DEBUG
            Console.Write(bestMove.move.ToString().Substring(7, 4)); // #DEBUG
            bestMove = bestMove.node.bestMove;// #DEBUG
        } // #DEBUG
        Console.Write("\n"); // #DEBUG
    } // #DEBUG

    int DebugGetDepth(Node node) // #DEBUG
    { // #DEBUG
        var depth = 0; // #DEBUG
        var bestMove = node.bestMove; // #DEBUG
        while (bestMove != null) // #DEBUG
        { // #DEBUG
            depth++; // #DEBUG
            bestMove = bestMove.node.bestMove;// #DEBUG
        } // #DEBUG

        return depth; //#DEBUG
    } // #DEBUG
}