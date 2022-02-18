using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;
using Jellyfin.Plugin.Webhook.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Webhook.Destinations.Discord
{
    /// <summary>
    /// Client for the <see cref="DiscordOption"/>.
    /// </summary>
    public class DiscordClient : BaseClient, IWebhookClient<DiscordOption>
    {
        private readonly ILogger<DiscordClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordClient"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{DiscordDestination}"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the<see cref="IHttpClientFactory"/> interface.</param>
        public DiscordClient(ILogger<DiscordClient> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task SendAsync(DiscordOption option, Dictionary<string, object> data)
        {
            try
            {
                if (string.IsNullOrEmpty(option.WebhookUri))
                {
                    throw new ArgumentException(nameof(option.WebhookUri));
                }

                if (!SendWebhook(_logger, option, data))
                {
                    return;
                }

                // Add discord specific properties.
                data["MentionType"] = GetMentionType(option.MentionType);
                if (!string.IsNullOrEmpty(option.EmbedColor))
                {
                    data["EmbedColor"] = FormatColorCode(option.EmbedColor);
                }

                if (!string.IsNullOrEmpty(option.AvatarUrl))
                {
                    data["AvatarUrl"] = option.AvatarUrl;
                }

                if (!string.IsNullOrEmpty(option.Username))
                {
                    data["Username"] = option.Username;
                    data["BotUsername"] = option.Username;
                }

                // foreach (var (key, value) in data)
                //     _logger.LogWarning("{@Key}: {@Value}", key, value);

                // mine

                string GetTitle()
                {
                    switch (Enum.Parse<NotificationType>((string)data["NotificationType"]))
                    {
                        case NotificationType.SessionStart:
                            return
                                $"Session started by {data["NotificationUsername"]} on {data["Client"]}({data["DeviceName"]})";
                        case NotificationType.PlaybackStart:
                            return
                                $"{data["NotificationUsername"]} is playing {data["Name"]} on {data["ClientName"]}({data["DeviceName"]})";
                        case NotificationType.PlaybackStop:
                            return
                                $"{data["NotificationUsername"]} stopped playing {data["Name"]} on {data["ClientName"]}({data["DeviceName"]})";
                        case NotificationType.UserLockedOut:
                            return $"User {data["NotificationUsername"]} has been locked out";
                        case NotificationType.UserCreated:
                            return $"User {data["NotificationUsername"]} has been created";
                        case NotificationType.UserDeleted:
                            return $"User {data["NotificationUsername"]} has been deleted";
                        case NotificationType.PendingRestart:
                            return "Server pending restart";
                        case NotificationType.ItemAdded:
                            return $"New content: {data["Name"]}";
                    }

                    return null;
                }

                string GetDescription()
                {
                    switch (Enum.Parse<NotificationType>((string)data["NotificationType"]))
                    {
                        case NotificationType.ItemAdded:
                            return
                                (string)data["Overview"];
                    }

                    return null;
                }

                var cl = new DiscordWebhookClient(option.WebhookUri);
                // i wonder how someone, in a language with the violent splash of strong typing
                // would use
                // A FUCKING DICTIONARY
                // INSTEAD OF A NORMAL FUCKING TYPE
                // how mentally ill do you have to be to do that
                // it's like a bri'ish person drinking milk instead of tea

                await cl.SendMessageAsync(embeds: new List<Embed>
                    {
                        new EmbedBuilder
                        {
                            Color = new Color((uint)FormatColorCode(option.EmbedColor)),
                            Description = GetDescription(),
                            Title = GetTitle(),
                            Timestamp = data.ContainsKey("UtcTimestamp") ? (DateTime)data["UtcTimestamp"] : null,
                            ThumbnailUrl = data.ContainsKey("item")
                                ? ((BaseItem)data["item"]).ImageInfos
                                .FirstOrDefault(i => !i.IsLocalFile && i.Type == ImageType.Primary)?.Path
                                : null
                        }.Build()
                    }, username: option.Username, avatarUrl: option.AvatarUrl, text: GetMentionType(option.MentionType))
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                _logger.LogWarning(e, "Error sending notification");
            }
        }

        private static int FormatColorCode(string hexCode)
        {
            return int.Parse(hexCode[1..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static string GetMentionType(DiscordMentionType mentionType)
        {
            return mentionType switch
            {
                DiscordMentionType.Everyone => "@everyone",
                DiscordMentionType.Here => "@here",
                _ => string.Empty
            };
        }
    }
}