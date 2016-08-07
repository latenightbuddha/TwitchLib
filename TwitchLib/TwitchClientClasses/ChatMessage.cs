﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace TwitchLib
{
    /// <summary>Class represents ChatMessage in a Twitch channel.</summary>
    public class ChatMessage
    {
        private MessageEmoteCollection _emoteCollection;

        /// <summary>Twitch-unique integer assigned on per account basis.</summary>
        public int UserId { get; protected set; }
        /// <summary>Username of sender of chat message.</summary>
        public string Username { get; protected set; }
        /// <summary>Case-sensitive username of sender of chat message.</summary>
        public string DisplayName { get; protected set; }
        /// <summary>Hex representation of username color in chat.</summary>
        public string ColorHex { get; protected set; }
        /// <summary>Emote Ids that exist in message.</summary>
        public string EmoteSet { get; protected set; }
        /// <summary>Twitch chat message contents.</summary>
        public string Message { get; protected set; }
        /// <summary>User type can be viewer, moderator, global mod, admin, or staff</summary>
        public Common.UserType UserType { get; protected set; }
        /// <summary>Twitch channel message was sent from (useful for multi-channel bots).</summary>
        public string Channel { get; protected set; }
        /// <summary>Channel specific subscriber status.</summary>
        public bool Subscriber { get; protected set; }
        /// <summary>Twitch site-wide turbo status.</summary>
        public bool Turbo { get; protected set; }
        /// <summary>Channel specific moderator status.</summary>
        public bool IsModerator { get; protected set; }
        /// <summary>Chat message /me identifier flag.</summary>
        public bool IsMe { get; protected set; }
        /// <summary>Chat message from broadcaster identifier flag</summary>
        public bool IsBroadcaster { get; protected set; }
        /// <summary>Raw IRC-style text received from Twitch.</summary>
        public string RawIrcMessage { get; protected set; }
        /// <summary>Text after emotes have been handled (if desired). Will be null if replaceEmotes is false.</summary>
        public string EmoteReplacedMessage { get; protected set; }
        /// <summary>List of key-value pair badges.</summary>
        public List<KeyValuePair<string,string>> Badges { get; protected set; }

        //Example IRC message: @badges=moderator/1,warcraft/alliance;color=;display-name=Swiftyspiffyv4;emotes=;mod=1;room-id=40876073;subscriber=0;turbo=0;user-id=103325214;user-type=mod :swiftyspiffyv4!swiftyspiffyv4@swiftyspiffyv4.tmi.twitch.tv PRIVMSG #swiftyspiffy :asd
        /// <summary>Constructor for ChatMessage object.</summary>
        /// <param name="ircString">The raw string received from Twitch to be processed.</param>
        /// <param name="emoteCollection">The <see cref="MessageEmoteCollection"/> to register new emotes on and, if desired, use for emote replacement.</param>
        /// <param name="replaceEmotes">Whether to replace emotes for this chat message. Defaults to false.</param>
        public ChatMessage(string ircString, ref MessageEmoteCollection emoteCollection, bool replaceEmotes = false)
        {
            RawIrcMessage = ircString;
            _emoteCollection = emoteCollection;
            foreach (var part in ircString.Split(';'))
            {
                if (part.Contains("!"))
                {
                    if (Channel == null)
                        Channel = part.Split('#')[1].Split(' ')[0];
                    if (Username == null)
                        Username = part.Split('!')[1].Split('@')[0];
                }
                else if(part.Contains("@badges="))
                {
                    string badges = part.Split('=')[1];
                    if(badges.Contains('/'))
                    {
                        if (!badges.Contains(","))
                            Badges.Add(new KeyValuePair<string, string>(badges.Split('/')[0], badges.Split('/')[1]));
                        else
                            foreach (string badge in badges.Split(','))
                                Badges.Add(new KeyValuePair<string, string>(badge.Split('/')[0], badge.Split('/')[1]));
                    }
                }
                else if (part.Contains("color="))
                {
                    if (ColorHex == null)
                        ColorHex = part.Split('=')[1];
                }
                else if (part.Contains("display-name"))
                {
                    if (DisplayName == null)
                        DisplayName = part.Split('=')[1];
                }
                else if (part.Contains("emotes="))
                {
                    if (EmoteSet == null)
                    {
                        EmoteSet = part.Split('=')[1]; ;
                    }

                }
                else if (part.Contains("subscriber="))
                {
                    Subscriber = part.Split('=')[1] == "1";
                }
                else if (part.Contains("turbo="))
                {
                    Turbo = part.Split('=')[1] == "1";
                }
                else if (part.Contains("user-id="))
                {
                    UserId = int.Parse(part.Split('=')[1]);
                }
                else if (part.Contains("user-type="))
                {
                    switch (part.Split('=')[1].Split(' ')[0])
                    {
                        case "mod":
                            UserType = Common.UserType.Moderator;
                            break;
                        case "global_mod":
                            UserType = Common.UserType.GlobalModerator;
                            break;
                        case "admin":
                            UserType = Common.UserType.Admin;
                            break;
                        case "staff":
                            UserType = Common.UserType.Staff;
                            break;
                        default:
                            UserType = Common.UserType.Viewer;
                            break;
                    }
                }
                else if (part.Contains("mod="))
                {
                    IsModerator = part.Split('=')[1] == "1";
                }
            }
            Message = ircString.Split(new[] {$" PRIVMSG #{Channel} :"}, StringSplitOptions.None)[1];
            if ((byte)Message[0] == 1 && (byte)Message[Message.Length-1] == 1)
            {
              //Actions (/me {action}) are wrapped by byte=1 and prepended with "ACTION "
              //This setup clears all of that leaving just the action's text.
              //If you want to clear just the nonstandard bytes, use:
              //_message = _message.Substring(1, text.Length-2);
              Message = Message.Substring(8, Message.Length-9);
              IsMe = true;
            }

            //Parse the emoteSet
            if (EmoteSet != null && Message != null)
            {
                string[] uniqueEmotes = EmoteSet.Split('/');
                string id, text;
                int firstColon, firstComma, firstDash, low, high;
                foreach (string emote in uniqueEmotes)
                {
                    firstColon = emote.IndexOf(':');
                    firstComma = emote.IndexOf(',');
                    if (firstComma == -1) firstComma = emote.Length;
                    firstDash = emote.IndexOf('-');
                    if (firstColon > 0 && firstDash > firstColon && firstComma > firstDash)
                    {
                        if (Int32.TryParse(emote.Substring(firstColon + 1, (firstDash - firstColon) - 1), out low) &&
                            Int32.TryParse(emote.Substring(firstDash + 1, (firstComma - firstDash) - 1), out high))
                        {
                            if (low >= 0 && low < high && high < Message.Length)
                            {
                                //Valid emote, let's parse
                                id = emote.Substring(0, firstColon);
                                //Pull the emote text from the message
                                text = Message.Substring(low, (high - low) + 1);
                                _emoteCollection.Add(new MessageEmote(id, text));
                            }
                        }
                    }
                }
                if (replaceEmotes)
                {
                    EmoteReplacedMessage = _emoteCollection.ReplaceEmotes(Message);
                }
            }

            if(Channel.ToLower() == Channel.ToLower())
            {
                UserType = Common.UserType.Broadcaster;
                IsBroadcaster = true;
            }
        }

        private static bool ConvertToBool(string data)
        {
            return data == "1";
        }
    }
}