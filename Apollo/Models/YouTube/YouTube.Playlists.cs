// <copyright file="YouTube.Playlists.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

namespace Apollo
{
    /// <summary>
    /// Class to search for and retrieve playlist and video data from YouTube using the web client API.
    /// </summary>
    internal static partial class YouTube
    {
        /// <summary>
        /// Class representing a playlist from YouTube.
        /// </summary>
        public class Playlist
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Playlist"/> class.
            /// </summary>
            /// <param name="playlistId">The playlist ID of the playlist.</param>
            /// <param name="title">The title of the playlist.</param>
            /// <param name="author">The author (channel name) of the playlist.</param>
            /// <param name="description">A description of the playlist.</param>
            /// <param name="videoCount">The number of videos in the playlist.</param>
            /// <param name="thumbnails">A <see cref="Thumbnails"/> set to use for the playlist.</param>
            public Playlist(string playlistId, string title, string author, string description, int videoCount, Thumbnails thumbnails)
            {
                PlaylistId = playlistId;
                Title = title;
                Author = author;
                Description = description;
                VideoCount = videoCount;
                Thumbnails = thumbnails;
            }

            public string PlaylistId { get; }

            public string Title { get; }

            public string Author { get; }

            public string Description { get; }

            public int VideoCount { get; }

            public Thumbnails Thumbnails { get; }
        }
    }
}