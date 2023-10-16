// <copyright file="YouTube.Network.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        public static class Network
        {
            private static readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            });

            /// <summary>
            /// Sends an HTTP POST request containing data to the specified URI.
            /// </summary>
            /// <param name="uri">The URI to send a POST request to.</param>
            /// <param name="data">The <see cref="JObject"/> payload data to send.</param>
            /// <param name="visitorData">User identifier required by YouTube for some API requests.</param>
            /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the operation.</param>
            /// <returns>The response from the POST request.</returns>
            public static async Task<string> SendPostRequest(string uri, JObject data, string visitorData, CancellationToken cancellationToken)
            {
                // Merge proper context with request data
                ConfigurePayloadContext(data, uri);

                // Create request
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(uri),
                    Method = HttpMethod.Post,
                    Content = new StringContent(data.ToString(), Encoding.UTF8, "application/json"),
                };

                // Add proper headers
                ConfigureRequestHeaders(request, uri, visitorData);

                // Send request and read the response
                var response = await HttpClient.SendAsync(request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync();

                return content;
            }

            /// <summary>
            /// Adds the relevant context data based on the request URI.
            /// </summary>
            /// <param name="data">The <see cref="JObject"/> payload data to send.</param>
            /// <param name="uri">The URI for the request.</param>
            private static void ConfigurePayloadContext(JObject data, string uri)
            {
                if (uri.Contains(Constants.PlayerEndpoint))
                    data.Merge(GetYouTubeStreamInfoContextJson());
                else if (uri.Contains(Constants.YouTubeDomain))
                    data.Merge(GetYouTubeContextJson());
                else
                    data.Merge(GetYouTubeMusicContextJson());
            }

            /// <summary>
            /// Configures default and custom headers for requests to the YouTube API.
            /// </summary>
            /// <param name="request">The <see cref="HttpRequestMessage"/> to configure the headers for.</param>
            /// <param name="uri">The URI for the request.</param>
            /// <param name="visitorData">User identifier required by YouTube for some API requests.</param>
            private static void ConfigureRequestHeaders(HttpRequestMessage request, string uri, string visitorData)
            {
                // Set the origin header based on the presence of a domain in the URI argument
                string origin;

                if (uri.Contains(Constants.YouTubeDomain))
                    origin = Constants.YouTubeUrl;
                else
                    origin = Constants.YouTubeMusicUrl;

                // Configure content headers
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content.Headers.ContentEncoding.Add("gzip");

                // Configure default headers
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

                // Add custom headers
                request.Headers.Add("origin", origin);
                request.Headers.Add("x-goog-authuser", "0");
                request.Headers.Add("user-agent", Constants.UserAgent);

                // Add custom visitor data header if present
                if (visitorData != null)
                    request.Headers.Add("x-goog-visitor-id", visitorData);
            }

            /// <summary>
            /// Returns the context JSON required by YouTube API requests.
            /// </summary>
            /// <returns>A <see cref="JObject"/> containing required context data.</returns>
            private static JObject GetYouTubeContextJson()
            {
                return JObject.Parse(@"{'context':{'client':{'clientName':'WEB','clientVersion':'2.20210408.08.00','hl':'en','gl':'US','utcOffsetMinutes':0},'user':{'lockedSafetyMode':false}}}");
            }

            /// <summary>
            /// Returns the context JSON required by YouTube Music API requests.
            /// </summary>
            /// <returns>A <see cref="JObject"/> containing required context data.</returns>
            private static JObject GetYouTubeMusicContextJson()
            {
                return JObject.Parse(@"{'context':{'client':{'clientName':'WEB_REMIX','clientVersion':'1.20220815.01.00','hl':'en'},'user':{}}}");
            }

            /// <summary>
            /// Returns the context JSON required for requesting stream info from YouTube.
            /// </summary>
            /// <returns>A <see cref="JObject"/> containing required context data.</returns>
            private static JObject GetYouTubeStreamInfoContextJson()
            {
                // Client configurations at: https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/youtube.py
                return JObject.Parse(@"{'context':{'client':{'hl':'en','gl':'US','clientName':'ANDROID','clientVersion':'17.31.35', 'androidSdkVersion': 33}}}");
            }
        }
    }
}