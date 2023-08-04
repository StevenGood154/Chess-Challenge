using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

//using static ChessChallenge.Application.ConsoleHelper;

public class MyBot : IChessBot
{
    private readonly int[] _pieceValues = { 0, 100, 320, 300, 500, 900, 50000 };

    private readonly int _bigNumber = 1000000000;

    //private bool _isWhite;

    private Move _bestMove = Move.NullMove;

    private Dictionary<ulong, int> transpositionTable = new Dictionary<ulong, int>();

    private Dictionary<int, (int, int)> depthTracker = new Dictionary<int, (int, int)>(); // #DEBUG

    private int _positionsSearched = 0; // #DEBUG

    private int _tableLookups = 0; // #DEBUG

    public Move Think(Board board, Timer timer)
    {
        //Console.WriteLine($"Starting Evaluation: {EvaluatePosition(board)}"); // DEBUG

        //var depthTimer = new List<int>();

        var depth = 4;
        Search(board, depth, -_bigNumber, _bigNumber, true);

        //for (var depth = 3; ; depth++)
        //{
        //    if (depthTimer.Count >= 2 && depthTimer[depthTimer.Count - 2] > 0)
        //    {
        //        var branchingFactor = depthTimer[depthTimer.Count - 1] / depthTimer[depthTimer.Count - 2];
        //        var nextLength = 2 * depthTimer[depthTimer.Count - 1] * branchingFactor;
        //        if (nextLength > 500)
        //            break;
                    
        //    }
        //    Search(board, _root, depth, -_bigNumber, _bigNumber);
        //    depthTimer.Add(timer.MillisecondsElapsedThisTurn);
        //}

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

        //Console.WriteLine($"BestMove Evaluation: {_root.moveStrength}"); // #DEBUG

        Console.WriteLine($"depth: {depth} in {timer.MillisecondsElapsedThisTurn}");
        Console.WriteLine($"{_positionsSearched} positions searched"); // #DEBUG
        Console.WriteLine($"{_tableLookups} table lookups"); // #DEBUG
        Console.WriteLine(); // #DEBUG

        // ---------- END DEBUG -------------

        //var ourMove = _root.bestMove;
        //_root = ourMove.node;
        //return ourMove.move;
        return _bestMove;
    }

    private int Search(Board board, int depth, int alpha, int beta, bool isTopLevelCall = false)
    {
        var zobristKey = board.ZobristKey;
        if (depth == 0)
        {
            _positionsSearched++; // #DEBUG
            if (transpositionTable.ContainsKey(zobristKey)) // TODO: Use TryGetValue
            {
                _tableLookups++; // #DEBUG
                return board.IsRepeatedPosition() ? 0 : transpositionTable[zobristKey];
            }

            var evaluation = EvaluatePosition(board);
            transpositionTable[zobristKey] = evaluation;
            return evaluation;
        }

        var legalMoves = board.GetLegalMoves(); // TODO: Use GetLegalMovesNonAlloc
        Array.Sort(legalMoves, (a, b) => _pieceValues[(int)a.CapturePieceType] - _pieceValues[(int)a.MovePieceType] - _pieceValues[(int)b.CapturePieceType] + _pieceValues[(int)b.MovePieceType]);

        if (legalMoves.Length == 0)
        {
            _positionsSearched++; // #DEBUG
            var evaluation = EvaluatePosition(board);
            transpositionTable[zobristKey] = evaluation;
            return evaluation;
        }

        var strengthOfBoardPosition = board.IsWhiteToMove ? -_bigNumber : _bigNumber;

        //if (!board.IsWhiteToMove) node.edges.Reverse(); // slow

        foreach (var moveUnderTest in legalMoves)
        {
            board.MakeMove(moveUnderTest);
            var strengthOfMoveUnderTest = Search(board,  depth - 1, alpha, beta);
            board.UndoMove(moveUnderTest);

            if (board.IsWhiteToMove)
            {
                //node.moveStrength = Math.Max(node.moveStrength, edge.node.moveStrength);
                if (strengthOfMoveUnderTest > strengthOfBoardPosition)
                {
                    strengthOfBoardPosition = strengthOfMoveUnderTest;
                    if (isTopLevelCall) _bestMove = moveUnderTest;
                }

                alpha = Math.Max(alpha, strengthOfBoardPosition);
                if (strengthOfBoardPosition >= beta)
                    break;
            }
            else
            {
                //node.moveStrength = Math.Min(node.moveStrength, edge.node.moveStrength);
                if (strengthOfMoveUnderTest < strengthOfBoardPosition)
                {
                    strengthOfBoardPosition = strengthOfMoveUnderTest;
                    if (isTopLevelCall) _bestMove = moveUnderTest;
                }

                beta = Math.Min(beta, strengthOfBoardPosition);
                if (strengthOfBoardPosition <= alpha)
                    break;
            }

            //if (beta <= alpha)
            //    break;
        }

        return strengthOfBoardPosition;
        //node.bestMove ??= node.edges.First();
        //node.edges.Sort();
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

    //void DebugPrintBestLine(Node node) // #DEBUG
    //{ // #DEBUG
    //    Console.WriteLine($"Evaluated Strength: {node.moveStrength}"); // #DEBUG
  
    //    Console.Write("Best Line: "); // #DEBUG

    //    var bestMove = node.bestMove; // #DEBUG
    //    while (bestMove != null) // #DEBUG
    //    { // #DEBUG
    //        Console.Write(" "); // #DEBUG
    //        Console.Write(bestMove.move.ToString().Substring(7, 4)); // #DEBUG
    //        bestMove = bestMove.node.bestMove;// #DEBUG
    //    } // #DEBUG
    //    Console.Write("\n"); // #DEBUG
    //} // #DEBUG

    //int DebugGetDepth(Node node) // #DEBUG
    //{ // #DEBUG
    //    var depth = 0; // #DEBUG
    //    var bestMove = node.bestMove; // #DEBUG
    //    while (bestMove != null) // #DEBUG
    //    { // #DEBUG
    //        depth++; // #DEBUG
    //        bestMove = bestMove.node.bestMove;// #DEBUG
    //    } // #DEBUG

    //    return depth; //#DEBUG
    //} // #DEBUG

    // ---------- END DEBUG -------------
}