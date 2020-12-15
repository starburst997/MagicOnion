using Benchmark.Client.Reports;
using Benchmark.Server.Shared;
using Benchmark.Shared;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Client;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Benchmark.Client
{
    public class UnaryBenchmarkScenario
    {
        private readonly IBenchmarkService client;
        private readonly BenchReporter _reporter;

        public UnaryBenchmarkScenario(GrpcChannel channel, BenchReporter reporter)
        {
            client = MagicOnionClient.Create<IBenchmarkService>(channel);
            _reporter = reporter;
        }

        public async Task Run(int requestCount)
        {
            using (var statistics = new Statistics(nameof(PlainTextAsync)))
            {
                await PlainTextAsync(requestCount);

                _reporter.AddBenchDetail(new BenchReportItem
                {
                    TestName = nameof(PlainTextAsync),
                    Begin = statistics.Begin,
                    End = DateTime.UtcNow,
                    DurationMs = statistics.Elapsed.TotalMilliseconds,
                    RequestCount = requestCount,
                    Type = nameof(UnaryBenchmarkScenario),
                });
            }
        }

        private async Task SumAsync(int requestCount)
        {
            for (var i = 0; i <= requestCount; i++)
            {
                // Call the server-side method using the proxy.
                _ = await client.SumAsync(i, i);
            }
        }
        public async Task SumParallel(int requestCount)
        {
            var tasks = new List<UnaryResult<int>>();
            for (var i = 0; i <= requestCount; i++)
            {
                // Call the server-side method using the proxy.
                var task = client.SumAsync(i, i);
                tasks.Add(task);
            }
            await ValueTaskUtils.WhenAll(tasks.ToArray());
        }

        private async Task PlainTextAsync(int requestCount)
        {
            for (var i = 0; i <= requestCount; i++)
            {
                var data = new BenchmarkData
                {
                    PlainText = i.ToString(),
                };
                _ = await client.PlainTextAsync(data);
            }
        }
        private async Task PlainTextParallel(int requestCount)
        {
            var tasks = new List<UnaryResult<Nil>>();
            for (var i = 0; i <= requestCount; i++)
            {
                var data = new BenchmarkData
                {
                    PlainText = i.ToString(),
                };
                var task = client.PlainTextAsync(data);
                tasks.Add(task);
            }
            await ValueTaskUtils.WhenAll(tasks);
        }
    }
}
