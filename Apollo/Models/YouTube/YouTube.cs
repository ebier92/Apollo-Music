// <copyright file="YouTube.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Apollo
{
    /// <summary>
    /// Class to search for and retrieve playlist and video data from YouTube using the web client API.
    /// </summary>
    internal static partial class YouTube
    {
        /// <summary>
        /// Set the content type filter preference for YouTube Music searches.
        /// </summary>
        public enum MusicSearchFilter
        {
            /// <summary>
            /// Filters YouTube Music search results to albums.
            /// </summary>
            Albums,

            /// <summary>
            /// Filters YouTube Music search results to featured playlists.
            /// </summary>
            FeaturedPlaylists,

            /// <summary>
            /// Filters YouTube Music search results to community playlists.
            /// </summary>
            CommunityPlaylists,

            /// <summary>
            /// Filters YouTube Music search results to songs.
            /// </summary>
            Songs,
        }

        /// <summary>
        /// Returns <see cref="ContentItem"/>s from the YouTube music channel page.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns><see cref="ContentItem"/>s from the YouTube music channel page.</returns>
        public static async Task<List<ContentItem>> GetHomePageContentItems(CancellationToken cancellationToken)
        {
            var contentItems = new List<ContentItem>();

            // Set up API URL
            var url = Constants.YouTubeUrl + Constants.BaseApi + Constants.BrowseEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["browseId"] = Constants.YouTubeMusicChannelBrowseId,
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract shelf renderers from JSON
            var shelfRenderers = JsonParsers.ExtractShelfRenderers(jsonResponse);

            // Process items in each shelf renderer
            foreach (var shelfRenderer in shelfRenderers)
            {
                // Add a section header home item based on the text in the shelf renderer
                var sectionHeader = (string)shelfRenderer.SelectTokens("title.runs[*].text").First();

                // Create a list of possible item types that may be stored within a shelf renderer
                var itemTypes = new string[] { "compactStationRenderer", "gridVideoRenderer", "gridPlaylistRenderer" };

                List<JToken> jsonHomeItems = new List<JToken>();
                string itemType = null;
                var i = 0;

                // Attempt to extract possible content item types from the shelf renderer and save the type name if data was found
                while (jsonHomeItems.Count == 0)
                {
                    jsonHomeItems = shelfRenderer.SelectTokens("$.." + itemTypes[i]).ToList();
                    itemType = itemTypes[i];
                    i++;
                }

                // Parse and extract content data from the item based on type
                ContentItem contentItem = null;
                bool sectionHeaderAdded = false;

                foreach (var jsonHomeItem in jsonHomeItems)
                {
                    if (itemType == "compactStationRenderer")
                        contentItem = new ContentItem(JsonParsers.ExtractPlaylistFromCompactStationRenderer(jsonHomeItem), null, null, null);
                    else if (itemType == "gridVideoRenderer")
                        contentItem = new ContentItem(JsonParsers.ExtractVideoFromGridVideoRenderer(jsonHomeItem), null, null, null);
                    else if (itemType == "gridPlaylistRenderer")
                        contentItem = new ContentItem(JsonParsers.ExtractPlaylistFromGridPlaylistRenderer(jsonHomeItem), null, null, null);

                    // Add the section header if the first home item is successfully parsed to prevent empty sections from appearing
                    if (contentItem.Content != null && !sectionHeaderAdded)
                    {
                        contentItems.Add(new ContentItem((Video)null, new Dictionary<string, string> { { "sectionHeader", sectionHeader } }, null, null));
                        sectionHeaderAdded = true;
                    }

                    // Add the extracted home item if its value is not null
                    if (contentItem.Content != null)
                        contentItems.Add(contentItem);
                }
            }

            return contentItems;
        }

        /// <summary>
        /// Retrieves video and audio stream info for a specified YouTube video URL.
        /// </summary>
        /// <param name="videoId">The video ID to get related videos from.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="StreamInfo"/>s for the video.</returns>
        public static async Task<List<StreamInfo>> GetStreamInfo(string videoId, CancellationToken cancellationToken)
        {
            List<StreamInfo> streamInfoItems = new List<StreamInfo>();

            // Set up API URL
            var url = Constants.YouTubeUrl + Constants.BaseApi + Constants.PlayerEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["videoId"] = videoId,
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract stream info items
            var jsonStreamData = JsonParsers.ExtractStreamData(jsonResponse);

            // Convert JSON data into stream info
            foreach (var jsonStreamDataItem in jsonStreamData)
            {
                var streamUrl = (string)jsonStreamDataItem["url"];
                var mimeType = (string)jsonStreamDataItem["mimeType"];
                var bitRate = (int)jsonStreamDataItem["bitrate"];

                streamInfoItems.Add(new StreamInfo(streamUrl, mimeType, bitRate));
            }

            return streamInfoItems;
        }

        /// <summary>
        /// Peforms a search of YouTube based on the input query and returns relevant <see cref="ContentItem"/>s.
        /// </summary>
        /// <param name="query">The query to search by.</param>
        /// <param name="continuationToken">An optional API token to retrieve another page of content from the search results.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="ContentItem"/>s matching the query.</returns>
        public static async IAsyncEnumerable<ContentItem> SearchYouTube(string query, string continuationToken, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up URL
            var url = Constants.YouTubeUrl + Constants.BaseApi + Constants.SearchEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["query"] = query,
                ["continuation"] = continuationToken,
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract continuation token from JSON
            var newContinuationToken = JsonParsers.ExtractContinuationToken(jsonResponse);

            // Get result items from JSON
            var searchResultJsonItems = JsonParsers.ExtractContentsFromSearchResults(jsonResponse);

            // Loop over extracted items
            foreach (var searchResultJsonItem in searchResultJsonItems)
            {
                // Check for the existence of the "videoRenderer" element to indicate the item is a video
                if (searchResultJsonItem["videoRenderer"] != null)
                {
                    // Get video renderer object
                    var videoRenderer = searchResultJsonItem["videoRenderer"];

                    // Extract a video
                    var video = JsonParsers.ExtractVideoFromVideoRenderer(videoRenderer);

                    // Create and return a new content item containing the video if not null
                    if (video != null)
                        yield return new ContentItem(video, null, newContinuationToken, null);
                } // Check for the existence of a "playlistRenderer" element to indicate the item is a playlist
                else if (searchResultJsonItem["playlistRenderer"] != null)
                {
                    // Get playlistRenderer object
                    var playlistRenderer = searchResultJsonItem["playlistRenderer"];

                    // Extract a playlist
                    var playlist = JsonParsers.ExtractPlaylistFromPlaylistRenderer(playlistRenderer);

                    // Create and return a new content item containing the playlist if not null
                    if (playlist != null)
                        yield return new ContentItem(playlist, null, newContinuationToken, null);
                }
            }
        }

        /// <summary>
        /// Peforms a search of YouTube based on the input query and returns relevant <see cref="ContentItem"/>s.
        /// </summary>
        /// <param name="query">The query to search by.</param>
        /// <param name="continuationToken">An optional API token to retrieve another page of content from the search results.</param>
        /// <param name="searchFilter">A filter to control the type of content returned by the search.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="ContentItem"/>s matching the query.</returns>
        public static async IAsyncEnumerable<ContentItem> SearchYouTubeMusic(string query, string continuationToken, MusicSearchFilter searchFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up URL
            var url = Constants.YouTubeMusicUrl + Constants.BaseApi + Constants.SearchEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["query"] = query,
                ["continuation"] = continuationToken,
            };

            // Build parameters argument based on the desired filter and add to the data
            string parameters = null;

            if (searchFilter == MusicSearchFilter.Albums)
                parameters = "EgWKAQIYAWoMEA4QChADEAQQCRAF";
            else if (searchFilter == MusicSearchFilter.FeaturedPlaylists)
                parameters = "EgeKAQQoADgBagwQDhAKEAMQBBAJEAU%3D";
            else if (searchFilter == MusicSearchFilter.CommunityPlaylists)
                parameters = "EgeKAQQoAEABagwQDhAKEAMQBBAJEAU%3D";
            else if (searchFilter == MusicSearchFilter.Songs)
                parameters = "EgWKAQIIAWoMEA4QChADEAQQCRAF";

            data["params"] = parameters;
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract continuation token from JSON
            var newContinuationToken = JsonParsers.ExtractContinuationToken(jsonResponse);

            // Get items from JSON
            var musicResponsiveListItemRenderers = JsonParsers.ExtractMusicResponsiveListItemRenderers(jsonResponse);

            // Loop over extracted items
            foreach (var musicResponsiveListItemRenderer in musicResponsiveListItemRenderers)
            {
                // Use the filter argument to determine what type of data should be parsed
                if (searchFilter == MusicSearchFilter.Albums || searchFilter == MusicSearchFilter.FeaturedPlaylists || searchFilter == MusicSearchFilter.CommunityPlaylists)
                {
                    // Extract a playlist
                    var playlist = JsonParsers.ExtractPlaylistFromMusicResponsiveListItemRenderer(musicResponsiveListItemRenderer);

                    // Create and return a new search result item containing the playlist if not null
                    if (playlist != null)
                        yield return new ContentItem(playlist, null, newContinuationToken, null);
                }
                else if (searchFilter == MusicSearchFilter.Songs)
                {
                    // Extract a video
                    var video = JsonParsers.ExtractVideoFromMusicResponsiveListItemRenderer(musicResponsiveListItemRenderer);

                    // Create and return a new content item containing the video if not null
                    if (video != null)
                        yield return new ContentItem(video, null, newContinuationToken, null);
                }
            }
        }

        /// <summary>
        /// Returns related videos from the specified video ID.
        /// </summary>
        /// <param name="videoId">The video ID to get related videos from.</param>
        /// <param name="continuationToken">An optional API token to retrieve another page of content from the related video results.</param>
        /// <param name="visitorData">An optional visitor data value to retrieve another page of content from the playlist videos.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="ContentItem"/> containing related videos.</returns>
        public static async IAsyncEnumerable<ContentItem> GetRelatedVideos(string videoId, string continuationToken, string visitorData, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up URL
            var url = Constants.YouTubeUrl + Constants.BaseApi + Constants.NextEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["videoId"] = videoId,
                ["continuation"] = continuationToken,
                ["context"] = new Dictionary<string, object>
                {
                    ["client"] = new Dictionary<string, object>
                    {
                        ["visitorData"] = visitorData,
                    },
                },
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract continuation token from JSON
            var newContinuationToken = JsonParsers.ExtractContinuationToken(jsonResponse);

            // Get items from JSON
            var compactVideoRenderers = JsonParsers.ExtractCompactVideoRenderers(jsonResponse);

            // Loop over extracted items
            foreach (var compactVideoRenderer in compactVideoRenderers)
            {
                var video = JsonParsers.ExtractVideoFromCompactVideoRenderer(compactVideoRenderer);

                // Create and return a new related video item if the video was not null
                if (video != null)
                    yield return new ContentItem(video, null, newContinuationToken, visitorData);
            }
        }

        /// <summary>
        /// Returns videos from the specified playlist ID.
        /// </summary>
        /// <param name="playlistId">The playlist ID to return videos from.</param>
        /// <param name="continuationToken">An optional API token to retrieve another page of content from the playlist videos.</param>
        /// <param name="visitorData">An optional visitor data value to retrieve another page of content from the playlist videos.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="ContentItem"/> containing related videos.</returns>
        public static async IAsyncEnumerable<ContentItem> GetPlaylistVideos(string playlistId, string continuationToken, string visitorData, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up URL
            var url = Constants.YouTubeUrl + Constants.BaseApi + Constants.BrowseEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["browseId"] = "VL" + playlistId,
                ["continuation"] = continuationToken,
                ["context"] = new Dictionary<string, object>
                {
                    ["client"] = new Dictionary<string, object>
                    {
                        ["visitorData"] = visitorData,
                    },
                },
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, null, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract continuation token from JSON
            var newContinuationToken = JsonParsers.ExtractContinuationToken(jsonResponse);

            // Extract playlist title from JSON
            var playlistTitle = JsonParsers.ExtractPlaylistTitle(jsonResponse);

            // Extract visitor data from JSON if not provided
            if (visitorData == null)
                visitorData = JsonParsers.ExtractVisitorData(jsonResponse);

            // Set up data to store playlist title within each content item
            var contentItemData = new Dictionary<string, string> { { "playlistTitle", playlistTitle } };

            // Get items from JSON
            var playlistVideoRenderers = JsonParsers.ExtractPlaylistVideoRenderers(jsonResponse);

            // Loop over result items
            foreach (var playlistVideoRenderer in playlistVideoRenderers)
            {
                var video = JsonParsers.ExtractVideoFromVideoRenderer(playlistVideoRenderer);

                // Create and return a new related video item if the video was not null
                if (video != null)
                    yield return new ContentItem(video, contentItemData, newContinuationToken, visitorData);
            }
        }

        /// <summary>
        /// Returns videos from the automatically generated watch playlist that is generated with each video on YouTube Music.
        /// </summary>
        /// <param name="videoId">The video ID to get the watch playlist videos from.</param>
        /// <param name="continuationToken">An optional API token to retrieve another page of content from the playlist videos.</param>
        /// <param name="visitorData">User identifier required by YouTube for some API requests.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="ContentItem"/> containing watch playlist videos.</returns>
        public static async IAsyncEnumerable<ContentItem> GetWatchPlaylistVideos(string videoId, string continuationToken, string visitorData, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up URL
            var url = Constants.YouTubeMusicUrl + Constants.BaseApi + Constants.NextEndpoint + Constants.ApiParameters;

            // Create JSON object from input data
            var data = new Dictionary<string, object>
            {
                ["enablePersistentPlaylistPanel"] = true,
                ["isAudioOnly"] = true,
                ["videoId"] = videoId,
                ["playlistId"] = "RDAMVM" + videoId,
                ["continuation"] = continuationToken,
                ["watchEndpointMusicSupportedConfigs"] = new Dictionary<string, object>
                {
                    ["watchEndpointMusicConfig"] = new Dictionary<string, object>
                    {
                        ["hasPersistentPlaylistPanel"] = true,
                        ["musicVideoType"] = "MUSIC_VIDEO_TYPE_OMV",
                    },
                },
            };
            var jsonData = JObject.FromObject(data);

            // Send the API request and parse the response
            var response = await Network.SendPostRequest(url, jsonData, visitorData, cancellationToken);
            var jsonResponse = JToken.Parse(response);

            // Extract continuation token from JSON
            var newContinuationToken = JsonParsers.ExtractContinuationToken(jsonResponse);

            // Extract visitor data from JSON if not provided
            if (visitorData == null)
                visitorData = JsonParsers.ExtractVisitorData(jsonResponse);

            // Get items from JSON
            var playlistPanelVideoRenderers = JsonParsers.ExtractPlaylistPanelVideoRenderer(jsonResponse);

            // Loop over extracted items
            foreach (var playlistPanelVideoRenderer in playlistPanelVideoRenderers)
            {
                var video = JsonParsers.ExtractVideoFromPlaylistPanelVideoRenderer(playlistPanelVideoRenderer);

                // Create and return a new video item if the video was not null
                if (video != null)
                    yield return new ContentItem(video, null, newContinuationToken, visitorData);
            }
        }

        /// <summary>
        /// Extracts the YouTube video ID value from a URL.
        /// </summary>
        /// <param name="url">A YouTube or YouTube Music video URL.</param>
        /// <returns>The video ID value.</returns>
        public static string GetVideoId(string url)
        {
            var videoIdRegex = new Regex(@"v=(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var videoIdMatch = videoIdRegex.Match(url);
            var videoId = videoIdMatch.Groups[1].Value;

            return videoId;
        }
    }
}