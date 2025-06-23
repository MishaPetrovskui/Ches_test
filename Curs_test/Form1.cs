using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Text;
using System.IO;
using System.Text.Json;

namespace Curs_test
{
    public partial class Form1 : Form
    {
        private Button[,] cells = new Button[8, 8];
        private Button selectedButton = null;
        private bool isWhiteTurn = true;
        private List<Point> validMoves = new List<Point>();
        private Label statusLabel;
        private Label turnLabel;
        private ChessPiece[,] board = new ChessPiece[8, 8];
        private Panel gamePanel;
        private Button resetButton;

        public Form1()
        {
            InitializeComponent();
            InitializeForm();
            CreateGameInterface();
            GenerateBoard();
            SetupPieces();
            UpdateStatusLabels();
            CenterBoard();
        }

        private void InitializeForm()
        {
            this.Size = new Size(800, 700);
            this.MinimumSize = new Size(600, 500);
            this.Text = "Шахматы - Chess Game";
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Normal;
        }

        private void CreateGameInterface()
        {
            // Главная панель для игры
            gamePanel = new Panel
            {
                Size = new Size(480, 480),
                BackColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Панель статуса сверху
            Panel topStatusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };

            turnLabel = new Label
            {
                Text = "Ход: Белые",
                Location = new Point(20, 15),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            statusLabel = new Label
            {
                Text = "Выберите фигуру для хода",
                Location = new Point(20, 45),
                Size = new Size(400, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent
            };

            // Кнопки управления
            resetButton = new Button
            {
                Text = "Новая игра",
                Size = new Size(100, 30),
                Location = new Point(500, 15),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.Click += ResetButton_Click;

            topStatusPanel.Controls.AddRange(new Control[] { turnLabel, statusLabel, resetButton });

            // Панель снизу для дополнительной информации
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            Label infoLabel = new Label
            {
                Text = "Онлайн шахматы с сохранением на сервер",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray
            };

            bottomPanel.Controls.Add(infoLabel);

            this.Controls.AddRange(new Control[] { topStatusPanel, bottomPanel });
            this.Controls.Add(gamePanel);

            // Обработчик изменения размера окна
            this.Resize += Form1_Resize;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            CenterBoard();
        }

        private void CenterBoard()
        {
            if (tableLayoutPanel1 != null && gamePanel != null)
            {
                // Центрирование игровой панели
                int x = (this.ClientSize.Width - gamePanel.Width) / 2;
                int y = (this.ClientSize.Height - gamePanel.Height) / 2;
                gamePanel.Location = new Point(x, Math.Max(y - 40, 90)); // Учитываем верхнюю панель

                // Размещение TableLayoutPanel внутри игровой панели
                tableLayoutPanel1.Size = new Size(470, 470);
                tableLayoutPanel1.Location = new Point(5, 5);
            }
        }

        private void GenerateBoard()
        {
            if (gamePanel != null)
            {
                gamePanel.Controls.Add(tableLayoutPanel1);
            }

            tableLayoutPanel1.Controls.Clear();
            tableLayoutPanel1.RowCount = 8;
            tableLayoutPanel1.ColumnCount = 8;
            tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;

            // Очистка стилей
            tableLayoutPanel1.RowStyles.Clear();
            tableLayoutPanel1.ColumnStyles.Clear();

            for (int row = 0; row < 8; row++)
            {
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5f));
                tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

                for (int col = 0; col < 8; col++)
                {
                    Button btn = new Button();
                    btn.Dock = DockStyle.Fill;
                    btn.Font = new Font("Segoe UI", 18, FontStyle.Bold);
                    btn.Margin = new Padding(0);
                    btn.Tag = new Point(row, col);

                    // Красивые цвета для шахматной доски
                    Color lightSquare = Color.FromArgb(240, 217, 181);
                    Color darkSquare = Color.FromArgb(181, 136, 99);

                    btn.BackColor = (row + col) % 2 == 0 ? lightSquare : darkSquare;
                    btn.Click += Cell_Click;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 0, 100);

                    // Эффект наведения
                    btn.MouseEnter += (s, e) => {
                        if (btn != selectedButton && !validMoves.Contains((Point)btn.Tag))
                        {
                            btn.BackColor = Color.FromArgb(200, btn.BackColor);
                        }
                    };

                    btn.MouseLeave += (s, e) => {
                        if (btn != selectedButton && !validMoves.Contains((Point)btn.Tag))
                        {
                            Point pos = (Point)btn.Tag;
                            btn.BackColor = GetOriginalColor(pos);
                        }
                    };

                    cells[row, col] = btn;
                    tableLayoutPanel1.Controls.Add(btn, col, row);
                }
            }
        }

        private void SetupPieces()
        {
            // Инициализация массива фигур
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    board[i, j] = null;
                }
            }

            // Используем Unicode символы для красивых фигур
            string[] blackPieces = { "♜", "♞", "♝", "♛", "♚", "♝", "♞", "♜" };
            string[] whitePieces = { "♖", "♘", "♗", "♕", "♔", "♗", "♘", "♖" };

            // Чёрные пешки
            for (int i = 0; i < 8; i++)
            {
                board[1, i] = new Pawn(PieceColor.Black);
                cells[1, i].Text = "♟";
                cells[1, i].ForeColor = Color.Black;
            }

            // Белые пешки
            for (int i = 0; i < 8; i++)
            {
                board[6, i] = new Pawn(PieceColor.White);
                cells[6, i].Text = "♙";
                cells[6, i].ForeColor = Color.White;
            }

            // Чёрные фигуры
            ChessPiece[] blackPieceTypes = {
                new Rook(PieceColor.Black), new Knight(PieceColor.Black), new Bishop(PieceColor.Black),
                new Queen(PieceColor.Black), new King(PieceColor.Black), new Bishop(PieceColor.Black),
                new Knight(PieceColor.Black), new Rook(PieceColor.Black)
            };

            for (int i = 0; i < 8; i++)
            {
                board[0, i] = blackPieceTypes[i];
                cells[0, i].Text = blackPieces[i];
                cells[0, i].ForeColor = Color.Black;
            }

            // Белые фигуры
            ChessPiece[] whitePieceTypes = {
                new Rook(PieceColor.White), new Knight(PieceColor.White), new Bishop(PieceColor.White),
                new Queen(PieceColor.White), new King(PieceColor.White), new Bishop(PieceColor.White),
                new Knight(PieceColor.White), new Rook(PieceColor.White)
            };

            for (int i = 0; i < 8; i++)
            {
                board[7, i] = whitePieceTypes[i];
                cells[7, i].Text = whitePieces[i];
                cells[7, i].ForeColor = Color.White;
            }
        }

        private async void Cell_Click(object sender, EventArgs e)
        {
            Button clicked = sender as Button;
            Point pos = (Point)clicked.Tag;

            await Task.Run(() =>
            {
                this.Invoke((Action)(() => ProcessCellClick(clicked, pos)));
            });
        }

        private void ProcessCellClick(Button clicked, Point pos)
        {
            if (selectedButton == null)
            {
                SelectPiece(clicked, pos);
            }
            else
            {
                MakeMove(clicked, pos);
            }
        }

        private void SelectPiece(Button clicked, Point pos)
        {
            ChessPiece piece = board[pos.X, pos.Y];

            if (piece != null && IsCorrectPlayerTurn(piece.Color))
            {
                selectedButton = clicked;
                clicked.BackColor = Color.FromArgb(255, 215, 0); // Золотой цвет для выбранной фигуры

                validMoves = GetValidMoves(pos.X, pos.Y);
                HighlightValidMoves();

                statusLabel.Text = $"Выбрана фигура: {GetPieceName(piece)}. Возможных ходов: {validMoves.Count}";
            }
            else if (piece != null)
            {
                statusLabel.Text = "Не ваш ход!";
            }
            else
            {
                statusLabel.Text = "Пустая клетка. Выберите фигуру.";
            }
        }

        private string GetPieceName(ChessPiece piece)
        {
            return piece.GetType().Name switch
            {
                "King" => "Король",
                "Queen" => "Ферзь",
                "Rook" => "Ладья",
                "Bishop" => "Слон",
                "Knight" => "Конь",
                "Pawn" => "Пешка",
                _ => "Фигура"
            };
        }

        private void MakeMove(Button clicked, Point to)
        {
            Point from = (Point)selectedButton.Tag;

            if (from.Equals(to))
            {
                ClearSelection();
                statusLabel.Text = "Выбор отменён";
                return;
            }

            if (IsValidMove(from, to))
            {
                ExecuteMove(from, to);
                ClearSelection();

                isWhiteTurn = !isWhiteTurn;
                UpdateStatusLabels();
                CheckGameEnd();
            }
            else
            {
                statusLabel.Text = "Недопустимый ход!";
                ClearSelection();
            }
        }

        private void ExecuteMove(Point from, Point to)
        {
            ChessPiece piece = board[from.X, from.Y];
            ChessPiece capturedPiece = board[to.X, to.Y];

            board[to.X, to.Y] = piece;
            board[from.X, from.Y] = null;
            piece.HasMoved = true;

            cells[to.X, to.Y].Text = cells[from.X, from.Y].Text;
            cells[to.X, to.Y].ForeColor = cells[from.X, from.Y].ForeColor;
            cells[from.X, from.Y].Text = "";

            string moveDescription = capturedPiece != null ?
                $"Взята фигура: {GetPieceName(capturedPiece)}" : "Ход выполнен";
            statusLabel.Text = moveDescription;
        }

        private void HighlightValidMoves()
        {
            foreach (Point move in validMoves)
            {
                if (cells[move.X, move.Y].BackColor != Color.FromArgb(255, 215, 0))
                {
                    cells[move.X, move.Y].BackColor = Color.FromArgb(144, 238, 144); // Светло-зеленый
                }
            }
        }

        private void ClearSelection()
        {
            selectedButton = null;
            validMoves.Clear();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    cells[row, col].BackColor = GetOriginalColor(new Point(row, col));
                }
            }
        }

        private Color GetOriginalColor(Point pos)
        {
            Color lightSquare = Color.FromArgb(240, 217, 181);
            Color darkSquare = Color.FromArgb(181, 136, 99);
            return (pos.X + pos.Y) % 2 == 0 ? lightSquare : darkSquare;
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Начать новую игру?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                isWhiteTurn = true;
                ClearSelection();
                SetupPieces();
                UpdateStatusLabels();
            }
        }

        // Остальные методы игровой логики остаются без изменений
        private List<Point> GetValidMoves(int x, int y)
        {
            ChessPiece piece = board[x, y];
            if (piece == null) return new List<Point>();

            var moves = piece.GetValidMoves(x, y, board);
            return moves.Where(move => !WouldBeInCheckAfterMove(x, y, move.X, move.Y)).ToList();
        }

        private bool IsValidMove(Point from, Point to)
        {
            return validMoves.Any(move => move.X == to.X && move.Y == to.Y);
        }

        private bool IsCorrectPlayerTurn(PieceColor pieceColor)
        {
            return (isWhiteTurn && pieceColor == PieceColor.White) ||
                   (!isWhiteTurn && pieceColor == PieceColor.Black);
        }

        private void UpdateStatusLabels()
        {
            turnLabel.Text = $"Ход: {(isWhiteTurn ? "Белые" : "Чёрные")}";

            PieceColor currentPlayer = isWhiteTurn ? PieceColor.White : PieceColor.Black;
            if (IsInCheck(currentPlayer))
            {
                statusLabel.Text = "ШАХ!";
                statusLabel.ForeColor = Color.Red;
            }
            else
            {
                statusLabel.ForeColor = Color.LightGray;
            }
        }

        private void CheckGameEnd()
        {
            PieceColor currentPlayer = isWhiteTurn ? PieceColor.White : PieceColor.Black;

            if (IsCheckmate(currentPlayer))
            {
                string winner = isWhiteTurn ? "Чёрные" : "Белые";
                statusLabel.Text = $"МАТ! Победили {winner}!";
                statusLabel.ForeColor = Color.Gold;
                MessageBox.Show($"Игра окончена!\nПобедили {winner}!", "Конец игры",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DisableBoard();
            }
            else if (IsStalemate(currentPlayer))
            {
                statusLabel.Text = "ПАТ! Ничья!";
                statusLabel.ForeColor = Color.Orange;
                MessageBox.Show("Пат! Игра закончилась ничьей!", "Конец игры",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DisableBoard();
            }
        }

        private void DisableBoard()
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    cells[i, j].Enabled = false;
                }
            }
        }

        // Методы проверки игровой логики (без изменений)
        private bool IsInCheck(PieceColor color)
        {
            Point? kingPos = FindKing(color);
            if (kingPos == null) return false;

            PieceColor enemyColor = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
            return IsSquareUnderAttack(kingPos.Value.X, kingPos.Value.Y, enemyColor);
        }

        private bool IsCheckmate(PieceColor color)
        {
            return IsInCheck(color) && !HasValidMoves(color);
        }

        private bool IsStalemate(PieceColor color)
        {
            return !IsInCheck(color) && !HasValidMoves(color);
        }

        private bool HasValidMoves(PieceColor color)
        {
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    ChessPiece piece = board[x, y];
                    if (piece != null && piece.Color == color)
                    {
                        if (GetValidMoves(x, y).Count > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private Point? FindKing(PieceColor color)
        {
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    if (board[x, y] is King && board[x, y].Color == color)
                    {
                        return new Point(x, y);
                    }
                }
            }
            return null;
        }

        private bool IsSquareUnderAttack(int x, int y, PieceColor attackingColor)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    ChessPiece piece = board[i, j];
                    if (piece != null && piece.Color == attackingColor)
                    {
                        var moves = piece.GetValidMoves(i, j, board);
                        if (moves.Any(m => m.X == x && m.Y == y))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool WouldBeInCheckAfterMove(int fromX, int fromY, int toX, int toY)
        {
            ChessPiece piece = board[fromX, fromY];
            ChessPiece capturedPiece = board[toX, toY];

            board[toX, toY] = piece;
            board[fromX, fromY] = null;

            bool inCheck = IsInCheck(piece.Color);

            board[fromX, fromY] = piece;
            board[toX, toY] = capturedPiece;

            return inCheck;
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {
        }
    }

    // Классы шахматных фигур (остаются без изменений)
    public enum PieceColor
    {
        White,
        Black
    }

    public abstract class ChessPiece
    {
        public PieceColor Color { get; set; }
        public bool HasMoved { get; set; } = false;

        public ChessPiece(PieceColor color)
        {
            Color = color;
        }

        public abstract List<Point> GetValidMoves(int x, int y, ChessPiece[,] board);
    }

    public class King : ChessPiece
    {
        public King(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

            for (int i = 0; i < 8; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
                {
                    if (board[nx, ny] == null || board[nx, ny].Color != Color)
                    {
                        moves.Add(new Point(nx, ny));
                    }
                }
            }

            return moves;
        }
    }

    public class Queen : ChessPiece
    {
        public Queen(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

            for (int i = 0; i < 8; i++)
            {
                for (int step = 1; step < 8; step++)
                {
                    int nx = x + dx[i] * step;
                    int ny = y + dy[i] * step;

                    if (nx < 0 || nx >= 8 || ny < 0 || ny >= 8) break;

                    if (board[nx, ny] == null)
                    {
                        moves.Add(new Point(nx, ny));
                    }
                    else
                    {
                        if (board[nx, ny].Color != Color)
                        {
                            moves.Add(new Point(nx, ny));
                        }
                        break;
                    }
                }
            }

            return moves;
        }
    }

    public class Rook : ChessPiece
    {
        public Rook(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                for (int step = 1; step < 8; step++)
                {
                    int nx = x + dx[i] * step;
                    int ny = y + dy[i] * step;

                    if (nx < 0 || nx >= 8 || ny < 0 || ny >= 8) break;

                    if (board[nx, ny] == null)
                    {
                        moves.Add(new Point(nx, ny));
                    }
                    else
                    { // govno
                        if (board[nx, ny].Color != Color)
                        {
                            moves.Add(new Point(nx, ny));
                        }
                        break;
                    }
                }
            }

            return moves;
        }
    }

    public class Bishop : ChessPiece
    {
        public Bishop(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int[] dx = { -1, -1, 1, 1 };
            int[] dy = { -1, 1, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                for (int step = 1; step < 8; step++)
                {
                    int nx = x + dx[i] * step;
                    int ny = y + dy[i] * step;

                    if (nx < 0 || nx >= 8 || ny < 0 || ny >= 8) break;

                    if (board[nx, ny] == null)
                    {
                        moves.Add(new Point(nx, ny));
                    }
                    else
                    {
                        if (board[nx, ny].Color != Color)
                        {
                            moves.Add(new Point(nx, ny));
                        }
                        break;
                    }
                }
            }

            return moves;
        }
    }

    public class Knight : ChessPiece
    {
        public Knight(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int[] dx = { -2, -2, -1, -1, 1, 1, 2, 2 };
            int[] dy = { -1, 1, -2, 2, -2, 2, -1, 1 };

            for (int i = 0; i < 8; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
                {
                    if (board[nx, ny] == null || board[nx, ny].Color != Color)
                    {
                        moves.Add(new Point(nx, ny));
                    }
                }
            }

            return moves;
        }
    }

    public class Pawn : ChessPiece
    {
        public Pawn(PieceColor color) : base(color) { }

        public override List<Point> GetValidMoves(int x, int y, ChessPiece[,] board)
        {
            var moves = new List<Point>();
            int direction = Color == PieceColor.White ? -1 : 1;

            // Движение вперед
            if (x + direction >= 0 && x + direction < 8 && board[x + direction, y] == null)
            {
                moves.Add(new Point(x + direction, y));

                // Двойной ход с начальной позиции
                if (!HasMoved && x + 2 * direction >= 0 && x + 2 * direction < 8 && board[x + 2 * direction, y] == null)
                {
                    moves.Add(new Point(x + 2 * direction, y));
                }
            }

            // Атака по диагонали
            for (int dy = -1; dy <= 1; dy += 2)
            {
                int nx = x + direction;
                int ny = y + dy;

                if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8 && board[nx, ny] != null && board[nx, ny].Color != Color)
                {
                    moves.Add(new Point(nx, ny));
                }
            }

            return moves;
        }
    }
}