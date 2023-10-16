// <copyright file="YouTube.Video.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;

namespace Apollo
{
    /// <summary>
    /// Class to search for and retrieve playlist and video data from YouTube using the web client API.
    /// </summary>
    internal static partial class YouTube
    {
        /// <summary>
        /// Class representing a video from YouTube.
        /// </summary>
        public class Video
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Video"/> class.
            /// </summary>
            /// <param name="videoId">The video ID of the video.</param>
            /// <param name="title">The title of the video.</param>
            /// <param name="author">The author (channel name) of the video.</param>
            /// <param name="duration">The duration of the video.</param>
            public Video(string videoId, string title, string author, TimeSpan duration)
            {
                VideoId = videoId;
                Title = title;
                Author = author;
                Duration = duration;
                Thumbnails = new Thumbnails
                {
                    VideoId = videoId,
                };
            }

            public string VideoId { get; }

            public string Url
            {
                get { return $"https://www.youtube.com/watch?v={VideoId}"; }
            }

            public string Title { get; }

            public string Author { get; }

            public TimeSpan Duration { get; }

            public Thumbnails Thumbnails { get; }
        }
    }
}