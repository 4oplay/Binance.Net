﻿using System;
using Binance.Net.Objects;
using CryptoExchange.Net;

namespace Binance.Net
{
    public class BinanceClientOptions: ExchangeOptions
    {
        public string BaseAddress { get; set; } = "https://api.binance.com";
        /// <summary>
        /// Whether or not to automatically sync the local time with the server time
        /// </summary>
        public bool AutoTimestamp { get; set; } = false;
        /// <summary>
        /// Whether to check the trade rules when placing new orders and what to do if the trade isn't valid
        /// </summary>
        public TradeRulesBehaviour TradeRulesBehaviour { get; set; } = TradeRulesBehaviour.None;
        /// <summary>
        /// How often the trade rules should be updated. Only used when TradeRulesBehaviour is not None
        /// </summary>
        public TimeSpan TradeRulesUpdateInterval { get; set; } = TimeSpan.FromMinutes(60);
    }

    public class BinanceSocketClientOptions : ExchangeOptions
    {
        /// <summary>
        /// The base adress for the socket connections
        /// </summary>
        public string BaseSocketAddress { get; set; } = "wss://stream.binance.com:9443/ws/";
    }
}
