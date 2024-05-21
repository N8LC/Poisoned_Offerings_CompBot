using ChessChallenge.API;
//using ChessChallenge.Chess;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    // Default Vals: 5.75, 1.25, 1.75, 3.3, 2.0, 2.8, 1.5, 2.0, 1.5, 1.5, 7.0, 2.4, 2.4, 1.5, 2.6, 1.5, 6.8, 2.0, 1.2, 1.25, 1, 1.7, 1, 4, .7, 1.5, .7, 6, 1, 5.5, 1 
    // Current Best 6.2346403184429935, 1.2935692497523388, 1.6170090090995846, 4.107261189619279, 2.3528710374380504, 2.681288314152552, 1.1771408258856277, 1.7778025250058735, 1.8404471751643239, 1.5122208808734618, 5.652493056318367, 2.8710728046656913, 2.2410907939664537, 1.9488710643157303, 2.091394671191594, 1.2549069109882427, 6.849996473372929, 3.1195891484783855, 1.1060085265131754, 1.3541286569994082, 1.1286598820853428, 1.5557265598379992, 1.459888281934701, 4.746786186293157, 0.7963945431939259, 1.4462402820765092, 0.7720269292591588, 4.252778592267265, 0.838723416614591, 5.165945994167033, 1.263080142016497



    /// Current color (White or Black)
    public bool areWeWhite;

    // This is the values for the positional aspects of the board evaluation
    /*public double[] values = new double[] {
        5.950929878597145, 2.739400151737597, 1.98747707374706, 4.399977900597099, 1.631649301677858, -0.025179585108370928, 0.4481680027011854, 1.8579620286810918, 2.0747748904381673, 1.984459708493875, 5.730400201207056, -0.3912574836231806, 3.336468035998802, 1.8088006607347735, 3.770585880372823, 2.4390908984561745, 6.944565287618426, 4.3452503160318425, 2.7875284987941047, 0.06474563941653666, 2.501889485667594, 0.1994288161853689, 2.733234068006898, 0.714353567883774, -0.7306414638031885, 1.5793199432460763, 1.8695047166692884, -0.2798872730549671, 1.9957046797687714, 2.424961478775342, 1.107777211267805
    };*/
    public double[] values = new double[] {
6.985276059254072, 1.440756868262853, 1.471602756206613, 4.153128860307227, 2.165839608338816, 2.7372438226703197, 1.140516525109266, 1.9471640489481348, 1.5572233146919552, 1.2828453992377244, 6.2818263748824466, 2.3916115454174816, 1.880142719849608, 1.318462133242958, 2.0232949833633787, 1.6299038299842343, 7.431541562037035, 2.553831521972569, 1.2347643785066515, 1.4561321222031642, 0.9295644866675574, 1.1879130668784457, 1.171958788617485, 5.97289526154109, 0.690253343857657, 1.102412761034281, 0.6948504618377056, 4.898673419968283, 1.0006381114773057, 6.494777351054253, 1.2228624123942722
        };

    //Move previousMove;

    // Transposition Table size
    readonly public static int hashSize = 4_800_000;

    // Transposition Table
    readonly Dictionary<int, Hashentry> tttable = new Dictionary<int, Hashentry>(hashSize);

    // Sets the time for the bot to move at 1000ms or 1 second
    public static int timeToMove = 1300;

    // This stores the values for the point-value table
    //readonly Dictionary<int, double[]> pvtable = new Dictionary<int, double[]>(hashSize*2); // See if this helps

    // This tracks the boardstate so that we can figure out each individual contribution
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
                num = boardState[63 - (i - ((i) % 8) + (7 - ((i) % 8)))] / 100.0;
            }
            else
            {
                num = boardState[(i - ((i) % 8) + (7 - ((i) % 8)))] / 100.0;
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
    public int baseDepth = 8;
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

    private Move GetMove(Move[] moves, Board board, int depth, bool color, Timer timer)
    {
        // Sets the default best move to he first move
        Move bestMove = moves[0];

        // Presorts the moves
        moves = preSort(board, board.GetLegalMoves(), color);

        // Sets the current best eval to lowest double val
        double bestEval = double.MinValue;

        // This is a copy of current board
        Board newBaseBoard = Board.CreateBoardFromFEN(board.GetFenString());

        // Changes moves into dictionary for easier sorting
        Dictionary<Move, double> sortedMoves = moves.ToDictionary(item => item, item => -1000.0);

        // Updates the board state
        updateBoardState(board, board, new Move(), color, true);

        // Copies current board to reset it when necessary
        double[] baseValues = new double[64];
        boardState.CopyTo(baseValues, 0);

        for (int i = 0; i <= baseDepth; i++)
        {
            // Best move at this depth
            Move bestMoveThisGen = new Move();

            // Best eval last depth
            //double bestEvalLastGen = bestEval;

            // Starting best eval
            //bestEval = -999;

            foreach (KeyValuePair<Move, double> move in sortedMoves)
            {
                // Gets the base boards
                Move currMove = move.Key;
                board.MakeMove(currMove);

                //updateBoardState(board, newBaseBoard, currMove, color);

                double newEval = -getDeepEval(board, newBaseBoard, baseDepth - i, -100000, 100000, !color, timer, 0, currMove);

                //Console.WriteLine("Eval: " + newEval);
                sortedMoves[currMove] = newEval;

                // FIX THIS ISSUE - the timer might cut off new best moves.
                if (newEval > bestEval)
                {
                    bestMoveThisGen = currMove;
                    bestEval = newEval;
                }

                // Resets basevalues
                baseValues.CopyTo(boardState, 0);

                // Undoes move
                board.UndoMove(currMove);

                if (timer.MillisecondsElapsedThisTurn > timeToMove)
                {
                    break;
                }
            }

            if (timer.MillisecondsElapsedThisTurn > timeToMove)
            {
                /*var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
                sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);
                if (bestEval > bestEvalLastGen)
                {
                    bestMove = sortedMoves.First().Key;
                }
                var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
                sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);*/

                // Be careful with this, sets best move and curr best move to SAME OBJECT
                if (bestMoveThisGen != new Move())
                {
                    bestMove = bestMoveThisGen;
                }
                break;
            }
            else
            {
                var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
                sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

                bestMove = sortedMoves.First().Key;
            }


            /*bestMove = bestMoveThisGen;

            var sortedDict = from entry in sortedMoves orderby -entry.Value ascending select entry;
            sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

            if (timer.MillisecondsElapsedThisTurn > timeToMove)
            {
                break;
            }*/


            //getDepth(timer);
            //if (timer.MillisecondsElapsedThisTurn < 8000)
            //{
            //    bestMove = bestMoveThisGen;
            //} else
            //{
            //    break;
            //}

            //Console.WriteLine("Depth: " + i);
            //Console.WriteLine("Best Eval: " + bestEval/100.0);
        }
        //var sortedDict = from entry in sortedMoves orderby entry.Value ascending select entry;
        //sortedMoves = sortedDict.ToDictionary(pair => pair.Key, pair => pair.Value);

        Console.WriteLine("Best Eval: " + bestEval / 100.0 + " - BaseBot\n");
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
            }
            else if ((lookup.ancient - timer.MillisecondsRemaining) > 5000)
            {
                tttable.Remove(entryVal);
                updateBoardState(board, baseBoard, move, color);
            }
            else
            {
                updateBoardState(board, baseBoard, move, color);
            }
        }
        else
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
            if ((moves[i].IsCapture || board.IsInCheck()) && maxq < 15)
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
            baseValues.CopyTo(boardState, 0);

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

        // Hopefully this will reduce blundering draws in completely winning positions
        if (board.IsDraw())
        {
            //defVals.CopyTo(boardState, 0);
            return 0;
        }
        else if (board.IsInCheckmate())
        {
            //defVals.CopyTo(boardState, 0);
            return -100000 - (baseDepth - depth);
        }

        if ((depth >= baseDepth) || extraDepth > 3)
        {
            //defVals.CopyTo(boardState, 0);
            //board = Board.CreateBoardFromFEN(baseBoard.GetFenString());
            return qSearch(alpha, beta, board, baseBoard, color, depth + extraDepth, timer, move); // Check this later
                                                                                                   //return getBoardVal(board, color);
        }

        //printBoardValues(areWeWhite);
        double aOrig = alpha;

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
            }
            else if ((lookup.ancient - timer.MillisecondsRemaining) > 5000)
            {
                tttable.Remove(entryVal);
                updateBoardState(board, baseBoard, move, color);
            }
            else
            {
                updateBoardState(board, baseBoard, move, color);
            }
        }
        else
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
            baseValues.CopyTo(boardState, 0);

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

        for (int i = 0; i < 64; i++)
        {

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
        ulong currSquare = 0000000000000000000000000000000000000000000000000000000000000000 | ((ulong)1 << square.Index);
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
        double distanceFromCenter = Math.Abs((3.5 - (double)square.File));
        //double opponentKingRankInput = (color) ? (double)board.GetKingSquare(!color).Rank : (7.0 - (double)board.GetKingSquare(!color).Rank);
        //double opponentKingFileInput = (double)board.GetKingSquare(!color).File;
        double mobilityInput;
        if (pieceType == PieceType.King)
        {
            mobilityInput = (double)BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(PieceType.Queen, square, ourPieces, color));
        }
        else
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
                pieceValue += rankInput / 2 * (values[0] / distanceFromCenter * 2);
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
                pieceValue += ((mobilityInput) * ((values[23] / piecesLeft) / 3));
                pieceValue += numPiecesAttacking * values[24];
                pieceValue -= Math.Pow(numPiecesAttackingUs, 2) * values[25];
                pieceValue += Math.Pow(numPiecesDefendingUs, 2) * values[26];
                break;
            case (PieceType.King):
                pieceValue += 1000; // Material
                pieceValue -= numPiecesDefending * 3;
                pieceValue -= Math.Pow(mobilityInput, 1.23) * ((values[27] / piecesLeft) / 3);
                pieceValue -= Math.Pow(numPiecesAttackingUs, 4) * values[28];
                if (piecesLeft > 20)
                {
                    if (distanceFromCenter < 3.5)
                    {
                        pieceValue += distanceFromCenter * values[29];
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
