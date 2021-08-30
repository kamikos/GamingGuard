using Discord;
using Discord.Commands;
using Discord.Gateway;
using LiteDB;
using SteamAuth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using static DB;

namespace GamingGuard
{
    class Program
    {
        public static string prefix = "guard!";
        static void Main(string[] args)
        {
            string token;
            if (File.Exists("token.txt"))
            {
                token = File.ReadAllText("token.txt");
            }
            else
            {
                StreamWriter sw = File.CreateText("token.txt");
                sw.Write("paste bot token here");
                sw.Close();
                Process.Start("notepad.exe", "token.txt").WaitForExit();
                token = File.ReadAllText("token.txt");
            }
            if (File.Exists("prefix.txt"))
            {
                prefix = File.ReadAllText("prefix.txt");
            }
            DiscordSocketClient client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                Intents = DiscordGatewayIntent.Guilds | DiscordGatewayIntent.GuildMessages | DiscordGatewayIntent.GuildVoiceStates | DiscordGatewayIntent.GuildMessageReactions,
                ApiVersion = 7,
            });
            client.CreateCommandHandler(prefix);
            client.Login("Bot " + token);
            client.OnLoggedIn += Client_OnLoggedIn;
            Thread.Sleep(-1);
        }

        private static void Client_OnLoggedIn(DiscordSocketClient client, LoginEventArgs args)
        {
            Console.Clear();
            Console.WriteLine($"Logged in as {client.User.Username}#{client.User.Discriminator} with prefix `{client.CommandHandler.Prefix}`", Color.Lime);
        }

        [Command("add", "Add a account")]
        public class Add : CommandBase
        {
            [Parameter("accountName")]
            public string accountName { get; set; }
            [Parameter("guardSecret")]
            public string guardSecret { get; set; }
            public override void Execute()
            {
                SteamGuardAccount steamGuardAccount = new SteamGuardAccount();
                steamGuardAccount.SharedSecret = guardSecret;
                if (steamGuardAccount.GenerateSteamGuardCode() == null)
                {
                    Message.Channel.SendMessage("Seems like the secret is invalid");
                    return;
                }
                steamGuardAccount.GenerateSteamGuardCode();
                User owner = GetUserByDiscordId(Message.Author.User.Id);
                if (owner is null)
                {
                    owner = new DB.User() { DiscordId = Message.Author.User.Id, allowedAccounts = new List<ObjectId>(), ownedAccounts = new List<ObjectId>() };
                    AddUserToDb(owner);
                }

                Account acc = new Account()
                {
                    Owner = owner,
                    allowedUsers = new List<DB.User>() { owner },
                    AccountName = accountName,
                    SharedSecret = guardSecret,
                };
                DB.AddAccountToDb(acc);
                AddToOwnerList(owner, acc);
                Message.Channel.SendMessage($"Added `{accountName}` to <@{owner.DiscordId}> account\nUse `{prefix}get {accountName}` to get your steam guard code");
            }

            public override void HandleError(string parameterName, string providedValue, Exception exception)
            {
                if (providedValue == null)
                {
                    Message.Channel.SendMessage("Error: " + parameterName, false, null);
                    return;
                }
                base.Message.Channel.SendMessage(string.Concat(new string[]
                {
                "Error:  ",
                parameterName,
                "```css\n",
                (exception != null) ? exception.ToString() : null,
                "```"
                }), false, null);
            }
        }
        [Command("get", "Get guard code")]
        public class Get : CommandBase
        {
            [Parameter("accountName")]
            public string accountName { get; set; }

            public override void Execute()
            {
                Account acc = DB.GetAccountByName(accountName);
                try
                 {
                if (acc.allowedUsers.Where(x => x.DiscordId == Message.Author.User.Id).ToList().Count > 0)
                    {
                        SteamGuardAccount steamGuardAccount = new SteamGuardAccount();
                        steamGuardAccount.SharedSecret = acc.SharedSecret;
                        EmbedMaker embed = new EmbedMaker() { Description = $"Your steam guard code: {steamGuardAccount.GenerateSteamGuardCode()}" };
                        Message.Channel.SendMessage("", false, embed);
                    }
                    else
                    {
                        Message.Channel.SendMessage("You don't have permission to use this account");
                    }
                }
                catch (Exception ex)
                {
                    Message.Channel.SendMessage("You don't have permission to use this account");
                    return;
                }
            }

            public override void HandleError(string parameterName, string providedValue, Exception exception)
            {
                if (providedValue == null)
                {
                    Message.Channel.SendMessage("Error: " + parameterName, false, null);
                    return;
                }
                base.Message.Channel.SendMessage(string.Concat(new string[]
                {
                "Error:  ",
                parameterName,
                "```css\n",
                (exception != null) ? exception.ToString() : null,
                "```"
                }), false, null);
            }
        }
        [Command("allow", "allow other user to get guard for account")]
        public class Allow : CommandBase
        {
            [Parameter("accountName")]
            public string accountName { get; set; }

            [Parameter("users to allow mention/id")]
            public string users { get; set; }

            public override void Execute()
            {
                Account acc = DB.GetAccountByName(accountName);
                if (acc.Owner.DiscordId != Message.Author.User.Id)
                {
                    Message.Channel.SendMessage("You don't have permission to edit this account");
                    return;
                }
                if (Message.Mentions.Count > 0)
                {
                    foreach (DiscordUser discordUser in Message.Mentions)
                    {
                        User user = new User() { DiscordId = discordUser.Id, allowedAccounts = new List<ObjectId>(), ownedAccounts = new List<ObjectId>() };
                        AddUserToDb(user);
                        AddToAllowedLlist(user, acc);
                        acc.allowedUsers.Add(user);
                    }
                    UpdateAccountInDb(acc);
                }
                else
                {
                    try
                    {
                        User user = new User()
                        {
                            DiscordId = Convert.ToUInt64(users),
                            allowedAccounts = new List<ObjectId>(),
                            ownedAccounts = new List<ObjectId>()
                        };
                        AddUserToDb(user);
                        acc.allowedUsers.Add(user);
                        UpdateAccountInDb(acc);
                        AddToAllowedLlist(user, acc);
                    }
                    catch
                    {
                        Message.Channel.SendMessage("Invalid id");
                        return;
                    }
                }
                Message.Channel.SendMessage("Added!");
            }

            public override void HandleError(string parameterName, string providedValue, Exception exception)
            {
                if (providedValue == null)
                {
                    Message.Channel.SendMessage("Error: " + parameterName, false, null);
                    return;
                }
                base.Message.Channel.SendMessage(string.Concat(new string[]
                {
                "Error:  ",
                parameterName,
                "```css\n",
                (exception != null) ? exception.ToString() : null,
                "```"
                }), false, null);
            }
            [Command("help", "no")]
            public class Help : CommandBase
            {
                public override void Execute()
                {
                    EmbedMaker embed = new EmbedMaker()
                    {
                        Title = "GamingGuard Help",
                        Color = Color.White,
                    };
                    foreach (var cmd in Client.CommandHandler.Commands.Values)
                    {
                        StringBuilder args = new StringBuilder();

                        foreach (var arg in cmd.Parameters)
                        {
                            if (arg.Optional)
                                args.Append($" <{arg.Name}>");
                            else
                                args.Append($" [{arg.Name}]");
                        }
                        if (cmd.Name == "help") { } else { embed.AddField(Client.CommandHandler.Prefix + cmd.Name + args, $"\n" + cmd.Description); };

                    }
                    Message.Channel.SendMessage("", false, embed);
                }
            }
            [Command("list", "show accounts you have access to")]
            public class List : CommandBase
            {
                public override void Execute()
                {
                    EmbedMaker embed = new EmbedMaker()
                    {
                        Title = $"{Message.Author.User.Username} list",
                        Color = Color.White,
                    };
                    StringBuilder owned = new StringBuilder();
                    User user = GetUserByDiscordId(Message.Author.User.Id);
                    if (user is null || user.ownedAccounts.Count == 0)
                    {
                        embed.AddField("Owned accounts", $"None use `{prefix}add [accountName] [guardSecret]` to add yours");
                    }
                    else
                    {
                        foreach (ObjectId id in user.ownedAccounts)
                        {
                            Account acc = GetAccountById(id);
                            owned.Append(acc.AccountName + "\n");
                        }
                        embed.AddField("Owned accounts", owned.ToString());
                    }
                    if (user is null || user.allowedAccounts.Count == 0)
                    {
                        embed.AddField("Allowed to use", $"None ask your freind to `{prefix}allow [accountName] [users to allow mention/id]` to allow you to use their accounts");
                    }
                    else
                    {
                        StringBuilder allowed = new StringBuilder();
                        foreach (ObjectId id in user.allowedAccounts)
                        {
                            Account acc = GetAccountById(id);
                            allowed.Append(acc.AccountName + "\n");
                        }
                        embed.AddField("Allowed to use", allowed.ToString());
                    }
                    Message.Channel.SendMessage("", false, embed);
                }
            }
        }
    }
}
public class DB
{
    private static string dbfilename = @"Filename=!database.db;connection=shared";


    public static void AddAccountToDb(Account account)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<Account> col = db.GetCollection<Account>("accounts");
            col.EnsureIndex(x => x.Id, true);
            col.Insert(account);
        }
    }
    public static void DeleteAccountFromDb(Account account)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<Account> col = db.GetCollection<Account>("accounts");
            col.Delete(account.Id);
        }
    }
    public static void UpdateAccountInDb(Account account)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<Account> col = db.GetCollection<Account>("accounts");
            col.Update(account);
        }
    }
    public static Account GetAccountByName(string accountName)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<Account> col = db.GetCollection<Account>("accounts");
            return col.FindOne(x => x.AccountName == accountName);
        }
    }
    public static Account GetAccountById(ObjectId id)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<Account> col = db.GetCollection<Account>("accounts");
            return col.FindById(id);
        }
    }

    public static IEnumerable<User> GetAllUsers()
    {
        LiteDatabase db = new LiteDatabase(dbfilename);

        return db.GetCollection<User>("users").FindAll();

    }
    public static void DeleteUserByDiscordId(ulong dcid)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<User> col = db.GetCollection<User>("users");
            User user = col.FindOne(x => x.DiscordId == dcid);
            col.Delete(user.Id);
        }
    }
    public static void AddUserToDb(User user)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<User> col = db.GetCollection<User>("users");
            col.EnsureIndex(x => x.Id, true);
            col.Insert(user);
        }
    }

    public static User GetUserByDiscordId(ulong discordId)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<User> col = db.GetCollection<User>("users");
            return col.FindOne(x => x.DiscordId == discordId);
        }
    }


    public static void AddToAllowedLlist(User user, Account account)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<User> col = db.GetCollection<User>("users");
            user.allowedAccounts.Add(account.Id);
            col.Update(user);
        }
    }
    public static void AddToOwnerList(User user, Account account)
    {
        using (LiteDatabase db = new LiteDatabase(dbfilename))
        {
            ILiteCollection<User> col = db.GetCollection<User>("users");
            user.ownedAccounts.Add(account.Id);
            col.Update(user);
        }
    }
    public class User
    {
        public ObjectId Id { get; set; }
        public ulong DiscordId { get; set; }
        // TODO: list of accounts that user owns
        public List<ObjectId> ownedAccounts { get; set; }
        // TODO: list of accounts that he can use
        public List<ObjectId> allowedAccounts { get; set; }
    }

    public class Account
    {
        public ObjectId Id { get; set; }
        public User Owner { get; set; }
        public string AccountName { get; set; }
        // TODO: dm owner a code used to change owner discord id
        public string RecoverySecret { get; set; }
        public string SharedSecret { get; set; }

        public List<User> allowedUsers { get; set; }
    }

}

