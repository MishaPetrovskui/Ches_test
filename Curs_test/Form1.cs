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
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;

    public class ChessClientApi
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ChessClientApi(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
        }

        public async Task<int> RegisterName(string name)
        {
            var content = new StringContent($"\"{name}\"", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/Name", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return int.Parse(json);
        }

        public async Task<int> StartNewGame(Lobby lobby)
        {
            var json = JsonSerializer.Serialize(lobby);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/startNewGame", content);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return int.Parse(result);
        }

        public async Task<List<Lobby>> GetLobbies()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/lobby");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Lobby>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<bool> ConnectToGame(int userId, int lobbyId)
        {
            var data = new int[] { userId, lobbyId };
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/connectiontogame", content);
            return response.IsSuccessStatusCode;
        }

        public async Task UpdateChessField(Lobby lobby)
        {
            var json = JsonSerializer.Serialize(new
            {
                Id = lobby.id,
                ChessFieldJson = JsonSerializer.Serialize(lobby.chessField),
                Final = lobby.final
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/updateChessField", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ChessField> GetChessField(int lobbyId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/getChessField/{lobbyId}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(json) ? new ChessField() :
                JsonSerializer.Deserialize<ChessField>(json);
        }

        public async Task<bool> CheckIfGameConnected(int lobbyId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/areGameConnected/{lobbyId}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return bool.Parse(json);
        }

        public async Task Surrender(int userId, int lobbyId)
        {
            var data = new int[] { userId, lobbyId };
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/surrender", content);
            response.EnsureSuccessStatusCode();
        }
    }

    // Основные модели данных
    public enum PieceType
    {
        None, Pawn, Rook, Knight, Bishop, Queen, King
    }

    public enum PlayerTeam
    {
        White, Black
    }

    public enum GameStatus
    {
        WaitingForPlayers,
        InProgress,
        Finished,
        Abandoned
    }

    public class GameStatusResponse
    {
        public bool IsGameOver { get; set; }
        public bool Final { get; set; }
        public PlayerTeam? Winner { get; set; }
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

        public ChessPiece Clone()
        {
            return new ChessPiece(Type, Team, X, Y) { HasMoved = HasMoved };
        }
    }

    public class GameState
    {
        public ChessPiece[,] Board { get; set; }
        public PlayerTeam CurrentPlayer { get; set; }
        public bool IsGameOver { get; set; }
        public PlayerTeam? Winner { get; set; }
        public Point? LastMoveFrom { get; set; }
        public Point? LastMoveTo { get; set; }
        public bool IsInCheck { get; set; }
        public int MoveCount { get; set; }
    }

    // Основная форма игры
    public partial class Form1 : Form
    {
        private readonly HttpClient httpClient;
        private const string BASE_URL = "https://serverforchess-production.up.railway.app/";

        // Игровые параметры
        private readonly int playerId;
        private readonly int lobbyId;
        private readonly PlayerTeam myTeam;
        private GameState gameState;

        // UI элементы
        private Panel boardPanel;
        private Button[,] squares;
        private Panel infoPanel;
        private Label lblGameInfo;
        private Label lblTurnInfo;
        private Label lblStatusInfo;
        private Button btnSurrender;
        private Button btnDrawOffer;
        private ProgressBar progressBarThinking;

        // Игровая логика
        private Point selectedSquare = new Point(-1, -1);
        private ChessPiece selectedPiece;
        private List<Point> validMoves = new List<Point>();
        private bool myTurn = false;
        private CancellationTokenSource pollingCts;

        // Константы для UI
        private const int SQUARE_SIZE = 70;
        private const int BOARD_OFFSET = 35;
        private static readonly Color LIGHT_SQUARE = Color.FromArgb(240, 217, 181);
        private static readonly Color DARK_SQUARE = Color.FromArgb(181, 136, 99);
        private static readonly Color SELECTED_SQUARE = Color.FromArgb(255, 255, 0);
        private static readonly Color VALID_MOVE = Color.FromArgb(144, 238, 144);
        private static readonly Color CHECK_HIGHLIGHT = Color.FromArgb(255, 99, 99);
        private static readonly Color LAST_MOVE_HIGHLIGHT = Color.FromArgb(255, 255, 204);

        public Form1(int playerId, int lobbyId, PlayerTeam team)
        {
            this.playerId = playerId;
            this.lobbyId = lobbyId;
            this.myTeam = team;

            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(BASE_URL);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            InitializeGameState();
            InitializeUI();
            StartGamePolling();
        }

        private void InitializeGameState()
        {
            gameState = new GameState
            {
                Board = new ChessPiece[8, 8],
                CurrentPlayer = PlayerTeam.White,
                IsGameOver = false,
                Winner = null,
                MoveCount = 0
            };

            myTurn = (myTeam == PlayerTeam.White);
            InitializeChessBoard();
        }

        private void InitializeUI()
        {
            SetupMainForm();
            SetupInfoPanel();
            SetupBoardPanel();
            SetupBoardSquares();
            AddBoardLabels();
            UpdateBoardDisplay();
            UpdateGameInfo();
        }

        private void SetupMainForm()
        {
            this.Text = $"Шахматы - Игрок {playerId} ({(myTeam == PlayerTeam.White ? "Белые" : "Черные")})";
            this.Size = new Size(950, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(240, 217, 181);
            this.FormClosing += ChessGameForm_FormClosing;
        }

        private void SetupInfoPanel()
        {
            infoPanel = new Panel
            {
                Location = new Point(650, 20),
                Size = new Size(270, 680),
                BackColor = Color.FromArgb(181, 136, 99),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(infoPanel);

            lblGameInfo = new Label
            {
                Text = "Шахматная партия",
                Font = new Font("Arial", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 20),
                Size = new Size(250, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(lblGameInfo);

            lblTurnInfo = new Label
            {
                Text = "Ход белых",
                Font = new Font("Arial", 12, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(10, 60),
                Size = new Size(250, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(lblTurnInfo);

            lblStatusInfo = new Label
            {
                Text = "Ожидание хода противника...",
                Font = new Font("Arial", 10, FontStyle.Regular),
                ForeColor = Color.LightGray,
                Location = new Point(10, 90),
                Size = new Size(250, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(lblStatusInfo);

            progressBarThinking = new ProgressBar
            {
                Location = new Point(10, 140),
                Size = new Size(250, 20),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            infoPanel.Controls.Add(progressBarThinking);

            btnSurrender = new Button
            {
                Text = "Сдаться",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(10, 580),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(139, 69, 19),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSurrender.Click += BtnSurrender_Click;
            infoPanel.Controls.Add(btnSurrender);

            btnDrawOffer = new Button
            {
                Text = "Предложить ничью",
                Font = new Font("Arial", 9, FontStyle.Bold),
                Location = new Point(140, 580),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(139, 69, 19),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDrawOffer.Click += BtnDrawOffer_Click;
            infoPanel.Controls.Add(btnDrawOffer);
        }

        private void SetupBoardPanel()
        {
            boardPanel = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(SQUARE_SIZE * 8 + BOARD_OFFSET * 2, SQUARE_SIZE * 8 + BOARD_OFFSET * 2),
                BackColor = Color.FromArgb(139, 69, 19),
                BorderStyle = BorderStyle.Fixed3D
            };
            this.Controls.Add(boardPanel);
        }

        private void SetupBoardSquares()
        {
            squares = new Button[8, 8];

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Button square = new Button
                    {
                        Size = new Size(SQUARE_SIZE, SQUARE_SIZE),
                        Location = new Point(col * SQUARE_SIZE + BOARD_OFFSET, row * SQUARE_SIZE + BOARD_OFFSET),
                        Font = new Font("Arial", 28, FontStyle.Bold),
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        Tag = new Point(row, col)
                    };

                    square.FlatAppearance.BorderSize = 2;
                    square.FlatAppearance.BorderColor = Color.Black;
                    square.BackColor = (row + col) % 2 == 0 ? LIGHT_SQUARE : DARK_SQUARE;

                    square.Click += Square_Click;
                    square.MouseEnter += Square_MouseEnter;
                    square.MouseLeave += Square_MouseLeave;

                    squares[row, col] = square;
                    boardPanel.Controls.Add(square);
                }
            }
        }

        private void AddBoardLabels()
        {
            // Буквы сверху и снизу
            for (int i = 0; i < 8; i++)
            {
                char letter = (char)('A' + i);

                Label topLabel = new Label
                {
                    Text = letter.ToString(),
                    Size = new Size(SQUARE_SIZE, 25),
                    Location = new Point(i * SQUARE_SIZE + BOARD_OFFSET, 5),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White
                };
                boardPanel.Controls.Add(topLabel);

                Label bottomLabel = new Label
                {
                    Text = letter.ToString(),
                    Size = new Size(SQUARE_SIZE, 25),
                    Location = new Point(i * SQUARE_SIZE + BOARD_OFFSET, BOARD_OFFSET + SQUARE_SIZE * 8 + 5),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White
                };
                boardPanel.Controls.Add(bottomLabel);
            }

            // Цифры слева и справа
            for (int i = 0; i < 8; i++)
            {
                int number = 8 - i;

                Label leftLabel = new Label
                {
                    Text = number.ToString(),
                    Size = new Size(25, SQUARE_SIZE),
                    Location = new Point(5, i * SQUARE_SIZE + BOARD_OFFSET),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White
                };
                boardPanel.Controls.Add(leftLabel);

                Label rightLabel = new Label
                {
                    Text = number.ToString(),
                    Size = new Size(25, SQUARE_SIZE),
                    Location = new Point(BOARD_OFFSET + SQUARE_SIZE * 8 + 5, i * SQUARE_SIZE + BOARD_OFFSET),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White
                };
                boardPanel.Controls.Add(rightLabel);
            }
        }

        private void InitializeChessBoard()
        {
            // Очистка доски
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    gameState.Board[i, j] = null;
                }
            }

            // Черные фигуры (верх доски)
            var blackPieces = new PieceType[] { PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook };
            for (int col = 0; col < 8; col++)
            {
                gameState.Board[0, col] = new ChessPiece(blackPieces[col], PlayerTeam.Black, 0, col);
                gameState.Board[1, col] = new ChessPiece(PieceType.Pawn, PlayerTeam.Black, 1, col);
            }

            // Белые фигуры (низ доски)
            var whitePieces = new PieceType[] { PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook };
            for (int col = 0; col < 8; col++)
            {
                gameState.Board[7, col] = new ChessPiece(whitePieces[col], PlayerTeam.White, 7, col);
                gameState.Board[6, col] = new ChessPiece(PieceType.Pawn, PlayerTeam.White, 6, col);
            }
        }

        private void UpdateBoardDisplay()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Button square = squares[row, col];
                    ChessPiece piece = gameState.Board[row, col];

                    // Установка текста фигуры
                    if (piece != null)
                    {
                        square.Text = GetPieceSymbol(piece);
                        square.ForeColor = piece.Team == PlayerTeam.White ? Color.WhiteSmoke : Color.Black;
                    }
                    else
                    {
                        square.Text = "";
                    }

                    // Сброс цвета клетки к базовому
                    square.BackColor = (row + col) % 2 == 0 ? LIGHT_SQUARE : DARK_SQUARE;
                }
            }

            // Подсветка последнего хода
            if (gameState.LastMoveFrom.HasValue && gameState.LastMoveTo.HasValue)
            {
                var from = gameState.LastMoveFrom.Value;
                var to = gameState.LastMoveTo.Value;
                squares[from.X, from.Y].BackColor = LAST_MOVE_HIGHLIGHT;
                squares[to.X, to.Y].BackColor = LAST_MOVE_HIGHLIGHT;
            }

            // Подсветка выбранной фигуры
            if (selectedSquare.X != -1 && selectedSquare.Y != -1)
            {
                squares[selectedSquare.X, selectedSquare.Y].BackColor = SELECTED_SQUARE;
            }

            // Подсветка возможных ходов
            foreach (Point move in validMoves)
            {
                squares[move.X, move.Y].BackColor = VALID_MOVE;
            }

            // Подсветка шаха
            if (gameState.IsInCheck)
            {
                Point kingPos = FindKing(gameState.CurrentPlayer);
                if (kingPos.X != -1)
                {
                    squares[kingPos.X, kingPos.Y].BackColor = CHECK_HIGHLIGHT;
                }
            }
        }

        private string GetPieceSymbol(ChessPiece piece)
        {
            string[] whiteSymbols = { "", "♙", "♖", "♘", "♗", "♕", "♔" };
            string[] blackSymbols = { "", "♟", "♜", "♞", "♝", "♛", "♚" };

            return piece.Team == PlayerTeam.White ? whiteSymbols[(int)piece.Type] : blackSymbols[(int)piece.Type];
        }

        private async void Square_Click(object sender, EventArgs e)
        {
            if (gameState.IsGameOver || !myTurn)
            {
                return;
            }

            Button clickedSquare = sender as Button;
            Point position = (Point)clickedSquare.Tag;
            ChessPiece clickedPiece = gameState.Board[position.X, position.Y];

            // Если выбрана наша фигура
            if (clickedPiece != null && clickedPiece.Team == myTeam)
            {
                SelectPiece(position, clickedPiece);
            }
            // Если кликнули по возможному ходу
            else if (selectedPiece != null && validMoves.Contains(position))
            {
                await MakeMove(selectedSquare, position);
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

        private async Task MakeMove(Point from, Point to)
        {
            try
            {
                // Сохранение информации о последнем ходе
                gameState.LastMoveFrom = from;
                gameState.LastMoveTo = to;

                // Выполнение хода
                ChessPiece movingPiece = gameState.Board[from.X, from.Y];
                ChessPiece capturedPiece = gameState.Board[to.X, to.Y];

                gameState.Board[to.X, to.Y] = movingPiece;
                gameState.Board[from.X, from.Y] = null;
                movingPiece.X = to.X;
                movingPiece.Y = to.Y;
                movingPiece.HasMoved = true;

                // Проверка на превращение пешки
                if (movingPiece.Type == PieceType.Pawn)
                {
                    if ((movingPiece.Team == PlayerTeam.White && to.X == 0) ||
                        (movingPiece.Team == PlayerTeam.Black && to.X == 7))
                    {
                        movingPiece.Type = PieceType.Queen; // Автоматическое превращение в ферзя
                    }
                }

                // Обновление игрового состояния
                gameState.CurrentPlayer = gameState.CurrentPlayer == PlayerTeam.White ? PlayerTeam.Black : PlayerTeam.White;
                gameState.MoveCount++;
                gameState.IsInCheck = IsInCheck(gameState.CurrentPlayer);

                // Проверка на окончание игры
                if (IsCheckmate(gameState.CurrentPlayer))
                {
                    gameState.IsGameOver = true;
                    gameState.Winner = myTeam;
                }
                else if (IsStalemate(gameState.CurrentPlayer))
                {
                    gameState.IsGameOver = true;
                    gameState.Winner = null; // Ничья
                }

                ClearSelection();
                myTurn = false;
                UpdateBoardDisplay();
                UpdateGameInfo();

                // Отправка хода на сервер
                await SendMoveToServer(from, to);

                if (gameState.IsGameOver)
                {
                    await HandleGameEnd();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении хода: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SendMoveToServer(Point from, Point to)
        {
            try
            {
                var chessField = BuildChessFieldForServer();
                var move = new Move
                {
                    UserId = playerId,
                    LobbyId = lobbyId,
                    chessField = chessField
                };

                var json = JsonSerializer.Serialize(move);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("api/movingInGame", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Сервер вернул ошибку: {response.StatusCode}. {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке хода на сервер: {ex.Message}", "Ошибка сети",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // Откат хода при ошибке сети
                // Здесь можно добавить логику отката
            }
        }

        private ChessField BuildChessFieldForServer()
        {
            var field = new ChessField
            {
                Board = new List<List<ChessPiece>>(),
                CurrentPlayer = gameState.CurrentPlayer,
                IsGameOver = gameState.IsGameOver,
                Winner = gameState.Winner
            };

            for (int i = 0; i < 8; i++)
            {
                var row = new List<ChessPiece>();
                for (int j = 0; j < 8; j++)
                {
                    var piece = gameState.Board[i, j];
                    row.Add(piece?.Clone() ?? new ChessPiece());
                }
                field.Board.Add(row);
            }

            return field;
        }

        private void StartGamePolling()
        {
            pollingCts?.Cancel();
            pollingCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!pollingCts.Token.IsCancellationRequested && !gameState.IsGameOver)
                {
                    try
                    {
                        await Task.Delay(1000, pollingCts.Token); // Опрос каждую секунду

                        var response = await httpClient.GetAsync($"api/getChessField/{lobbyId}");
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(json) && json != "null")
                            {
                                var serverField = JsonSerializer.Deserialize<ChessField>(json);
                                if (serverField != null)
                                {
                                    await ProcessServerUpdate(serverField);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Polling error: {ex.Message}");
                        // Продолжаем опрос даже при ошибках
                    }
                }
            }, pollingCts.Token);
        }

        private async Task ProcessServerUpdate(ChessField serverField)
        {
            try
            {
                // Проверка на окончание игры
                if (serverField.IsGameOver && !gameState.IsGameOver)
                {
                    BeginInvoke(() =>
                    {
                        gameState.IsGameOver = true;
                        gameState.Winner = serverField.Winner;
                        HandleGameEndFromServer(serverField);
                    });
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing server update: {ex.Message}");
            }
        }

        private void UpdateGameStateFromServer(ChessField serverField)
        {
            gameState.Board = ConvertToArray(serverField.Board);
            gameState.CurrentPlayer = serverField.CurrentPlayer;
            gameState.IsGameOver = serverField.IsGameOver;
            gameState.Winner = serverField.Winner;
            gameState.IsInCheck = IsInCheck(gameState.CurrentPlayer);
        }

        private ChessPiece[,] ConvertToArray(List<List<ChessPiece>> list)
        {
            var array = new ChessPiece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    var piece = list[i][j];
                    array[i, j] = (piece.Type == PieceType.None) ? null : piece.Clone();
                }
            }
            return array;
        }

        private void HandleGameEndFromServer(ChessField serverField)
        {
            string message;
            if (serverField.Winner == null)
            {
                message = "Игра завершена ничьей!";
            }
            else if (serverField.Winner == myTeam)
            {
                message = "Поздравляем! Вы победили!";
            }
            else
            {
                message = "Вы проиграли. Удачи в следующий раз!";
            }

            MessageBox.Show(message, "Игра завершена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnSurrender.Enabled = false;
            btnDrawOffer.Enabled = false;
        }

        private async Task HandleGameEnd()
        {
            try
            {
                if (gameState.Winner == myTeam)
                {
                    await SendGameResultToServer("win");
                }
                else if (gameState.Winner == null)
                {
                    await SendGameResultToServer("draw");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending game result: {ex.Message}");
            }
        }

        private async Task SendGameResultToServer(string result)
        {
            try
            {
                var gameResult = new
                {
                    UserId = playerId,
                    LobbyId = lobbyId,
                    Result = result,
                    ChessField = BuildChessFieldForServer()
                };

                var json = JsonSerializer.Serialize(gameResult);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string endpoint = result == "win" ? "api/Win" : "api/Draw";
                await httpClient.PostAsync(endpoint, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending game result to server: {ex.Message}");
            }
        }

        private void UpdateGameInfo()
        {
            string currentPlayerText = gameState.CurrentPlayer == PlayerTeam.White ? "Белые" : "Черные";
            lblTurnInfo.Text = $"Ход: {currentPlayerText}";

            if (gameState.IsGameOver)
            {
                if (gameState.Winner == null)
                {
                    lblStatusInfo.Text = "Игра завершена ничьей";
                }
                else
                {
                    string winner = gameState.Winner == PlayerTeam.White ? "Белые" : "Черные";
                    lblStatusInfo.Text = $"Победили {winner}!";
                }
                progressBarThinking.Visible = false;
            }
            else if (myTurn)
            {
                lblStatusInfo.Text = "Ваш ход";
                progressBarThinking.Visible = false;
            }
            else
            {
                lblStatusInfo.Text = "Ожидание хода противника...";
                progressBarThinking.Visible = true;
            }

            if (gameState.IsInCheck)
            {
                lblStatusInfo.Text += " (ШАХ!)";
            }
        }

        // Логика валидации ходов
        private List<Point> GetValidMoves(ChessPiece piece)
        {
            var moves = new List<Point>();

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

            // Фильтрация ходов, оставляющих короля под шахом
            return moves.Where(move => !WouldLeaveKingInCheck(piece, move)).ToList();
        }

        private List<Point> GetPawnMoves(ChessPiece pawn)
        {
            var moves = new List<Point>();
            int direction = pawn.Team == PlayerTeam.White ? -1 : 1;
            int startRow = pawn.Team == PlayerTeam.White ? 6 : 1;

            // Движение вперед
            int newRow = pawn.X + direction;
            if (IsValidPosition(newRow, pawn.Y) && gameState.Board[newRow, pawn.Y] == null)
            {
                moves.Add(new Point(newRow, pawn.Y));

                // Двойной ход с начальной позиции
                if (pawn.X == startRow)
                {
                    newRow = pawn.X + 2 * direction;
                    if (IsValidPosition(newRow, pawn.Y) && gameState.Board[newRow, pawn.Y] == null)
                    {
                        moves.Add(new Point(newRow, pawn.Y));
                    }
                }
            }

            // Атака по диагонали
            for (int colOffset = -1; colOffset <= 1; colOffset += 2)
            {
                int attackRow = pawn.X + direction;
                int attackCol = pawn.Y + colOffset;

                if (IsValidPosition(attackRow, attackCol))
                {
                    ChessPiece target = gameState.Board[attackRow, attackCol];
                    if (target != null && target.Team != pawn.Team)
                    {
                        moves.Add(new Point(attackRow, attackCol));
                    }
                }
            }

            return moves;
        }

        private List<Point> GetRookMoves(ChessPiece rook)
        {
            var moves = new List<Point>();

            // Горизонтальные и вертикальные направления
            int[,] directions = { { 0, 1 }, { 0, -1 }, { 1, 0 }, { -1, 0 } };

            for (int d = 0; d < 4; d++)
            {
                int rowDir = directions[d, 0];
                int colDir = directions[d, 1];

                for (int i = 1; i < 8; i++)
                {
                    int newRow = rook.X + i * rowDir;
                    int newCol = rook.Y + i * colDir;

                    if (!IsValidPosition(newRow, newCol))
                        break;

                    ChessPiece target = gameState.Board[newRow, newCol];
                    if (target == null)
                    {
                        moves.Add(new Point(newRow, newCol));
                    }
                    else
                    {
                        if (target.Team != rook.Team)
                        {
                            moves.Add(new Point(newRow, newCol));
                        }
                        break;
                    }
                }
            }

            return moves;
        }

        private List<Point> GetKnightMoves(ChessPiece knight)
        {
            var moves = new List<Point>();
            int[,] knightMoves = { {-2, -1}, {-2, 1}, {-1, -2}, {-1, 2},
                                  {1, -2}, {1, 2}, {2, -1}, {2, 1} };

            for (int i = 0; i < 8; i++)
            {
                int newRow = knight.X + knightMoves[i, 0];
                int newCol = knight.Y + knightMoves[i, 1];

                if (IsValidPosition(newRow, newCol))
                {
                    ChessPiece target = gameState.Board[newRow, newCol];
                    if (target == null || target.Team != knight.Team)
                    {
                        moves.Add(new Point(newRow, newCol));
                    }
                }
            }

            return moves;
        }

        private List<Point> GetBishopMoves(ChessPiece bishop)
        {
            var moves = new List<Point>();

            // Диагональные направления
            int[,] directions = { { -1, -1 }, { -1, 1 }, { 1, -1 }, { 1, 1 } };

            for (int d = 0; d < 4; d++)
            {
                int rowDir = directions[d, 0];
                int colDir = directions[d, 1];

                for (int i = 1; i < 8; i++)
                {
                    int newRow = bishop.X + i * rowDir;
                    int newCol = bishop.Y + i * colDir;

                    if (!IsValidPosition(newRow, newCol))
                        break;

                    ChessPiece target = gameState.Board[newRow, newCol];
                    if (target == null)
                    {
                        moves.Add(new Point(newRow, newCol));
                    }
                    else
                    {
                        if (target.Team != bishop.Team)
                        {
                            moves.Add(new Point(newRow, newCol));
                        }
                        break;
                    }
                }
            }

            return moves;
        }

        private List<Point> GetQueenMoves(ChessPiece queen)
        {
            var moves = new List<Point>();
            moves.AddRange(GetRookMoves(queen));
            moves.AddRange(GetBishopMoves(queen));
            return moves;
        }

        private List<Point> GetKingMoves(ChessPiece king)
        {
            var moves = new List<Point>();

            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int colOffset = -1; colOffset <= 1; colOffset++)
                {
                    if (rowOffset == 0 && colOffset == 0)
                        continue;

                    int newRow = king.X + rowOffset;
                    int newCol = king.Y + colOffset;

                    if (IsValidPosition(newRow, newCol))
                    {
                        ChessPiece target = gameState.Board[newRow, newCol];
                        if (target == null || target.Team != king.Team)
                        {
                            moves.Add(new Point(newRow, newCol));
                        }
                    }
                }
            }

            // Рокировка (упрощенная версия)
            if (!king.HasMoved && !IsInCheck(king.Team))
            {
                // Короткая рокировка
                if (CanCastle(king, true))
                {
                    moves.Add(new Point(king.X, king.Y + 2));
                }

                // Длинная рокировка
                if (CanCastle(king, false))
                {
                    moves.Add(new Point(king.X, king.Y - 2));
                }
            }

            return moves;
        }

        private bool CanCastle(ChessPiece king, bool shortCastle)
        {
            int rookCol = shortCastle ? 7 : 0;
            int direction = shortCastle ? 1 : -1;

            // Проверка ладьи
            ChessPiece rook = gameState.Board[king.X, rookCol];
            if (rook == null || rook.Type != PieceType.Rook || rook.HasMoved)
                return false;

            // Проверка пустых клеток между королем и ладьей
            int startCol = king.Y + direction;
            int endCol = shortCastle ? rookCol - 1 : rookCol + 1;

            for (int col = Math.Min(startCol, endCol); col <= Math.Max(startCol, endCol); col++)
            {
                if (gameState.Board[king.X, col] != null)
                    return false;
            }

            // Проверка, что король не проходит через атакованные поля
            for (int col = king.Y; col != king.Y + 2 * direction + direction; col += direction)
            {
                if (IsSquareUnderAttack(new Point(king.X, col), GetOppositeTeam(king.Team)))
                    return false;
            }

            return true;
        }

        private bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        private bool WouldLeaveKingInCheck(ChessPiece piece, Point move)
        {
            // Создание копии доски для симуляции хода
            ChessPiece[,] tempBoard = new ChessPiece[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    tempBoard[i, j] = gameState.Board[i, j]?.Clone();
                }
            }

            // Выполнение временного хода
            tempBoard[move.X, move.Y] = tempBoard[piece.X, piece.Y];
            tempBoard[piece.X, piece.Y] = null;

            // Проверка шаха после хода
            return IsKingInCheck(piece.Team, tempBoard);
        }

        private bool IsInCheck(PlayerTeam team)
        {
            return IsKingInCheck(team, gameState.Board);
        }

        private bool IsKingInCheck(PlayerTeam team, ChessPiece[,] board)
        {
            Point kingPos = FindKing(team, board);
            if (kingPos.X == -1)
                return false;

            return IsSquareUnderAttack(kingPos, GetOppositeTeam(team), board);
        }

        private Point FindKing(PlayerTeam team)
        {
            return FindKing(team, gameState.Board);
        }

        private Point FindKing(PlayerTeam team, ChessPiece[,] board)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = board[row, col];
                    if (piece != null && piece.Type == PieceType.King && piece.Team == team)
                    {
                        return new Point(row, col);
                    }
                }
            }
            return new Point(-1, -1);
        }

        private bool IsSquareUnderAttack(Point square, PlayerTeam attackingTeam)
        {
            return IsSquareUnderAttack(square, attackingTeam, gameState.Board);
        }

        private bool IsSquareUnderAttack(Point square, PlayerTeam attackingTeam, ChessPiece[,] board)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = board[row, col];
                    if (piece != null && piece.Team == attackingTeam)
                    {
                        if (CanPieceAttackSquare(piece, square, board))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool CanPieceAttackSquare(ChessPiece piece, Point target, ChessPiece[,] board)
        {
            switch (piece.Type)
            {
                case PieceType.Pawn:
                    return CanPawnAttack(piece, target);
                case PieceType.Rook:
                    return CanRookAttack(piece, target, board);
                case PieceType.Knight:
                    return CanKnightAttack(piece, target);
                case PieceType.Bishop:
                    return CanBishopAttack(piece, target, board);
                case PieceType.Queen:
                    return CanRookAttack(piece, target, board) || CanBishopAttack(piece, target, board);
                case PieceType.King:
                    return CanKingAttack(piece, target);
                default:
                    return false;
            }
        }

        private bool CanPawnAttack(ChessPiece pawn, Point target)
        {
            int direction = pawn.Team == PlayerTeam.White ? -1 : 1;
            int attackRow = pawn.X + direction;

            return attackRow == target.X && Math.Abs(pawn.Y - target.Y) == 1;
        }

        private bool CanRookAttack(ChessPiece rook, Point target, ChessPiece[,] board)
        {
            if (rook.X != target.X && rook.Y != target.Y)
                return false;

            int rowDir = Math.Sign(target.X - rook.X);
            int colDir = Math.Sign(target.Y - rook.Y);

            int currentRow = rook.X + rowDir;
            int currentCol = rook.Y + colDir;

            while (currentRow != target.X || currentCol != target.Y)
            {
                if (board[currentRow, currentCol] != null)
                    return false;

                currentRow += rowDir;
                currentCol += colDir;
            }

            return true;
        }

        private bool CanKnightAttack(ChessPiece knight, Point target)
        {
            int rowDiff = Math.Abs(knight.X - target.X);
            int colDiff = Math.Abs(knight.Y - target.Y);

            return (rowDiff == 2 && colDiff == 1) || (rowDiff == 1 && colDiff == 2);
        }

        private bool CanBishopAttack(ChessPiece bishop, Point target, ChessPiece[,] board)
        {
            int rowDiff = Math.Abs(bishop.X - target.X);
            int colDiff = Math.Abs(bishop.Y - target.Y);

            if (rowDiff != colDiff)
                return false;

            int rowDir = Math.Sign(target.X - bishop.X);
            int colDir = Math.Sign(target.Y - bishop.Y);

            int currentRow = bishop.X + rowDir;
            int currentCol = bishop.Y + colDir;

            while (currentRow != target.X || currentCol != target.Y)
            {
                if (board[currentRow, currentCol] != null)
                    return false;

                currentRow += rowDir;
                currentCol += colDir;
            }

            return true;
        }

        private bool CanKingAttack(ChessPiece king, Point target)
        {
            int rowDiff = Math.Abs(king.X - target.X);
            int colDiff = Math.Abs(king.Y - target.Y);

            return rowDiff <= 1 && colDiff <= 1 && (rowDiff + colDiff > 0);
        }

        private PlayerTeam GetOppositeTeam(PlayerTeam team)
        {
            return team == PlayerTeam.White ? PlayerTeam.Black : PlayerTeam.White;
        }

        private bool IsCheckmate(PlayerTeam team)
        {
            if (!IsInCheck(team))
                return false;

            return !HasValidMoves(team);
        }

        private bool IsStalemate(PlayerTeam team)
        {
            if (IsInCheck(team))
                return false;

            return !HasValidMoves(team);
        }

        private bool HasValidMoves(PlayerTeam team)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = gameState.Board[row, col];
                    if (piece != null && piece.Team == team)
                    {
                        var moves = GetValidMoves(piece);
                        if (moves.Count > 0)
                            return true;
                    }
                }
            }
            return false;
        }

        // Обработчики событий UI
        private async void BtnSurrender_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите сдаться?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    await SendSurrenderToServer();
                    gameState.IsGameOver = true;
                    gameState.Winner = GetOppositeTeam(myTeam);

                    MessageBox.Show("Вы сдались.", "Игра завершена",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    btnSurrender.Enabled = false;
                    btnDrawOffer.Enabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при отправке сдачи: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnDrawOffer_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Предложить ничью сопернику?", "Предложение ничьи",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    await SendDrawOfferToServer();
                    MessageBox.Show("Предложение ничьи отправлено сопернику.", "Информация",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при отправке предложения ничьи: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task SendSurrenderToServer()
        {
            var surrenderData = new
            {
                UserId = playerId,
                LobbyId = lobbyId,
                ChessField = BuildChessFieldForServer()
            };

            var json = JsonSerializer.Serialize(surrenderData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("api/Surrender", content);
            response.EnsureSuccessStatusCode();
        }

        private async Task SendDrawOfferToServer()
        {
            var drawData = new
            {
                UserId = playerId,
                LobbyId = lobbyId,
                ChessField = BuildChessFieldForServer()
            };

            var json = JsonSerializer.Serialize(drawData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("api/OfferDraw", content);
            response.EnsureSuccessStatusCode();
        }

        private void Square_MouseEnter(object sender, EventArgs e)
        {
            Button square = sender as Button;
            if (!gameState.IsGameOver && myTurn)
            {
                square.Cursor = Cursors.Hand;
            }
        }

        private void Square_MouseLeave(object sender, EventArgs e)
        {
            Button square = sender as Button;
            square.Cursor = Cursors.Default;
        }

        private void ChessGameForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            pollingCts?.Cancel();
            httpClient?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pollingCts?.Cancel();
                pollingCts?.Dispose();
                httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}