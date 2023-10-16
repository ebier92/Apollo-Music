// <copyright file="RecommendationsManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Random = Java.Util.Random;

namespace Apollo
{
    /// <summary>
    /// Interface to access saved selected track history and generate track recommendations.
    /// </summary>
    internal static class RecommendationsManager
    {
        /// <summary>
        /// Gets the count of tracks previously listened to by the user.
        /// </summary>
        public static int HistoricalTrackCount
        {
            get
            {
                // Get recommendations data
                var recommendations = ReadJson();

                // Return the number of historical tracks
                return recommendations.Tracks.Count;
            }
        }

        /// <summary>
        /// Gets a list of historical tracks previously listened to by the user.
        /// </summary>
        public static List<YouTube.Video> HistoricalTracks
        {
            get
            {
                // Get recommendations data
                var recommendations = ReadJson();

                var historicalTracks = new List<YouTube.Video>();

                // Add all available historical tracks to the list
                foreach (var track in recommendations.Tracks)
                {
                    // Create YouTube video
                    var video = new YouTube.Video(
                        ExtractVideoId(track.Url),
                        track.TrackName,
                        track.Artist,
                        TimeSpan.FromMilliseconds(track.Duration));

                    // Create content item with source marked as "historical"
                    historicalTracks.Add(video);
                }

                return historicalTracks;
            }
        }

        /// <summary>
        /// Gets the file path for the settings JSON file.
        /// </summary>
        private static string JsonFilePath
        {
            get
            {
                // Get system path for JSON settings file
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "recommendations.json");
            }
        }

        /// <summary>
        /// Creates a default recommendations data JSON file if one does not yet exist.
        /// </summary>
        public static void InitializeJsonFile()
        {
            if (!File.Exists(JsonFilePath))
                WriteJson(new RecommendationData());
        }

        /// <summary>
        /// Adds a track to the recommendation data.
        /// </summary>
        /// <param name="trackName">The name of the track.</param>
        /// <param name="artist">The artist of the track.</param>
        /// <param name="duration">The duration of the track in milliseconds.</param>
        /// <param name="url">The source URL of the track.</param>
        public static void AddTrack(string trackName, string artist, long duration, string url)
        {
            // Get recommendations data
            var recommendations = ReadJson();

            // Create a new track
            var track = new Track
            {
                TrackName = trackName,
                Artist = artist,
                Duration = duration,
                Url = url,
            };

            // Delete any previous occurrences of the track based on URL
            var trackOccurrences = recommendations.Tracks.Where(recommendationsTrack => recommendationsTrack.Url == track.Url).ToList();

            foreach (var trackOccurrence in trackOccurrences)
            {
                recommendations.Tracks.Remove(trackOccurrence);
            }

            // Delete tracks when the number of saved tracks exceeds 500
            if (recommendations.Tracks.Count > 500)
            {
                for (int i = recommendations.Tracks.Count - 1; i > 500; i--)
                {
                    recommendations.Tracks.RemoveAt(i);
                }
            }

            // Insert track at beginning of the list
            recommendations.Tracks.Insert(0, track);

            // Write JSON data back to file
            WriteJson(recommendations);
        }

        /// <summary>
        /// Removes a track from the recommendation data by URL.
        /// </summary>
        /// <param name="url">The source URL of the track to remove.</param>
        public static void RemoveTrack(string url)
        {
            // Get recommendations data
            var recommendations = ReadJson();

            // Delete any previous occurrences of the track based on URL
            var trackOccurrences = recommendations.Tracks.Where(recommendationsTrack => recommendationsTrack.Url == url).ToList();

            foreach (var trackOccurrence in trackOccurrences)
            {
                recommendations.Tracks.Remove(trackOccurrence);
            }

            // Write JSON data back to file
            WriteJson(recommendations);
        }

        /// <summary>
        /// Clears all user recommendation data.
        /// </summary>
        public static void ClearRecommendations()
        {
            var recommendations = ReadJson();
            recommendations.Tracks = new List<Track>();
            WriteJson(recommendations);
        }

        /// <summary>
        /// Returns recommended content items based on the user's track selection history.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="YouTube.ContentItem"/> recommendations.</returns>
        public static async Task<List<YouTube.ContentItem>> GetRecommendations(CancellationToken cancellationToken)
        {
            // Read recommendation data
            var recommendationsData = ReadJson();
            var recommendationsDataTracks = recommendationsData.Tracks.ToList();

            // If there are no tracks recorded for recommendations, return a null
            if (recommendationsData.Tracks.Count == 0)
                return null;

            // Assign a max number of seed tracks to retrieve based on the number of available historical tracks
            int numberSeedTracks;

            if (recommendationsData.Tracks.Count < 5)
                numberSeedTracks = recommendationsData.Tracks.Count;
            else
                numberSeedTracks = 5;

            // Randomly select 5 different tracks using reciprocal weighting
            var selectedSeedTracks = new List<Track>();

            for (int i = 0; i < numberSeedTracks; i++)
            {
                var selectedSeedTrack = recommendationsDataTracks[SelectItemFromList(recommendationsDataTracks.Count, 0.5)];
                selectedSeedTracks.Add(selectedSeedTrack);
                recommendationsDataTracks.Remove(selectedSeedTrack);
            }

            // Get the first generated playlist page from each track and randomly select 5 tracks from the playlist to add to the recommended content
            var recommendedContent = new List<YouTube.ContentItem>();

            // Use semaphore for task throttling
            using var semaphore = new SemaphoreSlim(initialCount: 5);

            // Set up tasks to download the first playlist page for the five tracks
            var tasks = selectedSeedTracks.Select(async selectedSeedTrack =>
            {
                // Wait if throttling is required
                await semaphore.WaitAsync();

                try
                {
                    var pageTracks = new List<YouTube.ContentItem>();

                    // Get first page of playlist
                    await foreach (var pageTrack in YouTube.GetWatchPlaylistVideos(ExtractVideoId(selectedSeedTrack.Url), null, null, cancellationToken))
                    {
                        // Mark the data source for all YouTube items as "recommended"
                        pageTracks.Add(new YouTube.ContentItem((YouTube.Video)pageTrack.Content, new Dictionary<string, string> { { "source", "recommended" } }, null, null));
                    }

                    // If tracks were retrieved, remove the first track since it is the seed track
                    if (pageTracks.Count > 0)
                        pageTracks.RemoveAt(0);

                    // Assign the number of page tracks to select
                    int numberPageTracks;

                    if (pageTracks.Count < 5)
                        numberPageTracks = pageTracks.Count;
                    else
                        numberPageTracks = (int)Math.Round((float)(24 / numberSeedTracks));

                    // Randomly the required number of tracks
                    for (int i = 0; i < numberPageTracks; i++)
                    {
                        // Break if no more tracks are left to select
                        if (pageTracks.Count == 0)
                            break;

                        var selectedContentItem = pageTracks[SelectItemFromList(pageTracks.Count, 1)];
                        recommendedContent.Add(selectedContentItem);
                        pageTracks.Remove(selectedContentItem);
                    }
                }
                finally
                {
                    // Release semaphore when complete
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Shuffle the recommendations
            Shuffle(recommendedContent);

            // Add a recommended tracks section header to the beginning of the list after shuffling
            var recommendedHeaderData = new Dictionary<string, string> { { "sectionHeader", Application.Context.GetString(Resource.String.recommended_section_header) } };
            var recommendedHeaderContentItem = new YouTube.ContentItem((YouTube.Video)null, recommendedHeaderData, null, null);
            recommendedContent.Insert(0, recommendedHeaderContentItem);

            // Add a section header at the end of the list for historical tracks
            var historicalTracksHeaderData = new Dictionary<string, string> { { "sectionHeader", Application.Context.GetString(Resource.String.listen_again_section_header) } };
            var historicalTracksHeaderContentItem = new YouTube.ContentItem((YouTube.Video)null, historicalTracksHeaderData, null, null);
            recommendedContent.Insert(recommendedContent.Count, historicalTracksHeaderContentItem);

            // Add all available historical tracks to the list
            foreach (var recommendationsDataTrack in recommendationsData.Tracks)
            {
                // Create YouTube video
                var video = new YouTube.Video(
                    ExtractVideoId(recommendationsDataTrack.Url),
                    recommendationsDataTrack.TrackName,
                    recommendationsDataTrack.Artist,
                    TimeSpan.FromMilliseconds(recommendationsDataTrack.Duration));

                // Create content item with source marked as "historical"
                var historicalTrackContentItem = new YouTube.ContentItem(video, new Dictionary<string, string> { { "source", "historical" } }, null, null);
                recommendedContent.Add(historicalTrackContentItem);
            }

            return recommendedContent;
        }

        /// <summary>
        /// Generates a playlist based on user recommendations.
        /// </summary>
        /// <param name="seedTrackData">A list of <see cref="SeedTrackData"/>s to store continuation token and visitor data information for producing playlist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the content download if connectivity is lost during the process.</param>
        /// <returns>A list of <see cref="YouTube.ContentItem"/>s for the playlist.</returns>
        public static async Task<(List<YouTube.ContentItem>, List<SeedTrackData>)> GenerateRecommendedPlaylist(List<SeedTrackData> seedTrackData, CancellationToken cancellationToken)
        {
            // Throw an exception if the seed track data list is not initialized
            if (seedTrackData == null)
                throw new ArgumentNullException("The seedTrackData argument can not be null.");

            var playlistContentItems = new List<YouTube.ContentItem>();
            var recommendationsData = ReadJson();

            // Get seed track data from recommendations if there is no data yet
            if (seedTrackData.Count == 0)
            {
                // If there are no tracks recorded for recommendations, return an empty list
                if (recommendationsData.Tracks.Count == 0)
                    return (playlistContentItems, seedTrackData);

                seedTrackData = new List<SeedTrackData>();

                // Add all available historical tracks to the list
                foreach (var track in recommendationsData.Tracks)
                {
                    // Create seed track data
                    var seedTrack = new SeedTrackData
                    {
                        Url = track.Url,
                    };

                    // Create content item with source marked as "historical"
                    seedTrackData.Add(seedTrack);
                }
            }

            // Assign a max number of seed tracks to retrieve based on the number of available seed tracks tracks
            int numberSeedTracks;

            if (seedTrackData.Count < 5)
                numberSeedTracks = seedTrackData.Count;
            else
                numberSeedTracks = 5;

            // Create a separate list of seed tracks to use for random selection, so seed tracks can be safely removed after being selected
            var seedTrackDataSelection = seedTrackData.ToList();

            // Randomly select 5 different tracks using reciprocal weighting
            var selectedSeedTracks = new List<SeedTrackData>();

            for (int i = 0; i < numberSeedTracks; i++)
            {
                var selectedSeedTrack = seedTrackDataSelection[SelectItemFromList(seedTrackDataSelection.Count, 0.75)];
                selectedSeedTracks.Add(selectedSeedTrack);
                seedTrackDataSelection.Remove(selectedSeedTrack);
            }

            // Use semaphore for task throttling
            using var semaphore = new SemaphoreSlim(initialCount: 5);

            // Set up tasks to download the first playlist page for the five tracks
            var tasks = selectedSeedTracks.Select(async selectedSeedTrack =>
            {
                // Wait if throttling is required
                await semaphore.WaitAsync();

                try
                {
                    var pageTracks = new List<YouTube.ContentItem>();

                    // Get first page of playlist
                    await foreach (var pageTrack in YouTube.GetWatchPlaylistVideos(ExtractVideoId(selectedSeedTrack.Url), selectedSeedTrack.ContinuationToken, selectedSeedTrack.VisitorData, cancellationToken))
                    {
                        // Mark the data source for all YouTube items as "recommended"
                        pageTracks.Add(pageTrack);
                    }

                    // Remove the first track since it is the seed track
                    pageTracks.RemoveAt(0);

                    // Assign the number of page tracks to select
                    int numberPageTracks;

                    if (pageTracks.Count < 5)
                        numberPageTracks = pageTracks.Count;
                    else
                        numberPageTracks = (int)Math.Round((float)(24 / numberSeedTracks));

                    // Randomly select the required number of tracks
                    for (int i = 0; i < numberPageTracks; i++)
                    {
                        // Break if no more tracks are left to select
                        if (pageTracks.Count == 0)
                            break;

                        var selectedContentItem = pageTracks[SelectItemFromList(pageTracks.Count, 1)];
                        playlistContentItems.Add(selectedContentItem);
                        pageTracks.Remove(selectedContentItem);

                        // Add the next continuation token if not yet saved or if it has changed
                        if (selectedSeedTrack.ContinuationToken == null || selectedContentItem.ContinuationToken != selectedSeedTrack.ContinuationToken)
                            selectedSeedTrack.ContinuationToken = selectedContentItem.ContinuationToken;

                        // Save visitor data if not yet saved or if it has changed
                        if (selectedSeedTrack.VisitorData == null || selectedContentItem.VisitorData != selectedSeedTrack.VisitorData)
                            selectedSeedTrack.VisitorData = selectedContentItem.VisitorData;
                    }
                }
                finally
                {
                    // Release semaphore when complete
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Shuffle the recommendations
            Shuffle(playlistContentItems);

            return (playlistContentItems, seedTrackData);
        }

        /// <summary>
        /// Exports the <see cref="JObject"/> from the JSON file path so this data can be exported to a file.
        /// </summary>
        /// <returns>A <see cref="JObject"/> containing recommendations data.</returns>
        public static JObject ExportJson()
        {
            JObject json;

            using (StreamReader streamReader = new StreamReader(JsonFilePath))
            {
                json = JObject.Parse(streamReader.ReadToEnd());
            }

            return json;
        }

        /// <summary>
        /// Imports <see cref="JObject"/> content and writes to the recommendations file.
        /// </summary>
        /// <param name="json">An external <see cref="JObject"/> to write to the recommendations file.</param>
        public static void ImportJson(JObject json)
        {
            using StreamWriter jsonFile = File.CreateText(JsonFilePath);
            using JsonTextWriter jsonWriter = new JsonTextWriter(jsonFile);
            json.WriteTo(jsonWriter);
        }

        /// <summary>
        /// Reads saved JSON recommendation data from the file.
        /// </summary>
        /// <returns><see cref="RecommendationData"/>JSON recommendation data expressed as a <see cref="RecommendationData"/> object.</returns>
        private static RecommendationData ReadJson()
        {
            string json;

            using (StreamReader streamReader = new StreamReader(JsonFilePath))
            {
                json = streamReader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<RecommendationData>(json);
        }

        /// <summary>
        /// Writes a <see cref="RecommendationData"/> object to the JSON file.
        /// </summary>
        /// <param name="recommendationData"><see cref="RecommendationData"/> to write to the file.</param>
        private static void WriteJson(RecommendationData recommendationData)
        {
            var recommendationDataJson = JObject.FromObject(recommendationData);
            using StreamWriter jsonFile = File.CreateText(JsonFilePath);
            using JsonTextWriter jsonWriter = new JsonTextWriter(jsonFile);

            recommendationDataJson.WriteTo(jsonWriter);
        }

        /// <summary>
        /// Selects an index from a list based on either a reciprocal curve of decreasing probability weights (decreasing from the first element) or equal probability weights.
        /// </summary>
        /// <param name="itemCount">A list of possible page numbers to select from.</param>
        /// <param name="randomness">A decimal value between 0 and 1 with 0 being least random (favoring a reciprocal weighting curve decreasing from the first item) and 1 being the most random (equal weighting).</param>
        /// <returns>A list index selected based on probability weights.</returns>
        private static int SelectItemFromList(int itemCount, double randomness)
        {
            // Throw exception if the randomness argument is not between 0 and 1
            if (randomness > 1 || randomness < 0)
                throw new ArgumentOutOfRangeException("The randomness value must be between 0 and 1.");

            // Return -1 if input has no data
            if (itemCount == 0)
                return -1;

            // Calculate the sum of the total probability weight
            var totalProbabilityWeight = 0D;

            // Calculate total probability weight
            for (int i = 1; i <= itemCount; i++)
            {
                totalProbabilityWeight += ((1D / i) * (1 - randomness)) + randomness;
            }

            // Generate a random number scaled along the total probability weight
            var randomNumber = new Random().NextDouble() * totalProbabilityWeight;

            // Calculate an increasing probability weight
            var accumulatedWeight = 0D;

            for (int i = 1; i <= itemCount; i++)
            {
                // Increment the accumulated weight
                accumulatedWeight += ((1D / i) * (1 - randomness)) + randomness;

                // Return the page where the accumulated weight meets or exceeds the random number
                if (accumulatedWeight >= randomNumber)
                    return i - 1;
            }

            // Return the last page if no earlier page was selected
            return itemCount - 1;
        }

        /// <summary>
        /// Performs a shuffle operation on a list of content items.
        /// </summary>
        /// <param name="contentItems">The content item list to shuffle.</param>
        private static void Shuffle(List<YouTube.ContentItem> contentItems)
        {
            // Exit if queue count is 0
            if (contentItems.Count <= 1)
                return;

            // Set flag to indicate when at least one item is moved to a different position meaning the shuffle was successful
            var shuffleSuccess = false;

            while (!shuffleSuccess)
            {
                // Shuffle using Fisher-Yates
                var random = new Random();
                var n = contentItems.Count;

                while (n > 1)
                {
                    n--;
                    var k = random.NextInt(n + 1);

                    // Set shuffle success flag
                    if (!shuffleSuccess && n != k)
                        shuffleSuccess = true;

                    // Swap queue item order
                    var queueItem = contentItems[k];
                    contentItems[k] = contentItems[n];
                    contentItems[n] = queueItem;
                }
            }
        }

        /// <summary>
        /// Extracts the YouTube video ID value from a track URL.
        /// </summary>
        /// <param name="trackUrl">The URL of the track to play.</param>
        /// <returns>The YouTube video ID value.</returns>
        private static string ExtractVideoId(string trackUrl)
        {
            var videoIdRegex = new Regex(@"v=(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var videoIdMatch = videoIdRegex.Match(trackUrl);
            var videoId = videoIdMatch.Groups[1].Value;

            return videoId;
        }

        /// <summary>
        /// Class representation of seed track data used to generate playlists based on recommendations.
        /// </summary>
        public class SeedTrackData
        {
            public string Url { get; set; }

            public string ContinuationToken { get; set; }

            public string VisitorData { get; set; }
        }

        /// <summary>
        /// Class representation of a music track.
        /// </summary>
        private class Track
        {
            public string TrackName { get; set; }

            public string Artist { get; set; }

            public long Duration { get; set; }

            public string Url { get; set; }
        }

        /// <summary>
        /// Class representation of recommendation data.
        /// </summary>
        private class RecommendationData
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="RecommendationData"/> class.
            /// </summary>
            public RecommendationData()
            {
                Tracks = new List<Track>();
            }

            public List<Track> Tracks { get; set; }
        }
    }
}