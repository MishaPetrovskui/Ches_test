using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChessClient
{
    public enum PieceType
    {
        None, Pawn, Rook, Knight, Bishop, Queen, King
    }

    public enum PlayerTeam
    {
        White, Black
    }

    public class ChessPiece
    {
        public PieceType Type { get; set; }
        public PlayerTeam Team { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool HasMoved { get; set; }

        public ChessPiece() { }

        public ChessPiece(PieceType type, PlayerTeam team, int x, int y)
        {
            Type = type;
            Team = team;
            X = x;
            Y = y;
            HasMoved = false;
        }

    }

    public partial class Form1 : Form
    {
        private readonly HttpClient http = new HttpClient();
        private const string baseUrl = "https://serverforchess-production.up.railway.app/";

        private Panel boardPanel;
        private Button[,] squares;
        private ChessPiece[,] board;
        private ChessPiece selectedPiece;
        private Point selectedSquare = new Point(-1, -1);
        private List<Point> validMoves = new List<Point>();

        private PlayerTeam currentPlayer = PlayerTeam.White;
        private PlayerTeam myTeam;
        private bool gameStarted = false;
        private bool myTurn = false;

        private Label lblGameInfo;
        private Label lblTurnInfo;
        private Button btnSurrender;
        private Panel infoPanel;

        private int playerId;
        private int lobbyId;
        private CancellationTokenSource pollCts;

        public Form1(int playerId, int lobbyId, PlayerTeam team)
        {
            http.BaseAddress = new Uri(baseUrl);
            this.playerId = playerId;
            this.lobbyId = lobbyId;
            this.myTeam = team;
            this.currentPlayer = PlayerTeam.White;
            this.myTurn = (team == PlayerTeam.White);
            gameStarted = true;

            MessageBox.Show($"Form1 открыта. myTeam = {myTeam}, myTurn = {myTurn}");
            // InitializeComponent();
            SetupForm();
            SetupBoard();
            InitializeBoard();
            UpdateGameInfo();
            StartGamePolling();
        }

        private void SetupForm()
        {
            this.Text = "Шахматы";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(240, 217, 181);

            // Панель информации
            infoPanel = new Panel();
            infoPanel.Location = new Point(650, 20);
            infoPanel.Size = new Size(220, 600);
            infoPanel.BackColor = Color.FromArgb(181, 136, 99);
            infoPanel.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(infoPanel);

            lblGameInfo = new Label();
            lblGameInfo.Text = "Шахматная партия";
            lblGameInfo.Font = new Font("Arial", 14, FontStyle.Bold);
            lblGameInfo.ForeColor = Color.White;
            lblGameInfo.Location = new Point(10, 20);
            lblGameInfo.Size = new Size(200, 30);
            lblGameInfo.TextAlign = ContentAlignment.MiddleCenter;
            infoPanel.Controls.Add(lblGameInfo);

            lblTurnInfo = new Label();
            lblTurnInfo.Text = "Ход белых";
            lblTurnInfo.Font = new Font("Arial", 12, FontStyle.Regular);
            lblTurnInfo.ForeColor = Color.White;
            lblTurnInfo.Location = new Point(10, 60);
            lblTurnInfo.Size = new Size(200, 25);
            lblTurnInfo.TextAlign = ContentAlignment.MiddleCenter;
            infoPanel.Controls.Add(lblTurnInfo);

            btnSurrender = new Button();
            btnSurrender.Text = "Сдаться";
            btnSurrender.Font = new Font("Arial", 10, FontStyle.Bold);
            btnSurrender.Location = new Point(10, 500);
            btnSurrender.Size = new Size(200, 40);
            btnSurrender.BackColor = Color.FromArgb(139, 69, 19);
            btnSurrender.ForeColor = Color.White;
            btnSurrender.FlatStyle = FlatStyle.Flat;
            btnSurrender.Click += BtnSurrender_Click;
            infoPanel.Controls.Add(btnSurrender);

            // Панель доски
            boardPanel = new Panel();
            boardPanel.Location = new Point(20, 20);
            boardPanel.Size = new Size(600, 600);
            boardPanel.BackColor = Color.FromArgb(139, 69, 19);
            boardPanel.BorderStyle = BorderStyle.Fixed3D;
            this.Controls.Add(boardPanel);
        }

        private void SetupBoard()
        {
            squares = new Button[8, 8];
            board = new ChessPiece[8, 8];

            int buttonSize = 70;
            int offset = 35;

            boardPanel.Size = new Size(buttonSize * 8 + offset * 2, buttonSize * 8 + offset * 2);

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Button square = new Button();
                    square.Size = new Size(buttonSize, buttonSize);
                    square.Margin = new Padding(0);
                    square.Padding = new Padding(0);
                    square.Location = new Point(col * buttonSize + offset, row * buttonSize + offset);
                    square.Font = new Font("Arial", 28, FontStyle.Bold);
                    square.FlatStyle = FlatStyle.Flat;
                    square.FlatAppearance.BorderSize = 2;
                    square.FlatAppearance.BorderColor = Color.Black;

                    // Цвет клетки
                    if ((row + col) % 2 == 0)
                        square.BackColor = Color.FromArgb(240, 217, 181); // Светлые клетки
                    else
                        square.BackColor = Color.FromArgb(181, 136, 99);  // Темные клетки

                    square.Tag = new Point(row, col);
                    square.Click += Square_Click;
                    square.MouseEnter += Square_MouseEnter;
                    square.MouseLeave += Square_MouseLeave;

                    squares[row, col] = square;
                    boardPanel.Controls.Add(square);
                }
            }

            // Добавляем буквы сверху и снизу
            for (int i = 0; i < 8; i++)
            {
                char letter = (char)('A' + i);

                Label topLabel = new Label();
                topLabel.Text = letter.ToString();
                topLabel.Size = new Size(buttonSize, 25);
                topLabel.Location = new Point(i * buttonSize + offset, 0);
                topLabel.TextAlign = ContentAlignment.MiddleCenter;
                boardPanel.Controls.Add(topLabel);

                Label bottomLabel = new Label();
                bottomLabel.Text = letter.ToString();
                bottomLabel.Size = new Size(buttonSize, 25);
                bottomLabel.Location = new Point(i * buttonSize + offset, offset + buttonSize * 8);
                bottomLabel.TextAlign = ContentAlignment.MiddleCenter;
                boardPanel.Controls.Add(bottomLabel);
            }

            // Добавляем цифры слева и справа
            for (int i = 0; i < 8; i++)
            {
                int number = 8 - i;

                Label leftLabel = new Label();
                leftLabel.Text = number.ToString();
                leftLabel.Size = new Size(25, buttonSize);
                leftLabel.Location = new Point(0, i * buttonSize + offset);
                leftLabel.TextAlign = ContentAlignment.MiddleCenter;
                boardPanel.Controls.Add(leftLabel);

                Label rightLabel = new Label();
                rightLabel.Text = number.ToString();
                rightLabel.Size = new Size(25, buttonSize);
                rightLabel.Location = new Point(offset + buttonSize * 8, i * buttonSize + offset);
                rightLabel.TextAlign = ContentAlignment.MiddleCenter;
                boardPanel.Controls.Add(rightLabel);
            }
        }

        private void InitializeBoard()
        {
            // Очистка доски
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    board[i, j] = null;
                }
            }

            // Расстановка фигур
            // Черные фигуры (верх доски)
            board[0, 0] = new ChessPiece(PieceType.Rook, PlayerTeam.Black, 0, 0);
            board[0, 1] = new ChessPiece(PieceType.Knight, PlayerTeam.Black, 0, 1);
            board[0, 2] = new ChessPiece(PieceType.Bishop, PlayerTeam.Black, 0, 2);
            board[0, 3] = new ChessPiece(PieceType.Queen, PlayerTeam.Black, 0, 3);
            board[0, 4] = new ChessPiece(PieceType.King, PlayerTeam.Black, 0, 4);
            board[0, 5] = new ChessPiece(PieceType.Bishop, PlayerTeam.Black, 0, 5);
            board[0, 6] = new ChessPiece(PieceType.Knight, PlayerTeam.Black, 0, 6);
            board[0, 7] = new ChessPiece(PieceType.Rook, PlayerTeam.Black, 0, 7);

            for (int col = 0; col < 8; col++)
            {
                board[1, col] = new ChessPiece(PieceType.Pawn, PlayerTeam.Black, 1, col);
            }

            // Белые фигуры (низ доски)
            board[7, 0] = new ChessPiece(PieceType.Rook, PlayerTeam.White, 7, 0);
            board[7, 1] = new ChessPiece(PieceType.Knight, PlayerTeam.White, 7, 1);
            board[7, 2] = new ChessPiece(PieceType.Bishop, PlayerTeam.White, 7, 2);
            board[7, 3] = new ChessPiece(PieceType.Queen, PlayerTeam.White, 7, 3);
            board[7, 4] = new ChessPiece(PieceType.King, PlayerTeam.White, 7, 4);
            board[7, 5] = new ChessPiece(PieceType.Bishop, PlayerTeam.White, 7, 5);
            board[7, 6] = new ChessPiece(PieceType.Knight, PlayerTeam.White, 7, 6);
            board[7, 7] = new ChessPiece(PieceType.Rook, PlayerTeam.White, 7, 7);

            for (int col = 0; col < 8; col++)
            {
                board[6, col] = new ChessPiece(PieceType.Pawn, PlayerTeam.White, 6, col);
            }

            UpdateBoardDisplay();
        }

        private void UpdateBoardDisplay()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Button square = squares[row, col];
                    ChessPiece piece = board[row, col];

                    if (piece != null)
                    {
                        square.Text = GetPieceSymbol(piece);
                        square.ForeColor = piece.Team == PlayerTeam.White ? Color.WhiteSmoke : Color.Black;
                    }
                    else
                    {
                        square.Text = "";
                    }

                    // Сброс цвета клетки
                    if ((row + col) % 2 == 0)
                        square.BackColor = Color.FromArgb(240, 217, 181);
                    else
                        square.BackColor = Color.FromArgb(181, 136, 99);
                }
            }

            // Подсветка выбранной фигуры
            if (selectedSquare.X != -1 && selectedSquare.Y != -1)
            {
                squares[selectedSquare.X, selectedSquare.Y].BackColor = Color.Yellow;
            }

            // Подсветка возможных ходов
            foreach (Point move in validMoves)
            {
                squares[move.X, move.Y].BackColor = Color.LightGreen;
            }
        }

        private string GetPieceSymbol(ChessPiece piece)
        {
            string[] whiteSymbols = { "", "♙", "♖", "♘", "♗", "♕", "♔" };
            string[] blackSymbols = { "", "♟", "♜", "♞", "♝", "♛", "♚" };

            return piece.Team == PlayerTeam.White ? whiteSymbols[(int)piece.Type] : blackSymbols[(int)piece.Type];
        }

        private void Square_Click(object sender, EventArgs e)
        {
            if (!gameStarted || !myTurn)
            {
                Console.WriteLine($"Клик заблокирован: gameStarted={gameStarted}, myTurn={myTurn}");
                return;
            }

            Button clickedSquare = sender as Button;
            Point position = (Point)clickedSquare.Tag;

            ChessPiece clickedPiece = board[position.X, position.Y];

            // Если выбрана наша фигура
            if (clickedPiece != null && clickedPiece.Team == myTeam)
            {
                SelectPiece(position, clickedPiece);
            }
            // Если кликнули по возможному ходу
            else if (selectedPiece != null && validMoves.Contains(position))
            {
                MakeMove(selectedSquare, position);
            }
            // Снять выделение
            else
            {
                ClearSelection();
            }
        }

        private void SelectPiece(Point position, ChessPiece piece)
        {
            selectedSquare = position;
            selectedPiece = piece;
            validMoves = GetValidMoves(piece);
            UpdateBoardDisplay();
        }

        private void ClearSelection()
        {
            selectedSquare = new Point(-1, -1);
            selectedPiece = null;
            validMoves.Clear();
            UpdateBoardDisplay();
        }

        private async void MakeMove(Point from, Point to)
        {
            // Проверка на взятие фигуры
            ChessPiece capturedPiece = board[to.X, to.Y];

            // Выполнение хода
            board[to.X, to.Y] = selectedPiece;
            board[from.X, from.Y] = null;
            selectedPiece.X = to.X;
            selectedPiece.Y = to.Y;
            selectedPiece.HasMoved = true;

            // Проверка на превращение пешки
            if (selectedPiece.Type == PieceType.Pawn)
            {
                if ((selectedPiece.Team == PlayerTeam.White && to.X == 0) ||
                    (selectedPiece.Team == PlayerTeam.Black && to.X == 7))
                {
                    // Превращение в ферзя
                    selectedPiece.Type = PieceType.Queen;
                }
            }

            ClearSelection();
            myTurn = false;
            UpdateBoardDisplay();
            UpdateGameInfo();

            // Отправка хода на сервер
            await UpdateServerChessField();
            myTurn = false;
            // Проверка на мат/шах
            PlayerTeam opponentTeam = myTeam == PlayerTeam.White ? PlayerTeam.Black : PlayerTeam.White;
            if (IsCheckmate(opponentTeam))
            {
                await SendWinToServer();
                MessageBox.Show($"Шах и мат! Вы победили!", "Игра окончена",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }

        private List<Point> GetValidMoves(ChessPiece piece)
        {
            List<Point> moves = new List<Point>();

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    moves = GetPawnMoves(piece);
                    break;
                case PieceType.Rook:
                    moves = GetRookMoves(piece);
                    break;
                case PieceType.Knight:
                    moves = GetKnightMoves(piece);
                    break;
                case PieceType.Bishop:
                    moves = GetBishopMoves(piece);
                    break;
                case PieceType.Queen:
                    moves = GetQueenMoves(piece);
                    break;
                case PieceType.King:
                    moves = GetKingMoves(piece);
                    break;
            }

            return moves.Where(IsValidPosition).ToList();
        }

        private List<Point> GetPawnMoves(ChessPiece pawn)
        {
            List<Point> moves = new List<Point>();
            int direction = pawn.Team == PlayerTeam.White ? -1 : 1;
            int startRow = pawn.Team == PlayerTeam.White ? 6 : 1;

            // Движение вперед
            if (IsValidPosition(pawn.X + direction, pawn.Y) && board[pawn.X + direction, pawn.Y] == null)
            {
                moves.Add(new Point(pawn.X + direction, pawn.Y));

                // Двойной ход с начальной позиции
                if (pawn.X == startRow && board[pawn.X + 2 * direction, pawn.Y] == null)
                {
                    moves.Add(new Point(pawn.X + 2 * direction, pawn.Y));
                }
            }

            // Взятие по диагонали
            for (int dy = -1; dy <= 1; dy += 2)
            {
                if (IsValidPosition(pawn.X + direction, pawn.Y + dy))
                {
                    ChessPiece target = board[pawn.X + direction, pawn.Y + dy];
                    if (target != null && target.Team != pawn.Team)
                    {
                        moves.Add(new Point(pawn.X + direction, pawn.Y + dy));
                    }
                }
            }

            return moves;
        }

        private List<Point> GetRookMoves(ChessPiece rook)
        {
            List<Point> moves = new List<Point>();

            // Горизонтальные и вертикальные направления
            int[,] directions = { { 0, 1 }, { 0, -1 }, { 1, 0 }, { -1, 0 } };

            for (int i = 0; i < 4; i++)
            {
                int dx = directions[i, 0];
                int dy = directions[i, 1];

                for (int step = 1; step < 8; step++)
                {
                    int newX = rook.X + dx * step;
                    int newY = rook.Y + dy * step;

                    if (!IsValidPosition(newX, newY)) break;

                    ChessPiece target = board[newX, newY];
                    if (target == null)
                    {
                        moves.Add(new Point(newX, newY));
                    }
                    else
                    {
                        if (target.Team != rook.Team)
                            moves.Add(new Point(newX, newY));
                        break;
                    }
                }
            }

            return moves;
        }

        private List<Point> GetKnightMoves(ChessPiece knight)
        {
            List<Point> moves = new List<Point>();
            int[,] knightMoves = { { -2, -1 }, { -2, 1 }, { -1, -2 }, { -1, 2 }, { 1, -2 }, { 1, 2 }, { 2, -1 }, { 2, 1 } };

            for (int i = 0; i < 8; i++)
            {
                int newX = knight.X + knightMoves[i, 0];
                int newY = knight.Y + knightMoves[i, 1];

                if (IsValidPosition(newX, newY))
                {
                    ChessPiece target = board[newX, newY];
                    if (target == null || target.Team != knight.Team)
                    {
                        moves.Add(new Point(newX, newY));
                    }
                }
            }

            return moves;
        }

        private List<Point> GetBishopMoves(ChessPiece bishop)
        {
            List<Point> moves = new List<Point>();

            // Диагональные направления
            int[,] directions = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

            for (int i = 0; i < 4; i++)
            {
                int dx = directions[i, 0];
                int dy = directions[i, 1];

                for (int step = 1; step < 8; step++)
                {
                    int newX = bishop.X + dx * step;
                    int newY = bishop.Y + dy * step;

                    if (!IsValidPosition(newX, newY)) break;

                    ChessPiece target = board[newX, newY];
                    if (target == null)
                    {
                        moves.Add(new Point(newX, newY));
                    }
                    else
                    {
                        if (target.Team != bishop.Team)
                            moves.Add(new Point(newX, newY));
                        break;
                    }
                }
            }

            return moves;
        }

        private List<Point> GetQueenMoves(ChessPiece queen)
        {
            List<Point> moves = new List<Point>();
            moves.AddRange(GetRookMoves(queen));
            moves.AddRange(GetBishopMoves(queen));
            return moves;
        }

        private List<Point> GetKingMoves(ChessPiece king)
        {
            List<Point> moves = new List<Point>();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int newX = king.X + dx;
                    int newY = king.Y + dy;

                    if (IsValidPosition(newX, newY))
                    {
                        ChessPiece target = board[newX, newY];
                        if (target == null || target.Team != king.Team)
                        {
                            moves.Add(new Point(newX, newY));
                        }
                    }
                }
            }

            return moves;
        }

        private bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < 8 && y >= 0 && y < 8;
        }

        private bool IsValidPosition(Point position)
        {
            return IsValidPosition(position.X, position.Y);
        }

        private bool IsCheckmate(PlayerTeam team)
        {
            // Упрощенная проверка на мат - проверяем, есть ли возможные ходы
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    ChessPiece piece = board[x, y];
                    if (piece != null && piece.Team == team)
                    {
                        if (GetValidMoves(piece).Count > 0)
                            return false;
                    }
                }
            }
            return true;
        }

        private void UpdateGameInfo()
        {
            lblTurnInfo.Text = currentPlayer == PlayerTeam.White ? "Ход белых" : "Ход черных";
            if (myTurn)
                lblTurnInfo.Text += " (Ваш ход)";
            else
                lblTurnInfo.Text += " (Ход противника)";
        }

        private void Square_MouseEnter(object sender, EventArgs e)
        {
            Button square = sender as Button;
            Point position = (Point)square.Tag;

            if (selectedPiece != null && validMoves.Contains(position))
            {
                square.BackColor = Color.LightBlue;
            }
        }

        private void Square_MouseLeave(object sender, EventArgs e)
        {
            UpdateBoardDisplay();
        }

        private async Task Surrender()
        {
            try
            {
                var field = BuildChessFieldForServer();
                field.IsGameOver = true;
                field.Winner = (myTeam == PlayerTeam.White) ? PlayerTeam.Black : PlayerTeam.White;

                var lobby = new Lobby
                {
                    id = lobbyId,
                    chessField = field
                };

                var json = JsonSerializer.Serialize(new LobbyEntity
                {
                    Id = lobby.id,
                    ChessFieldJson = JsonSerializer.Serialize(field)
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await http.PostAsync("api/updateChessField", content);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сдаче: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSurrender_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите сдаться?", "Сдача",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await Surrender();

                MessageBox.Show("Вы сдались. Победа противника!", "Игра окончена",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }

        private async Task UpdateServerChessField()
        {
            try
            {
                var field = BuildChessFieldForServer();
                var lobby = new Lobby
                {
                    id = lobbyId,
                    chessField = field
                };

                var json = JsonSerializer.Serialize(new LobbyEntity
                {
                    Id = lobby.id,
                    ChessFieldJson = JsonSerializer.Serialize(field)
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync("api/updateChessField", content);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Ошибка обновления поля на сервере", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при отправке на сервер: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async Task SendWinToServer()
        {
            try
            {
                var move = new Move
                {
                    UserId = playerId,
                    chessField = BuildChessFieldForServer()
                };
                var content = new StringContent(JsonSerializer.Serialize(move), Encoding.UTF8, "application/json");
                await http.PostAsync("api/Win", content);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при отправке победы: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ChessField BuildChessFieldForServer()
        {
            var field = new ChessField
            {
                Board = new List<List<ChessPiece>>(),
                CurrentPlayer = (myTeam == PlayerTeam.White) ? PlayerTeam.Black : PlayerTeam.White,
                IsGameOver = false,
                Winner = null
            };

            for (int i = 0; i < 8; i++)
            {
                var row = new List<ChessPiece>();
                for (int j = 0; j < 8; j++)
                {
                    var piece = board[i, j];
                    row.Add(piece ?? new ChessPiece());
                }
                field.Board.Add(row);
            }

            return field;
        }

        private void StartGamePolling()
        {
            pollCts?.Cancel();
            pollCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!pollCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var response = await http.GetAsync($"api/getChessField/{lobbyId}");
                        if (response.IsSuccessStatusCode)
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(body) && body != "null")
                            {
                                var serverField = JsonSerializer.Deserialize<ChessField>(body);
                                if (serverField != null)
                                {
                                    if (serverField.IsGameOver)
                                    {
                                        BeginInvoke(() =>
                                        {
                                            string winner = serverField.Winner == myTeam ? "Вы победили!" : "Вы проиграли!";
                                            ShowGameEndDialog(winner, "Игра окончена");
                                        });
                                        return;
                                    }

                                    if (serverField.CurrentPlayer == myTeam)
                                    {
                                        // Наш ход
                                        if (!BoardsAreEqual(serverField.Board, board))
                                        {
                                            BeginInvoke(() =>
                                            {
                                                board = ConvertToArray(serverField.Board);
                                                UpdateBoardDisplay();
                                                myTurn = true;
                                                currentPlayer = myTeam;
                                                UpdateGameInfo();
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Polling error: {ex.Message}");
                    }

                    await Task.Delay(1000, pollCts.Token);
                }
            }, pollCts.Token);
        }

        private bool BoardsAreEqual(List<List<ChessPiece>> serverBoard, ChessPiece[,] localBoard)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (serverBoard[i][j].Type != localBoard[i, j].Type ||
                        serverBoard[i][j].Team != localBoard[i, j].Team)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private ChessPiece[,] ConvertToArray(List<List<ChessPiece>> list)
        {
            var array = new ChessPiece[8, 8];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    array[i, j] = list[i][j];
            return array;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pollCts?.Cancel();
            http?.Dispose();
            base.OnFormClosed(e);
        }

        // Дополнительные методы для улучшения функциональности

        private bool IsInCheck(PlayerTeam team)
        {
            // Найти короля команды
            Point kingPosition = new Point(-1, -1);
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    ChessPiece piece = board[x, y];
                    if (piece != null && piece.Type == PieceType.King && piece.Team == team)
                    {
                        kingPosition = new Point(x, y);
                        break;
                    }
                }
                if (kingPosition.X != -1) break;
            }

            if (kingPosition.X == -1) return false; // Король не найден

            // Проверить, может ли любая фигура противника атаковать короля
            PlayerTeam opponentTeam = team == PlayerTeam.White ? PlayerTeam.Black : PlayerTeam.White;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    ChessPiece piece = board[x, y];
                    if (piece != null && piece.Team == opponentTeam)
                    {
                        List<Point> moves = GetValidMoves(piece);
                        if (moves.Contains(kingPosition))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsMoveLegal(ChessPiece piece, Point from, Point to)
        {
            // Сохранить текущее состояние
            ChessPiece originalPiece = board[to.X, to.Y];
            ChessPiece movingPiece = board[from.X, from.Y];

            // Сделать временный ход
            board[to.X, to.Y] = movingPiece;
            board[from.X, from.Y] = null;

            // Проверить, остается ли король под шахом
            bool isLegal = !IsInCheck(piece.Team);

            // Восстановить состояние
            board[from.X, from.Y] = movingPiece;
            board[to.X, to.Y] = originalPiece;

            return isLegal;
        }

        private void HighlightCheck()
        {
            // Подсветить короля, если он под шахом
            if (IsInCheck(myTeam))
            {
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        ChessPiece piece = board[x, y];
                        if (piece != null && piece.Type == PieceType.King && piece.Team == myTeam)
                        {
                            squares[x, y].BackColor = Color.Red;
                            return;
                        }
                    }
                }
            }
        }

        private bool CanCastle(PlayerTeam team, bool kingSide)
        {
            int row = team == PlayerTeam.White ? 7 : 0;
            int kingCol = 4;
            int rookCol = kingSide ? 7 : 0;

            // Проверить, что король и ладья не двигались
            ChessPiece king = board[row, kingCol];
            ChessPiece rook = board[row, rookCol];

            if (king == null || king.Type != PieceType.King || king.HasMoved) return false;
            if (rook == null || rook.Type != PieceType.Rook || rook.HasMoved) return false;

            // Проверить, что между королем и ладьей нет фигур
            int start = Math.Min(kingCol, rookCol) + 1;
            int end = Math.Max(kingCol, rookCol);
            for (int col = start; col < end; col++)
            {
                if (board[row, col] != null) return false;
            }

            // Проверить, что король не под шахом
            if (IsInCheck(team)) return false;

            // Проверить, что король не пройдет через шах
            int direction = kingSide ? 1 : -1;
            for (int i = 1; i <= 2; i++)
            {
                int testCol = kingCol + direction * i;
                board[row, testCol] = king;
                board[row, kingCol] = null;

                bool inCheck = IsInCheck(team);

                board[row, kingCol] = king;
                board[row, testCol] = null;

                if (inCheck) return false;
            }

            return true;
        }

        private void PerformCastling(PlayerTeam team, bool kingSide)
        {
            int row = team == PlayerTeam.White ? 7 : 0;
            int kingCol = 4;
            int rookCol = kingSide ? 7 : 0;
            int newKingCol = kingSide ? 6 : 2;
            int newRookCol = kingSide ? 5 : 3;

            ChessPiece king = board[row, kingCol];
            ChessPiece rook = board[row, rookCol];

            // Переместить короля
            board[row, newKingCol] = king;
            board[row, kingCol] = null;
            king.X = row;
            king.Y = newKingCol;
            king.HasMoved = true;

            // Переместить ладью
            board[row, newRookCol] = rook;
            board[row, rookCol] = null;
            rook.X = row;
            rook.Y = newRookCol;
            rook.HasMoved = true;
        }

        private List<Point> GetEnPassantMoves(ChessPiece pawn)
        {
            List<Point> moves = new List<Point>();

            if (pawn.Type != PieceType.Pawn) return moves;

            int direction = pawn.Team == PlayerTeam.White ? -1 : 1;
            int enPassantRow = pawn.Team == PlayerTeam.White ? 3 : 4;

            if (pawn.X != enPassantRow) return moves;

            // Проверить соседние пешки противника
            for (int dy = -1; dy <= 1; dy += 2)
            {
                int checkY = pawn.Y + dy;
                if (IsValidPosition(pawn.X, checkY))
                {
                    ChessPiece adjacentPiece = board[pawn.X, checkY];
                    if (adjacentPiece != null &&
                        adjacentPiece.Type == PieceType.Pawn &&
                        adjacentPiece.Team != pawn.Team)
                    {
                        // Здесь нужно проверить, что пешка только что сделала двойной ход
                        // Для упрощения добавим ход взятия на проходе
                        Point enPassantSquare = new Point(pawn.X + direction, checkY);
                        if (IsValidPosition(enPassantSquare) && board[enPassantSquare.X, enPassantSquare.Y] == null)
                        {
                            moves.Add(enPassantSquare);
                        }
                    }
                }
            }

            return moves;
        }

        private void ShowGameEndDialog(string message, string title)
        {
            var result = MessageBox.Show(message + "\n\nХотите начать новую игру?", title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                // Здесь можно добавить логику для начала новой игры
                InitializeBoard();
                gameStarted = true;
                myTurn = (myTeam == PlayerTeam.White);
                currentPlayer = PlayerTeam.White;
                UpdateGameInfo();
            }
            else
            {
                this.Close();
            }
        }

        private void SaveGameState()
        {
            // Метод для сохранения состояния игры (можно реализовать позже)
            try
            {
                string gameState = JsonSerializer.Serialize(board);
                // Сохранить в файл или отправить на сервер
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения игры: {ex.Message}");
            }
        }

        private void LoadGameState(string gameState)
        {
            // Метод для загрузки состояния игры
            try
            {
                var loadedBoard = JsonSerializer.Deserialize<ChessPiece[,]>(gameState);
                if (loadedBoard != null)
                {
                    board = loadedBoard;
                    UpdateBoardDisplay();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки игры: {ex.Message}");
            }
        }
    }
}