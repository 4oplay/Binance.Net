﻿using Binance.Net.Converters;
using Binance.Net.Logging;
using Binance.Net.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;

namespace Binance.Net
{
    public class BinanceSocketClient: BinanceAbstractClient, IDisposable
    {
        #region fields
        private const string BaseWebsocketAddress = "wss://stream.binance.com:9443/ws/";

        private List<BinanceStream> sockets = new List<BinanceStream>();
        private int lastStreamId;
        private object streamIdLock = new object();
        private Action<BinanceStreamAccountInfo> accountInfoCallback;
        private Action<BinanceStreamOrderUpdate> orderUpdateCallback;

        private const string DepthStreamEndpoint = "@depth";
        private const string KlineStreamEndpoint = "@kline";
        private const string TradesStreamEndpoint = "@aggTrade";
        #endregion

        #region constructor/destructor

        /// <summary>
        /// Create a new instance of BinanceSocketClient
        /// </summary>
        public BinanceSocketClient()
        {
        }

        /// <summary>
        /// Create a new instance of BinanceSocketClient using provided credentials. Api keys can be managed at https://www.binance.com/userCenter/createApi.html
        /// </summary>
        /// <param name="apiKey">The api key</param>
        /// <param name="apiSecret">The api secret associated with the key</param>
        public BinanceSocketClient(string apiKey, string apiSecret)
        {
            SetAPICredentials(apiKey, apiSecret);
        }

        ~BinanceSocketClient()
        {
            Dispose(false);
        }

        #endregion 

        #region methods
        /// <summary>
        /// Subscribes to the candlestick update stream for the provided symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="interval">The interval of the candlesticks</param>
        /// <param name="onMessage">The event handler for the received data</param>
        /// <returns>Returns a <see cref="BinanceStreamConnection"/> object which contains a success flag and a stream id. This stream id can be used to close this 
        /// specific stream using the <see cref="StopStream(int)"/> method</returns>
        public BinanceStreamConnection SubscribeToKlineStream(string symbol, KlineInterval interval, Action<BinanceStreamKline> onMessage)
        {
            symbol = symbol.ToLower();
            var socket = CreateSocket(BaseWebsocketAddress + symbol + KlineStreamEndpoint + "_" + JsonConvert.SerializeObject(interval, new KlineIntervalConverter(false)));
            if (socket == null)
                return new BinanceStreamConnection() { Succes = false };

            socket.Socket.OnMessage += (o, s) => onMessage(JsonConvert.DeserializeObject<BinanceStreamKline>(s.Data));

            log.Write(LogVerbosity.Debug, $"Started kline stream for {symbol}: {interval}");
            return new BinanceStreamConnection() { StreamId = socket.StreamId, Succes = true };
        }

        /// <summary>
        /// Subscribes to the depth update stream for the provided symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="onMessage">The event handler for the received data</param>
        /// <returns>Returns a <see cref="BinanceStreamConnection"/> object which contains a success flag and a stream id. This stream id can be used to close this 
        /// specific stream using the <see cref="StopStream(int)"/> method</returns>
        public BinanceStreamConnection SubscribeToDepthStream(string symbol, Action<BinanceStreamDepth> onMessage)
        {
            symbol = symbol.ToLower();
            var socket = CreateSocket(BaseWebsocketAddress + symbol + DepthStreamEndpoint);
            if (socket == null)
                return new BinanceStreamConnection() { Succes = false };

            socket.Socket.OnMessage += (o, s) => onMessage(JsonConvert.DeserializeObject<BinanceStreamDepth>(s.Data));

            log.Write(LogVerbosity.Debug, $"Started depth stream for {symbol}");
            return new BinanceStreamConnection() { StreamId = socket.StreamId, Succes = true };
        }

        /// <summary>
        /// Subscribes to the trades update stream for the provided symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="onMessage">The event handler for the received data</param>
        /// <returns>Returns a <see cref="BinanceStreamConnection"/> object which contains a success flag and a stream id. This stream id can be used to close this 
        /// specific stream using the <see cref="StopStream(int)"/> method</returns>
        public BinanceStreamConnection SubscribeToTradesStream(string symbol, Action<BinanceStreamTrade> onMessage)
        {
            symbol = symbol.ToLower();
            var socket = CreateSocket(BaseWebsocketAddress + symbol + TradesStreamEndpoint);
            if (socket == null)
                return new BinanceStreamConnection() { Succes = false };

            socket.Socket.OnMessage += (o, s) => onMessage(JsonConvert.DeserializeObject<BinanceStreamTrade>(s.Data));

            log.Write(LogVerbosity.Debug, $"Started trade stream for {symbol}");
            return new BinanceStreamConnection() { StreamId = socket.StreamId, Succes = true };
        }

        /// <summary>
        /// Subscribes to the account update stream. Prior to using this, the <see cref="StartUserStream"/> method should be called.
        /// </summary>
        /// <param name="listenKey">Listen key retrieved by the StartUserStream method</param>
        /// <param name="onMessage">The event handler for the data received</param>
        /// <returns>bool indicating success</returns>
        public ApiResult<bool> SubscribeToAccountUpdateStream(string listenKey, Action<BinanceStreamAccountInfo> onMessage)
        {
            if (listenKey == null)
                return ThrowErrorMessage<bool>("Cannot start stream without listen key. Call the StartUserStream function and try again");

            accountInfoCallback = onMessage;

            return new ApiResult<bool>() { Success = CreateUserStream(listenKey) };
        }

        /// <summary>
        /// Subscribes to the order update stream. Prior to using this, the <see cref="StartUserStream"/> method should be called.
        /// </summary>
        /// <param name="listenKey">Listen key retrieved by the StartUserStream method</param>
        /// <param name="onMessage">The event handler for the data received</param>
        /// <returns>bool indicating success</returns>
        public ApiResult<bool> SubscribeToOrderUpdateStream(string listenKey, Action<BinanceStreamOrderUpdate> onMessage)
        {
            if (listenKey == null)
                return ThrowErrorMessage<bool>("Cannot start stream without listen key. Call the StartUserStream function and try again");

            orderUpdateCallback = onMessage;
            return new ApiResult<bool>() { Success = CreateUserStream(listenKey) };
        }

        /// <summary>
        /// Unsubscribes from the account update stream
        /// </summary>
        /// <returns></returns>
        public void UnsubscribeFromAccountUpdateStream()
        {
            accountInfoCallback = null;

            // Close the socket if we're not listening for anything
            if (orderUpdateCallback == null)
            {
                lock (sockets)
                    sockets.SingleOrDefault(s => s.UserStream)?.Socket.Close();
            }
        }

        /// <summary>
        /// Unsubscribes from the order update stream
        /// </summary>
        /// <returns></returns>
        public void UnsubscribeFromOrderUpdateStream()
        {
            orderUpdateCallback = null;

            // Close the socket if we're not listening for anything
            if (accountInfoCallback == null)
            {
                lock (sockets)
                    sockets.SingleOrDefault(s => s.UserStream)?.Socket.Close();
            }
        }

        /// <summary>
        /// Unsubscribes from a stream with the provided stream id
        /// </summary>
        /// <param name="streamId">SteamId of the stream</param>
        public void UnsubscribeFromStream(int streamId)
        {
            lock (sockets)
                sockets.SingleOrDefault(s => s.StreamId == streamId)?.Socket.Close();
        }

        /// <summary>
        /// Unsubscribes from all streams
        /// </summary>
        public void UnsubscribeAllStreams()
        {
            lock (sockets)
                sockets.ToList().ForEach(s => s.Socket.Close());

            orderUpdateCallback = null;
            accountInfoCallback = null;
        }

        /// <summary>
        /// Dispose this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void OnUserMessage(string data)
        {
            if (data.Contains("outboundAccountInfo"))
                accountInfoCallback?.Invoke(JsonConvert.DeserializeObject<BinanceStreamAccountInfo>(data));
            else if (data.Contains("executionReport"))
                orderUpdateCallback?.Invoke(JsonConvert.DeserializeObject<BinanceStreamOrderUpdate>(data));
        }

        private bool CreateUserStream(string listenKey)
        {
            lock (sockets)
                if (sockets.Any(s => s.UserStream))
                    return true;

            var socket = CreateSocket(BaseWebsocketAddress + listenKey);
            if (socket == null)
                return false;

            socket.UserStream = true;
            socket.Socket.OnMessage += (o, s) => OnUserMessage(s.Data);
            log.Write(LogVerbosity.Debug, $"User stream started");
            return true;
        }


        private BinanceStream CreateSocket(string url)
        {
            try
            {
                var socket = new WebSocket(url);
                socket.OnClose += Socket_OnClose;
                socket.OnError += Socket_OnError;
                socket.OnOpen += Socket_OnOpen;
                socket.Connect();
                var socketObject = new BinanceStream() { Socket = socket, StreamId = NextStreamId() };
                lock (sockets)
                    sockets.Add(socketObject);
                return socketObject;
            }
            catch (Exception e)
            {
                log.Write(LogVerbosity.Error, $"Couldn't open socket stream: {e.Message}");
                return null;
            }
        }

        private void Socket_OnOpen(object sender, EventArgs e)
        {
            log.Write(LogVerbosity.Debug, $"Socket opened to {((WebSocket)sender).Url}");
        }

        private void Socket_OnError(object sender, ErrorEventArgs e)
        {
            log.Write(LogVerbosity.Error, $"Socket error {e.Message}");
        }

        private void Socket_OnClose(object sender, CloseEventArgs e)
        {
            log.Write(LogVerbosity.Debug, $"Socket closed");
            lock (sockets)
                sockets.RemoveAll(s => s.Socket == (WebSocket)sender);
        }

        private int NextStreamId()
        {
            lock (streamIdLock)
            {
                lastStreamId++;
                return lastStreamId;
            }
        }
        
        private void Dispose(bool disposing)
        {
            lock (sockets)
                sockets.ToList().ForEach(s => s.Socket.Close());
        }
        #endregion
    }
}
