// <copyright file="ContentManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Android.Media;
using Android.Media.Browse;
using Android.Media.Session;
using Android.OS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apollo
{
    /// <summary>
    /// Interface to access JSON playlist and track data.
    /// </summary>
    internal static class ContentManager
    {
        public const string RootId = "__ROOT__";
        public const string DefaultPlaylistName = "[Unsaved Playlist]";
        public const char Separator = '/';

        /// <summary>
        /// Gets the file path for the content JSON file.
        /// </summary>
        private static string JsonFilePath
        {
            get
            {
                // Get system path for JSON content file
                return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "content.json");
            }
        }

        /// <summary>
        /// Returns a randomly generated track media ID belonging to a playlist.
        /// </summary>
        /// <param name="playlistName">The playlist name to create a media ID for.</param>
        /// <param name="trackUrl">The track URL to create a media ID for.</param>
        /// <returns>Track media ID.</returns>
        public static string CreateTrackMediaId(string playlistName, string trackUrl)
        {
            var randomId = Guid.NewGuid().GetHashCode().ToString();

            if (playlistName == null)
                return RootId + Separator + DefaultPlaylistName + Separator + ExtractVideoId(trackUrl) + randomId;
            else
                return RootId + Separator + playlistName + Separator + ExtractVideoId(trackUrl) + randomId;
        }

        /// <summary>
        /// Returns a media ID created from a playlist name.
        /// </summary>
        /// <param name="playlistName">The playlist name to create a media ID for.</param>
        /// <returns>Playlist media ID.</returns>
        public static string CreatePlaylistMediaId(string playlistName)
        {
            return RootId + Separator + playlistName;
        }

        /// <summary>
        /// Determines if a specific media ID hierarchy refers to a playlist.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>True if the media ID hierarchy refers to a playlist.</returns>
        public static bool IsPlaylist(string mediaId)
        {
            if (mediaId.Count(character => character == Separator) == 1)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Determines if a specific media ID hierarchy refers to a playlist and also that the same playlist already exists in the content.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>True if the media ID hierarchy refers to a playlist and the playlist already exists.</returns>
        public static bool IsPlaylistAndExists(string mediaId)
        {
            if (!IsPlaylist(mediaId))
                return false;

            // Deserialize media content from JSON
            var content = ReadJson();

            // Get playlist name
            var playlistName = mediaId.Split(Separator)[1];

            // Look for a matching playlist name and return true if found
            foreach (var playlist in content.Playlists)
            {
                if (playlist.PlaylistName == playlistName)
                    return true;
            }

            // Return false otherwise
            return false;
        }

        /// <summary>
        /// Determines if a specific media ID hierarchy refers to a track.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>True if the media ID hierarchy refers to a track.</returns>
        public static bool IsTrack(string mediaId)
        {
            if (mediaId.Count(character => character == Separator) == 2)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Extracts the playlist name portion of a media ID hierarchy.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>A string playlist name.</returns>
        public static string GetPlaylistNameFromMediaId(string mediaId)
        {
            if (IsTrack(mediaId) || IsPlaylist(mediaId))
                return mediaId.Split(Separator)[1];
            else
                return null;
        }

        /// <summary>
        /// Creates an empty JSON content file if one does not yet exist.
        /// </summary>
        public static void InitializeJsonFile()
        {
            // Create an empty JSON file if one does not exist
            if (!File.Exists(JsonFilePath))
                WriteJson(new Content());
        }

        /// <summary>
        /// Saves a list of <see cref="MediaBrowser.MediaItem"/>s to the JSON content file as a playlist. Existing playlists of the same name are overwritten.
        /// </summary>
        /// <param name="mediaId">The media ID of the parent playlist.</param>
        /// <param name="mediaItems">List of <see cref="MediaBrowser.MediaItem"/> tracks.</param>
        public static void SaveMediaItems(string mediaId, List<MediaBrowser.MediaItem> mediaItems)
        {
            // Exit the method if the input contains 0 items
            if (mediaItems.Count == 0)
                return;

            // Initialize new list of queue items and queue index
            var updatedMediaItems = new List<MediaBrowser.MediaItem>();

            // Loop through each item and update the playlist media ID to ensure all tracks have the same parent playlist.
            foreach (var mediaItem in mediaItems)
            {
                // Extract each item meda ID and update the playlist name part of the string
                var oldMediaId = mediaItem.Description.MediaId;
                var oldPlaylistName = GetPlaylistNameFromMediaId(oldMediaId);
                var updatedMediaId = mediaItem.Description.MediaId.Replace(
                    CreatePlaylistMediaId(oldPlaylistName) + Separator,
                    mediaId + Separator);

                // Create a new media item description incorporating the new media ID
                var updatedMediaItem = BuildMediaItem(
                    updatedMediaId,
                    mediaItem.Description.Extras.GetLong("Duration"),
                    mediaItem.Description.MediaUri.ToString(),
                    mediaItem.Description.Title,
                    mediaItem.Description.Subtitle,
                    mediaItem.Description.IconUri.ToString());

                // Add to the list of updated items
                updatedMediaItems.Add(updatedMediaItem);
            }

            // Assigned updated media items
            mediaItems = updatedMediaItems;

            // Get the playlist name from the new media ID
            var playlistName = GetPlaylistNameFromMediaId(mediaId);

            // Deserialize media content from JSON
            var content = ReadJson();

            // Create a playlist object
            var playlist = new Playlist()
            {
                PlaylistName = playlistName,
            };

            // Convert media items to tracks and add to the playlist's track list
            foreach (var mediaItem in mediaItems)
            {
                playlist.Tracks.Add(new Track()
                {
                    TrackName = mediaItem.Description.Title,
                    Artist = mediaItem.Description.Subtitle,
                    Duration = mediaItem.Description.Extras.GetLong("Duration"),
                    Url = mediaItem.Description.MediaUri.ToString(),
                });
            }

            // Check if there is an existing playlist of the same name in the content and delete it if so
            Playlist playlistToRemove = null;

            foreach (var contentPlaylist in content.Playlists)
            {
                // Mark the playlist for removal if the name matches and break the loop
                if (contentPlaylist.PlaylistName == playlistName)
                {
                    playlistToRemove = contentPlaylist;
                    break;
                }
            }

            // Delete the matching playlist if one was found
            if (playlistToRemove != null)
                content.Playlists.Remove(playlistToRemove);

            // Save the playlist and sort them alphabetically by name
            content.Playlists.Add(playlist);
            content.Playlists = content.Playlists.OrderBy(element => element.PlaylistName).ToList();

            // Write content to the JSON file
            WriteJson(content);
        }

        /// <summary>
        /// Saves a <see cref="MediaBrowser.MediaItem"/> to the JSON content file at the end of a previously saved playlist.
        /// </summary>
        /// <param name="mediaId">The media ID of the parent playlist.</param>
        /// <param name="mediaItem">The <see cref="MediaBrowser.MediaItem"/> track.</param>
        public static void SaveMediaItem(string mediaId, MediaBrowser.MediaItem mediaItem)
        {
            // Get the playlist name from the new media ID
            var playlistName = GetPlaylistNameFromMediaId(mediaId);

            // Proceed if the playlist exists
            if (IsPlaylistAndExists(mediaId))
            {
                // Deserialize media content from JSON
                var content = ReadJson();

                // Find matching playlist
                foreach (var playlist in content.Playlists)
                {
                    if (playlist.PlaylistName == playlistName)
                    {
                        // Add track to the end of the playlist
                        playlist.Tracks.Add(new Track()
                        {
                            TrackName = mediaItem.Description.Title,
                            Artist = mediaItem.Description.Subtitle,
                            Duration = mediaItem.Description.Extras.GetLong("Duration"),
                            Url = mediaItem.Description.MediaUri.ToString(),
                        });

                        // Write content to JSON
                        WriteJson(content);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a playlist from the JSON content file by media ID.
        /// </summary>
        /// <param name="mediaId">String media ID of a playlist.</param>
        public static void DeleteMediaItems(string mediaId)
        {
            // Exit if the media ID is either not a playlist or does not exist.
            if (!IsPlaylistAndExists(mediaId))
                return;

            // Extract the playlist name
            var playlistName = GetPlaylistNameFromMediaId(mediaId);

            // Deserialize media content from JSON
            var content = ReadJson();

            // Delete the playlist from the content by name
            Playlist playlistToRemove = null;
            foreach (var contentPlaylist in content.Playlists)
            {
                // Mark the playlist for removal if the name matches and break the loop
                if (contentPlaylist.PlaylistName == playlistName)
                {
                    playlistToRemove = contentPlaylist;
                    break;
                }
            }

            // Delete the matching playlist if one was found
            if (playlistToRemove != null)
                content.Playlists.Remove(playlistToRemove);

            // Write the content back to the file
            WriteJson(content);
        }

        /// <summary>
        /// Returns a List of <see cref="MediaBrowser.MediaItem"/> of either playlists or tracks.
        /// </summary>
        /// <param name="mediaId">The media ID of the target track or playlist.</param>
        /// <returns>List of <see cref="MediaBrowser.MediaItem"/> tracks or playlists.</returns>
        public static List<MediaBrowser.MediaItem> GetMediaItems(string mediaId)
        {
            var mediaItems = new List<MediaBrowser.MediaItem>();

            // Media ID is root, return list of playlists
            if (mediaId == RootId)
            {
                // Deserialize media content from JSON
                var content = ReadJson();

                // Build a media item from each playlist and add to the results
                foreach (var playlist in content.Playlists)
                {
                    // Set the playlist thumbnail from the thumbnail of the first track
                    var firstTrackUrl = playlist.Tracks[0].Url;
                    var thumbnails = new YouTube.Thumbnails()
                    {
                        VideoId = ExtractVideoId(firstTrackUrl),
                    };

                    var mediaItem = new MediaBrowser.MediaItem(
                        new MediaDescription.Builder()
                        .SetMediaId(RootId + Separator + playlist.PlaylistName)
                        .SetTitle(playlist.PlaylistName)
                        .SetSubtitle(string.Format("{0} Tracks", playlist.Tracks.Count()))
                        .SetIconUri(Android.Net.Uri.Parse(thumbnails.MediumResUrl))
                        .Build(), MediaItemFlags.Playable);

                    mediaItems.Add(mediaItem);
                }
            } // Media ID contains one seperator so it must refer to a playlist, return a list of tracks
            else if (IsPlaylist(mediaId))
            {
                // Get the playlist name from the media ID
                var playlistName = GetPlaylistNameFromMediaId(mediaId);

                // Deserialize media content from JSON
                var content = ReadJson();

                // Look for playlist matching media ID
                foreach (var playlist in content.Playlists)
                {
                    if (playlist.PlaylistName == playlistName)
                    {
                        // Loop through all tracks on the playlist
                        foreach (var track in playlist.Tracks)
                        {
                            // Create media ID for track
                            var trackMediaId = CreateTrackMediaId(playlistName, track.Url);

                            // Get thumbnail URLs for the track
                            var thumbnails = new YouTube.Thumbnails()
                            {
                                VideoId = ExtractVideoId(track.Url),
                            };

                            // Build media item
                            var mediaItem = BuildMediaItem(
                                trackMediaId,
                                track.Duration,
                                track.Url,
                                track.TrackName,
                                track.Artist,
                                thumbnails.MediumResUrl);

                            mediaItems.Add(mediaItem);
                        }

                        break;
                    }
                }
            }

            return mediaItems;
        }

        /// <summary>
        /// Deletes all saved media content.
        /// </summary>
        public static void ClearContent()
        {
            var playlistMediaItems = GetMediaItems(RootId);

            foreach (var playlistMediaItem in playlistMediaItems)
            {
                DeleteMediaItems(playlistMediaItem.MediaId);
            }
        }

        /// <summary>
        /// Creates a <see cref="MediaSession.QueueItem"/> from the required data.
        /// </summary>
        /// <param name="mediaId">The media ID of the queue item.</param>
        /// <param name="duration">The track duration in milliseconds.</param>
        /// <param name="url">The track URL.</param>
        /// <param name="title">The track title.</param>
        /// <param name="artist">The track artist.</param>
        /// <param name="iconUri">The URL for the album art icon.</param>
        /// <param name="queueId">The queue ID to assign for the queue item.</param>
        /// <returns>A <see cref="MediaSession.QueueItem"/>.</returns>
        public static MediaSession.QueueItem BuildQueueItem(string mediaId, long duration, string url, string title, string artist, string iconUri, long queueId)
        {
            // Build queue item description
            var extras = new Bundle();
            extras.PutLong("Duration", duration);

            var queueItemDescription = new MediaDescription.Builder()
                .SetMediaId(mediaId)
                .SetTitle(title)
                .SetSubtitle(artist)
                .SetIconUri(Android.Net.Uri.Parse(iconUri))
                .SetMediaUri(Android.Net.Uri.Parse(url))
                .SetExtras(extras)
                .Build();

            // Build the queue item
            var queueItem = new MediaSession.QueueItem(queueItemDescription, queueId);

            return queueItem;
        }

        /// <summary>
        /// Creates a <see cref="MediaBrowser.MediaItem"/> from the required data.
        /// </summary>
        /// <param name="mediaId">The media ID of the media item.</param>
        /// <param name="duration">The track duration in milliseconds.</param>
        /// <param name="url">The track URL.</param>
        /// <param name="title">The track title.</param>
        /// <param name="artist">The track artist.</param>
        /// <param name="iconUri">The URL for the album art icon.</param>
        /// <returns>A <see cref="MediaBrowser.MediaItem"/>.</returns>
        public static MediaBrowser.MediaItem BuildMediaItem(string mediaId, long duration, string url, string title, string artist, string iconUri)
        {
            // Build queue item description
            var extras = new Bundle();
            extras.PutLong("Duration", duration);

            var mediaItemDescription = new MediaDescription.Builder()
                .SetMediaId(mediaId)
                .SetTitle(title)
                .SetSubtitle(artist)
                .SetIconUri(Android.Net.Uri.Parse(iconUri))
                .SetMediaUri(Android.Net.Uri.Parse(url))
                .SetExtras(extras)
                .Build();

            // Build the media item
            var mediaItem = new MediaBrowser.MediaItem(mediaItemDescription, MediaItemFlags.Playable);

            return mediaItem;
        }

        /// <summary>
        /// Converts a List of <see cref="MediaBrowser.MediaItem"/>s into a list of <see cref="MediaSession.QueueItem"/>s.
        /// </summary>
        /// <param name="mediaItemList">List of <see cref="MediaBrowser.MediaItem"/>s to be converted.</param>
        /// <param name="queueIndex">Optional starting index to use to create item queue IDs.</param>
        /// <returns>List of <see cref="MediaSession.QueueItem"/>s converted media items.</returns>
        public static List<MediaSession.QueueItem> ConvertMediaItemsToQueue(List<MediaBrowser.MediaItem> mediaItemList, int queueIndex = 0)
        {
            var queue = new List<MediaSession.QueueItem>();

            foreach (var mediaItem in mediaItemList)
            {
                var queueItem = new MediaSession.QueueItem(mediaItem.Description, queueIndex++);
                queue.Add(queueItem);
            }

            return queue;
        }

        /// <summary>
        /// Converts a List of <see cref="MediaSession.QueueItem"/>s into a list of <see cref="MediaBrowser.MediaItem"/>s.
        /// </summary>
        /// <param name="queueItems">List of <see cref="MediaSession.QueueItem"/>s to be converted.</param>
        /// <returns>List of <see cref="MediaBrowser.MediaItem"/>s converted queue items.</returns>
        public static List<MediaBrowser.MediaItem> ConvertQueueToMediaItems(List<MediaSession.QueueItem> queueItems)
        {
            var mediaItems = new List<MediaBrowser.MediaItem>();

            foreach (var queueItem in queueItems)
            {
                var mediaItem = new MediaBrowser.MediaItem(queueItem.Description, MediaItemFlags.Playable);
                mediaItems.Add(mediaItem);
            }

            return mediaItems;
        }

        /// <summary>
        /// Converts a <see cref="YouTube.Video"/> into a <see cref=MediaSession.QueueItem"/>.
        /// </summary>
        /// <param name="video">The <see cref="YouTube.Video"/> to be converted.</param>
        /// <param name="queueId">The queue ID value to assign to the <see cref="MediaSession.QueueItem"/>.</param>
        /// <returns>A <see cref="MediaSession.QueueItem"/>.</returns>
        public static MediaSession.QueueItem ConvertVideoToQueueItem(YouTube.Video video, long queueId)
        {
            // Build the queue item
            var queueItem = BuildQueueItem(
                CreateTrackMediaId(DefaultPlaylistName, video.Url),
                (long)video.Duration.TotalMilliseconds,
                video.Url,
                video.Title,
                video.Author,
                video.Thumbnails.MediumResUrl,
                queueId);

            return queueItem;
        }

        /// <summary>
        /// Converts a <see cref="YouTube.Video"/> into a <see cref="MediaBrowser.MediaItem"/>.
        /// </summary>
        /// <param name="video">The <see cref="YouTube.Video"/> to be converted.</param>
        /// <returns>A <see cref="MediaBrowser.MediaItem"/>.</returns>
        public static MediaBrowser.MediaItem ConvertVideoToMediaItem(YouTube.Video video)
        {
            // Build the media item
            var mediaItem = BuildMediaItem(
                CreateTrackMediaId(DefaultPlaylistName, video.Url),
                (long)video.Duration.TotalMilliseconds,
                video.Url,
                video.Title,
                video.Author,
                video.Thumbnails.MediumResUrl);

            return mediaItem;
        }

        /// <summary>
        /// Returns the <see cref="MediaMetadata"/> for a particular track.
        /// </summary>
        /// <param name="mediaId">The media ID of the target track.</param>
        /// <returns><see cref="MediaMetadata"/> for a track.</returns>
        public static MediaMetadata GetTrackMetadata(string mediaId)
        {
            MediaMetadata mediaMetadata = null;

            // Media ID has two seperators so it must refer to a track
            if (IsTrack(mediaId))
            {
                // Get the playlist name from the media ID
                var playlistName = GetPlaylistNameFromMediaId(mediaId);

                // Deserialize media content from JSON
                var content = ReadJson();

                // Look for playlist matching media ID
                foreach (var playlist in content.Playlists)
                {
                    if (playlist.PlaylistName == playlistName)
                    {
                        // Loop through all tracks on the playlist
                        foreach (var track in playlist.Tracks)
                        {
                            // Create media ID for track
                            var trackMediaId = RootId + Separator + playlistName + Separator + ExtractVideoId(track.Url);

                            // Check for a matching track media ID
                            if (mediaId == trackMediaId)
                            {
                                // Get thumbnail URLs for the track
                                var thumbnails = new YouTube.Thumbnails()
                                {
                                    VideoId = ExtractVideoId(track.Url),
                                };

                                // Build metadata item
                                mediaMetadata = new MediaMetadata.Builder()
                                    .PutString(MediaMetadata.MetadataKeyMediaId, trackMediaId)
                                    .PutString(MediaMetadata.MetadataKeyTitle, track.TrackName)
                                    .PutString(MediaMetadata.MetadataKeyArtist, track.Artist)
                                    .PutLong(MediaMetadata.MetadataKeyDuration, track.Duration)
                                    .PutString(MediaMetadata.MetadataKeyAlbumArtUri, thumbnails.StandardResUrl)
                                    .PutString(MusicService.MetadataCustomKeys.AlbumArtUriBackup, thumbnails.HighResUrl)
                                    .PutString(MediaMetadata.MetadataKeyDisplayIconUri, thumbnails.MediumResUrl)
                                    .PutString(MediaMetadata.MetadataKeyMediaUri, track.Url)
                                    .Build();

                                break;
                            }
                        }

                        break;
                    }
                }
            }

            return mediaMetadata;
        }

        /// <summary>
        /// Builds a <see cref="MediaMetadata"/> from a <see cref="MediaSession.QueueItem"/>.
        /// </summary>
        /// <param name="queueItem">The <see cref="MediaSession.QueueItem"/> to extract metadata from.</param>
        /// <returns><see cref="MediaMetadata"/> from the <see cref="MediaSession.QueueItem"/>.</returns>
        public static MediaMetadata GetQueueItemMetadata(MediaSession.QueueItem queueItem)
        {
            var description = queueItem.Description;

            // Get thumbnail URLs for the track
            var thumbnails = new YouTube.Thumbnails()
            {
                VideoId = ExtractVideoId(description.MediaUri.ToString()),
            };

            // Build metadata item
            var mediaMetadata = new MediaMetadata.Builder()
                .PutString(MediaMetadata.MetadataKeyMediaId, description.MediaId)
                .PutString(MediaMetadata.MetadataKeyTitle, description.Title)
                .PutString(MediaMetadata.MetadataKeyArtist, description.Subtitle)
                .PutLong(MediaMetadata.MetadataKeyDuration, description.Extras.GetLong("Duration"))
                .PutString(MediaMetadata.MetadataKeyAlbumArtUri, thumbnails.StandardResUrl)
                .PutString(MusicService.MetadataCustomKeys.AlbumArtUriBackup, thumbnails.HighResUrl)
                .PutString(MediaMetadata.MetadataKeyDisplayIconUri, thumbnails.MediumResUrl)
                .PutString(MediaMetadata.MetadataKeyMediaUri, description.MediaUri.ToString())
                .Build();

            return mediaMetadata;
        }

        /// <summary>
        /// Exports the <see cref="JObject"/> from the JSON file path so this data can be exported to a file.
        /// </summary>
        /// <returns>A <see cref="JObject"/> containing content data.</returns>
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
        /// Imports <see cref="JObject"/> content and writes to the content file.
        /// </summary>
        /// <param name="json">An external <see cref="JObject"/> to write to the content file.</param>
        public static void ImportJson(JObject json)
        {
            using StreamWriter jsonFile = File.CreateText(JsonFilePath);
            using JsonTextWriter jsonWriter = new JsonTextWriter(jsonFile);
            json.WriteTo(jsonWriter);
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
        /// Reads saved JSON playlist and track data from the file.
        /// </summary>
        /// <returns><see cref="Content"/>JSON content data expressed as a <see cref="Content"/> object.</returns>
        private static Content ReadJson()
        {
            string json;

            using (StreamReader streamReader = new StreamReader(JsonFilePath))
            {
                json = streamReader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<Content>(json);
        }

        /// <summary>
        /// Writes a <see cref="Content"/> object to the JSON file.
        /// </summary>
        /// <param name="content"><see cref="Content"/> to write to the file.</param>
        private static void WriteJson(Content content)
        {
            var contentJson = JObject.FromObject(content);
            using StreamWriter jsonFile = File.CreateText(JsonFilePath);
            using JsonTextWriter jsonWriter = new JsonTextWriter(jsonFile);
            contentJson.WriteTo(jsonWriter);
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
        /// Class representation of a playlist.
        /// </summary>
        private class Playlist
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Playlist"/> class.
            /// </summary>
            public Playlist()
            {
                Tracks = new List<Track>();
            }

            public string PlaylistName { get; set; }

            public List<Track> Tracks { get; set; }
        }

        /// <summary>
        /// Class representation of the content.
        /// </summary>
        private class Content
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Content"/> class.
            /// </summary>
            public Content()
            {
                Playlists = new List<Playlist>();
            }

            public List<Playlist> Playlists { get; set; }
        }
    }
}