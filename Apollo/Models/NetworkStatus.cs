// <copyright file="NetworkStatus.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using Android.App;
using Android.Content;
using Android.Net;

namespace Apollo
{
    /// <summary>
    /// Class to monitor network connectivity.
    /// </summary>
    internal static class NetworkStatus
    {
        /// <summary>
        /// Gets a value indicating whether the network is connected or not.
        /// </summary>
        public static bool IsConnected
        {
            get
            {
                var connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
                var network = connectivityManager.ActiveNetwork;

                // Check if network is not null, otherwise return false
                if (network != null)
                {
                    var networkCapabilities = connectivityManager.GetNetworkCapabilities(network);

                    // Return true only if network capabilities is not null and has a transport type of wifi, cell, ethernet, or bluetooth
                    return networkCapabilities != null
                        && (networkCapabilities.HasTransport(TransportType.Wifi)
                        || networkCapabilities.HasTransport(TransportType.Cellular)
                        || networkCapabilities.HasTransport(TransportType.Ethernet)
                        || networkCapabilities.HasTransport(TransportType.Bluetooth));
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Callback class to respond to changes in network connectivity.
        /// </summary>
        public class NetworkChangeCallback : ConnectivityManager.NetworkCallback
        {
            public Action<Network> OnAvailableAction { get; set; }

            public Action<Network> OnLostAction { get; set; }

            public override void OnAvailable(Network network)
            {
                OnAvailableAction(network);
            }

            public override void OnLost(Network network)
            {
                OnLostAction(network);
            }
        }
    }
}