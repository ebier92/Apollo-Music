// <copyright file="YouTube.JsonParsers.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Apollo
{
    /// <summary>
    /// Class to search for and retrieve playlist and video data from YouTube using the web client API.
    /// </summary>
    internal static partial class YouTube
    {
        /// <summary>
        /// Class to handle parsing methods to extract content from JSON objects.
        /// </summary>
        private static class JsonParsers
        {
            /// <summary>
            /// Extracts the continuation token to get the next page of search results from the YouTube search API.
            /// </summary>
            /// <param name="json">YouTube JSON.</param>
            /// <returns>The API continuation token.</returns>
            public static string ExtractContinuationToken(JToken json)
            {
                // Query continuation token under two possible names
                string continuationToken;

                try
                {
                    continuationToken = (string)json.SelectTokens("$..token").First();
                }
                catch
                {
                    try
                    {
                        continuationToken = (string)json.SelectTokens("$..continuation").First();
                    }
                    catch
                    {
                        continuationToken = "";
                    }
                }

                return continuationToken;
            }

            /// <summary>
            /// Extracts the visitor data required to get the next page of search results from the YouTube search API.
            /// </summary>
            /// <param name="json">YouTube JSON.</param>
            /// <returns>YouTube visitor data.</returns>
            public static string ExtractVisitorData(JToken json)
            {
                string visitorData;

                try
                {
                    visitorData = (string)json["responseContext"]["visitorData"];
                }
                catch
                {
                    visitorData = "";
                }

                return visitorData;
            }

            /// <summary>
            /// Extracts the title of a playlist.
            /// </summary>
            /// <param name="json">YouTube JSON.</param>
            /// <returns>The title of the playlist.</returns>
            public static string ExtractPlaylistTitle(JToken json)
            {
                string playlistTitle;

                try
                {
                    playlistTitle = (string)json["metadata"]["playlistMetadataRenderer"]["title"];
                }
                catch
                {
                    playlistTitle = "";
                }

                return playlistTitle;
            }

            /// <summary>
            /// Extracts audio and video stream info from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Stream info items as a list of <see cref="JToken"/>s.</returns>
            public static JToken ExtractStreamData(JToken json)
            {
                var formats = (JArray)json["streamingData"]["formats"];
                var adaptiveFormats = (JArray)json["streamingData"]["adaptiveFormats"];
                formats.Merge(adaptiveFormats);

                var streamData = formats;

                return streamData;
            }

            /// <summary>
            /// Extracts the search result items from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Search results as a <see cref="JToken"/>.</returns>
            public static JToken ExtractContentsFromSearchResults(JToken json)
            {
                JToken resultItems;

                // Get item section renderers from JSON
                var itemSectionRenderers = json.SelectTokens("$..itemSectionRenderer").ToList();

                // If there are multiple item section renderers, use the one with the most content
                if (itemSectionRenderers.Count > 1)
                {
                    // Initialize with the items from the first item section renderer
                    resultItems = itemSectionRenderers[0]["contents"];

                    // Set the result item with the most content
                    foreach (var itemSectionRenderer in itemSectionRenderers)
                    {
                        if (itemSectionRenderer["contents"].Count() > resultItems.Count())
                            resultItems = itemSectionRenderer["contents"];
                    }
                } // Get first and only item section renderer if there is only one
                else if (itemSectionRenderers.Count == 1)
                {
                    resultItems = itemSectionRenderers[0]["contents"];
                }
                else
                {
                    resultItems = null;
                }

                return resultItems;
            }

            /// <summary>
            /// Extracts shelf renderers from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Shelf renderers as a list of <see cref="JToken"/>s.</returns>
            public static List<JToken> ExtractShelfRenderers(JToken json)
            {
                var shelfRenderers = json.SelectTokens("$..shelfRenderer").ToList();

                return shelfRenderers;
            }

            /// <summary>
            /// Extracts compact video renderers from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Compact video renderers as a list of <see cref="JToken"/>s.</returns>
            public static List<JToken> ExtractCompactVideoRenderers(JToken json)
            {
                var compactVideoRenderers = json.SelectTokens("$..compactVideoRenderer").ToList();

                return compactVideoRenderers;
            }

            /// <summary>
            /// Extracts playlist video renderers from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Playlist video renderers as a list of <see cref="JToken"/>s.</returns>
            public static List<JToken> ExtractPlaylistVideoRenderers(JToken json)
            {
                // Get item section renderers from JSON
                var playlistVideoRenderers = json.SelectTokens("$..playlistVideoRenderer").ToList();

                return playlistVideoRenderers;
            }

            /// <summary>
            /// Extracts music responsive list item renderers from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Music responsive list item renderers as a list of <see cref="JToken"/>s.</returns>
            public static List<JToken> ExtractMusicResponsiveListItemRenderers(JToken json)
            {
                var musicResponsiveListItemRenderers = json.SelectTokens("$..musicResponsiveListItemRenderer").ToList();

                return musicResponsiveListItemRenderers;
            }

            /// <summary>
            /// Extracts playlist panel video renderers from JSON.
            /// </summary>
            /// <param name="json">JSON returned by YouTube.</param>
            /// <returns>Playlist panel video renderers as a list of <see cref="JToken"/>s.</returns>
            public static List<JToken> ExtractPlaylistPanelVideoRenderer(JToken json)
            {
                var playlistPanelVideoRenderers = json.SelectTokens("$..playlistPanelVideoRenderer").ToList();

                return playlistPanelVideoRenderers;
            }

            /// <summary>
            /// Extracts a <see cref="Video"/> from a grid video renderer JSON object.
            /// </summary>
            /// <param name="gridVideoRenderer">The grid video renderer JSON object.</param>
            /// <returns>A <see cref="Video"/>.</returns>
            public static Video ExtractVideoFromGridVideoRenderer(JToken gridVideoRenderer)
            {
                // Declare data variables used to create the video
                string videoId;
                string title;
                string author;
                TimeSpan duration;

                // Try and get minimum essential data to create the video
                try
                {
                    videoId = (string)gridVideoRenderer["videoId"];
                    title = (string)gridVideoRenderer["title"]["simpleText"];
                    var durationString = (string)gridVideoRenderer.SelectTokens("thumbnailOverlays[*].thumbnailOverlayTimeStatusRenderer.text.simpleText").First();
                    duration = TimeSpan.Parse(durationString.Count(x => x == ':') == 1 ? "00:" + durationString : durationString);
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    author = (string)gridVideoRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                }
                catch
                {
                    author = "";
                }

                // Create and return new video item
                return new Video(videoId, title, author, duration);
            }

            /// <summary>
            /// Extracts a <see cref="Video"/> from a video renderer JSON object.
            /// </summary>
            /// <param name="videoRenderer">The video renderer JSON object.</param>
            /// <returns>A <see cref="Video"/>.</returns>
            public static Video ExtractVideoFromVideoRenderer(JToken videoRenderer)
            {
                // Declare data variables used to create the video
                string videoId;
                string title;
                string author;
                TimeSpan duration;

                // Try and get minimum essential data to create the video
                try
                {
                    videoId = (string)videoRenderer["videoId"];
                    title = (string)videoRenderer.SelectTokens("title.runs[*].text").First();
                    var durationString = (string)videoRenderer.SelectTokens("thumbnailOverlays[*].thumbnailOverlayTimeStatusRenderer.text.simpleText").First();
                    duration = TimeSpan.Parse(durationString.Count(x => x == ':') == 1 ? "00:" + durationString : durationString);
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    author = (string)videoRenderer.SelectTokens("longBylineText.runs[*].text").First();
                }
                catch
                {
                    try
                    {
                        // Attempt a different JSON path if the first one causes an error
                        author = (string)videoRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                    }
                    catch
                    {
                        author = "";
                    }
                }

                // Create and return new video item
                return new Video(videoId, title, author, duration);
            }

            /// <summary>
            /// Extracts a <see cref="Video"/> from a compact video renderer JSON object.
            /// </summary>
            /// <param name="compactVideoRenderer">The compact video renderer JSON object.</param>
            /// <returns>A <see cref="Video"/>.</returns>
            public static Video ExtractVideoFromCompactVideoRenderer(JToken compactVideoRenderer)
            {
                // Declare data variables used to create the video
                string videoId;
                string title;
                string author;
                TimeSpan duration;

                // Try and get minimum essential data to create the video
                try
                {
                    videoId = (string)compactVideoRenderer["videoId"];
                    title = (string)compactVideoRenderer["title"]["simpleText"];
                    var durationString = (string)compactVideoRenderer.SelectTokens("thumbnailOverlays[*].thumbnailOverlayTimeStatusRenderer.text.simpleText").First();
                    duration = TimeSpan.Parse(durationString.Count(x => x == ':') == 1 ? "00:" + durationString : durationString);
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    author = (string)compactVideoRenderer.SelectTokens("longBylineText.runs[*].text").First();
                }
                catch
                {
                    try
                    {
                        // Attempt a different JSON path if the first one causes an error
                        author = (string)compactVideoRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                    }
                    catch
                    {
                        author = "";
                    }
                }

                // Create and return new video item
                return new Video(videoId, title, author, duration);
            }

            /// <summary>
            /// Extracts a <see cref="Video"/> from a music responsive list item renderer JSON object.
            /// </summary>
            /// <param name="musicResponsiveListItemRenderer">The music responsive list item renderer JSON object.</param>
            /// <returns>A <see cref="Video"/>.</returns>
            public static Video ExtractVideoFromMusicResponsiveListItemRenderer(JToken musicResponsiveListItemRenderer)
            {
                // Declare data variables used to create the video
                string videoId;
                string title;
                string author;
                TimeSpan duration;

                // Try and get minimum essential data to create the video
                try
                {
                    videoId = (string)musicResponsiveListItemRenderer["playlistItemData"]["videoId"];
                    title = (string)musicResponsiveListItemRenderer["flexColumns"][0]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][0]["text"];
                    var durationString = (string)musicResponsiveListItemRenderer["flexColumns"][1]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][4]["text"];
                    duration = TimeSpan.Parse(durationString.Count(x => x == ':') == 1 ? "00:" + durationString : durationString);
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data
                string artist;
                string album;

                try
                {
                    artist = (string)musicResponsiveListItemRenderer["flexColumns"][1]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][0]["text"];
                }
                catch
                {
                    artist = null;
                }

                try
                {
                    album = (string)musicResponsiveListItemRenderer["flexColumns"][1]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][2]["text"];
                }
                catch
                {
                    album = null;
                }

                if (artist != null && album != null)
                    author = artist + " • " + album;
                else if (artist != null)
                    author = artist;
                else
                    author = "";

                // Create and return new video item
                return new Video(videoId, title, author, duration);
            }

            /// <summary>
            /// Extracts a <see cref="Video"/> from a playlist panel video renderer JSON object.
            /// </summary>
            /// <param name="playlistPanelVideoRenderer">The playlist panel video renderer JSON object.</param>
            /// <returns>A <see cref="Video"/>.</returns>
            public static Video ExtractVideoFromPlaylistPanelVideoRenderer(JToken playlistPanelVideoRenderer)
            {
                // Declare data variables used to create the video
                string videoId;
                string title;
                string author;
                TimeSpan duration;

                // Try and get minimum essential data to create the video
                try
                {
                    videoId = (string)playlistPanelVideoRenderer["videoId"];
                    title = (string)playlistPanelVideoRenderer["title"]["runs"][0]["text"];
                    var durationString = (string)playlistPanelVideoRenderer.SelectTokens("lengthText.runs[*].text").First();
                    duration = TimeSpan.Parse(durationString.Count(x => x == ':') == 1 ? "00:" + durationString : durationString);
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data
                string artist;
                string album;

                try
                {
                    artist = (string)playlistPanelVideoRenderer["longBylineText"]["runs"][0]["text"];
                }
                catch
                {
                    artist = null;
                }

                try
                {
                    album = (string)playlistPanelVideoRenderer["longBylineText"]["runs"][2]["text"];

                    // Remove album text if view count is found in this spot
                    if (album.ToLower().Contains("view"))
                        album = null;
                }
                catch
                {
                    album = null;
                }

                if (artist != null && album != null)
                    author = artist + " • " + album;
                else if (artist != null)
                    author = artist;
                else
                    author = "";

                // Create and return new video item
                return new Video(videoId, title, author, duration);
            }

            /// <summary>
            /// Extracts a <see cref="Playlist"/> from a compact station renderer JSON object.
            /// </summary>
            /// <param name="compactStationRenderer">The compact station renderer JSON object.</param>
            /// <returns>A <see cref="Playlist"/>.</returns>
            public static Playlist ExtractPlaylistFromCompactStationRenderer(JToken compactStationRenderer)
            {
                // Declare variables used to create the playlist
                string playlistId;
                string title;
                string author;
                string description;
                int videoCount;
                Thumbnails thumbnails;

                // Try and get minimum essential data to create the playlist
                try
                {
                    playlistId = (string)compactStationRenderer["navigationEndpoint"]["watchEndpoint"]["playlistId"];
                    title = (string)compactStationRenderer["title"]["simpleText"];
                    var thumbnailVideoId = (string)compactStationRenderer["navigationEndpoint"]["watchEndpoint"]["videoId"];
                    thumbnails = new Thumbnails()
                    {
                        VideoId = thumbnailVideoId,
                    };
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    description = (string)compactStationRenderer["description"]["simpleText"];
                }
                catch
                {
                    description = "";
                }

                try
                {
                    videoCount = int.Parse((string)compactStationRenderer.SelectTokens("videoCountText.runs[*].text").First());
                }
                catch
                {
                    videoCount = -1;
                }

                // Assign default values to data that is not parsed
                author = "";

                // Create and return a new playlist item
                return new Playlist(playlistId, title, author, description, videoCount, thumbnails);
            }

            /// <summary>
            /// Extracts a <see cref="Playlist"/> from a compact station renderer JSON object.
            /// </summary>
            /// <param name="gridPlaylistRenderer">The compact station renderer JSON object.</param>
            /// <returns>A <see cref="Playlist"/>.</returns>
            public static Playlist ExtractPlaylistFromGridPlaylistRenderer(JToken gridPlaylistRenderer)
            {
                // Declare variables used to create the playlist
                string playlistId;
                string title;
                string author;
                string description;
                int videoCount;
                Thumbnails thumbnails;

                // Try and get minimum essential data to create the playlist
                try
                {
                    playlistId = (string)gridPlaylistRenderer["playlistId"];
                    title = (string)gridPlaylistRenderer.SelectTokens("title.runs[*].text").First();
                    var thumbnailVideoId = (string)gridPlaylistRenderer["navigationEndpoint"]["watchEndpoint"]["videoId"];
                    thumbnails = new Thumbnails()
                    {
                        VideoId = thumbnailVideoId,
                    };
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    description = (string)gridPlaylistRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                }
                catch
                {
                    description = "";
                }

                try
                {
                    videoCount = int.Parse((string)gridPlaylistRenderer.SelectTokens("videoCountText.runs[*].text").First());
                }
                catch
                {
                    videoCount = -1;
                }

                try
                {
                    author = (string)gridPlaylistRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                }
                catch
                {
                    author = "";
                }

                // Create and return a new playlist item
                return new Playlist(playlistId, title, author, description, videoCount, thumbnails);
            }

            /// <summary>
            /// Extracts a <see cref="Playlist"/> from a playlist renderer JSON object.
            /// </summary>
            /// <param name="playlistRenderer">The video renderer JSON object.</param>
            /// <returns>A <see cref="Playlist"/>.</returns>
            public static Playlist ExtractPlaylistFromPlaylistRenderer(JToken playlistRenderer)
            {
                // Declare variables used to create the playlist
                string playlistId;
                string title;
                string author;
                string description;
                int videoCount;
                Thumbnails thumbnails;

                // Try and get minimum essential data to create the playlist
                try
                {
                    playlistId = (string)playlistRenderer["playlistId"];
                    title = (string)playlistRenderer["title"]["simpleText"];

                    // Extract video ID for thumbnail
                    var thumbnailUrl = (string)playlistRenderer.SelectTokens("thumbnails[*].thumbnails[*].url").First();
                    var videoIdRegex = new Regex(@"\/vi\/(.+)\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    var videoIdMatch = videoIdRegex.Match(thumbnailUrl);
                    var thumbnailVideoId = videoIdMatch.Groups[1].Value;
                    thumbnails = new Thumbnails()
                    {
                        VideoId = thumbnailVideoId,
                    };
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    author = (string)playlistRenderer.SelectTokens("shortBylineText.runs[*].text").First();
                }
                catch
                {
                    author = "";
                }

                try
                {
                    videoCount = int.Parse((string)playlistRenderer["videoCount"]);
                }
                catch
                {
                    videoCount = -1;
                }

                // Assign default values to data that is not parsed
                description = "";

                // Create and return a new playlist item
                return new Playlist(playlistId, title, author, description, videoCount, thumbnails);
            }

            /// <summary>
            /// Extracts a <see cref="Playlist"/> from a music responsive list renderer JSON object.
            /// </summary>
            /// <param name="musicResponsiveListItemRenderer">The music responsive list renderer JSON object.</param>
            /// <returns>A <see cref="Playlist"/>.</returns>
            public static Playlist ExtractPlaylistFromMusicResponsiveListItemRenderer(JToken musicResponsiveListItemRenderer)
            {
                // Declare variables used to create the playlist
                string playlistId;
                string title;
                string author;
                string description;
                int videoCount;
                Thumbnails thumbnails;

                // Try and get minimum essential data to create the playlist
                try
                {
                    playlistId = (string)musicResponsiveListItemRenderer.SelectTokens("$..playlistId").First();
                    title = (string)musicResponsiveListItemRenderer["flexColumns"][0]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][0]["text"];
                    var thumbnailItems = musicResponsiveListItemRenderer["thumbnail"]["musicThumbnailRenderer"]["thumbnail"]["thumbnails"];

                    // Set up thumbnails
                    thumbnails = new Thumbnails();

                    if (thumbnailItems.Count() >= 1)
                        thumbnails.LowResUrl = (string)thumbnailItems[0]["url"];

                    if (thumbnailItems.Count() >= 2)
                        thumbnails.MediumResUrl = (string)thumbnailItems[1]["url"];

                    if (thumbnailItems.Count() >= 3)
                        thumbnails.HighResUrl = (string)thumbnailItems[2]["url"];

                    if (thumbnailItems.Count() >= 4)
                        thumbnails.StandardResUrl = (string)thumbnailItems[3]["url"];
                }
                catch
                {
                    // Return a null value if there is a problem parsing any essential data
                    return null;
                }

                // Try and get non-essential data, assign default values if there are any errors
                try
                {
                    var artist = (string)musicResponsiveListItemRenderer["flexColumns"][1]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][0]["text"];

                    // Attempt to extract either the album name or the video count depending on the where the JSON item came from
                    try
                    {
                        var textRun = (string)musicResponsiveListItemRenderer["flexColumns"][1]["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][2]["text"];

                        // Check if the text run element contains video count information
                        if (Regex.IsMatch(textRun, @"^\d+ song[s]*"))
                        {
                            // Remove non-numeric characters and parse to an integer
                            videoCount = int.Parse(Regex.Replace(textRun, @"[^0-9]", ""));
                            author = artist;
                        } // Text run element does not match and must be an album
                        else
                        {
                            // Set author value as artist and album
                            author = artist + " • " + textRun;
                            videoCount = -1;
                        }
                    }
                    catch
                    {
                        author = artist;
                        videoCount = -1;
                    }
                }
                catch
                {
                    author = "";
                    videoCount = -1;
                }

                // Assign default values to data that is not parsed
                description = "";

                // Create and return a new playlist item
                return new Playlist(playlistId, title, author, description, videoCount, thumbnails);
            }
        }
    }
}