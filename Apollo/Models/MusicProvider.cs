// <copyright file="MusicProvider.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Apollo
{
    /// <summary>
    /// Class to retrieve audio streams from YouTube videos.
    /// </summary>
    internal static class MusicProvider
    {
        private const int Timeout = 10000;

        /// <summary>
        /// Retrieves the audio media stream for a specific video.
        /// </summary>
        /// <param name="url">URL of a YouTube video.</param>
        /// <param name="streamQualitySetting">The user stream quality setting.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the task if network connectivity is interrupted.</param>
        /// <returns>URL of the music stream.</returns>
        public static async Task<string> GetMusicStream(string url, SettingsManager.StreamQualitySettingOptions streamQualitySetting, CancellationToken cancellationToken)
        {
            // Check if the provided token has been cancelled on each attempt and return null if so
            if (cancellationToken.IsCancellationRequested)
                return null;

            // Try to get a set of available audio media streams
            List<YouTube.StreamInfo> audioStreams = new List<YouTube.StreamInfo>();
            var videoId = YouTube.GetVideoId(url);

            List<YouTube.StreamInfo> streamInfoItems;

            try
            {
                streamInfoItems = await YouTube.GetStreamInfo(videoId, cancellationToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                // Return an empty string if there is any problem getting streams other than the task being cancelled
                return null;
            }

            // Retrieve only audio streams
            foreach (var streamInfoItem in streamInfoItems)
            {
                if (streamInfoItem.MimeType.Contains("audio/"))
                    audioStreams.Add(streamInfoItem);
            }

            // Sort by total file size
            audioStreams = audioStreams.OrderBy(x => x.BitRate).ToList();

            // Return lowest available quality stream
            if (streamQualitySetting == SettingsManager.StreamQualitySettingOptions.Low && audioStreams.Count > 0)
            {
                return audioStreams[0].Url;
            } // Return midrange quality stream if available
            else if (streamQualitySetting == SettingsManager.StreamQualitySettingOptions.Medium && audioStreams.Count >= 2)
            {
                return audioStreams[1].Url;
            } // Retrun highest quality stream available
            else if (streamQualitySetting == SettingsManager.StreamQualitySettingOptions.High && audioStreams.Count > 0)
            {
                return audioStreams[^1].Url;
            } // Return only available stream if conditions are not met
            else if (audioStreams.Count > 0)
            {
                return audioStreams[0].Url;
            } // No streams available, return null
            else
            {
                return null;
            }
        }
    }
}