﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sora_Bot_1.SoraBot.Services;

namespace Sora_Bot_1.SoraBot.Core
{
    public class CommandHandler
    {
        private DiscordSocketClient client;
        private CommandService commands;
        private DependencyMap map;
        private CommandHandler handler => this;
        private MusicService musicService;
        private ReminderService remService;
        public static Dictionary<ulong, string> prefixDict = new Dictionary<ulong, string>();
        private JsonSerializer jSerializer = new JsonSerializer();

        public async Task Install(DiscordSocketClient c)
        {
            InitializeLoader();
            LoadDatabase();
            client = c;

            musicService = new MusicService();
            //remService = new ReminderService();

            commands = new CommandService();
            map = new DependencyMap();

            map.Add(musicService);
            map.Add(handler);
            map.Add(commands);
            //map.Add(remService);

            //Discover all of the commands in this assembly and load them
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
            //Hook the messagereceive event into our command handler
            client.MessageReceived += HandleCommand;
        }

        private void InitializeLoader()
        {
            jSerializer.Converters.Add(new JavaScriptDateTimeConverter());
            jSerializer.NullValueHandling = NullValueHandling.Ignore;
        }

        public void SaveDatabase()
        {
            using (StreamWriter sw = File.CreateText(@"guildPrefix.json"))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    jSerializer.Serialize(writer, prefixDict);
                }
            }
        }

        public void UpdateDict(ulong ID, string prefix)
        {
            if (prefixDict.ContainsKey(ID))
            {
                prefixDict[ID] = prefix;
            }
            else
            {
                prefixDict.Add(ID, prefix);
            }
        }

        public string GetPrefix(ulong ID)
        {
            if (prefixDict.ContainsKey(ID))
            {
                return prefixDict[ID];
            }
            else
            {
                return "$";
            }
        }

        private void LoadDatabase()
        {
            if (File.Exists("guildPrefix.json"))
            {
                using (StreamReader sr = File.OpenText(@"guildPrefix.json"))
                {
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        prefixDict = jSerializer.Deserialize<Dictionary<ulong, string>>(reader);
                    }
                }
            }
            else
            {
                File.Create("guildPrefix.json");
            }
        }


        public async Task HandleCommand(SocketMessage messageParam)
        {
            //Don't process the comand if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;


            //Create a command Context
            var context = new CommandContext(client, message);

            string prefix;
            if (!prefixDict.TryGetValue(context.Guild.Id, out prefix))
                prefix = "$";
            //create a number to track where the prefix ends and the command begins
            int argPos = 0;
            //Determine if the message is a command based on if it starts with ! or a mention prefix
            if (
                !(message.HasStringPrefix(prefix, ref argPos) ||
                  message.HasMentionPrefix(client.CurrentUser, ref argPos)))
                return;

            //Execute the command. (result does no indicate a return value
            // rather an object starting if the command executed successfully
            var result = await commands.ExecuteAsync(context, argPos, map);
            //if (!result.IsSuccess)
            //  await context.Channel.SendMessageAsync(result.ErrorReason);
        }
    }
}