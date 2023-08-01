using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.Application;


namespace ChessChallenge.Example
{
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

        //public ulong? position { get; set; } = null;
        public List<Edge>? edges { get; set; }
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
                    return tuple.Item2;
            }

            return null;
        }

        public void LogBoard(ulong zobristKey, int depth, int score)
        {
            table[zobristKey] = (depth, score);
        }
    }

    public class EvilBot : IChessBot
    {
        private readonly int[] _pieceValues = { 0, 100, 320, 300, 500, 900, 50000 };

        private readonly int _bigNumber = 1000000000;

        private bool _isWhite;

        private Node? _root;

        private TranspositionTable tTable = new();

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
                    {
                        //Console.WriteLine($"Evaluated Depth: {depth}");
                        break;
                    }
                }
                Search(_root, depth, -_bigNumber, _bigNumber, board);
                depthTimer.Add(timer.MillisecondsElapsedThisTurn);
            }

            //PrintBestLine(_root);
            //Console.WriteLine();

            var ourMove = _root.bestMove;
            _root = ourMove.node;
            return ourMove.move;
        }

        private void Search(Node node, int depth, int alpha, int beta, Board board)
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

                node.edges = legalMoves.Select(move => new Edge(move, new Node(!board.IsWhiteToMove ? -_bigNumber : _bigNumber))).ToList();
            }

            if (node.edges.Count == 0)
            {
                node.moveStrength = EvaluatePosition(board);
                return;
            }

            node.moveStrength = board.IsWhiteToMove ? -_bigNumber : _bigNumber;

            if (!board.IsWhiteToMove) node.edges.Reverse();

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
                        break;
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
                        break;
                }

                //if (beta <= alpha)
                //    break;
            }

            tTable.LogBoard(board.ZobristKey, depth, node.moveStrength);
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
                    var listIsWhite = pieceList.IsWhitePieceList;
                    var pieceListValue = pieceList.Count * _pieceValues[(int)pieceList.TypeOfPieceInList];

                    switch (pieceList.TypeOfPieceInList)

                    {
                        case PieceType.Pawn:
                            foreach (var pawn in pieceList)
                            {
                                var rank = pawn.Square.Rank;
                                pieceListValue += listIsWhite
                                    ? rank * rank
                                    : (7 - rank) * (7 - rank);

                                var file = pawn.Square.Rank;
                                if (board.GetPiece(new Square(Math.Max(0, file - 1), rank - 1)).IsPawn ||
                                    board.GetPiece(new Square(Math.Min(7, file + 1), rank - 1)).IsPawn)
                                {
                                    pieceListValue += 25;
                                }
                            }
                            break;
                        case PieceType.Knight or PieceType.Queen:
                            foreach (var knight in pieceList)
                            {
                                var file = 2 * knight.Square.File - 7;
                                var rank = 2 * knight.Square.Rank - 7;
                                pieceListValue += 50 - 10 * (int)Math.Sqrt((rank * rank) + (file * file));
                            }
                            break;
                        case PieceType.Bishop:
                            foreach (var bishop in pieceList)
                            {
                                pieceListValue += 15 * (2 - Math.Min(Math.Abs(bishop.Square.Rank - bishop.Square.File), Math.Abs(bishop.Square.Rank + bishop.Square.File)));
                            }
                            break;
                        case PieceType.Rook:
                            foreach (var rook in pieceList)
                            {
                                var friendlyPawnBitboard = board.GetPieceBitboard(PieceType.Pawn, listIsWhite);
                                var opponentPawnBitboard = board.GetPieceBitboard(PieceType.Pawn, !listIsWhite);

                                var rookAttacksBitboard =
                                    BitboardHelper.GetSliderAttacks(PieceType.Rook, rook.Square, board);

                                for (int i = 0; i < 8; i++)
                                {
                                    BitboardHelper.ClearSquare(ref rookAttacksBitboard, new Square(i, rook.Square.Rank));
                                }

                                var attackedFriendlyPawnOnFileBitboard = friendlyPawnBitboard & rookAttacksBitboard;
                                var attackedOpponentsPawnOnFileBitboard = opponentPawnBitboard & rookAttacksBitboard;

                                if (attackedOpponentsPawnOnFileBitboard > 0)
                                {
                                    pieceListValue += 50;
                                    if (attackedFriendlyPawnOnFileBitboard == 0)
                                    {
                                        pieceListValue += 40;
                                    }
                                }

                            }
                            break;
                    }

                    if (!pieceList.IsWhitePieceList) pieceListValue *= -1;

                    evaluation += pieceListValue;
                }
            }

            return evaluation;
        }

        void PrintBestLine(Node node)
        {
            Console.WriteLine($"Evaluated Strength: {node.moveStrength}");

            Console.Write("Best Line: ");
           
            var bestMove = node.bestMove;
            while (bestMove != null)
            {
                Console.Write(" ");
                Console.Write(bestMove.move.ToString().Substring(7, 4));
                bestMove = bestMove.node.bestMove;
            }
            Console.Write("\n");
        }
    }
}