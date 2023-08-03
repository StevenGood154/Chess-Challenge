using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;

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

public class MyBot : IChessBot
{
    private readonly int[] _pieceValues = { 0, 100, 320, 300, 500, 900, 50000 };

    private readonly int _bigNumber = 1000000000;

    private bool _isWhite;

    private Node? _root;

    private Dictionary<ulong, int> transpositionTable = new Dictionary<ulong, int>();

    private Dictionary<int, (int, int)> depthTracker = new Dictionary<int, (int, int)>(); // #DEBUG

    public Move Think(Board board, Timer timer)
    {
        Console.WriteLine($"Starting Evaluation: {EvaluatePosition(board)}"); // DEBUG

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
            Search(board, _root, depth, -_bigNumber, _bigNumber);
            depthTimer.Add(timer.MillisecondsElapsedThisTurn);
        }

        // ---------- START DEBUG -------------
        //var d = DebugGetDepth(_root);  // #DEBUG
        //if (!depthTracker.ContainsKey(d)) // #DEBUG
        //{ // #DEBUG
        //    depthTracker[d] = (1, timer.MillisecondsElapsedThisTurn); // #DEBUG
        //} // #DEBUG
        //else // #DEBUG
        //{ // #DEBUG
        //    depthTracker[d] = (depthTracker[d].Item1 + 1, depthTracker[d].Item2 + timer.MillisecondsElapsedThisTurn); // #DEBUG
        //} // #DEBUG

        //Console.Write(timer.MillisecondsElapsedThisTurn); // #DEBUG
        //Console.Write(" ms");  // #DEBUG
        //Console.WriteLine(); // #DEBUG
        //foreach (var key in depthTracker.Keys) // #DEBUG
        //{ // #DEBUG
        //    Console.Write(key); // #DEBUG
        //    Console.Write(": "); // #DEBUG
        //    Console.Write(depthTracker[key].Item2 / depthTracker[key].Item1); // #DEBUG
        //    Console.Write(" ms / move"); // #DEBUG
        //    Console.WriteLine(); // #DEBUG
        //} // #DEBUG
        //Console.WriteLine(); // #DEBUG
        // ---------- END DEBUG -------------

        Console.WriteLine($"BestMove Evaluation: {_root.moveStrength}"); // #DEBUG
        Console.WriteLine(); // #DEBUG

        var ourMove = _root.bestMove;
        _root = ourMove.node;
        return ourMove.move;
    }

    private void Search(Board board, Node node, int depth, int alpha, int beta)
    {
        var zobristKey = board.ZobristKey;
        if (depth == 0)
        {
            if (transpositionTable.ContainsKey(zobristKey))
            {
                node.moveStrength = board.IsRepeatedPosition() ? 0 : transpositionTable[zobristKey];
                return;
            }

            var evaluation = EvaluatePosition(board);
            node.moveStrength = evaluation;
            transpositionTable[zobristKey] = evaluation;
            return;
        }

        if (node.edges == null)
        {
            var legalMoves = board.GetLegalMoves();
            Array.Sort(legalMoves, (Move a, Move b) =>
            {
                //var aVal = 0.5 * (Math.Abs(4.5 - a.StartSquare.Rank) + Math.Abs(4.5 - a.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - a.TargetSquare.Rank) + Math.Abs(4.5 - a.TargetSquare.File)) - _pieceValues[(int)a.CapturePieceType];
                //var bVal = 0.5 * (Math.Abs(4.5 - b.StartSquare.Rank) + Math.Abs(4.5 - b.StartSquare.File)) + 0.5 * (Math.Abs(4.5 - b.TargetSquare.Rank) + Math.Abs(4.5 - b.TargetSquare.File)) - _pieceValues[(int)b.CapturePieceType];

                //var aVal = _pieceValues[(int)a.CapturePieceType] - _pieceValues[(int)a.MovePieceType];
                //var bVal = _pieceValues[(int)b.CapturePieceType] - _pieceValues[(int)b.MovePieceType];

                return _pieceValues[(int)a.CapturePieceType] - _pieceValues[(int)a.MovePieceType] - _pieceValues[(int)b.CapturePieceType] + _pieceValues[(int)b.MovePieceType];
            });

            node.edges = legalMoves.Select(move => new Edge(move, new Node(!board.IsWhiteToMove ? -_bigNumber : _bigNumber))).ToList();
        }

        if (node.edges.Count == 0)
        {
            var evaluation = EvaluatePosition(board);
            node.moveStrength = evaluation;
            transpositionTable[zobristKey] = evaluation;
            return;
        }

        node.moveStrength = board.IsWhiteToMove ? -_bigNumber : _bigNumber;

        if (!board.IsWhiteToMove) node.edges.Reverse(); // slow

        foreach (var edge in node.edges)
        {
            board.MakeMove(edge.move);
            Search(board, edge.node, depth - 1, alpha, beta);
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

        node.bestMove ??= node.edges.First();
        node.edges.Sort();
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
                    pieceValue += isWhite ? rank * rank : 49 - 14 * rank + rank * rank;
                    var pawnDirection = isWhite ? 1 : -1;
                    if (board.GetPiece(new Square(Math.Max(0, file - 1), rank - pawnDirection)).IsPawn ||
                        board.GetPiece(new Square(Math.Min(7, file + 1), rank - pawnDirection)).IsPawn)
                    {
                        pieceValue += 15;
                    }
                    break;
                case PieceType.Knight or PieceType.Queen:
                    pieceValue += 50 - 10 * (int)Distance(3.5, 3.5, file, rank); // TODO: Rewrite to use distance function
                    break;
                case PieceType.Bishop:
                    pieceValue += 15 * (2 - Math.Min(Math.Abs(rank - file), Math.Abs(rank + file)));
                    break;
                case PieceType.Rook:
                    break;
                default: // PieceType.King
                    var earlyGameKingEval = 30 - 15 * (int)Math.Min(Distance(1, isWhite ? 0 : 7, file, rank), Distance(6, isWhite ? 0 : 7, file, rank));
                    pieceValue += earlyGameKingEval;
                    break;
            }
            evaluation += isWhite ? pieceValue : -pieceValue;
        }
        return evaluation;
    }

    double Distance(double x1, double y1, int x2, int y2) // TODO: Investigate static
    {
        return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
    }

    // ---------- START DEBUG -------------

    void DebugRunEvaluateSpeedTest(Board board, int numberOfRuns) // #DEBUG
    { // #DEBUG
        Stopwatch sw = Stopwatch.StartNew(); // #DEBUG
        for (int i = 0; i < numberOfRuns; i++) {  // #DEBUG
            EvaluatePosition(board); // #DEBUG
        } // #DEBUG
        sw.Stop(); // #DEBUG
        Console.WriteLine($"'Evaluate' ran {numberOfRuns} times in {(double)sw.ElapsedMilliseconds / 1000} s"); // #DEBUG
    } // #DEBUG

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

    // ---------- END DEBUG -------------
}