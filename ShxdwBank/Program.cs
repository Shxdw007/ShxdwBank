using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

using var db = new BankContext();
db.Database.EnsureCreated();

var bank = new BankService(db);
var app = new BankUi(bank);

await app.RunAsync();

public class BankUi
{
    private readonly BankService _bank;

    public BankUi(BankService bank)
    {
        _bank = bank;
    }

    public async Task RunAsync()
    {
        await BootSequenceAsync();

        while (true)
        {
            AnsiConsole.Clear();
            RenderHeader();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]///MENU_V3.0///[/]")
                    .PageSize(12)
                    .MoreChoicesText("[grey](листай вниз)[/]")
                    .AddChoices(new[]
                    {
                        "Список клиентов",
                        "Добавить нового клиента",
                        "Открыть счет",
                        "Удалить счет",
                        "Пополнить счет (Deposit)",
                        "Снять со счета (Withdraw)",
                        "Перевод (Transfer)",
                        "История транзакций",
                        "LIVE Dashboard",
                        "[red]EXIT_SYSTEM[/]"
                    }));

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
                case "LIVE Dashboard": await LiveDashboardAsync(); break;
                case "[red]EXIT_SYSTEM[/]":
                    AnsiConsole.MarkupLine("[bold red]Session terminated. Database connection closed.[/]");
                    return;
            }

            if (choice != "[red]EXIT_SYSTEM[/]")
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Нажми любую клавишу для возврата в меню...[/]");
                Console.ReadKey(true);
            }
        }
    }

    private async Task BootSequenceAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("SHXDW BANK").LeftJustified().Color(Color.DeepSkyBlue1));

        var logs = new List<string> { "Init EF Core Context...", "Migrating Database...", "Loading Entities...", "Starting UI..." };

        await AnsiConsole.Live(new Panel(new Markup("[grey]Booting...[/]")))
            .StartAsync(async ctx =>
            {
                var sb = new StringBuilder();
                var random = Random.Shared;

                foreach (var log in logs)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var glitch = GenerateSafeGlitch(log.Length);

                        ctx.UpdateTarget(
                            new Panel(new Markup($"{sb}[grey]{glitch}[/]"))
                                .Border(BoxBorder.Rounded)
                                .BorderStyle(new Style(Color.DeepSkyBlue1))
                                .Header("SHXDW_OS_KERNEL")
                        );
                        await Task.Delay(50);
                    }
                    sb.AppendLine($"[green]OK[/] :: {log}");
                    ctx.UpdateTarget(
                            new Panel(new Markup(sb.ToString()))
                                .Border(BoxBorder.Rounded)
                                .BorderStyle(new Style(Color.DeepSkyBlue1))
                                .Header("SHXDW_OS_KERNEL")
                        );
                    await Task.Delay(100);
                }
            });
    }

    private string GenerateSafeGlitch(int length)
    {
        const string chars = "!@#$%^&*()_+-={} |;':,./<>?";
        var rnd = Random.Shared;
        return new string(Enumerable.Repeat(chars, length).Select(s => s[rnd.Next(s.Length)]).ToArray());
    }

    private void RenderHeader()
    {
        var panel = new Panel(new Markup("[deepskyblue1]Post[/]: Admin | [deepskyblue1]User[/]: Shxdw"))
            .Header("[bold]SHXDW BANK SYSTEM[/]")
            .Border(BoxBorder.Heavy)
            .BorderStyle(Style.Parse("deepskyblue1"));
        AnsiConsole.Write(panel);
    }

    private void ShowClients()
    {
        var clients = _bank.GetAllClients();
        if (!clients.Any()) { AnsiConsole.MarkupLine("[yellow]База данных пуста.[/]"); return; }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Имя");
        table.AddColumn("Счетов");
        table.AddColumn("Баланс (Всего)");

        foreach (var c in clients)
        {
            var total = c.Accounts.Sum(a => a.Balance);
            table.AddRow(c.Id.ToString(), c.Name, c.Accounts.Count.ToString(), $"[green]{total:N2}[/]");
        }
        AnsiConsole.Write(table);
    }

    private async Task AddClientAsync()
    {
        AnsiConsole.MarkupLine("[bold]Добавление нового клиента[/]");
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>("Введите [deepskyblue1]Имя клиента[/] (или 'exit' для отмены):")
            .Validate(n => n.Length > 0));

        if (name.ToLower() == "exit") return;

        await AnsiConsole.Status().StartAsync("Сохранение в БД...", async _ =>
        {
            await Task.Delay(500);
            var c = _bank.CreateClient(name);
            AnsiConsole.MarkupLine($"[green]Успешно![/] Клиент {c.Name} (ID: {c.Id}) создан.");
        });
    }

    private async Task OpenAccountAsync()
    {
        var client = SelectClient("Кому открыть счет?");
        if (client == null) return; 

        var currency = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Выберите валюту:")
                .AddChoices("RUB", "USD", "EUR", "⬅ Назад")); 

        if (currency == "⬅ Назад") return;

        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Транзакция БД...", async _ =>
        {
            await Task.Delay(500);
            var acc = _bank.CreateAccount(client.Id, currency);
            AnsiConsole.MarkupLine($"Счет [bold]{acc.Number}[/] создан.");
        });
    }

    private async Task DeleteAccountAsync()
    {
        var acc = SelectAccount("Выберите счет для удаления:");
        if (acc == null) return;

        if (acc.Balance != 0)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка:[/] Баланс {acc.Balance}. Удаление невозможно.");
            return;
        }

        if (!AnsiConsole.Confirm($"Удалить счет {acc.Number}?")) return;

        _bank.DeleteAccount(acc.Number);
        AnsiConsole.MarkupLine("[red]Счет удален из БД.[/]");
    }

    private async Task ProcessTransactionAsync(TransactionType type)
    {
        var acc = SelectAccount($"Счет для {type}:");
        if (acc == null) return;

        var amount = AnsiConsole.Ask<decimal>("Введите сумму (0 для отмены):");
        if (amount == 0) return;

        try
        {
            await AnsiConsole.Status().StartAsync("Обработка...", async _ =>
            {
                await Task.Delay(300);
                if (type == TransactionType.Deposit) _bank.Deposit(acc.Number, amount);
                else _bank.Withdraw(acc.Number, amount);
            });
            var updatedAcc = _bank.GetAccount(acc.Number);
            AnsiConsole.MarkupLine($"[green]Успешно.[/] Баланс: {updatedAcc.Balance:N2} {updatedAcc.Currency}");
        }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Ошибка:[/] {ex.Message}"); }
    }

    private async Task ProcessTransferAsync()
    {
        var from = SelectAccount("Откуда переводим?");
        if (from == null) return;

        var to = SelectAccount("Куда переводим?");
        if (to == null) return;

        var amount = AnsiConsole.Ask<decimal>("Сумма перевода (0 для отмены):");
        if (amount == 0) return;

        try
        {
            _bank.Transfer(from.Number, to.Number, amount);
            AnsiConsole.MarkupLine("[green]Перевод записан в БД.[/]");
        }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Ошибка:[/] {ex.Message}"); }
    }

    private void ShowHistory()
    {
        var acc = SelectAccount("Выберите счет для просмотра истории:");
        if (acc == null) return; 

        var txs = _bank.GetTransactions(acc.Number); 

        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("Дата");
        table.AddColumn("Тип");
        table.AddColumn(new TableColumn("Сумма").RightAligned());
        table.AddColumn("Инфо");

        foreach (var t in txs.OrderByDescending(x => x.Date).Take(20))
        {
            var color = t.Amount > 0 ? "green" : "red";
            table.AddRow(t.Date.ToString("g"), t.Type, $"[{color}]{t.Amount:N2}[/]", t.Comment);
        }
        AnsiConsole.Write(table);
    }

    private Client? SelectClient(string title)
    {
        var clients = _bank.GetAllClients();
        if (!clients.Any()) { AnsiConsole.MarkupLine("[red]Нет клиентов.[/]"); return null; }

        var map = clients.ToDictionary(c => $"{c.Id}. {c.Name}", c => c);
        var choices = map.Keys.ToList();
        choices.Add("[red]⬅ Назад[/]"); 

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(choices));

        if (selected == "[red]⬅ Назад[/]") return null;
        return map[selected];
    }

    private Account? SelectAccount(string title)
    {
        var accounts = _bank.GetAllAccounts(); 
        if (!accounts.Any()) { AnsiConsole.MarkupLine("[red]Нет счетов.[/]"); return null; }

        var map = new Dictionary<string, Account>();
        foreach (var a in accounts)
        {
            var clientName = a.Client?.Name ?? "Unknown";
            map[$"{a.Number} | {clientName} | {a.Balance:N2} {a.Currency}"] = a;
        }

        var choices = map.Keys.ToList();
        choices.Add("[red]⬅ Назад[/]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .AddChoices(choices));

        if (selected == "[red]⬅ Назад[/]") return null;
        return map[selected];
    }

    private async Task LiveDashboardAsync()
    {
        AnsiConsole.Clear();
        var table = new Table().Title("LIVE DB MONITOR [grey](Нажми Enter для выхода)[/]");
        table.AddColumn("Клиент");
        table.AddColumn("Счет");
        table.AddColumn("Баланс");

        await AnsiConsole.Live(table).AutoClear(true).StartAsync(async ctx =>
        {
            while (!Console.KeyAvailable)
            {
                table.Rows.Clear();
                var top = _bank.GetAllAccounts().OrderByDescending(a => a.Balance).Take(8);

                foreach (var a in top)
                    table.AddRow(a.Client?.Name ?? "?", a.Number, $"[bold gold1]{a.Balance:N2}[/] {a.Currency}");

                table.Caption = new TableTitle($"[grey]Last Sync: {DateTime.Now:HH:mm:ss}[/]");
                ctx.Refresh();
                await Task.Delay(500);
            }
            Console.ReadKey(true);
        });
    }
}


public class BankContext : DbContext
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=shxdw_bank.db");
}

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public List<Account> Accounts { get; set; } = new();
}

public class Account
{
    public int Id { get; set; }

    public string Number { get; set; } = null!;

    public string Currency { get; set; } = "RUB";
    public decimal Balance { get; set; }

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public List<Transaction> Transactions { get; set; } = new();
}

public class Transaction
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Comment { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;
    public Account? Account { get; set; }
}

public class BankService
{
    private readonly BankContext _db;

    public BankService(BankContext db)
    {
        _db = db;
    }

    public List<Client> GetAllClients()
    {
        return _db.Clients.Include(c => c.Accounts).ToList();
    }

    public List<Account> GetAllAccounts()
    {
        return _db.Accounts.Include(a => a.Client).ToList();
    }

    public Client CreateClient(string name)
    {
        var client = new Client { Name = name };
        _db.Clients.Add(client);
        _db.SaveChanges();
        return client;
    }

    public Account CreateAccount(int clientId, string currency)
    {
        var number = $"SHX-{Random.Shared.Next(10000, 99999)}-{Random.Shared.Next(100, 999)}";
        var acc = new Account { Number = number, ClientId = clientId, Currency = currency, Balance = 0 };
        _db.Accounts.Add(acc);
        _db.SaveChanges();
        return acc;
    }

    public Account GetAccount(string number)
    {
        return _db.Accounts.FirstOrDefault(a => a.Number == number)
               ?? throw new Exception("Счет не найден");
    }

    public void DeleteAccount(string number)
    {
        var acc = GetAccount(number);
        _db.Accounts.Remove(acc);
        _db.SaveChanges();
    }

    public void Deposit(string number, decimal amount)
    {
        var acc = GetAccount(number);
        acc.Balance += amount;
        AddTx(acc, "DEPOSIT", amount, "ATM Deposit");
        _db.SaveChanges(); 
    }

    public void Withdraw(string number, decimal amount)
    {
        var acc = GetAccount(number);
        if (acc.Balance < amount) throw new Exception("Мало денег");
        acc.Balance -= amount;
        AddTx(acc, "WITHDRAW", -amount, "ATM Withdraw");
        _db.SaveChanges();
    }

    public void Transfer(string fromNum, string toNum, decimal amount)
    {
        var from = GetAccount(fromNum);
        var to = GetAccount(toNum);

        if (from.Currency != to.Currency) throw new Exception("Валюты не совпадают");
        if (from.Balance < amount) throw new Exception("Мало денег");

        from.Balance -= amount;
        to.Balance += amount;

        AddTx(from, "TRANSFER_OUT", -amount, $"To {to.Number}");
        AddTx(to, "TRANSFER_IN", amount, $"From {from.Number}");

        _db.SaveChanges();
    }

    public List<Transaction> GetTransactions(string accNum)
    {
        return _db.Transactions.Where(t => t.AccountNumber == accNum).ToList();
    }

    private void AddTx(Account acc, string type, decimal amount, string comment)
    {
        var tx = new Transaction
        {
            Date = DateTime.Now,
            Type = type,
            Amount = amount,
            Comment = comment,
            AccountNumber = acc.Number
        };
        _db.Transactions.Add(tx);
    }
}

public enum TransactionType { Deposit, Withdraw }
