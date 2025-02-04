# Tiny Chess Bots Competition Entry
## EvilBot
POv1 Poisoned Offerings as it was entered into the competition. (ELO ~800)

This bot was contained in the allowed tokens limit of 1200, and ranked in the top 50% of bots. It was
made in about a week, but was worked on further for a few months after to get to version 3.2. It uses
a normal minimax search with alpha beta pruning, although it does have some bugs that I did not fix since
they were there at the time of the competition. Besides that, it is a very basic bot that uses hard coded material
evaluation, and very little positional evaluation besides encouraging pawn pushes through manipulating the 
value of the pawn during the presort. Just for ease of comparison I will list the common features here so
it can be compared to version 3.2.

### POv1 Features
- Rudimentary Dynamic depth calculations depending on time remaining
- Presorting (Based on captures and previous lower depth evaluations)
- Minimax search with Alpha-Beta pruning
- Very Minimal positional Evaluation
- Material Evaluation based on standard theory (1 for pawn, ~3 for knights and bishops, 5 for rooks, 9 for queens)

## MyBot
POv3.2 (ELO ~1800)

Version 3.2, while a lot stronger is currently sitting at 3336 tokens, although it is important to mention that there
has been no attempts to facilitate optimization since the competition finished. At version 3.2, this bot has a lot more
features than version 1.0. Specifically, this bot uses a unique method to determine the boardstate of a given position.
To do this, the bot stores a table named the PVT (Piece Value Table) table (Yes I know I say table twice its funny), in 
which each piece is given a value. White pieces are positive while black pieces are negative. To get an evalutation of a 
position, the table is summed (its really just a list best visualized as a table), and if it is positive then white has 
an advantage and if it is negative then black has an advantage. The important part to note about this setup is that the 
positional evaluation of a piece is baked into its value, so each piece is individually evaluated to determine the positional
evaluation of a boardstate. But the optimization that this allows for is that only certain affected pieces are calculated each
move, so instead of recalculating all the pieces on a board the bot only recalculates about 6-7 of them usually per move. It 
decides which pieces to calculate using a lot of tactics including some bit manipulation and other such fancy methods. The
actual method in which positional values are calculated out is fairly lengthy, and also includes extraneous values that were 
determined through testing using a version of the genetic algorithm. Otherwise, the bot uses relatively standard techniques.

### POv3.2 Features
- Dynamic Depth Calculations based on time allowed per move
- Presorting (Only based on captures and promotions)
- Negamax Search with Alpha-Beta pruning
- Positional Evaluation based on a number of factors, and tuned using the genetic algorithm
- Transposition Table (Stores a couple hundred thousand positions per game)
- PVT Table (explained above)
- qsearch (Pretty standard capture search, stores positions in transposition table as well)
