using Curs_test;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        Win,
        ChatMessage,
        LobbyUpdate
    }

    class Message
    {
        public MessageType messageType { get; set; }
        public byte[] data { get; set; }
    }

    public class Lobby
    {
        public int id { get; set; }
        public string Name { get; set; }
        public bool IsPassword { get; set; }
        public string Password { get; set; }
        public int HostID { get; set; }
        public int UsersID { get; set; }
    }
}

namespace ChessClient
{
    public partial class Form2 : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private string playerName;
        private List<Lobby> lobbies = new List<Lobby>();
        private bool isInGame = false;
        private bool isHost = false;
        private int currentLobbyId = -1;

        public Form2()
        {
            CreateAllControls();
            InitializeComponent();
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
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.Visible = false;

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Название лобби",
                Width = 200
            });
            dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsPassword",
                HeaderText = "Пароль",
                Width = 80
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "id",
                HeaderText = "ID",
                Width = 50
            });
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
        }

        private void HideAllControls()
        {
            foreach (Control control in this.Controls)
            {
                control.Visible = false;
            }
        }

        private int currentLobbyPlayerCount = 1;

        private void UpdateStartButtonState()
        {
            btnStart.Text = $"{currentLobbyPlayerCount}/2";
            btnStart.Enabled = currentLobbyPlayerCount >= 2;
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPlayerName.Text))
            {
                MessageBox.Show("Пожалуйста, введите ваше имя!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            playerName = txtPlayerName.Text.Trim();

            try
            {
                await ConnectToServer();
                lblWelcome.Text = $"Добро пожаловать, {playerName}!";
                ShowLobbyControls();
                await RefreshLobbies();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ConnectToServer()
        {
            client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5050);
            stream = client.GetStream();

            var nameMessage = new Message
            {
                messageType = MessageType.GetName,
                data = Encoding.UTF8.GetBytes(playerName)
            };
            SendMessage(nameMessage);

            Task.Run(ListenForMessages);
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshLobbies();
        }

        private async Task RefreshLobbies()
        {
            try
            {
                var message = new Message
                {
                    messageType = MessageType.GetLobbys,
                    data = new byte[0]
                };
                SendMessage(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении лобби: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCreateLobby_Click(object sender, EventArgs e)
        {
            if (lblLobbyName.Visible)
                HideCreateLobbyControls();
            else
                ShowCreateLobbyControls();
        }

        private void btnCreateGame_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLobbyName.Text))
            {
                MessageBox.Show("Введите название лобби!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var lobby = new Lobby
            {
                Name = txtLobbyName.Text,
                IsPassword = checkBox1.Checked,
                Password = checkBox1.Checked ? txtPassword.Text : ""
            };

            var message = new Message
            {
                messageType = MessageType.StartNewGame,
                data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lobby))
            };

            SendMessage(message);

            isHost = true;

            HideCreateLobbyControls();
            ShowGameControls();
            lblGameStatus.Text = "Ожидание второго игрока...";
            richTextBox1.AppendText($"Лобби '{lobby.Name}' создано. Ожидание подключения второго игрока...\n");

            SendMessage(new Message
            {
                messageType = MessageType.LobbyUpdate,
                data = Encoding.UTF8.GetBytes($"{playerName} создал лобби '{lobby.Name}'.")
            });

        }

        private void btnCancelCreate_Click(object sender, EventArgs e)
        {
            HideCreateLobbyControls();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.Enabled = checkBox1.Checked;
        }

        private void btnJoinLobby_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите лобби для подключения!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedLobby = dataGridView1.SelectedRows[0].DataBoundItem as Lobby;
            if (selectedLobby == null) return;

            if (selectedLobby.UsersID >= 2)
            {
                MessageBox.Show("Лобби уже заполнено!", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selectedLobby.IsPassword)
            {
                string password = ShowPasswordDialog();
                if (string.IsNullOrEmpty(password))
                {
                    return;
                }

                if (password != selectedLobby.Password)
                {
                    MessageBox.Show("Неверный пароль!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var message = new Message
            {
                messageType = MessageType.ConnectToGame,
                data = BitConverter.GetBytes(selectedLobby.id)
            };

            SendMessage(message);

            isHost = false;
            currentLobbyId = selectedLobby.id;

            ShowGameControls();
            lblGameStatus.Text = "Подключение к игре...";
            richTextBox1.AppendText($"Вы подключились к лобби '{selectedLobby.Name}'...\n");
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

        private void btnBackToLobby_Click(object sender, EventArgs e)
        {
            isInGame = false;
            isHost = false;
            currentLobbyId = -1;
            ShowLobbyControls();
            Task.Run(async () => await RefreshLobbies());
        }

        private void btnStart_Click()
        {
            if (currentLobbyPlayerCount >= 2)
            {
                Form1 form1 = new Form1();
                form1.ShowDialog();
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (currentLobbyPlayerCount < 2) return;

            var startMessage = new Message
            {
                messageType = MessageType.GameStart,
                data = new byte[0]
            };
            SendMessage(startMessage);

            var gameForm = new Form1(stream, isHost);
            gameForm.Show();
            this.Hide();
        }

        private void SendMessage(Message message)
        {
            if (stream == null) return;

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                stream.Write(buffer);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки сообщения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ListenForMessages()
        {
            byte[] buffer = new byte[4096];

            try
            {
                while (client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string jsonData = Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('\0')[0];
                        var message = JsonSerializer.Deserialize<Message>(jsonData);

                        this.Invoke(new Action(() => ProcessServerMessage(message)));
                    }
                }
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() => {
                    if (!isInGame)
                        MessageBox.Show($"Соединение с сервером потеряно: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
                Form1 form1 = new Form1();
                form1.ShowDialog();
            }
        }

        private void ProcessServerMessage(Message message)
        {
            switch (message.messageType)
            {
                case MessageType.GetLobbys:
                    string lobbiesJson = Encoding.UTF8.GetString(message.data);
                    lobbies = JsonSerializer.Deserialize<List<Lobby>>(lobbiesJson) ?? new List<Lobby>();
                    dataGridView1.DataSource = null;
                    dataGridView1.DataSource = lobbies;
                    break;

                case MessageType.GameStart:
                    currentLobbyPlayerCount = BitConverter.ToInt32(message.data, 0);
                    isInGame = true;
                    UpdateStartButtonState();
                    lblGameStatus.Text = "Игра началась!";
                    richTextBox1.AppendText("Игра началась! Удачи!\n");
                    break;

                case MessageType.MovingInGame:
                    string moveData = Encoding.UTF8.GetString(message.data);
                    richTextBox1.AppendText($"Ход противника: {moveData}\n");
                    break;

                case MessageType.Win:
                    string winMessage = message.data.Length > 0 ? Encoding.UTF8.GetString(message.data) : "Игра окончена!";
                    richTextBox1.AppendText($"{winMessage}\n");
                    lblGameStatus.Text = "Игра завершена";
                    isInGame = false;
                    break;
            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch { }
        }
    }
}