﻿using WebSocketSharp;

namespace Binance.Net.Objects
{
    public class BinanceStreamConnection
    {
        public bool Succes { get; set; }
        public int StreamId { get; set; }
    }

    public class BinanceStream
    {
        public WebSocket Socket { get; set; }
        public int StreamId { get; set; }
        public bool UserStream { get; set; }
    }
}
