using ChessChallenge.API;
//using ChessChallenge.Chess;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    // Current color (White or Black)
    public bool areWeWhite;
    public double[] values = new double[] { 5.413136806880654, 2.1062554207659585, 1.7594064333287096, 3.0995119207536392, 1.7565447773116385, 1.5162208881304697, 1.5106999527247158, 2.4920509741604566, 1.7280318656601161, 1.3626138693013292, 7.343030599571313, 1.409198975194804, 2.8196874433754417, 1.5859296690141456, 1.8566970128364382, 1.258812742661132, 7.414893676720045, 2.835985220892348, 1.2597758421952725, 1.8205573306514684, 1.0966818759202406, 1.0274005804804156, 1.1602526161168947, 1.197752028097283, 0.9466290350289219, 0.7224153844744039, 0.922722269512118, 1.2092289038511128, 1.3082461109423293, 1.1898470025927979, 0.9716962806283014 };
    //public double[] values = new double[] { 5.75, 2.0, 1.5, 3.0, 2.0, 2.0, 1.5, 2.0, 1.5, 1, 7.5, 1.3, 2.4, 2.0, 2.0, 1, 7.5, 2.5, 1.5, 1.5, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

    Move previousMove;

    readonly public static int hashSize = 4_800_000;
    readonly Dictionary<int, Hashentry> tttable = new Dictionary< int, Hashentry>(hashSize);

    // This stores the values for the point-value table
    //readonly Dictionary<int, double[]> pvtable = new Dictionary<int, double[]>(hashSize*2); // See if this helps


    private double[] boardState = new double[64];


    // Transposition Table Hash Entry
    private class Hashentry
    {
        public ulong zobrist;
        public int depth;
        public int flag; // 0 = exact, 1 = beta eval, 2 = alpha eval
        public double eval;
        public int ancient;
        public double[] pieceBoardValues = new double[64];
        public bool qSearch;

        public Hashentry(ulong zobrist, int depth, int flag,
                         double eval, int ancient, bool qSearch)
        {
            this.zobrist = zobrist;
            this.depth = depth;
            this.flag = flag;
            this.eval = eval;
            this.ancient = ancient;
            this.qSearch = qSearch;
        }
    }

    public void printBoardValues(bool color)
    {
        for (int i = 0; i < 64; i++)
        {
            double num;
            if (!color)
            {
                num = boardState[63 - (i - ((i) % 8) + (7 - ((i) % 8)))]/100.0;
            }
            else
            {
                num = boardState[(i - ((i) % 8) + (7 - ((i) % 8)))]/100.0;
            }
            if (num >= 0.0)
            {
                Console.Write(" " + string.Format("{0:0.00}", num) + " ");
            }
            else
            {
                Console.Write(string.Format("{0:0.00}", num) + " ");
            }
            if ((i + 1) % 8 == 0)
            {
                Console.Write("\n");
            }
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

    // Starting depth for bot
    public int baseDepth = 4;
    public bool haveNotRandomized = true;

    public Move Think(Board board, Timer timer)
    {
        // Randomize
        //if (board.IsWhiteToMove && (board.GetFenString().Equals(board.GameStartFenString)) && haveNotRandomized) {
        //    Random random = new Random(432143275);
        //    haveNotRandomized = false;
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //        values[i] += (random.NextDouble() - .5) / 2;
        //    }
        //} else
        //{
        //    haveNotRandomized = false;

        //}

        //Console.Write("{ ");
        //for (int i = 0; i < values.Length; i++)
        //{
        //    if (i != values.Length - 1)
        //    {
        //        Console.Write(values[i] + ", ");
        //    }
        //    else
        //    {
        //        Console.Write(values[i] + " }\n");
        //    }
        //}

        Move[] moves = board.GetLegalMoves();
        areWeWhite = board.IsWhiteToMove;

        Console.WriteLine(tttable.Count);
        //tttable.Clear();

        Move finalMove = GetMove(moves, board, 0, areWeWhite, timer);
        //printBoardValues(areWeWhite);
        return finalMove;
    }

    // Gets the depth that the it looks at
    private void getDepth(Timer timer)
    {
        baseDepth = 1000 / (timer.MillisecondsElapsedThisTurn+1);// / (timer.MillisecondsElapsedThisTurn + 1);
        //baseDepth = 0;
    }

    private Move GetMove(Move[] moves, Board board, int depth, bool color, Timer timer)
    {
        Move bestMove = moves[0];
        moves = preSort(board, board.GetLegalMoves(), color);
        double bestEval = 0;

        Board newBaseBoard = Board.CreateBoardFromFEN(board.GetFenString());

        Dictionary<Move, double> sortedMoves = moves.ToDictionary(item => item, item => -1000.0);

        updateBoardState(board, board, new Move(), color, true);

        // Reset board
        double[] baseValues = new double[64];
        boardState.CopyTo(baseValues, 0);

        //tttable.Clear();

        for (int i = 0; i <= baseDepth; i++)
        {
            Move bestMoveThisGen = bestMove;
            bestEval = double.MinValue;

            foreach (KeyValuePair<Move, double> move in sortedMoves)
            {
                if (timer.MillisecondsElapsedThisTurn > 500)
                {
                    break;
                }

                // Gets the base boards
                Move currMove = move.Key;
                board.MakeMove(currMove);

                //updateBoardState(board, newBaseBoard, currMove, color);

                double newEval = -getDeepEval(board, newBaseBoard, baseDepth - i, -100000, 100000, !color, timer, 0, currMove);

                //Console.WriteLine("Eval: " + newEval);
                sortedMoves[currMove] = newEval;
                if (newEval > bestEval)
                {
                    bestMoveThisGen = currMove;
                    bestEval = newEval;
                }

                baseValues.CopyTo(boardState, 0);

                board.UndoMove(currMove);

                // Makes sure we don't drag on too long
                //if (timer.MillisecondsElapsedThisTurn > 8000)
                //{
                //    break;
                //}
            }
            //getDepth(timer);
            //if (timer.MillisecondsElapsedThisTurn < 8000)
            //{
            //    bestMove = bestMoveThisGen;
            //} else
            //{
            //    break;
            //}
            bestMove = bestMoveThisGen;

            var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
            sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

            Console.WriteLine("Depth: " + i);
            Console.WriteLine("Best Eval: " + bestEval/100.0);

            if (timer.MillisecondsElapsedThisTurn > 500)
            {
                break;
            }
        }
        //var sortedDict = from entry in sortedMoves orderby entry.Value ascending select entry;
        //sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

        Console.WriteLine("Best Eval: " + bestEval/100.0);
        return bestMove;
    }

    // The issue might be due to the flipping of the pvt board
    // q-search
    private double qSearch(double alpha, double beta, Board board, Board baseBoard, bool color, int maxq, Timer timer, Move move)
    {
        // Def Vals
        //double[] defVals = new double[64];
        //boardState.CopyTo(defVals, 0);


        // Hopefully this will reduce blundering draws in completely winning positions
        if (board.IsDraw())
        {
            return 0;
        }
        else if (board.IsInCheckmate())
        {
            return -100000 - maxq;
        }

        double aOrig = alpha;

        // Check the Trasposition table before we move on to calculations
        int entryVal = Convert.ToInt32(board.ZobristKey % Convert.ToUInt64(hashSize));
        if (tttable.ContainsKey(entryVal) && tttable[entryVal].qSearch == true)
        {
            Hashentry lookup = tttable[entryVal];
            if (lookup.depth <= maxq && (board.ZobristKey == lookup.zobrist) && (lookup.ancient - timer.MillisecondsRemaining) <= 5000)
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

                // This is for copying legacy boardstate values
                tttable[entryVal].pieceBoardValues.CopyTo(boardState, 0);
            } else if ((lookup.ancient - timer.MillisecondsRemaining) > 5000)
            {
                tttable.Remove(entryVal);
                updateBoardState(board, baseBoard, move, color);
            } else
            {
                updateBoardState(board, baseBoard, move, color);
            }
        } else
        {
            updateBoardState(board, baseBoard, move, color);
        }

        // Evaluate position
        double eval = getBoardVal(color);

        Board newBaseBoard = Board.CreateBoardFromFEN(board.GetFenString());

        // Fail beta cuttoff
        if (eval >= beta)
        {
            //defVals.CopyTo(boardState, 0);
            return beta;
        }

        // alpha increase
        if (eval > alpha)
        {
            alpha = eval;
        }

        // Generate Movelist
        Move[] moves = preSort(board, board.GetLegalMoves(), color);

        // Reset board
        double[] baseValues = new double[64];
        boardState.CopyTo(baseValues, 0);

        for (int i = 0; i < moves.Length; i++)
        {
            //int keepGoing = 0;
            board.MakeMove(moves[i]);
            if ((moves[i].IsCapture || board.IsInCheck()) && maxq < 9)
            {
                //board.MakeMove(moves[i]);
                alpha = Math.Max(alpha, -qSearch(-beta, -alpha, board, newBaseBoard, !color, maxq + 1, timer, moves[i]));

                //alpha = Math.Max(alpha, eval);
                if (alpha >= beta)
                {
                    // Move was too good
                    board.UndoMove(moves[i]);
                    // Reset the values
                    baseValues.CopyTo(boardState, 0);
                    break;
                }
                board.UndoMove(moves[i]);
                // Reset the values
                //baseValues.CopyTo(boardState, 0);
            }
            else
            {
                board.UndoMove(moves[i]);
                // Reset the values
                baseValues.CopyTo(boardState, 0);
                break;
            }
            //board.UndoMove(moves[i]);

            // Reset the values
            baseValues.CopyTo(boardState,0);

            if (beta <= alpha)
            {
                break;
            }
        }


        if ((!tttable.ContainsKey(entryVal) || (tttable[entryVal].depth > maxq && tttable[entryVal].qSearch == true)))
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

            tttable[entryVal] = new Hashentry(board.ZobristKey, maxq, flagNum, alpha, timer.MillisecondsRemaining, true);

            // Copies the Values to The Hash
            baseValues.CopyTo(tttable[entryVal].pieceBoardValues, 0);
        }
        //defVals.CopyTo(boardState, 0);
        return alpha;
    }

    // Minimax algorithm with alpha beta pruning
    private double getDeepEval(Board board, Board baseBoard, int depth, double alpha, double beta, bool color, Timer timer, int extraDepth, Move move)
    {
        // Def Vals
        //double[] defVals = new double[64];
        //boardState.CopyTo(defVals, 0);

        if ((depth > baseDepth) || extraDepth > 3)
        {
            //defVals.CopyTo(boardState, 0);
            //board = Board.CreateBoardFromFEN(baseBoard.GetFenString());
            return qSearch(alpha, beta, board, baseBoard, color, depth + extraDepth, timer, move); // Check this later
            //return getBoardVal(board, color);
        }

        //printBoardValues(areWeWhite);
        double aOrig = alpha;

        // Hopefully this will reduce blundering draws in completely winning positions
        if (board.IsDraw())
        {
            //defVals.CopyTo(boardState, 0);
            return 0;
        } else if (board.IsInCheckmate())
        {
            //defVals.CopyTo(boardState, 0);
            return -100000 - (baseDepth - depth);
        }

        //tttable.Clear();

        // Check the Transposition table before we move on to calculations
        int entryVal = Convert.ToInt32(board.ZobristKey % Convert.ToUInt64(hashSize));
        if (tttable.ContainsKey(entryVal) && tttable[entryVal].qSearch == false)
        {
            Hashentry lookup = tttable[entryVal];
            if (lookup.depth >= (baseDepth - depth) && (board.ZobristKey == lookup.zobrist) && (lookup.ancient - timer.MillisecondsRemaining) < 5000) 
            {
                if (lookup.flag == 0)
                {
                    //defVals.CopyTo(boardState, 0);
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
                    //defVals.CopyTo(boardState, 0);
                    return lookup.eval;
                }
                tttable[entryVal].pieceBoardValues.CopyTo(boardState, 0);
            } else if ((lookup.ancient - timer.MillisecondsRemaining) > 5000)
            {
                tttable.Remove(entryVal);
                updateBoardState(board, baseBoard, move, color);
            } else
            {
                updateBoardState(board, baseBoard, move, color);
            }
        } else
        {
            updateBoardState(board, baseBoard, move, color);
        }

        double eval;

        Move[] moves = preSort(board, board.GetLegalMoves(), color);
        //Move[] moves = board.GetLegalMoves();

        // Creates the baseboard
        Board newBaseBoard = Board.CreateBoardFromFEN(board.GetFenString());

        // Reset board
        double[] baseValues = new double[64];
        boardState.CopyTo(baseValues, 0);

        
        // Negamax 
        for (int i = 0; i < moves.Length; i++)
        {
            int keepGoing = 0;
            board.MakeMove(moves[i]);
            //ulong enemyBit;
            //if (color)
            //{
            //    enemyBit = board.BlackPiecesBitboard;
            //} else
            //{
            //    enemyBit = board.WhitePiecesBitboard;
            //}
            if (board.IsInCheck())// || moves[i].IsPromotion || (moves[i].MovePieceType == PieceType.Pawn && BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) < 12)) //|| (moves[i].MovePieceType == PieceType.King && 6 > BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(PieceType.Queen, new Square(i), enemyBit, color))))
            {
                keepGoing = 1;
            }
            //Console.WriteLine("Eval - " + bestEval + ", White? " + color);
            if (i == 0)
            {
                eval = -getDeepEval(board, newBaseBoard, depth + 1 - keepGoing, -beta, -alpha, !color, timer, extraDepth + keepGoing, moves[i]);
            }
            else
            {
                eval = -getDeepEval(board, newBaseBoard, depth + 1 - keepGoing, -alpha - 1, -alpha, !color, timer, extraDepth + keepGoing, moves[i]);
                if (alpha < eval && eval < beta)
                {
                    // Reset the values
                    baseValues.CopyTo(boardState, 0);
                    eval = -getDeepEval(board, newBaseBoard, depth + 1 - keepGoing, -beta, -alpha, !color, timer, extraDepth + keepGoing, moves[i]);
                }
            }
            board.UndoMove(moves[i]);

            // Reset the values
            baseValues.CopyTo(boardState,0);

            alpha = Math.Max(alpha, eval);

            if (alpha >= beta)
            {
                // Move was too good
                //tttable[entryVal] = new Hashentry(board.ZobristKey, (baseDepth - depth), 1, alpha, timer.MillisecondsRemaining, false);
                //baseValues.CopyTo(tttable[entryVal].pieceBoardValues, 0);
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

            tttable[entryVal] = new Hashentry(board.ZobristKey, (baseDepth - depth), flagNum, alpha, timer.MillisecondsRemaining, false);

            // Copies the Values to The Hash
            baseValues.CopyTo(tttable[entryVal].pieceBoardValues, 0);
        }
        //defVals.CopyTo(boardState, 0);
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
    private double getBoardVal(bool color)
    {
        //updateBoardState(board);

        // Positional values are calculated within material value function
        if (color != true)
        {
            return -boardState.Sum();
        }
        return boardState.Sum();
    }

    private void updateBoardState(Board board, Board baseBoard, Move move, bool color, bool doAll = false)
    {
        // This code should reduce time it takes to calculate position by only updating necessary squares
        ulong updates = getPiecesAttackingUs(board, move.TargetSquare, color) ^ getPiecesAttackingUs(baseBoard, move.TargetSquare, color);
        updates |= (getPiecesDefendingUs(board, move.TargetSquare, color) ^ getPiecesDefendingUs(baseBoard, move.TargetSquare, color));
        updates |= getPiecesAttackingUs(board, move.StartSquare, color) ^ getPiecesAttackingUs(baseBoard, move.StartSquare, color);
        updates |= (getPiecesDefendingUs(board, move.StartSquare, color) ^ getPiecesDefendingUs(baseBoard, move.StartSquare, color));

        // Change this in future
        updates |= board.AllPiecesBitboard ^ baseBoard.AllPiecesBitboard;
        //updates |= 1U >> move.TargetSquare.Index;
        //updates |= 1U >> move.StartSquare.Index;
        BitboardHelper.SetSquare(ref updates, move.TargetSquare);
        BitboardHelper.SetSquare(ref updates, move.StartSquare);
        //printBoardValues(color);

        ulong wpawns = board.GetPieceBitboard(PieceType.Pawn, true);
        ulong wknights = board.GetPieceBitboard(PieceType.Knight, true);
        ulong wbishops = board.GetPieceBitboard(PieceType.Bishop, true);
        ulong wrooks = board.GetPieceBitboard(PieceType.Rook, true);
        ulong wqueens = board.GetPieceBitboard(PieceType.Queen, true);
        ulong wking = board.GetPieceBitboard(PieceType.King, true);

        ulong bpawns = board.GetPieceBitboard(PieceType.Pawn, false);
        ulong bknights = board.GetPieceBitboard(PieceType.Knight, false);
        ulong bbishops = board.GetPieceBitboard(PieceType.Bishop, false);
        ulong brooks = board.GetPieceBitboard(PieceType.Rook, false);
        ulong bqueens = board.GetPieceBitboard(PieceType.Queen, false);
        ulong bking = board.GetPieceBitboard(PieceType.King, false);

        for (int i = 0; i < 64; i++) {

            if (((updates >> i) & 0x1) == 1 || doAll)
            {
                if (((wpawns >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.Pawn);
                }
                else if (((wknights >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.Knight);
                }
                else if (((wbishops >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.Bishop);
                }
                else if (((wrooks >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.Rook);
                }
                else if (((wqueens >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.Queen);
                }
                else if (((wking >> i) & 0x1) == 1)
                {
                    boardState[i] = getPieceValue(board, true, new Square(i), PieceType.King);
                }
                else if (((bpawns >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.Pawn);
                }
                else if (((bknights >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.Knight);
                }
                else if (((bbishops >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.Bishop);
                }
                else if (((brooks >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.Rook);
                }
                else if (((bqueens >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.Queen);
                }
                else if (((bking >> i) & 0x1) == 1)
                {
                    boardState[i] = -getPieceValue(board, false, new Square(i), PieceType.King);
                }
                else
                {
                    boardState[i] = 0;
                }
            }
        }
    }

    private ulong getPiecesAttackingUs(Board board, Square square, bool color)
    {
        // The Pieces we have are used as blockers to call the getPieceAttacks function
        ulong blockers = board.AllPiecesBitboard;

        // Gets 
        //ulong emptySquares = ~blockers;

        // Number of pieces attacking us
        ulong numPiecesAttackingUs = 0;

        // Current Square in bitboard form
        ulong currSquare = 0000000000000000000000000000000000000000000000000000000000000000 | ((ulong)1 << square.Index);
        //BitboardHelper.VisualizeBitboard(currSquare);

        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            if (pieceList.IsWhitePieceList != color)
            {
                ulong enemyPiecesAttackingSquares = board.GetPieceBitboard(pieceList.TypeOfPieceInList, !color);
                //BitboardHelper.VisualizeBitboard(enemyPiecesAttackingSquares);
                int totalNumberOfPiecesInList = BitboardHelper.GetNumberOfSetBits(enemyPiecesAttackingSquares);
                for (int i = 0; i < totalNumberOfPiecesInList; i++)
                {
                    if (pieceList.TypeOfPieceInList == PieceType.Pawn)
                    {
                        numPiecesAttackingUs |= (BitboardHelper.GetPawnAttacks(new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref enemyPiecesAttackingSquares)), !color) & currSquare);
                    }
                    else
                    {
                        numPiecesAttackingUs |= (BitboardHelper.GetPieceAttacks(pieceList.TypeOfPieceInList, new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref enemyPiecesAttackingSquares)), blockers, color) & currSquare);
                    }
                }
            }
        }

        // Returns number of pieces attacking a given square
        return numPiecesAttackingUs;
    }

    private ulong getPiecesDefendingUs(Board board, Square square, bool color)
    {
        // The Pieces we have are used as blockers to call the getPieceAttacks function
        ulong blockers = board.AllPiecesBitboard;

        // Number of pieces attacking us
        ulong numPiecesDefendingUs = 0;

        // Current Square in bitboard form
        ulong currSquare = 0000000000000000000000000000000000000000000000000000000000000000 | ((ulong) 1 << square.Index);
        //BitboardHelper.VisualizeBitboard(currSquare);

        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            if (pieceList.IsWhitePieceList == color)
            {
                ulong ourPiecesDefendingSquares = board.GetPieceBitboard(pieceList.TypeOfPieceInList, color);
                //BitboardHelper.VisualizeBitboard(enemyPiecesAttackingSquares);
                int totalNumberOfPiecesInList = BitboardHelper.GetNumberOfSetBits(ourPiecesDefendingSquares);
                for (int i = 0; i < totalNumberOfPiecesInList; i++)
                {
                    if (pieceList.TypeOfPieceInList == PieceType.Pawn)
                    {
                        numPiecesDefendingUs |= (BitboardHelper.GetPawnAttacks(new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourPiecesDefendingSquares)), color) & currSquare);
                    }
                    else
                    {
                        numPiecesDefendingUs |= (BitboardHelper.GetPieceAttacks(pieceList.TypeOfPieceInList, new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourPiecesDefendingSquares)), blockers, !color) & currSquare);
                    }
                }
            }
        }

        // Returns number of pieces attacking a given square
        return numPiecesDefendingUs;
    }


    private double getPieceValue(Board board, bool color, Square square, PieceType pieceType)
    {
        double pieceValue = 0;

        ulong allPieces = board.AllPiecesBitboard;
        ulong ourPieces = (color) ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        ulong notOurPieces = (color) ? board.BlackPiecesBitboard : board.WhitePiecesBitboard;
        //double isOurTurn = (color == board.IsWhiteToMove) ? 1 : 0;
        double piecesLeft = (double)BitboardHelper.GetNumberOfSetBits(allPieces); // Gets how many pieces left
        double rankInput = (color) ? (double)square.Rank : (7.0 - (double)square.Rank);
        double distanceFromCenter = Math.Abs((3.5 - (double) square.File));
        //double opponentKingRankInput = (color) ? (double)board.GetKingSquare(!color).Rank : (7.0 - (double)board.GetKingSquare(!color).Rank);
        //double opponentKingFileInput = (double)board.GetKingSquare(!color).File;
        double mobilityInput;
        if (pieceType == PieceType.King)
        {
            mobilityInput = (double)BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(PieceType.Queen, square, ourPieces, color));
        } else
        {
            mobilityInput = (double)BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, square, ourPieces, color));
        }
        double numPiecesAttacking = (double)BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, square, allPieces, color) & notOurPieces);
        double numPiecesAttackingUs = (double)BitboardHelper.GetNumberOfSetBits(getPiecesAttackingUs(board, square, color));
        double numPiecesDefending = (double)BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(pieceType, square, allPieces, !color) & ourPieces);
        double numPiecesDefendingUs = (double)BitboardHelper.GetNumberOfSetBits(getPiecesDefendingUs(board, square, color));

        switch (pieceType)
        {
            case (PieceType.Pawn):
                pieceValue += 100; // Material
                pieceValue += rankInput/2 * (values[0] / distanceFromCenter*2);
                pieceValue -= Math.Pow(numPiecesAttackingUs, 3) / values[1];
                pieceValue += numPiecesAttacking * values[2];
                pieceValue += numPiecesDefending * values[3];
                pieceValue += numPiecesDefendingUs * values[4];
                break;
            case (PieceType.Bishop):
                pieceValue += 310; // Material
                pieceValue += mobilityInput * values[5];
                pieceValue += numPiecesAttacking * values[6];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 3) / values[7];
                pieceValue += numPiecesDefending * values[8];
                pieceValue += Math.Pow(numPiecesDefendingUs, 2) * values[9];
                pieceValue -= (rankInput == 0) ? values[10] : 0;
                break;
            case (PieceType.Knight):
                pieceValue += 300; // Material
                pieceValue += mobilityInput * values[11];
                pieceValue += numPiecesAttacking * values[12];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 3) / values[13];
                pieceValue += numPiecesDefending * values[14];
                pieceValue += Math.Pow(numPiecesDefendingUs, 2) * values[15];
                pieceValue -= (rankInput == 0) ? values[16] : 0;
                break;
            case (PieceType.Rook):
                pieceValue += mobilityInput / values[17];
                pieceValue += numPiecesAttacking * values[18];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 3) / values[19];
                pieceValue += numPiecesDefending * values[20];
                pieceValue += Math.Pow(numPiecesDefendingUs, 2) * values[21];
                pieceValue *= (1 - ((piecesLeft / 32) + .3)) * values[22];
                pieceValue += 500; // Material
                break;
            case (PieceType.Queen):
                pieceValue += 900; // Material
                pieceValue += ((mobilityInput) * (1-((piecesLeft/32)+.5))) * values[23];
                pieceValue += numPiecesAttacking * values[24];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 2) * values[25];
                pieceValue += Math.Pow(numPiecesDefendingUs, 2) * values[26];
                pieceValue += numPiecesDefendingUs * values[27];
                break;
            case (PieceType.King):
                pieceValue += 1000; // Material
                pieceValue -= numPiecesDefending*3;
                pieceValue -= Math.Pow(mobilityInput, 1.23) * (piecesLeft / 32) * values[28];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 4) * values[29];
                if (piecesLeft > 20)
                {
                    if (distanceFromCenter < 3.5)
                    {
                        pieceValue += distanceFromCenter * 5.5;
                    }
                    pieceValue -= (rankInput == 3.5) ? rankInput : rankInput * values[30];
                } 
                break;
        }
        if (pieceValue < 0)
        {
            pieceValue = 0;
        }
        return pieceValue;
    }
}