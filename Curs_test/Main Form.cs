using Curs_test;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChessClient
{
    enum MessageType
    {
        GetName,
        GetLobbys,
        StartNewGame,
        ConnectToGame,
        GameStart,
        IsReady,
        MovingInGame,
        Win
    }

    class Message
    {
        public MessageType messageType { get; set; }
        public byte[] data { get; set; }
    }

    public class Lobby
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool isPassword { get; set; }
        public string password { get; set; }
        public int hostID { get; set; }
        public int usersID { get; set; }
        public bool final { get; set; }
        public ChessField chessField { get; set; }
    }

    public class LobbyEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsPassword { get; set; }
        public string Password { get; set; }
        public int HostID { get; set; }
        public int UsersID { get; set; }
        public bool Final { get; set; }
        public string ChessFieldJson { get; set; }
    }

    public enum team
    {
        white,
        black
    }
    public class ChessField
    {
        public List<List<ChessPiece>> Board { get; set; }
        public PlayerTeam CurrentPlayer { get; set; }
        public bool IsGameOver { get; set; }
        public PlayerTeam? Winner { get; set; }

        public ChessField()
        {
            Board = new List<List<ChessPiece>>();
            for (int i = 0; i < 8; i++)
            {
                var row = new List<ChessPiece>();
                for (int j = 0; j < 8; j++)
                    row.Add(new ChessPiece());
                Board.Add(row);
            }
            CurrentPlayer = PlayerTeam.White;
            IsGameOver = false;
            Winner = null;
        }

        public ChessField(List<List<ChessPiece>> board, PlayerTeam currentPlayer)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
            IsGameOver = false;
            Winner = null;
        }

    }

    public abstract class Chessman
    {
        protected int x, y;
        protected team team;
    }

    public class Move
    {
        public int UserId { get; set; }
        public int LobbyId { get; set; }
        public ChessField chessField { get; set; }
    }

    public partial class Form2 : Form
    {
        private readonly HttpClient http = new HttpClient();
        private const string baseUrl = "https://serverforchess-production.up.railway.app/";

        private string playerName = "";
        private int playerId = -1;
        private int currentLobbyId = -1;
        private int currentLobbyPlayerCount = 1;
        private CancellationTokenSource pollCts;
        private bool isHost = false;
        private List<Lobby> lobbies = new List<Lobby>();

        public Form2()
        {
            InitializeComponent();
            http.BaseAddress = new Uri(baseUrl);
            CreateAllControls();
            ShowLoginControls();
        }

        // LOGIN
        private Label lblTitle;
        private Label lblPlayerName;
        private TextBox txtPlayerName;
        private Button btnLogin;
        // LOBBY
        private Label lblWelcome;
        private DataGridView dataGridView1;
        private Button btnRefresh;
        private Button btnCreateLobby;
        private Button btnJoinLobby;
        // ADD LOBBY
        private Label lblLobbyName;
        private TextBox txtLobbyName;
        private CheckBox checkBox1;
        private Label lblPassword;
        private TextBox txtPassword;
        private Button btnCreateGame;
        private Button btnCancelCreate;
        // LOBBY OF GAME
        private Label lblGameStatus;
        private RichTextBox richTextBox1;
        private Button btnBackToLobby;
        private Button btnStart;
        private Label lblStatus;

        private void CreateAllControls()
        {
            // LOGIN
            lblTitle = new Label();
            lblTitle.Text = "Добро пожаловать в Chess Game";
            lblTitle.Font = new Font("Arial", 16, FontStyle.Bold);
            lblTitle.Location = new Point(200, 150);
            lblTitle.Size = new Size(400, 30);
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblTitle);

            lblPlayerName = new Label();
            lblPlayerName.Text = "Введите ваше имя:";
            lblPlayerName.Location = new Point(250, 200);
            lblPlayerName.Size = new Size(150, 20);
            this.Controls.Add(lblPlayerName);

            txtPlayerName = new TextBox();
            txtPlayerName.Location = new Point(250, 230);
            txtPlayerName.Size = new Size(200, 25);
            this.Controls.Add(txtPlayerName);

            btnLogin = new Button();
            btnLogin.Text = "Войти";
            btnLogin.Location = new Point(300, 270);
            btnLogin.Size = new Size(100, 30);
            btnLogin.Click += new EventHandler(this.btnLogin_Click);
            this.Controls.Add(btnLogin);

            // LOBBY
            lblWelcome = new Label();
            lblWelcome.Text = "Добро пожаловать!";
            lblWelcome.Location = new Point(10, 10);
            lblWelcome.Size = new Size(300, 25);
            lblWelcome.Font = new Font("Arial", 12, FontStyle.Bold);
            lblWelcome.Visible = false;
            this.Controls.Add(lblWelcome);

            dataGridView1 = new DataGridView();
            dataGridView1.Location = new Point(10, 50);
            dataGridView1.Size = new Size(500, 300);
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.Visible = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.Columns.Clear();
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Название лобби",
                ReadOnly = true,
                Width = 200
            });
            /*dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsPassword",
                HeaderText = "Пароль",
                ReadOnly = true,
                Width = 80
            });*/
            var passwordColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Пароль",
                ReadOnly = true,
                Width = 80
            };
            passwordColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns.Add(passwordColumn);
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "id",
                HeaderText = "ID",
                ReadOnly = true,
                Width = 50
            });
            dataGridView1.CellFormatting += (sender, e) =>
            {
                if (e.ColumnIndex == 1) // Колонка пароля
                {
                    var lobby = dataGridView1.Rows[e.RowIndex].DataBoundItem as Lobby;
                    if (lobby != null)
                    {
                        e.Value = lobby.isPassword ? "Да" : "Нет";
                        e.FormattingApplied = true;
                    }
                }
            };
            this.Controls.Add(dataGridView1);

            btnRefresh = new Button();
            btnRefresh.Text = "Обновить";
            btnRefresh.Location = new Point(520, 50);
            btnRefresh.Size = new Size(100, 30);
            btnRefresh.Visible = false;
            btnRefresh.Click += new EventHandler(this.btnRefresh_Click);
            this.Controls.Add(btnRefresh);

            btnCreateLobby = new Button();
            btnCreateLobby.Text = "Создать лобби";
            btnCreateLobby.Location = new Point(520, 90);
            btnCreateLobby.Size = new Size(100, 30);
            btnCreateLobby.Visible = false;
            btnCreateLobby.Click += new EventHandler(this.btnCreateLobby_Click);
            this.Controls.Add(btnCreateLobby);

            btnJoinLobby = new Button();
            btnJoinLobby.Text = "Присоединиться";
            btnJoinLobby.Location = new Point(520, 130);
            btnJoinLobby.Size = new Size(100, 30);
            btnJoinLobby.Visible = false;
            btnJoinLobby.Click += new EventHandler(this.btnJoinLobby_Click);
            this.Controls.Add(btnJoinLobby);

            // ADD LOBBY
            lblLobbyName = new Label();
            lblLobbyName.Text = "Название лобби:";
            lblLobbyName.Location = new Point(10, 370);
            lblLobbyName.Size = new Size(120, 20);
            lblLobbyName.Visible = false;
            this.Controls.Add(lblLobbyName);

            txtLobbyName = new TextBox();
            txtLobbyName.Location = new Point(140, 368);
            txtLobbyName.Size = new Size(150, 25);
            txtLobbyName.Visible = false;
            this.Controls.Add(txtLobbyName);

            checkBox1 = new CheckBox();
            checkBox1.Text = "Использовать пароль";
            checkBox1.Location = new Point(10, 400);
            checkBox1.Size = new Size(150, 20);
            checkBox1.Visible = false;
            checkBox1.CheckedChanged += new EventHandler(this.checkBox1_CheckedChanged);
            this.Controls.Add(checkBox1);

            lblPassword = new Label();
            lblPassword.Text = "Пароль:";
            lblPassword.Location = new Point(10, 430);
            lblPassword.Size = new Size(80, 20);
            lblPassword.Visible = false;
            this.Controls.Add(lblPassword);

            txtPassword = new TextBox();
            txtPassword.Location = new Point(100, 428);
            txtPassword.Size = new Size(150, 25);
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.Enabled = false;
            txtPassword.Visible = false;
            this.Controls.Add(txtPassword);

            btnCreateGame = new Button();
            btnCreateGame.Text = "Создать";
            btnCreateGame.Location = new Point(300, 400);
            btnCreateGame.Size = new Size(80, 30);
            btnCreateGame.Visible = false;
            btnCreateGame.Click += new EventHandler(this.btnCreateGame_Click);
            this.Controls.Add(btnCreateGame);

            btnCancelCreate = new Button();
            btnCancelCreate.Text = "Отмена";
            btnCancelCreate.Location = new Point(390, 400);
            btnCancelCreate.Size = new Size(80, 30);
            btnCancelCreate.Visible = false;
            btnCancelCreate.Click += new EventHandler(this.btnCancelCreate_Click);
            this.Controls.Add(btnCancelCreate);

            // LOBBY OF GAME
            lblGameStatus = new Label();
            lblGameStatus.Text = "Ожидание начала игры...";
            lblGameStatus.Location = new Point(10, 10);
            lblGameStatus.Size = new Size(400, 30);
            lblGameStatus.Font = new Font("Arial", 14, FontStyle.Bold);
            lblGameStatus.Visible = false;
            this.Controls.Add(lblGameStatus);

            richTextBox1 = new RichTextBox();
            richTextBox1.Location = new Point(10, 50);
            richTextBox1.Size = new Size(600, 400);
            richTextBox1.ReadOnly = true;
            richTextBox1.Visible = false;
            this.Controls.Add(richTextBox1);

            btnBackToLobby = new Button();
            btnBackToLobby.Text = "Вернуться в лобби";
            btnBackToLobby.Location = new Point(10, 470);
            btnBackToLobby.Size = new Size(150, 35);
            btnBackToLobby.Visible = false;
            btnBackToLobby.Click += new EventHandler(this.btnBackToLobby_Click);
            this.Controls.Add(btnBackToLobby);

            btnStart = new Button();
            btnStart.Text = "Старт";
            btnStart.Location = new Point(170, 470);
            btnStart.Size = new Size(80, 30);
            btnStart.Visible = false;
            btnStart.Click += new EventHandler(this.btnStart_Click);
            this.Controls.Add(btnStart);
        }

        private void ShowLoginControls()
        {
            HideAllControls();
            lblTitle.Visible = true;
            lblPlayerName.Visible = true;
            txtPlayerName.Visible = true;
            btnLogin.Visible = true;
        }

        private void ShowLobbyControls()
        {
            HideAllControls();
            lblWelcome.Visible = true;
            dataGridView1.Visible = true;
            btnRefresh.Visible = true;
            btnCreateLobby.Visible = true;
            btnJoinLobby.Visible = true;
        }

        private void ShowCreateLobbyControls()
        {
            lblLobbyName.Visible = true;
            txtLobbyName.Visible = true;
            checkBox1.Visible = true;
            lblPassword.Visible = true;
            txtPassword.Visible = true;
            btnCreateGame.Visible = true;
            btnCancelCreate.Visible = true;
        }

        private void HideCreateLobbyControls()
        {
            lblLobbyName.Visible = false;
            txtLobbyName.Visible = false;
            checkBox1.Visible = false;
            lblPassword.Visible = false;
            txtPassword.Visible = false;
            btnCreateGame.Visible = false;
            btnCancelCreate.Visible = false;
        }

        private void ShowGameControls()
        {
            HideAllControls();
            lblGameStatus.Visible = true;
            richTextBox1.Visible = true;
            btnBackToLobby.Visible = true;
            btnStart.Visible = true;
            btnStart.Enabled = true;
            btnStart.Text = "Старт";
        }

        private void HideAllControls()
        {
            foreach (Control control in this.Controls)
            {
                control.Visible = false;
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPlayerName.Text))
            {
                MessageBox.Show("Введите имя", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            playerName = txtPlayerName.Text.Trim();
            var content = new StringContent(JsonSerializer.Serialize(playerName), Encoding.UTF8, "application/json");

            try
            {
                var resp = await http.PostAsync("api/Name", content);
                resp.EnsureSuccessStatusCode();

                var body = await resp.Content.ReadAsStringAsync();
                playerId = int.Parse(body);

                lblWelcome.Text = $"Добро пожаловать, {playerName}! (ID: {playerId})";
                ShowLobbyControls();
                await RefreshLobbies();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось войти: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshLobbies();
        }

        private async Task RefreshLobbies()
        {
            try
            {
                var resp = await http.GetAsync("api/lobby");
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                var lobbyEntities = JsonSerializer.Deserialize<List<LobbyEntity>>(json) ?? new List<LobbyEntity>();
                lobbies = lobbyEntities.Select(e => new Lobby
                {
                    id = e.Id,
                    name = e.Name,
                    isPassword = e.IsPassword,
                    password = e.Password,
                    hostID = e.HostID,
                    usersID = e.UsersID,
                    final = e.Final,
                    chessField = null
                }).ToList();
                // Принудительное обновление DataGridView
                dataGridView1.SuspendLayout();
                dataGridView1.DataSource = null;
                dataGridView1.DataSource = lobbies;
                dataGridView1.Refresh();
                dataGridView1.ResumeLayout();

                // Дополнительно вызываем принудительное обновление
                ForceRefreshDataGridView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки лобби: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ForceRefreshDataGridView()
        {
            if (dataGridView1.DataSource != null)
            {
                dataGridView1.SuspendLayout();
                var currentSource = dataGridView1.DataSource;
                dataGridView1.DataSource = null;
                dataGridView1.DataSource = currentSource;
                dataGridView1.Refresh();
                dataGridView1.ResumeLayout();
            }
        }

        private void btnCreateLobby_Click(object sender, EventArgs e)
        {
            if (lblLobbyName.Visible)
                HideCreateLobbyControls();
            else
                ShowCreateLobbyControls();
        }

        private async void btnCreateGame_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLobbyName.Text))
            {
                MessageBox.Show("Введите название", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var lobby = new LobbyEntity
            {
                Name = txtLobbyName.Text.Trim(),
                HostID = playerId,
                IsPassword = checkBox1.Checked,
                Password = checkBox1.Checked ? txtPassword.Text : "",
                ChessFieldJson = "{}"
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(lobby), Encoding.UTF8, "application/json");
                var resp = await http.PostAsync("api/startNewGame", content);
                resp.EnsureSuccessStatusCode();

                var idStr = await resp.Content.ReadAsStringAsync();
                currentLobbyId = int.Parse(idStr);
                isHost = true;
                await ConnectToOwnLobbyAsHost(currentLobbyId);
                HideCreateLobbyControls();
                txtLobbyName.Clear();
                txtPassword.Clear();
                checkBox1.Checked = false;
                ShowGameControls();
                lblGameStatus.Text = "Лобби создано, можно начинать игру…";
                richTextBox1.AppendText($"Лобби '{lobby.Name}' (ID:{currentLobbyId}) создано.\n");
                if (lobby.IsPassword)
                {
                    richTextBox1.AppendText("Лобби защищено паролем.\n");
                }
                richTextBox1.AppendText("Нажмите 'Старт' для начала игры.\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать лобби: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ConnectToOwnLobbyAsHost(int lobbyId)
        {
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(new[] { playerId, lobbyId }), Encoding.UTF8, "application/json");
                var resp = await http.PostAsync("api/connectiontogame", content);
                resp.EnsureSuccessStatusCode();
                richTextBox1.AppendText("Вы как хост тоже подключились к лобби.\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автоподключения хоста: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancelCreate_Click(object sender, EventArgs e)
        {
            HideCreateLobbyControls();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.Enabled = checkBox1.Checked;
        }

        private async void btnJoinLobby_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0 || dataGridView1.SelectedRows[0].DataBoundItem == null)
            {
                MessageBox.Show("Выберите лобби", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var sel = dataGridView1.SelectedRows[0].DataBoundItem as Lobby;
            if (sel == null || sel.id == 0)
            {
                MessageBox.Show("Невозможно подключиться", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (sel.isPassword && !string.IsNullOrEmpty(sel.password))
            {
                var enteredPassword = ShowPasswordDialog();
                if (string.IsNullOrEmpty(enteredPassword) || enteredPassword != sel.password)
                {
                    MessageBox.Show("Неверный пароль или отмена ввода", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(new[] { playerId, sel.id }), Encoding.UTF8, "application/json");
                var resp = await http.PostAsync("api/connectiontogame", content);
                resp.EnsureSuccessStatusCode();

                currentLobbyId = sel.id;
                isHost = false;
                ShowGameControls();
                lblGameStatus.Text = "Вы подключились. Ждём…";
                richTextBox1.AppendText($"Подключились к '{sel.name} {sel.id}'\n");

                StartPollingReady();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подключиться: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartPollingReady()
        {
            pollCts?.Cancel();
            pollCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!pollCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(2000, pollCts.Token);

                    try
                    {
                        var content = new StringContent(currentLobbyId.ToString(), Encoding.UTF8, "application/json");
                        var resp = await http.GetAsync($"api/areGameConnected/{currentLobbyId}");
                        resp.EnsureSuccessStatusCode();

                        var json = await resp.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(json) && json != "null")
                        {
                            BeginInvoke(async() =>
                            {
                                currentLobbyPlayerCount = 2;
                                lblGameStatus.Text = "Оба игрока подключены!";
                                if (!richTextBox1.Text.Contains("Оба игрока"))
                                    richTextBox1.AppendText("Оба игрока подключены! Запуск игры...\n");
                                //UpdateStartButtonState();
                                btnStart.Enabled = true;
                                btnStart.Text = "Старт";
                                await Task.Delay(1000);
                                await StartGame();
                            });
                            break;
                        }
                    }
                    catch { }
                }
            }, pollCts.Token);
        }

        private async Task StartGame()
        {
            try
            {
                pollCts?.Cancel();
                var selectedLobby = lobbies.FirstOrDefault(l => l.id == currentLobbyId);

                if (selectedLobby == null)
                {
                    MessageBox.Show("Лобби не найдено!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                PlayerTeam playerTeam = (selectedLobby.hostID == playerId) ? PlayerTeam.White : PlayerTeam.Black;
                richTextBox1.AppendText($"Игра началась! Вы играете за {(playerTeam == PlayerTeam.White ? "белых" : "черных")}\n");

                this.Hide();

                // Создаем форму игры
                Form1 gameForm = new Form1(playerId, currentLobbyId, playerTeam);

                // Обработчик закрытия формы игры
                gameForm.FormClosed += async (s, e) => {
                    if (isHost)
                    {
                        await DeleteLobby(currentLobbyId);
                    }
                    this.Show();
                    ShowLobbyControls();
                    await RefreshLobbies();
                };

                // Показываем форму игры модально
                gameForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить игру: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Show(); // Показываем главную форму в случае ошибки
            }
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            // Пример отправки хода
            var move = new Move { UserId = playerId, chessField = /* здесь ваше состояние */ null };
            _ = SendMove(move);
        }

        private async Task SendMove(Move move)
        {
            var content = new StringContent(JsonSerializer.Serialize(move), Encoding.UTF8, "application/json");
            var resp = await http.PostAsync("api/movingInGame", content);
            resp.EnsureSuccessStatusCode();

            richTextBox1.AppendText("Ход отправлен\n");
        }

        private string ShowPasswordDialog()
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Пароль",
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 240, Text = "Введите пароль:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 240, UseSystemPasswordChar = true };
            Button confirmation = new Button() { Text = "OK", Left = 100, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Отмена", Left = 190, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        private async void btnBackToLobby_Click(object sender, EventArgs e)
        {
            if (isHost)
            {
                await DeleteLobby(currentLobbyId);
            }
            pollCts?.Cancel();
            ShowLobbyControls();
            await RefreshLobbies();
        }

        private async Task WaitBothJoined(int lobbyId)
        {
            btnStart.Enabled = false;
            while (true)
            {
                await Task.Delay(1000);
                var resp = await http.PostAsync("api/areGameConnected",
                    new StringContent(JsonSerializer.Serialize(lobbyId), Encoding.UTF8, "application/json"));
                if (!resp.IsSuccessStatusCode) continue;

                var fieldJson = await resp.Content.ReadAsStringAsync();
                if (fieldJson != "null")
                {
                    btnStart.Enabled = true;
                    lblStatus.Text = "Оба в лобби — можно стартовать";
                    return;
                }
            }
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            /*if (currentLobbyPlayerCount < 2)
            {
                MessageBox.Show("Ожидаем второго игрока!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }*/

            await StartGame();
        }

        private async Task DeleteLobby(int lobbyId)
        {
            try
            {
                var content = new StringContent(lobbyId.ToString(), Encoding.UTF8, "application/json");
                await http.PostAsync("api/deleteLobby", content);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить лобби: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            // Отменяем все активные задачи при закрытии формы
            if (isHost && currentLobbyId != 1)
            {
                await DeleteLobby(currentLobbyId);
            }
            base.OnFormClosing(e);
        }
        /*private async void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                // Старт игры фактически на сервере — в вашей реализации отсутствует endpoint, но UI может вызвать публикацию хода
                richTextBox1.AppendText("Игра началась!\n");
                lblGameStatus.Text = "Игра началась!";
                btnStart.Enabled = false;
                Form1 form1 = new Form1(playerId, lobbyId, );
                form1.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось стартовать: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }*/
    }
}