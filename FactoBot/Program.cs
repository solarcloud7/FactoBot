using Discord;
using Discord.Commands;
using Discord.WebSocket;
using FactoBot.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FactoBot
{
    public class Program
    {
        private static IConfiguration Configuration { get; set; }

        public Process Factorio;

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        static void Main(string[] args)
        {
            try
            {
                //get config and parse it
                var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json");

                Configuration = builder.Build();
                Configuration.GetSection("App").Get<AppConfig>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
                return;
            }

            new Program().OnStart().GetAwaiter().GetResult();
        }

        public async Task OnStart()
        {
            // starts and connects to factorio
            StartFactorio();

            // starts discord.net bot
            await RunBotAsync();

            //accept input from cmd line
            await UserListenerAsync();
        }

        public async Task RunBotAsync()
        {

            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _services = new ServiceCollection()
                        .AddSingleton(_client)
                        .AddSingleton(_commands)
                        .BuildServiceProvider();

            string botToken = (IsDebug) ? AppConfig.DebugBotToken : AppConfig.BotToken;

            Debug($"{(IsDebug ? "DEBUG" : "PROD")}: Connecting to Discord using the token {botToken.Substring(0, 6)}***************");

            //event subscriptions
            _client.Log += Log;
            _client.Ready += Ready;

            await RegisterCommandsAsync();

            await _client.LoginAsync(Discord.TokenType.Bot, botToken);

            await _client.StartAsync();

            await _client.SetGameAsync("Factorio!");

            await UserListenerAsync();

            await Task.Delay(-1);
        }
        private Task Ready()
        {       
            return Task.CompletedTask;
        }
        public void StartFactorio()
        {
            //get info about factorio
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = $@"{AppConfig.FactorioDirectory}\Factorio.exe",
                Arguments = $"--start-server Server --server-settings \"{AppConfig.FactorioDirectory}\\server-settings.json\" -c {AppConfig.ConfigDirectory}\\config-server.ini",
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            //executes the server
            Factorio = Process.Start(startInfo);

            //register server events
            Factorio.OutputDataReceived += Factorio_OutputDataReceived;
            Factorio.ErrorDataReceived += Factorio_ErrorDataReceived;

            //start listening
            Factorio.BeginOutputReadLine();

        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);

            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        private void Factorio_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void Factorio_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
            var message = e.Data.Split("[CHAT]").ToList();
            if(message.Count > 1)
            {
                var guild = _client.Guilds.FirstOrDefault(x => x.Id == AppConfig.GuildId);
                var channel = guild?.TextChannels.FirstOrDefault(x => x.Id == AppConfig.ChannelId);
                if(channel != null)
                {
                    channel.SendMessageAsync(message[1]);
                }
                else
                {
                    Console.WriteLine("Invalid GuildId or ChannelId in appsettings");
                }
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            SocketCommandContext context;
            int argPos = 0;
            IResult result;
            string helpMessage;
            CommandInfo cmd;
            bool isPrivateMessage = (arg.Channel.GetType() == typeof(SocketDMChannel));

            // If there was no message or the source was a bot, simply do nothing.
            if (message == null || message.Author.IsBot) return;

            //print private messages
            if (isPrivateMessage)
            {
                var m = $"DM {DateTime.Now.ToString("M/d h:mm tt")} - {arg.Author.Username}: {message.Content}";
                Console.WriteLine(m);
            }


            if (message.HasStringPrefix("!", ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                // Retrieves the context of the message in a format that Discord will understand natively.
                context = new SocketCommandContext(_client, message);

                try
                {
                    result = await _commands.ExecuteAsync(context, argPos, _services);

                    if (result.Error == CommandError.BadArgCount)
                    {
                        // Invalid number of arguments provided; lookup that command to retrieve the built-in usage text.
                        cmd = _commands.Search(context, argPos).Commands[0].Command;

                        if (cmd.Remarks != "")
                        {
                            helpMessage = cmd.Remarks;
                        }
                        else
                        {
                            helpMessage = "Sorry, I need a bit more clarification.  Check !help if you need advice on running commands.";
                        }

                        throw new UserException(result.ErrorReason, helpMessage, UserException.EMOJI_PUZZLED);
                    }
                    else if (result.Error == CommandError.Exception && result is ExecuteResult)
                    {
                        // Splendid, already in an Exception format!
                        throw ((ExecuteResult)result).Exception;
                    }
                    else if (!result.IsSuccess)
                    {
                        Console.WriteLine(result.Error + ": " + result.ErrorReason);

                        cmd = _commands.Search(context, argPos).Commands[0].Command;

                        if (cmd.Remarks != "")
                        {
                            helpMessage = cmd.Remarks;
                        }
                        else
                        {
                            helpMessage = "Ummm, sorry, but I didn't understand that.  If you need advice, take a peek at !help.";
                        }

                        throw new UserException(result.ErrorReason, helpMessage, UserException.EMOJI_CRY);
                    }
                }
                catch (UserException ex)
                {
                    if (ex.Emoji != null)
                    {
                        await message.AddReactionAsync(new Emoji(ex.Emoji));
                    }

                    if (isPrivateMessage)
                    {
                        await message.Author.SendMessageAsync(ex.UserFriendlyMessage);
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync(ex.UserFriendlyMessage);
                    }
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    Console.WriteLine(ex.InnerException.Message);
                    Console.WriteLine(ex.InnerException.StackTrace);
                }
            }
        }
        static public void Debug(String message)
        {
            if (IsDebug)
                Console.WriteLine(message);
        }

        public static bool IsDebug
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        #region Dynamic Text Response
        private async Task UserListenerAsync()
        {
            var m = await GetInputAsync();
            if (!string.IsNullOrEmpty(m))
            {
                Factorio.StandardInput.WriteLine(m);
            }
            await UserListenerAsync();
        }

        public async Task<string> GetInputAsync()
        {
            return await Task.Run(() => Console.ReadLine());
        }
        #endregion
    }
}
