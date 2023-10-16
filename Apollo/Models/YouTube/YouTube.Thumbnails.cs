// <copyright file="YouTube.Thumbnails.cs" company="Erik Bierbrauer">
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
        /// Class representing a set of thumbnails for a YouTube video.
        /// </summary>
        public class Thumbnails
        {
            private string customLowResUrl;
            private string customMediumResUrl;
            private string customHighResUrl;
            private string customStandardResUrl;

            public string VideoId { get; set; }

            public string LowResUrl
            {
                get
                {
                    if (VideoId != null)
                        return $"https://img.youtube.com/vi/{VideoId}/default.jpg";
                    else if (customLowResUrl != null)
                        return customLowResUrl;
                    else
                        return null;
                }

                set
                {
                    customLowResUrl = value;
                }
            }

            public string MediumResUrl
            {
                get
                {
                    if (VideoId != null)
                        return $"https://img.youtube.com/vi/{VideoId}/mqdefault.jpg";
                    else if (customMediumResUrl != null)
                        return customMediumResUrl;
                    else
                        return null;
                }

                set
                {
                    customMediumResUrl = value;
                }
            }

            public string HighResUrl
            {
                get
                {
                    if (VideoId != null)
                        return $"https://img.youtube.com/vi/{VideoId}/hqdefault.jpg";
                    else if (customHighResUrl != null)
                        return customHighResUrl;
                    else
                        return null;
                }

                set
                {
                    customHighResUrl = value;
                }
            }

            public string StandardResUrl
            {
                get
                {
                    if (VideoId != null)
                        return $"https://img.youtube.com/vi/{VideoId}/sddefault.jpg";
                    else if (customStandardResUrl != null)
                        return customStandardResUrl;
                    else
                        return null;
                }

                set
                {
                    customStandardResUrl = value;
                }
            }
        }
    }
}