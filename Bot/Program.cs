using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;
using System.Linq;

var token = Environment.GetEnvironmentVariable("botToken") ?? "botToken";

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(token, cancellationToken: cts.Token);

var me = await bot.GetMe();

Dictionary<long, int> usersScore = new Dictionary<long, int>();
Dictionary<long, List<DateTime>> usersPullTime= new Dictionary<long, List<DateTime>>();

var rnd = new Random();

bot.OnError += OnError;
bot.OnMessage += OnMessage;
bot.OnUpdate += OnUpdate;

Console.WriteLine($"{me.Username} успешно запущен. Нажмите enter для завершения работы.");
await Task.Delay(Timeout.Infinite, cts.Token);
cts.Cancel();

async Task OnError(Exception exception, HandleErrorSource source)
{
    Console.WriteLine(exception);
    await Task.Delay(3000);
}

async Task OnMessage(Message msg, UpdateType type)
{
    var text = msg.Text;
    if (text!.StartsWith("/")) 
    {
        string command;
        int index = text.IndexOf(' ');
        if (index < 0) command = text;
        else command = text.Substring(0, index);
        command = command.ToLower();
        int atIndex = command.LastIndexOf('@');
        if(atIndex > 0 && me.Username != null)
        {
            if(command.Substring(atIndex + 1) == me.Username)
            {
                command = command.Substring(0, atIndex);
            }
        }
        await OnCommand(command, msg);
    }
}

async Task OnCommand(string command, Message msg)
{
    switch (command) 
    {
        case "/start":
            await bot.SendMessage(msg.Chat, "Меню бота:\n/pull - Увеличить/уменьшить страпон.\n/stats - Отобразить статистику\n/reload - Обнулить результат.");
            break;
        case "/pull":
            long usersId = msg.From!.Id;
            if (usersScore.TryGetValue(usersId, out var score) && score < -100)
            {
                await bot.SendMessage(msg.Chat, $"<i>{msg.From!.Username}, ты больше не можешь участвовать — твой страпон ликвидирован 💀. Заплати выкуп, чтобы продолжить играть.</i>", ParseMode.Html);
                break;
            }
            if (IsHourPassed(msg.From!.Id))
                await SendBottons(msg);
            else
                await bot.SendMessage(msg.Chat, "Команду <i>/pull</i> можно использовать не более 5 раз в час. Попробуйте позже.", ParseMode.Html);
            break;
        case "/stats":
            await GetStatistics(msg);
            break;
        case "/reload":
            await ResetToZero(msg);
            break;
        default:
            await bot.SendMessage(msg.Chat, "Неизвестная команда.");
            break;
    }
}

async Task SendBottons(Message msg)
{
    int index1 = rnd.Next(2);
    int index2 = 1 - index1;
    string[] data = { "plus", "minus" };
    var keyboard = new InlineKeyboardMarkup()
        .AddButton(InlineKeyboardButton.WithCallbackData("🔴", data[index1]))
        .AddButton(InlineKeyboardButton.WithCallbackData("🔵", data[index2]));
    await bot.SendMessage(msg.Chat, "Выберите кнопку", replyMarkup: keyboard);
}

async Task OnUpdate(Update update)
{
    if(update.Type == UpdateType.CallbackQuery)
    {
        var callBack = update.CallbackQuery;
        long usersId = callBack!.From.Id;
        int points = rnd.Next(1, 11);
        usersScore.TryAdd(usersId, 0);
        await bot.EditMessageReplyMarkup(callBack.Message!.Chat.Id, callBack.Message.MessageId, null);
        if (callBack.Data == "plus")
        {
            usersScore[usersId] += points;
            await bot.SendMessage(callBack.Message!.Chat, $"<i>{callBack.From.Username}</i>, твой страпон увеличен на {points} 😊! Твой текущий результат: <b>{usersScore[usersId]}</b>.", ParseMode.Html);
        }
        else
        {
            usersScore[usersId] -= points;
            await bot.SendMessage(callBack.Message!.Chat, $"<i>{callBack.From.Username}</i>, твой страпон уменьшен на {points} 😢! Твой текущий результат: <b>{usersScore[usersId]}</b>.", ParseMode.Html);
        }
    }
}

async Task GetStatistics(Message msg)
{
    string statistics = "<b>🏆 Топ участников:</b>\n";
    var sortedList = usersScore
        .OrderByDescending(x => x.Value)
        .ToList();
    int count = 1;

    if(sortedList.Count == 0)
    {
        await bot.SendMessage(msg.Chat, "Ошибка. Никто не участвует в игре.");
        return;
    }

    foreach (var user in sortedList)
    {
        long usersId = user.Key;
        var userInfo = await bot.GetChatMember(msg.Chat, usersId);
        string userName = userInfo.User.Username ?? userInfo.User.FirstName;
        switch (count)
        {
            case 1:
                statistics += $"🥇 - <i>{userName}</i>. Текущий результат: <b>{user.Value}</b>.\n";
                break;
            case 2:
                statistics += $"🥈 - <i>{userName}</i>. Текущий результат: <b>{user.Value}</b>.\n";
                break;
            case 3:
                statistics += $"🥉 - <i>{userName}</i>. Текущий результат: <b>{user.Value}</b>.\n";
                break;
            default:
                statistics += $"{count} - <i>{userName}</i>. Текущий результат: <b>{user.Value}</b>.\n";
                break;
        }
        count++;
    }
    await bot.SendMessage(msg.Chat, statistics, ParseMode.Html);
}

async Task ResetToZero(Message msg)
{
    long usersId = msg.From!.Id;
    if (usersScore.TryGetValue(usersId, out int score) && score < 0)
    {
        await bot.SendMessage(msg.Chat.Id, "Ошибка! Нельзя обнулить отрицательный страпон.");
        return;
    }
    if (usersScore.ContainsKey(usersId))
    {
        usersScore[usersId] = 0;
        await bot.SendMessage(msg.Chat, $"<i>{msg.From!.Username}</i>, Твой страпон успешно обнулён!", ParseMode.Html);
    }
    else
    {
        await bot.SendMessage(msg.Chat, "Нельзя обнулить то, чего нет...");
    }
}

bool IsHourPassed(long usersId)
{
    var now = DateTime.Now;
    if (!usersPullTime.ContainsKey(usersId))
    {
        usersPullTime[usersId] = new List<DateTime>();
    }
    usersPullTime[usersId] = usersPullTime[usersId]
        .Where(t => (now - t).TotalHours < 1)
        .ToList();
    if (usersPullTime[usersId].Count < 5)
    {
        usersPullTime[usersId].Add(now);
        return true;
    }
    return false;
}
