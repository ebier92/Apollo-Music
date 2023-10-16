// <copyright file="YouTube.StreamInfo.cs" company="Erik Bierbrauer">
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
        /// Class to represent stream information for a YouTube video.
        /// </summary>
        public class StreamInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StreamInfo"/> class.
            /// </summary>
            /// <param name="url">The URL of the stream.</param>
            /// <param name="mimeType">The codec type for the stream.</param>
            /// <param name="bitRate">The bit rate for the stream.</param>
            public StreamInfo(string url, string mimeType, int bitRate)
            {
                Url = url;
                MimeType = mimeType;
                BitRate = bitRate;
            }

            public string Url { get; }

            public string MimeType { get; }

            public int BitRate { get; }
        }
    }
}