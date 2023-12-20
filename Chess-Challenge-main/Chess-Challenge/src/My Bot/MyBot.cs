using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    // Current color (White or Black)
    public bool areWeWhite;
    public int extenDepth = 0;

    Move previousMove;

    readonly public static int hashSize = 3_800_000;
    readonly Dictionary<int, Hashentry> tttable = new Dictionary< int, Hashentry>(hashSize);


    // Transposition Table Hash Entry
    private class Hashentry
    {
        public ulong zobrist;
        public int depth;
        public int flag; // 0 = exact, 1 = beta eval, 2 = alpha eval
        public double eval;
        public int ancient;

        public Hashentry(ulong zobrist, int depth, int flag,
                         double eval, int ancient)
        {
            this.zobrist = zobrist;
            this.depth = depth;
            this.flag = flag;
            this.eval = eval;
            this.ancient = ancient;
        }
    }

    public static readonly int[] baseBoard =
    {
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    public int[] pawnboard;
    public int[] knightboard;
    public int[] queenboard;
    public int[] rookboard;
    public int[] bishopboard;

    // Starting depth for bot
    public int baseDepth = 0;

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        areWeWhite = board.IsWhiteToMove;

        Square opponentKing = board.GetKingSquare(!areWeWhite);

        //tttable.Clear();

        pawnboard = getNewValues(PieceType.Pawn, opponentKing.File, opponentKing.Rank);
        knightboard = getNewValues(PieceType.Knight, opponentKing.File, opponentKing.Rank);
        queenboard = getNewValues(PieceType.Queen, opponentKing.File, opponentKing.Rank);
        rookboard = getNewValues(PieceType.Rook, opponentKing.File, opponentKing.Rank);
        bishopboard = getNewValues(PieceType.Bishop, opponentKing.File, opponentKing.Rank);

        //for (int i = 0; i < 64; i++)
        //{
        //    if ((((board.GetPieceBitboard(PieceType.Rook, true)) >> i) & 0x1) == 1)
        //    {
        //        if (areWeWhite)
        //        {
        //            Console.WriteLine(rookboard[63 - i]);
        //        } else
        //        {
        //            Console.WriteLine(rookboard[i]);
        //        }
        //    }
        //}

        Console.WriteLine(tttable.Count());

        //Console.WriteLine(new BitArray(board.GetPieceBitboard(PieceType.Pawn, true)));


        //Console.WriteLine(getBoardVal(board, areWeWhite));

        // Console.WriteLine(getBoardVal(board, board.IsWhiteToMove));
        //Console.WriteLine("Pawn Index 24: " + getPieceValue(PieceType.Pawn, new Square(24), false));
        for (int i = 0; i < knightboard.Length; i++)
        {
            Console.Write($" ==> {knightboard[i]}");
            if ((i + 1) % 8 == 0)
            {
                Console.WriteLine("");
            }
        }

        Move finalMove = GetMove(moves, board, 0, areWeWhite, timer);
        previousMove = finalMove;
        return finalMove;
    }

    // Gets the depth that the it looks at
    private void getDepth(Timer timer)
    {
        baseDepth = 1500 / (timer.MillisecondsElapsedThisTurn + 1);
    }

    private Move GetMove(Move[] moves, Board board, int depth, bool color, Timer timer)
    {
        Move bestMove = moves[0];
        moves = preSort(board, board.GetLegalMoves(), color);
        double bestEval = 0;

        Dictionary<Move, double> sortedMoves = moves.ToDictionary(item => item,item => 0.0);

        //tttable.Clear();

        for (int i = 0; i <= baseDepth; i++)
        {
            //Console.WriteLine("Depth: " + i);
            //tttable.Clear();

            bestEval = Double.MinValue;

            foreach (KeyValuePair<Move, double> move in sortedMoves)
            {
                // Gets the base boards
                Move currMove = move.Key;
                board.MakeMove(currMove);
                double newEval = -getDeepEval(board, baseDepth - i, -100000, 100000, !color, timer, 0);
                //Console.WriteLine("Eval: " + newEval);
                sortedMoves[currMove] =  newEval;
                if (newEval > bestEval)
                {
                    bestMove = currMove;
                    bestEval = newEval;
                }

                board.UndoMove(currMove);
            }
            getDepth(timer);
            var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
            sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

            Console.WriteLine("Depth: " + i);
            //Console.WriteLine(tttable.Count);
            // Print out Moves
            //foreach (KeyValuePair<Move, double> kvp in sortedMoves)
            //{
            //    Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
            //}
            //Console.WriteLine(sortedMoves.ToArray());
        }
        //var sortedDict = from entry in sortedMoves orderby entry.Value ascending select entry;
        //sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

        Console.WriteLine("Best Eval: " + bestEval);
        return bestMove;
    }

    // q-search
    private double qSearch(double alpha, double beta, Board board, bool color, int maxq)
    {
        // Evaluate position
        double eval = getBoardVal(board, color);

        // Hopefully this will reduce blundering draws in completely winning positions
        if (board.IsDraw())
        {
            return 0;
        }
        else if (board.IsInCheckmate())
        {
            return -100000;
        }

        // Fail beta cuttoff
        if (eval >= beta)
        {
            return beta;
        }

        // alpha increase
        if (eval > alpha)
        {
            alpha = eval;
        }

        // Generate Movelist
        Move[] moves = preSort(board, board.GetLegalMoves(), color);

        for (int i = 0; i < moves.Length; i++)
        {
            //int keepGoing = 0;
            if ((moves[i].IsCapture) && maxq < 10)
            {
                board.MakeMove(moves[i]);
                eval = Math.Max(eval, -qSearch(-beta, -alpha, board, !color, maxq + 1));

                board.UndoMove(moves[i]);
                alpha = Math.Max(alpha, eval);
                if (alpha >= beta)
                {
                    // Move was too good
                    break;
                }
            }
            else
            {
                break;
            }

            if (beta <= alpha)
            {
                break;
            }
        }
        return alpha;
    }

    // Minimax algorithm with alpha beta pruning
    private double getDeepEval(Board board, int depth, double alpha, double beta, bool color, Timer timer, int extraDepth)
    {
        // Hopefully this will reduce blundering draws in completely winning positions
        if (board.IsDraw())
        {
            return 0;
        } else if (board.IsInCheckmate())
        {
            return -100000 - depth;
        }

        //tttable.Clear();
        double aOrig = alpha;
        // Check the Trasposition table before we move on to calculations
        int entryVal = Convert.ToInt32(board.ZobristKey % Convert.ToUInt64(hashSize));
        if (tttable.ContainsKey(entryVal))
        {
            Hashentry lookup = tttable[entryVal];
            if (lookup.depth >= (baseDepth - depth) && (lookup.ancient - timer.MillisecondsRemaining) < 2500 && (board.ZobristKey == lookup.zobrist))
            {
                if (lookup.flag == 0)
                {
                    return lookup.eval;
                }
                else if (lookup.flag == 1) // Check Later
                {
                    beta = Math.Min(beta, lookup.eval);
                }
                else if (lookup.flag == 2)
                {
                    alpha = Math.Max(alpha, lookup.eval);
                }

                if (alpha >= beta)
                {
                    return lookup.eval;
                }
            }
            else if ((lookup.ancient - timer.MillisecondsRemaining) > 2500)
            {
                tttable.Remove(entryVal);
            }
        }

        if ((depth > baseDepth) || extraDepth > 8)
        {
            return qSearch(alpha, beta, board, color, 0);
            //return getBoardVal(board, color);
        }

        double eval;

        Move[] moves = preSort(board, board.GetLegalMoves(), color);
        //Move[] moves = board.GetLegalMoves();

        
        // Negamax 
        for (int i = 0; i < moves.Length; i++)
        {
            int keepGoing = 0;
            board.MakeMove(moves[i]);
            // Gets a board we can return to
            int[] pawnBaseBoard = (int[])pawnboard.Clone();
            int[] knightBaseBoard = (int[])knightboard.Clone();
            int[] queenBaseBoard = (int[])queenboard.Clone();
            int[] rookBaseBoard = (int[])rookboard.Clone();
            int[] bishopBaseBoard = (int[])bishopboard.Clone();
            if (moves[i].TargetSquare.Equals(board.GetKingSquare(color)))
            {
                pawnboard = getNewValues(PieceType.Pawn, moves[i].TargetSquare.File, moves[i].TargetSquare.File);
                knightboard = getNewValues(PieceType.Knight, moves[i].TargetSquare.File, moves[i].TargetSquare.File);
                queenboard = getNewValues(PieceType.Queen, moves[i].TargetSquare.File, moves[i].TargetSquare.File);
                rookboard = getNewValues(PieceType.Rook, moves[i].TargetSquare.File, moves[i].TargetSquare.File);
                bishopboard = getNewValues(PieceType.Bishop, moves[i].TargetSquare.File, moves[i].TargetSquare.File);
            }
            if ((board.IsInCheck() || moves[i].IsPromotion))
            {
                keepGoing = 1;
            }
            //Console.WriteLine("Eval - " + bestEval + ", White? " + color);
            if (i == 0)
            {
                eval = -getDeepEval(board, depth + 1 - keepGoing, -beta, -alpha, !color, timer, extraDepth + keepGoing);
            }
            else
            {
                eval = -getDeepEval(board, depth + 1 - keepGoing, -alpha - 1, -alpha, !color, timer, extraDepth + keepGoing);
                if (alpha < eval && eval < beta)
                {
                    eval = -getDeepEval(board, depth + 1 - keepGoing, -beta, -alpha, !color, timer, extraDepth + keepGoing);
                }
            }
            //eval = Math.Max(eval, -getDeepEval(board, depth + 1 - keepGoing, -beta, -alpha, !color, timer, extraDepth + keepGoing));
            //Console.WriteLine("Eval - " + bestEval + ", White? " + color);
            pawnboard = (int[])pawnBaseBoard.Clone();
            knightboard = (int[])knightBaseBoard.Clone();
            queenboard = (int[])queenBaseBoard.Clone();
            rookboard = (int[])rookBaseBoard.Clone();
            bishopboard = (int[])rookBaseBoard.Clone();
            board.UndoMove(moves[i]);

            alpha = Math.Max(alpha, eval);

            if (alpha >= beta)
            {
                // Move was too good
                //tttable[entryVal] = new Hashentry(board.ZobristKey, (baseDepth - depth), 1, alpha, timer.MillisecondsRemaining);
                break;
            }
        }

        // Store Value to Trasposition Table
        //int entryVal = Convert.ToInt32(board.ZobristKey % Convert.ToUInt64(hashSize));
        if (!tttable.ContainsKey(entryVal) || tttable[entryVal].depth < (baseDepth - depth))
        {
            //tttable[entryVal] = new Hashentry(board.ZobristKey, depth, 0, alpha, timer.MillisecondsRemaining);
            int flagNum = 0;
            if (alpha <= aOrig)
            {
                flagNum = 1;
            }
            else if (alpha >= beta)
            {
                flagNum = 2;
            }

            tttable[entryVal] = new Hashentry(board.ZobristKey, (baseDepth - depth), flagNum, alpha, timer.MillisecondsRemaining);
        }

        return alpha;
    }

    // This function shortens time for alpha-beta
    private Move[] preSort(Board board, Move[] moves, bool color)
    {
        // Sorts moves and orders them by a very rough value
        return moves.OrderBy(move => moveVal(board, move, color)).ToArray();
    }

    // Gets a rough move value for the presort
    private double moveVal(Board board, Move move, bool color)
    {
        double val = 0.0;

        // Orderby sorts negative so we use - to indicate a better move

        if (move.IsCapture || move.IsPromotion)
        {
            //val -= (getPieceValue(move.MovePieceType, move.TargetSquare, color) - getPieceValue(move.CapturePieceType, move.TargetSquare, color)) + 90;
            val -= 9.0;
        }

        //val -= getBoardVal(board, ogColor);

        return val;
    }

    // This is the evaluation function for this bot
    private double getBoardVal(Board board, bool color)
    {
        // Positional values are calculated within material value function
        if (color != true)
        {
            return -materialAdvantage(board, false);
        }
        return materialAdvantage(board, false);
    }

    private double materialAdvantage(Board board, bool totalCount)
    {
        //PieceList[] listOfAllPieces = board.GetAllPieceLists();
        double whiteMaterialNum = 0.0;
        double blackMaterialNum = 0.0;

        ulong wpawns = board.GetPieceBitboard(PieceType.Pawn, true);
        ulong wknights = board.GetPieceBitboard(PieceType.Knight, true);
        ulong wbishops = board.GetPieceBitboard(PieceType.Bishop, true);
        ulong wrooks = board.GetPieceBitboard(PieceType.Rook, true);
        ulong wqueens = board.GetPieceBitboard(PieceType.Queen, true);
        ulong bpawns = board.GetPieceBitboard(PieceType.Pawn, false);
        ulong bknights = board.GetPieceBitboard(PieceType.Knight, false);
        ulong bbishops = board.GetPieceBitboard(PieceType.Bishop, false);
        ulong brooks = board.GetPieceBitboard(PieceType.Rook, false);
        ulong bqueens = board.GetPieceBitboard(PieceType.Queen, false);

        for (int i = 0; i < 64; i++)
        {
            if (((wpawns >> i) & 0x1) == 1)
            {
                whiteMaterialNum += pawnboard[63 - i];
            }
            else if (((wrooks >> i) & 0x1) == 1)
            {
                whiteMaterialNum += rookboard[63 - i];
            }
            else if (((wbishops >> i) & 0x1) == 1)
            {
                whiteMaterialNum += bishopboard[63 - i];
            }
            else if (((wknights >> i) & 0x1) == 1)
            {
                whiteMaterialNum += knightboard[63 - i];
            }
            else if (((wqueens >> i) & 0x1) == 1) 
            {
                whiteMaterialNum += queenboard[63 - i];
            }
            else if (((brooks >> i) & 0x1) == 1)
            {
                blackMaterialNum += rookboard[i];
            }
            else if (((bbishops >> i) & 0x1) == 1)
            {
                blackMaterialNum += bishopboard[i];
            }
            else if (((bknights >> i) & 0x1) == 1)
            {
                blackMaterialNum += knightboard[i];
            }
            else if (((bqueens >> i) & 0x1) == 1)
            {
                blackMaterialNum += queenboard[i];
            }
            else if (((bpawns >> i) & 0x1) == 1)
            {
                blackMaterialNum += pawnboard[i];
            }
        }

        // Counts up material advantage
        //foreach (PieceList piecelist in listOfAllPieces)
        //{
        //    if (piecelist != null)
        //    {
        //        if (piecelist.IsWhitePieceList)
        //        {
        //            whiteMaterialNum += countUpPieces(piecelist, true);
        //        }
        //        else
        //        {
        //            blackMaterialNum += countUpPieces(piecelist, false);
        //        }
        //    }
        //}

        //// Just provides a double use for the function
        //if (totalCount)
        //{
        //    return (whiteMaterialNum + blackMaterialNum);
        //}

        // Returns positive if white has a material advantage, negative otherwise
        return (whiteMaterialNum - blackMaterialNum);
    }

    // Evaluates the amount of material advantage each piece gives
    //private double countUpPieces(PieceList myPieceList, bool color)
    //{
    //    double retTotalPieceNum = 0.0;

    //    foreach (Piece piece in myPieceList)
    //    {
    //        retTotalPieceNum += getPieceValue(piece.PieceType, piece.Square, color);
    //    }

    //    return retTotalPieceNum;
    //}

    //// Gets values of the different pieces
    //private double getPieceValue(PieceType pieceType, Square square, bool color)
    //{
    //    int increase;
    //    int index = square.Index;
    //    if (color != true)
    //    {
    //        index = 63 - index;
    //        //Console.WriteLine($"White file: {file}, Whitle rank: {rank}");
    //    }
    //    switch (pieceType)
    //    {
    //        case PieceType.Pawn:
    //            //int[] incBoard = getNewValues(PieceType.Pawn, oppponentKingSquare.File, oppponentKingSquare.Rank);
    //            increase = pawnboard[(63 - index)];
    //            return increase;
    //        //return 10;
    //        case PieceType.Bishop:
    //            return 32.0;
    //        case PieceType.Knight:
    //            increase = knightboard[(63 - index)];
    //            return increase;
    //        case PieceType.Rook:
    //            return 50.0;
    //        case PieceType.Queen:
    //            return 90.0;
    //        default: return 0;

    //    }
    //}

    private int[] getNewValues(PieceType pieceType, int kingFile, int kingRank)
    {
        int[] retboard = (int[])baseBoard.Clone();
        switch (pieceType)
        {
            case PieceType.Pawn:
                for (int file = 0; file < 8; file++)
                {
                    int filediv = Math.Abs(file - (kingFile));
                    if (filediv == 0) { filediv = 1; }
                    for (int rank = 1; rank < 7; rank++)
                    {
                        retboard[(rank * 8 + (7 - file))] = ((Math.Abs(rank - 7) * 2) / filediv) + 10;
                    }
                }
                return retboard;
            case PieceType.Bishop:
                for (int file = 0; file < 8; file++)
                {
                    int fileDiv = ((kingFile - (file + 1)) < 0) ? Math.Abs(kingFile - (file+1)) : Math.Abs(kingFile - (file-1));
                    for (int rank = 0; rank < 8; rank++)
                    {
                        int rankDiv = ((kingRank - (rank + 1)) < 0) ? Math.Abs(kingRank - (rank + 1)) : Math.Abs(kingRank - (rank - 1));
                        int diagonal = (rankDiv == fileDiv) ? 4 : 0;
                        retboard[(rank * 8 + (7 - file))] = 30 + diagonal;
                    }
                }
                return retboard;
            case PieceType.Knight:
                for (int file = 0; file < 8; file++)
                {
                    int filediv = Math.Abs(file - (kingFile));
                    if (filediv == 0) { filediv = 1; }
                    for (int rank = 0; rank < 8; rank++)
                    {
                        int rankdiv = Math.Abs(rank - (kingRank));
                        if (rankdiv == 0) { rankdiv = 0; }
                        retboard[(rank * 8 + (7 - file))] = 36 - (rankdiv + filediv);
                    }
                }
                return retboard;
            case PieceType.Rook:
                for (int file = 0; file < 8; file++) {
                    int isKingFile = (kingFile == file) ? 3 : 0;
                    for (int rank = 0; rank < 8; rank++)
                    {
                        int isKingRank = (kingRank == (7-rank)) ? 4 : 0;
                        retboard[(rank * 8 + (7 - file))] = 50 + isKingFile + isKingRank;
                    }
                }
                return retboard;
            case PieceType.Queen:
                for (int file = 0; file < 8; file++)
                {
                    int filediv = Math.Abs(file - (kingFile));
                    if (filediv == 0) { filediv = 1; }
                    for (int rank = 0; rank < 8; rank++)
                    {
                        int rankdiv = Math.Abs(rank - (kingRank));
                        if (rankdiv == 0) { rankdiv = 0; }
                        retboard[(rank * 8 + (7 - file))] = 96 - (rankdiv + filediv);
                    }
                }
                return retboard;
            default: return null;
        }
    }
}