﻿using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public class MarketsService : Markets.MarketsBase
    {
        readonly ILogger<MarketsService> _logger;

        public MarketsService(ILogger<MarketsService> logger) => _logger = logger;

        public override async Task GetTickStream(Empty _, IServerStreamWriter<TickResponse> responseStream, ServerCallContext context)
        {
            _logger.LogInformation(nameof(GetTickStream));
            var count = 1;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var cts = new CancellationTokenSource(500);
                await foreach (var tick in GetTicksAsync(cancellationToken: cts.Token))
                {
                    if (count % 10 == 0)
                        _logger.LogInformation($"streaming {nameof(TickResponse)} #{count}");
                    await responseStream.WriteAsync(tick);
                    count++;
                }
            }
        }

        public override Task<TicksResponse> GetAllTicks(Empty _, ServerCallContext context)
        {
            _logger.LogInformation(nameof(GetAllTicks));
            var ticks = new List<TickResponse>();
            //await foreach (var tick in ticksChannel.Reader.ReadAllAsync())//no good because the channel never actually completes...
            //while (tickQueue.TryDequeue(out var tick))
            //    ticks.Add(tick);
            var l = tickBacklog.OrderByDescending(p => p.Date);
            foreach (var tick in l)
                ticks.Add(tick);
            var res = new TicksResponse
            {
                Ticks = { ticks }
            };
            return Task.FromResult(res);
        }

        //public override async Task GetAllTicksStream(Empty _, IServerStreamWriter<TickResponse> responseStream, ServerCallContext context)
        //{
        //    _logger.LogInformation(nameof(GetTickStream));
        //    var count = 1;
        //    while (!context.CancellationToken.IsCancellationRequested)
        //    {
        //        await foreach (var tick in ticksChannel.Reader.ReadAllAsync())
        //        {
        //            if (count % 10 == 0)
        //                _logger.LogInformation($"streaming {nameof(TickResponse)} #{count}");
        //            await responseStream.WriteAsync(tick);
        //            count++;
        //        }
        //    }
        //}

        List<TickResponse> GetTicks
        {
            get
            {
                var r = new Random();
                var limit = 1_000;
                var l = new List<TickResponse>(limit);
                for (var i = 0; i < limit; i++)
                    l.Add(GetTick(r));
                return l;
            }
        }

        static Channel<TickResponse> ticksChannel { get; set; } = Channel.CreateBounded<TickResponse>(
            new BoundedChannelOptions(1024)
            {
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });

        static ConcurrentQueue<TickResponse> tickQueue { get; set; } = new ConcurrentQueue<TickResponse>();

        static FixedSizedQueue<TickResponse> tickBacklog { get; set; } = new FixedSizedQueue<TickResponse>(30);

        async IAsyncEnumerable<TickResponse> GetTicksAsync([EnumeratorCancellation]CancellationToken cancellationToken)
        {
            var r = new Random();

            //cancellationToken.ThrowIfCancellationRequested();
            while (!cancellationToken.IsCancellationRequested)
            {
                //generate random time delay, a simulated tick gap
                await Task.Delay(r.Next(0, 5) * 100);
                var tick = GetTick(r);
                //lets store a limited backlog of data
                ticksChannel.Writer.TryWrite(tick);
                tickQueue.Enqueue(tick);
                tickBacklog.Enqueue(tick);
                yield return tick;
            }
        }

        TickResponse GetTick(Random r)
        {
            //pick out a random stock
            var stockIndex = r.Next(0, stocks.Count);
            var stock = stocks[stockIndex];

            //generate random price change
            //https://stackoverflow.com/questions/3975290/produce-a-random-number-in-a-range-using-c-sharp
            var rDiff = Math.Round((r.NextDouble() * 2) - 1.0, 1);

            //update stock prices
            stock.lastBid += rDiff;
            stock.lastOffer += rDiff;
            stock.date = DateTime.UtcNow;

            var tick = new TickResponse
            {
                Bid = stock.lastBid,
                Offer = stock.lastOffer,
                Symbol = stock.symbol,
                Date = stock.date.ToTimestamp(),
            };
            return tick;
        }

        static List<Stock> stocks
        {
            get
            {
                var s = new List<Stock>();
                s.Add(new Stock("msft") { lastBid = 130, lastOffer = 131 });
                s.Add(new Stock("appl") { lastBid = 77, lastOffer = 78 });
                s.Add(new Stock("tsco") { lastBid = 45, lastOffer = 46 });
                return s;
            }
        }
    }

    public class Stock
    {
        public Stock(string symbol)
        {
            this.symbol = symbol;
        }
        public string symbol { get; }
        public double lastBid { get; set; }
        public double lastOffer { get; set; }
        public DateTime date { get; set; }
    }
}