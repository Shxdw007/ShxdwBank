using Spectre.Console;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

// --- НАСТРОЙКА КОНСОЛИ ---
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

using var db = new BankContext();
db.Database.EnsureCreated();

// Инициализация сервисов
var auth = new AuthService(db);
var audit = new AuditService(db);
var bank = new BankService(db, audit);
var app = new BankUi(bank, auth, audit);

await app.RunAsync();

// --- UI СЛОЙ ---
public class BankUi
{
    private readonly BankService _bank;
    private readonly AuthService _auth;
    private readonly AuditService _audit;
    private User _currentUser = null!; 

    public BankUi(BankService bank, AuthService auth, AuditService audit)
    {
        _bank = bank;
        _auth = auth;
        _audit = audit;
    }

    public async Task RunAsync()
    {
        // 1. Экран входа
        if (!await LoginScreenAsync()) return;

        // 2. Анимация загрузки 
        await BootSequenceAsync();

        // 3. Главное меню
        while (true)
        {
            AnsiConsole.Clear();
            RenderHeader();

            //Сделал так, что Админ видит логи, обычный юзер - нет
            var choices = new List<string>
            {
                "Список клиентов",
                "Добавить нового клиента",
                "Открыть счет",
                "Удалить счет",
                "Пополнить счет (Deposit)",
                "Снять со счета (Withdraw)",
                "Перевод (Transfer)",
                "История транзакций"
            };

            if (_currentUser.Role == "Admin")
            {
                choices.Add("[yellow]Аудит системы (Logs)[/]");
                choices.Add("[yellow]Создать пользователя[/]");
            }

            choices.Add("LIVE Dashboard");
            choices.Add("[red]LOGOUT[/]");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]///_BANK_OS_V4.0///[/]")
                    .PageSize(12)
                    .AddChoices(choices));

            switch (choice)
            {
                case "Список клиентов": ShowClients(); break;
                case "Добавить нового клиента": await AddClientAsync(); break;
                case "Открыть счет": await OpenAccountAsync(); break;
                case "Удалить счет": await DeleteAccountAsync(); break;
                case "Пополнить счет (Deposit)": await ProcessTransactionAsync(TransactionType.Deposit); break;
                case "Снять со счета (Withdraw)": await ProcessTransactionAsync(TransactionType.Withdraw); break;
                case "Перевод (Transfer)": await ProcessTransferAsync(); break;
                case "История транзакций": ShowHistory(); break;
                case "[yellow]Аудит системы (Logs)[/]": ShowAuditLogs(); break;
                case "[yellow]Создать пользователя[/]": await CreateUserAsync(); break;
                case "LIVE Dashboard": await LiveDashboardAsync(); break;
                case "[red]LOGOUT[/]":
                    _audit.Log(_currentUser.Username, "LOGOUT", "Сессия завершена");
                    AnsiConsole.MarkupLine("[red]Выход из системы...[/]");
                    return;
            }

            if (choice != "[red]LOGOUT[/]")
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Нажми любую клавишу...[/]");
                Console.ReadKey(true);
            }
        }
    }

    private async Task<bool> LoginScreenAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("SHXDW LOGIN").Color(Color.Green));

        // Если пользователей нет вообще - создаётся админ
        if (!_auth.HasUsers())
        {
            AnsiConsole.MarkupLine("[yellow]Система чиста. Создание root-администратора...[/]");
            _auth.Register("admin", "admin", "Admin");
            AnsiConsole.MarkupLine("[green]Root создан: admin / admin[/]\n");
        }

        while (true)
        {
            var login = AnsiConsole.Ask<string>("Username:");
            var pass = AnsiConsole.Prompt(
                new TextPrompt<string>("Password:")
                    .Secret('*')); 

            var user = _auth.Login(login, pass);
            if (user != null)
            {
                _currentUser = user;
                _audit.Log(user.Username, "LOGIN", "Успешный вход в систему");
                return true;
            }

            AnsiConsole.MarkupLine("[red]Ошибка доступа. Неверный логин или пароль.[/]");
            if (!AnsiConsole.Confirm("Попробовать снова?")) return false;
        }
    }

    private async Task CreateUserAsync()
    {
        AnsiConsole.MarkupLine("[bold]Регистрация нового оператора[/]");
        var login = AnsiConsole.Ask<string>("Новый логин:");
        if (_auth.UserExists(login)) { AnsiConsole.MarkupLine("[red]Такой пользователь уже есть![/]"); return; }

        var pass = AnsiConsole.Prompt(new TextPrompt<string>("Пароль:").Secret());
        var role = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Роль:").AddChoices("Operator", "Admin"));

        _auth.Register(login, pass, role);
        _audit.Log(_currentUser.Username, "USER_CREATE", $"Создан юзер {login} ({role})");
        AnsiConsole.MarkupLine($"[green]Пользователь {login} создан.[/]");
    }

    private void ShowAuditLogs()
    {
        var logs = _audit.GetLogs();
        var table = new Table().Border(TableBorder.Rounded).Title("[yellow]SYSTEM AUDIT LOGS[/]");
        table.AddColumn("Time");
        table.AddColumn("User");
        table.AddColumn("Action");
        table.AddColumn("Details");

        foreach (var log in logs.OrderByDescending(l => l.Date).Take(30))
        {
            var color = log.Action == "LOGIN" ? "green" : (log.Action.Contains("ERR") ? "red" : "white");
            table.AddRow(
                $"[grey]{log.Date:MM-dd HH:mm:ss}[/]",
                $"[bold]{log.Username}[/]",
                $"[{color}]{log.Action}[/]",
                Markup.Escape(log.Details)); 
        }
        AnsiConsole.Write(table);
    }

    private async Task BootSequenceAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("SHXDW BANK").LeftJustified().Color(Color.DeepSkyBlue1));
        var logs = new List<string> { "Auth Module: OK", "Loading Neural Core...", "Connecting to DarkNet...", "System Ready" };

        await AnsiConsole.Live(new Panel(new Markup("[grey]Booting...[/]"))).StartAsync(async ctx =>
        {
            var sb = new StringBuilder();
            foreach (var log in logs)
            {
                sb.AppendLine($"[green]OK[/] :: {log}");
                ctx.UpdateTarget(new Panel(new Markup(sb.ToString())).Header("KERNEL").BorderColor(Color.DeepSkyBlue1));
                await Task.Delay(200);
            }
        });
    }

    private void RenderHeader()
    {
        var roleColor = _currentUser.Role == "Admin" ? "red" : "green";
        var panel = new Panel(new Markup($"[deepskyblue1]User[/]: {_currentUser.Username} | [deepskyblue1]Role[/]: [{roleColor}]{_currentUser.Role}[/]"))
            .Header("[bold]SHXDW BANK SYSTEM[/]")
            .Border(BoxBorder.Heavy)
            .BorderStyle(Style.Parse("deepskyblue1"));
        AnsiConsole.Write(panel);
    }

    // --- Методы банка ---
    private void ShowClients()
    {
        var clients = _bank.GetAllClients();
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID"); table.AddColumn("Имя"); table.AddColumn("Счетов"); table.AddColumn("Баланс");
        foreach (var c in clients)
            table.AddRow(c.Id.ToString(), c.Name, c.Accounts.Count.ToString(), $"{c.Accounts.Sum(a => a.Balance):N2}");
        AnsiConsole.Write(table);
    }

    private async Task AddClientAsync()
    {
        var name = AnsiConsole.Ask<string>("Имя клиента:");
        var c = _bank.CreateClient(name);
        _audit.Log(_currentUser.Username, "CLIENT_ADD", $"Добавлен клиент {name} (ID: {c.Id})");
        AnsiConsole.MarkupLine($"[green]Клиент {name} создан.[/]");
    }

    private async Task OpenAccountAsync()
    {
        var client = SelectClient("Кому открыть счет?");
        if (client == null) return;
        var currency = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Валюта:").AddChoices("RUB", "USD", "EUR", "⬅ Назад"));
        if (currency == "⬅ Назад") return;

        var acc = _bank.CreateAccount(client.Id, currency);
        _audit.Log(_currentUser.Username, "ACC_OPEN", $"Открыт счет {acc.Number} для {client.Name}");
        AnsiConsole.MarkupLine($"Счет [bold]{acc.Number}[/] открыт.");
    }

    private async Task DeleteAccountAsync()
    {
        var acc = SelectAccount("Удалить счет:");
        if (acc == null) return;
        if (acc.Balance != 0) { AnsiConsole.MarkupLine("[red]Баланс не 0![/]"); return; }

        _bank.DeleteAccount(acc.Number);
        _audit.Log(_currentUser.Username, "ACC_DEL", $"Удален счет {acc.Number}");
        AnsiConsole.MarkupLine("[red]Счет удален.[/]");
    }

    private async Task ProcessTransactionAsync(TransactionType type)
    {
        var acc = SelectAccount($"Счет для {type}:");
        if (acc == null) return;
        var amount = AnsiConsole.Ask<decimal>("Сумма:");
        try
        {
            if (type == TransactionType.Deposit) _bank.Deposit(acc.Number, amount);
            else _bank.Withdraw(acc.Number, amount);
            _audit.Log(_currentUser.Username, type.ToString().ToUpper(), $"{amount} -> {acc.Number}");
            AnsiConsole.MarkupLine("[green]Успешно.[/]");
        }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Ошибка: {ex.Message}[/]"); }
    }

    private async Task ProcessTransferAsync()
    {
        var from = SelectAccount("Откуда:"); if (from == null) return;
        var to = SelectAccount("Куда:"); if (to == null) return;
        var amount = AnsiConsole.Ask<decimal>("Сумма:");
        try
        {
            _bank.Transfer(from.Number, to.Number, amount);
            _audit.Log(_currentUser.Username, "TRANSFER", $"{amount}: {from.Number} -> {to.Number}");
            AnsiConsole.MarkupLine("[green]Перевод выполнен.[/]");
        }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Ошибка: {ex.Message}[/]"); }
    }

    private void ShowHistory()
    {
        var acc = SelectAccount("История счета:");
        if (acc == null) return;
        var txs = _bank.GetTransactions(acc.Number);
        var table = new Table().Border(TableBorder.Simple).AddColumns("Дата", "Тип", "Сумма", "Инфо");
        foreach (var t in txs.OrderByDescending(x => x.Date).Take(15))
            table.AddRow(t.Date.ToString("g"), t.Type, $"{t.Amount:N2}", t.Comment);
        AnsiConsole.Write(table);
    }

    private async Task LiveDashboardAsync()
    {
        AnsiConsole.Clear();
        var table = new Table().Title("LIVE DB MONITOR [grey](Enter - выход)[/]").AddColumns("Клиент", "Счет", "Баланс");
        await AnsiConsole.Live(table).AutoClear(true).StartAsync(async ctx => {
            while (!Console.KeyAvailable)
            {
                table.Rows.Clear();
                foreach (var a in _bank.GetAllAccounts().OrderByDescending(a => a.Balance).Take(8))
                    table.AddRow(a.Client?.Name ?? "?", a.Number, $"{a.Balance:N2} {a.Currency}");
                table.Caption = new TableTitle($"Last Sync: {DateTime.Now:HH:mm:ss}");
                ctx.Refresh(); await Task.Delay(500);
            }
            Console.ReadKey(true);
        });
    }

    private Client? SelectClient(string title)
    {
        var all = _bank.GetAllClients(); if (!all.Any()) return null;
        var choices = all.Select(c => $"{c.Id}. {c.Name}").ToList(); choices.Add("⬅ Назад");
        var sel = AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).AddChoices(choices));
        return sel == "⬅ Назад" ? null : all.First(c => $"{c.Id}. {c.Name}" == sel);
    }
    private Account? SelectAccount(string title)
    {
        var all = _bank.GetAllAccounts(); if (!all.Any()) return null;
        var map = all.ToDictionary(a => $"{a.Number} | {a.Balance}", a => a);
        var choices = map.Keys.ToList(); choices.Add("⬅ Назад");
        var sel = AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).AddChoices(choices));
        return sel == "⬅ Назад" ? null : map[sel];
    }
}

// --- СЕРВИСЫ ---
public class AuthService
{
    private readonly BankContext _db;
    public AuthService(BankContext db) => _db = db;

    public bool HasUsers() => _db.Users.Any();
    public bool UserExists(string login) => _db.Users.Any(u => u.Username == login);

    public void Register(string username, string password, string role)
    {
        _db.Users.Add(new User { Username = username, PasswordHash = Hash(password), Role = role });
        _db.SaveChanges();
    }

    public User? Login(string username, string password)
    {
        var hash = Hash(password);
        return _db.Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);
    }

    private string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

public class AuditService
{
    private readonly BankContext _db;
    public AuditService(BankContext db) => _db = db;

    public void Log(string username, string action, string details)
    {
        _db.AuditLogs.Add(new AuditLog { Username = username, Action = action, Details = details, Date = DateTime.Now });
        _db.SaveChanges();
    }

    public List<AuditLog> GetLogs() => _db.AuditLogs.ToList();
}

public class BankService
{
    private readonly BankContext _db;
    private readonly AuditService _audit; 

    public BankService(BankContext db, AuditService audit) { _db = db; _audit = audit; }

    public List<Client> GetAllClients() => _db.Clients.Include(c => c.Accounts).ToList();
    public List<Account> GetAllAccounts() => _db.Accounts.Include(a => a.Client).ToList();

    public Client CreateClient(string name)
    {
        var c = new Client { Name = name }; _db.Clients.Add(c); _db.SaveChanges(); return c;
    }
    public Account CreateAccount(int clientId, string cur)
    {
        string num;
        do
        {
            num = $"SHX-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(10, 99)}";
        } while (_db.Accounts.Any(a => a.Number == num));

        var acc = new Account { Number = num, ClientId = clientId, Currency = cur };
        _db.Accounts.Add(acc);
        _db.SaveChanges();
        return acc;
    }
    public void DeleteAccount(string num) { var acc = GetAcc(num); _db.Accounts.Remove(acc); _db.SaveChanges(); }

    public void Deposit(string num, decimal amount)
    {
        var acc = GetAcc(num); acc.Balance += amount; AddTx(acc, "DEPOSIT", amount, "Cash In"); _db.SaveChanges();
    }
    public void Withdraw(string num, decimal amount)
    {
        var acc = GetAcc(num); if (acc.Balance < amount) throw new Exception("Мало денег");
        acc.Balance -= amount; AddTx(acc, "WITHDRAW", -amount, "Cash Out"); _db.SaveChanges();
    }
    public void Transfer(string f, string t, decimal amt)
    {
        var from = GetAcc(f); var to = GetAcc(t);
        if (from.Currency != to.Currency) throw new Exception("Разные валюты");
        if (from.Balance < amt) throw new Exception("Мало денег");
        from.Balance -= amt; to.Balance += amt;
        AddTx(from, "TR_OUT", -amt, $"To {to.Number}"); AddTx(to, "TR_IN", amt, $"From {from.Number}");
        _db.SaveChanges();
    }

    public List<Transaction> GetTransactions(string num) => _db.Transactions.Where(t => t.AccountNumber == num).ToList();
    private Account GetAcc(string n) => _db.Accounts.FirstOrDefault(a => a.Number == n) ?? throw new Exception("Счет не найден");
    private void AddTx(Account a, string type, decimal amt, string cmt) =>
        _db.Transactions.Add(new Transaction { AccountNumber = a.Number, Type = type, Amount = amt, Comment = cmt, Date = DateTime.Now });
}

// --- БД и МОДЕЛИ ---
public class BankContext : DbContext
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<User> Users { get; set; } 
    public DbSet<AuditLog> AuditLogs { get; set; } 

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=shxdw_bank_v4.db");
    protected override void OnModelCreating(ModelBuilder mb) => mb.Entity<Account>().HasIndex(u => u.Number).IsUnique();
}

public class Client { public int Id { get; set; } public string Name { get; set; } = null!; public List<Account> Accounts { get; set; } = new(); }
public class Account { public int Id { get; set; } public string Number { get; set; } = null!; public string Currency { get; set; } = "RUB"; public decimal Balance { get; set; } public int ClientId { get; set; } public Client? Client { get; set; } }
public class Transaction { public int Id { get; set; } public DateTime Date { get; set; } public string Type { get; set; } = null!; public decimal Amount { get; set; } public string Comment { get; set; } = null!; public string AccountNumber { get; set; } = null!; }

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = "Operator";
}

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Username { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Details { get; set; } = null!;
}
public enum TransactionType
{
    Deposit,
    Withdraw
}

