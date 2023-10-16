// <copyright file="YouTube.ContentItem.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Apollo
{
    /// <summary>
    /// Class to search for and retrieve playlist and video data from YouTube using the web client API.
    /// </summary>
    internal static partial class YouTube
    {
        /// <summary>
        /// Class to represent an item returned from a mixed content collection.
        /// </summary>
        public class ContentItem
        {
            private readonly Video video;
            private readonly Playlist playlist;

            /// <summary>
            /// Initializes a new instance of the <see cref="ContentItem"/> class.
            /// </summary>
            /// <param name="video">A <see cref="Video"/> to store in the item.</param>
            /// <param name="data">Optional extra data that could be needed with the item.</param>
            /// <param name="continuationToken">A continuation token to request the next page of content if the item came from a paginated collection.</param>
            /// <param name="visitorData">User identifier required by YouTube for some API requests.</param>
            public ContentItem(Video video, Dictionary<string, string> data, string continuationToken, string visitorData)
            {
                this.video = video;
                Data = data;
                ContinuationToken = continuationToken;
                VisitorData = visitorData;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ContentItem"/> class.
            /// </summary>
            /// <param name="playlist">A <see cref="Playlist"/> to store in the item.</param>
            /// <param name="data">Optional extra data that could be needed with the item.</param>
            /// <param name="continuationToken">A continuation token to request the next page of content if the item came from a paginated collection.</param>
            /// <param name="visitorData">User identifier required by YouTube for some API requests.</param>
            public ContentItem(Playlist playlist, Dictionary<string, string> data, string continuationToken, string visitorData)
            {
                this.playlist = playlist;
                Data = data;
                ContinuationToken = continuationToken;
                VisitorData = visitorData;
            }

            /// <summary>
            /// Gets a the <see cref="Video"/> or <see cref="Playlist"/> content depending on the type of data that was assigned.
            /// </summary>
            public object Content
            {
                get
                {
                    if (video != null)
                        return video;
                    else if (playlist != null)
                        return playlist;
                    else
                        return null;
                }
            }

            /// <summary>
            /// Gets a dictionary to hold any extra data needed with the item.
            /// </summary>
            public Dictionary<string, string> Data { get; }

            public string ContinuationToken { get; }

            public string VisitorData { get; }
        }
    }
}