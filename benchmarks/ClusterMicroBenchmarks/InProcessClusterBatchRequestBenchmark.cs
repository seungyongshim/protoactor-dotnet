﻿// -----------------------------------------------------------------------
// <copyright file="InProcessClusterBatchRequestBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote.GrpcNet;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class InProcessClusterBatchRequestBenchmark
    {
        private const string Kind = "echo";

        [Params(1000, 5000, 10000)]
        public int BatchSize { get; set; }
        
        [Params(10000)]
        public int Identities { get; set; }

        private Cluster _cluster;
        private ClusterIdentity[] _ids;

        private PID pid;

        [GlobalSetup]
        public async Task Setup()
        {
            var echoProps = Props.FromFunc(ctx => {
                    if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                    return Task.CompletedTask;
                }
            );
            var echoKind = new ClusterKind(Kind, echoProps);

            var sys = new ActorSystem(new ActorSystemConfig())
                .WithRemote(GrpcNetRemoteConfig.BindToLocalhost(9090))
                .WithCluster(ClusterConfig().WithClusterKind(echoKind));

            _cluster = sys.Cluster();
            await _cluster.StartMemberAsync();
            _ids = new ClusterIdentity[Identities];

            for (int i = 0; i < _ids.Length; i++)
            {
                _ids[i] = ClusterIdentity.Create(i.ToString(), Kind);
            }
            // Activate identities
            foreach (var clusterIdentity in _ids)
            {
                await _cluster.RequestAsync<int>(clusterIdentity, 1, CancellationToken.None);
            }
        }

        private static ClusterConfig ClusterConfig() => Proto.Cluster.ClusterConfig.Setup("testcluster",
            new TestProvider(new TestProviderOptions(), new InMemAgent()),
            new PartitionIdentityLookup()
        );

        [GlobalCleanup]
        public Task Cleanup() => _cluster.ShutdownAsync();

        [Benchmark]
        public async Task ClusterRequestAsync()
        {
            var tasks = new Task[BatchSize];
            for (var i = 0; i < BatchSize; i++)
            {
                var id = _ids[i];
                tasks[i] = _cluster.RequestAsync<int>(id, i, CancellationToken.None);
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task ClusterRequestAsyncBatch()
        {
            using var batch = _cluster.System.Root.Batch(BatchSize, CancellationToken.None);
            var tasks = new Task[BatchSize];
            for (var i = 0; i < BatchSize; i++)
            {
                var id = _ids[i];
                tasks[i] = _cluster.RequestAsync<int>(id, i, batch,  CancellationToken.None);
            }

            await Task.WhenAll(tasks);
        }
    }
}