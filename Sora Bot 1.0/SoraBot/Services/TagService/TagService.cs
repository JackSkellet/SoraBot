﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Sora_Bot_1.SoraBot.Services.TagService
{
    public class TagService
    {
        private ConcurrentDictionary<ulong, List<TagStruct>> tagDict =
            new ConcurrentDictionary<ulong, List<TagStruct>>();

        private ConcurrentDictionary<ulong, GuildPermission> tagRestrictDict =
            new ConcurrentDictionary<ulong, GuildPermission>();

        private ConcurrentDictionary<string, GuildPermission> permissionsDict =
            new ConcurrentDictionary<string, GuildPermission>();


        private JsonSerializer jSerializer = new JsonSerializer();

        public TagService()
        {
            InitializeLoader();
            LoadDatabase();
            LoadDatabaseRestrict();
            permissionsDict.TryAdd("managechannels", GuildPermission.ManageChannels);
            permissionsDict.TryAdd("administrator", GuildPermission.Administrator);
            permissionsDict.TryAdd("kickmembers", GuildPermission.KickMembers);
            permissionsDict.TryAdd("banmembers", GuildPermission.BanMembers);
            permissionsDict.TryAdd("manageguild", GuildPermission.ManageGuild);
        }

        public async Task CreateTag(string entry, SocketCommandContext Context)
        {
            try
            {
                if (tagRestrictDict.ContainsKey(Context.Guild.Id))
                {
                    GuildPermission permiss;
                    tagRestrictDict.TryGetValue(Context.Guild.Id, out permiss);
                    if (!((SocketGuildUser) Context.User).GuildPermissions.Has(permiss))
                    {
                        await Context.Channel.SendMessageAsync(
                            ":no_entry_sign: You don't have the Permission to add a Tag!");
                        return;
                    }
                }
                int index = entry.IndexOf('|');
                //string[] tag = entry.Split('|');
                if (index < 1)
                {
                    await Context.Channel.SendMessageAsync(
                        $":no_entry_sign: Failed to add tag! Make sure its: `tag | what to do when tag is called`");
                    return;
                }
                string tag1 = entry.Remove(index);
                string content = entry.Substring(index);
                //if (tag.Length > 1 && !String.IsNullOrEmpty(tag[0]) && !String.IsNullOrEmpty(tag[1]))
                if (!String.IsNullOrEmpty(tag1) &&
                    !String.IsNullOrEmpty(content.Substring(content.IndexOf('|') + 1).Trim()))
                {
                    var tag = tag1.ToLower();
                    if (tag.Trim() == "taglist")
                    {
                        await Context.Channel.SendMessageAsync(
                        $":no_entry_sign: `taglist` is a command and cannot be added as tag! Use any other");
                        return;
                    }
                    TagStruct tagStruct = new TagStruct
                    {
                        tag = tag.Trim(),
                        value = content.Substring(content.IndexOf('|') + 1).Trim(),
                        creatorID = Context.User.Id
                    };
                    /*
                    if (tag.Length > 2)
                    {
                        for (int i = 2; i < tag.Length; i++)
                        {
                            tagStruct.value += tag[i];
                        }
                    }*/
                    List<TagStruct> tagList = new List<TagStruct>();
                    if (tagDict.ContainsKey(Context.Guild.Id))
                    {
                        tagDict.TryGetValue(Context.Guild.Id, out tagList);

                        foreach (var t in tagList)
                        {
                            if (t.tag.Equals(tagStruct.tag))
                            {
                                await Context.Channel.SendMessageAsync(":no_entry_sign: Tag already exists!");
                                return;
                            }
                        }

                        tagList.Add(tagStruct);
                        tagDict.TryUpdate(Context.Guild.Id, tagList);
                    }
                    else
                    {
                        tagList.Add(tagStruct);
                        tagDict.TryAdd(Context.Guild.Id, tagList);
                    }
                    var eb = new EmbedBuilder()
                    {
                        Color = new Color(4, 97, 247),
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"Requested by {Context.User.Username}#{Context.User.Discriminator}",
                            IconUrl =  (Context.User.GetAvatarUrl())
                        }
                    };
                    eb.AddField((x) =>
                    {
                        x.Name = "Successfully Added Tag!";
                        x.IsInline = true;
                        x.Value = $"**Tag**\n" +
                                  $"{tagStruct.tag}\n" +
                                  $"**Value**\n" +
                                  $"{tagStruct.value}";
                    });
                    SaveDatabase();
                    await Context.Channel.SendMessageAsync("", false, eb);
                }
                else
                {
                    await Context.Channel.SendMessageAsync(
                        $":no_entry_sign: Failed to add tag! Make sure its: `tag | what to do when tag is called`");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SentryService.SendError(e, Context);
            }
        }

        public async Task RestrictManageChannels(SocketCommandContext context, string perms)
        {
            try
            {
                if (((SocketGuildUser) context.User).GuildPermissions.Has(GuildPermission.Administrator))
                {
                    if (tagRestrictDict.ContainsKey(context.Guild.Id) && perms == null)
                    {
                        GuildPermission ignore;
                        tagRestrictDict.TryRemove(context.Guild.Id, out ignore);
                        SaveDatabaseRestrict();
                        await context.Channel.SendMessageAsync(":white_check_mark: Restriction removed!");
                    }
                    else if (perms != null)
                    {
                        if (permissionsDict.ContainsKey(perms))
                        {
                            GuildPermission permiss;
                            permissionsDict.TryGetValue(perms, out permiss);
                            if (tagRestrictDict.ContainsKey(context.Guild.Id))
                            {
                                tagRestrictDict.TryUpdate(context.Guild.Id, permiss);
                            }
                            else
                            {
                                tagRestrictDict.TryAdd(context.Guild.Id, permiss);
                            }
                            SaveDatabaseRestrict();
                            await context.Channel.SendMessageAsync(
                                $":white_check_mark: Successfully restricted tags to `{perms}`!");
                        }
                        else
                        {
                            await context.Channel.SendMessageAsync(":no_entry_sign: Permission does not exist!");
                        }
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync(":no_entry_sign: No restrictions set as of yet!");
                    }
                }
                else
                {
                    await context.Channel.SendMessageAsync(
                        ":no_entry_sign: You have to have the `Administrator` Permission to restrict tags!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SentryService.SendError(e, context);
            }
        }

        public async Task ListTags(SocketCommandContext Context)
        {
            try
            {
                if (tagDict.ContainsKey(Context.Guild.Id))
                {
                    List<TagStruct> tagStruct = new List<TagStruct>();
                    tagDict.TryGetValue(Context.Guild.Id, out tagStruct);

                    var eb = new EmbedBuilder()
                    {
                        Color = new Color(4, 97, 247),
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"Requested by {Context.User.Username}#{Context.User.Discriminator}",
                            IconUrl =  (Context.User.GetAvatarUrl())
                        },
                        Title = "Tags in this Guild"
                    };

                    int rowsCount = (int)Math.Ceiling((double)tagStruct.Count / 15);
                    if (rowsCount < 2)
                    {
                        string tagsInGuild = "";
                        foreach (var t in tagStruct)
                        {
                            tagsInGuild += $"**{t.tag}**\n";
                        }
                        eb.Description = tagsInGuild;
                    }
                    else
                    {
                        int addToJ = 0;
                        int amountLeft = tagStruct.Count;
                        for (int i = 1; i <= rowsCount; i++)
                        {
                            string toAdd = "";
                            for (int j = 0; j < (amountLeft > 15? 15: amountLeft); j++)
                            {
                                toAdd += $"{tagStruct[j+addToJ].tag}\n";
                            }
                            eb.AddField((x) =>
                            {
                                x.Name = $"Tags #{i}";
                                x.IsInline = true;
                                x.Value = toAdd;
                            });
                            amountLeft -= 15;
                            addToJ += 15;
                        }
                    }
                    await Context.Channel.SendMessageAsync("", false, eb);
                }
                else
                {
                    await Context.Channel.SendMessageAsync(":no_entry_sign: No tags in this Guild yet!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SentryService.SendError(e, Context);
            }
        }

        public async Task SearchTagAndSend(string tag, SocketCommandContext Context)
        {
            try
            {
                List<TagStruct> tagStruct = new List<TagStruct>();
                if (tagDict.ContainsKey(Context.Guild.Id))
                {
                    tagDict.TryGetValue(Context.Guild.Id, out tagStruct);
                    foreach (var t in tagStruct)
                    {
                        if (t.tag.Equals(tag.Trim().ToLower()))
                        {
                            await Context.Channel.SendMessageAsync(t.value);
                            return;
                        }
                    }
                    await Context.Channel.SendMessageAsync($":no_entry_sign: Tag `{tag}` was not found!");
                }
                else
                {
                    await Context.Channel.SendMessageAsync(":no_entry_sign: There are no tags in this guild!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SentryService.SendError(e, Context);
            }
        }

        public async Task RemoveTag(string tag, SocketCommandContext Context)
        {
            try
            {
                List<TagStruct> tagStruct = new List<TagStruct>();
                if (tagDict.ContainsKey(Context.Guild.Id))
                {
                    tagDict.TryGetValue(Context.Guild.Id, out tagStruct);
                    foreach (var t in tagStruct)
                    {
                        if (t.tag.Equals(tag.Trim().ToLower()))
                        {
                            GuildPermission permiss = GuildPermission.Connect;
                            if (tagRestrictDict.ContainsKey(Context.Guild.Id))
                            {
                                tagRestrictDict.TryGetValue(Context.Guild.Id, out permiss);
                            }
                            if (Context.User.Id == t.creatorID ||
                                ((permiss == GuildPermission.Connect)
                                    ? ((SocketGuildUser) Context.User).GuildPermissions.Has(
                                        GuildPermission.ManageChannels)
                                    : ((SocketGuildUser) Context.User).GuildPermissions.Has(permiss)))
                            {
                                tagStruct.Remove(t);
                                if (tagDict.TryUpdate(Context.Guild.Id, tagStruct))
                                {
                                    SaveDatabase();
                                    await Context.Channel.SendMessageAsync(
                                        $":white_check_mark: Successfully removed the Tag `{t.tag}`");
                                }
                                else
                                {
                                    await Context.Channel.SendMessageAsync("Something went wrong :(");
                                }
                            }
                            else
                            {
                                await Context.Channel.SendMessageAsync(
                                    ":no_entry_sign: You are neither the Creator of the tag nor have the permissions to delete it!");
                            }

                            return;
                        }
                    }
                    await Context.Channel.SendMessageAsync($":no_entry_sign: Tag `{tag}` was not found!");
                }
                else
                {
                    await Context.Channel.SendMessageAsync(":no_entry_sign: There are no tags in this guild!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SentryService.SendError(e, Context);
            }
        }

        private void InitializeLoader()
        {
            jSerializer.Converters.Add(new JavaScriptDateTimeConverter());
            jSerializer.NullValueHandling = NullValueHandling.Ignore;
        }

        public void SaveDatabaseRestrict()
        {
            using (StreamWriter sw = File.CreateText(@"TagRestriction.json"))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    jSerializer.Serialize(writer, tagRestrictDict);
                }
            }
        }

        private void LoadDatabaseRestrict()
        {
            if (File.Exists("TagRestriction.json"))
            {
                using (StreamReader sr = File.OpenText(@"TagRestriction.json"))
                {
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        var tagDicttemp = jSerializer.Deserialize<ConcurrentDictionary<ulong, GuildPermission>>(reader);
                        if (tagDicttemp != null)
                        {
                            tagRestrictDict = tagDicttemp;
                        }
                    }
                }
            }
            else
            {
                File.Create("TagRestriction.json").Dispose();
            }
        }

        public void SaveDatabase()
        {
            using (StreamWriter sw = File.CreateText(@"Tags.json"))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    jSerializer.Serialize(writer, tagDict);
                }
            }
        }

        private void LoadDatabase()
        {
            if (File.Exists("Tags.json"))
            {
                using (StreamReader sr = File.OpenText(@"Tags.json"))
                {
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        var tagDicttemp = jSerializer.Deserialize<ConcurrentDictionary<ulong, List<TagStruct>>>(reader);
                        if (tagDicttemp != null)
                        {
                            tagDict = tagDicttemp;
                        }
                    }
                }
            }
            else
            {
                File.Create("Tags.json").Dispose();
            }
        }


        public struct TagStruct
        {
            public string tag;
            public string value;
            public ulong creatorID;
        }

        public struct GuildStruct
        {
            public ulong guild_id;
            public List<TagStruct> tags;
        }
    }
}